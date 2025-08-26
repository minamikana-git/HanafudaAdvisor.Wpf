#define DISABLE_WINRT   // ← WinRT OCR を使う時はこの行をコメントアウト

#if !DISABLE_WINRT
// ===== WinRT OCR を使うパス =====
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace HanafudaAdvisor.Wpf.Services
{
    // Screen capture + OCR (digits only)
    public sealed class ScreenOcr
    {
        // normalized rect (0..1)
        public record RatioRect(double X, double Y, double W, double H)
        {
            public Rectangle ToPixelRect(int w, int h)
                => new Rectangle((int)(X * w), (int)(Y * h), (int)(W * w), (int)(H * h));
        }

        public sealed class RoiConfig
        {
            public List<RatioRect> HandDigits { get; set; } = new();
            public List<RatioRect> FieldDigits { get; set; } = new();

            public static RoiConfig CreateDefault()
            {
                var cfg = new RoiConfig();
                for (int i = 0; i < 8; i++)
                {
                    double x = 0.30 + i * 0.055;
                    cfg.HandDigits.Add(new RatioRect(x, 0.88, 0.035, 0.06));
                }
                var starts = new (double x, double y)[] {
                    (0.36,0.48),(0.46,0.48),(0.56,0.48),(0.66,0.48),
                    (0.36,0.62),(0.46,0.62),(0.56,0.62),(0.66,0.62)
                };
                foreach (var s in starts) cfg.FieldDigits.Add(new RatioRect(s.x, s.y, 0.035, 0.06));
                return cfg;
            }
        }

        private static System.Drawing.Bitmap CaptureScreen()
        {
            var b = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            var bmp = new System.Drawing.Bitmap(b.Width, b.Height, PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(b.Left, b.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            return bmp;
        }

        private static SoftwareBitmap ToSoftwareBitmap(System.Drawing.Bitmap bmp)
        {
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                int bytes = Math.Abs(data.Stride) * data.Height;
                byte[] buf = new byte[bytes];
                Marshal.Copy(data.Scan0, buf, 0, bytes);
                var sb = new SoftwareBitmap(BitmapPixelFormat.Bgra8, bmp.Width, bmp.Height, BitmapAlphaMode.Premultiplied);
                var ibuf = buf.AsBuffer();
                sb.CopyFromBuffer(ibuf);
                return sb;
            }
            finally { bmp.UnlockBits(data); }
        }

        private static System.Drawing.Bitmap SoftwareBitmapToBitmap(SoftwareBitmap s)
        {
            using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            var enc = BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, stream).AsTask().Result;
            enc.SetSoftwareBitmap(s);
            enc.FlushAsync().AsTask().Wait();
            stream.Seek(0);
            return new System.Drawing.Bitmap(stream.AsStream());
        }

        private static int ReadDigit(SoftwareBitmap full, Rectangle roi)
        {
            using var fullBmp = SoftwareBitmapToBitmap(full);
            using var cut = new System.Drawing.Bitmap(roi.Width, roi.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(cut))
                g.DrawImage(fullBmp, new Rectangle(0, 0, roi.Width, roi.Height), roi, GraphicsUnit.Pixel);

            var sb = ToSoftwareBitmap(cut);
            var engine = OcrEngine.TryCreateFromLanguage(new Language("en-US"));
            var res = engine.RecognizeAsync(sb).AsTask().GetAwaiter().GetResult();
            var digits = new string(res.Text.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out int n) ? n : -1;
        }

        public sealed record ReadResult(int[] HandMonths, int[] FieldMonths);

        public static ReadResult ReadMonths(RoiConfig? cfg = null)
        {
            cfg ??= RoiConfig.CreateDefault();
            using var screenBmp = CaptureScreen();
            var sb = ToSoftwareBitmap(screenBmp);
            int[] hand = cfg.HandDigits.Select(r => ReadDigit(sb, r.ToPixelRect(screenBmp.Width, screenBmp.Height))).ToArray();
            int[] field = cfg.FieldDigits.Select(r => ReadDigit(sb, r.ToPixelRect(screenBmp.Width, screenBmp.Height))).ToArray();
            return new ReadResult(hand, field);
        }

        public static void ApplyToGameState(HanafudaAdvisor.Wpf.Models.GameState g, ReadResult r)
        {
            g.MyHand.Clear();
            g.Field.Clear();
            var used = new List<HanafudaAdvisor.Wpf.Models.Card>();

            foreach (var m in r.HandMonths.Where(x => x >= 1 && x <= 12))
            {
                var c = PickUnused(m, used);
                g.MyHand.Add(c); used.Add(c);
            }
            foreach (var m in r.FieldMonths.Where(x => x >= 1 && x <= 12))
            {
                var c = PickUnused(m, used);
                g.Field.Add(c); used.Add(c);
            }
        }

        private static HanafudaAdvisor.Wpf.Models.Card PickUnused(int month, List<HanafudaAdvisor.Wpf.Models.Card> already)
        {
            var cand = HanafudaAdvisor.Wpf.Models.Deck.Full.Where(c => c.Month == month).ToList();
            foreach (var c in cand) if (!already.Contains(c)) return c;
            return cand.First();
        }
    }
}
#else
// ===== WinRT を使わない（ビルド用ダミー）パス =====
using System;

namespace HanafudaAdvisor.Wpf.Services
{
    public sealed class ScreenOcr
    {
        public sealed record ReadResult(int[] HandMonths, int[] FieldMonths);

        // 何も読まず空配列を返す（手動入力で使う）
        public static ReadResult ReadMonths(object? _ = null)
            => new ReadResult(Array.Empty<int>(), Array.Empty<int>());

        public static void ApplyToGameState(HanafudaAdvisor.Wpf.Models.GameState g, ReadResult r)
        {
            // no-op
        }
    }
}
#endif
