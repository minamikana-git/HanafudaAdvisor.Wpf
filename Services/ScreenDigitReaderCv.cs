using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Extensions;

// エイリアス（混同回避）
using SDRect = System.Drawing.Rectangle;
using SDSize = System.Drawing.Size;
using CvSize = OpenCvSharp.Size;
using CvRect = OpenCvSharp.Rect;

namespace HanafudaAdvisor.Wpf.Services
{
    public static class ScreenDigitReaderCv
    {
        // ========== ROI ==========
        public record RatioRect(double X, double Y, double W, double H)
        {
            public SDRect ToPixelRect(int w, int h)
                => new SDRect((int)(X * w), (int)(Y * h), (int)(W * w), (int)(H * h));
        }

        public sealed class RoiConfig
        {
            public List<RatioRect> HandDigits { get; } = new();
            public List<RatioRect> FieldDigits { get; } = new();

            public static RoiConfig CreateDefault()
            {
                var c = new RoiConfig();
                for (int i = 0; i < 8; i++)
                    c.HandDigits.Add(new RatioRect(0.30 + i * 0.055, 0.88, 0.035, 0.06));

                var starts = new (double x, double y)[] {
                    (0.36,0.48),(0.46,0.48),(0.56,0.48),(0.66,0.48),
                    (0.36,0.62),(0.46,0.62),(0.56,0.62),(0.66,0.62)
                };
                foreach (var s in starts) c.FieldDigits.Add(new RatioRect(s.x, s.y, 0.035, 0.06));
                return c;
            }
        }

        // ========== Public API ==========
        public sealed record ReadResult(int[] HandMonths, int[] FieldMonths);

        public static ReadResult ReadMonths(RoiConfig? cfg = null)
        {
            cfg ??= RoiConfig.CreateDefault();
            using var bmp = CaptureScreen();
            using var mat = BitmapConverter.ToMat(bmp);

            var templates = LoadTemplates("Assets/Digits");
            if (templates.Count == 0)
                throw new InvalidOperationException(
                    "テンプレがありません。SaveRoiSnaps を実行して Assets\\RoiSnaps の画像を 1.png〜12.png にリネームし、Assets\\Digits に置いてください。");

            int[] hand = cfg.HandDigits.Select(r => RecognizeOne(mat, r, templates)).ToArray();
            int[] field = cfg.FieldDigits.Select(r => RecognizeOne(mat, r, templates)).ToArray();
            return new ReadResult(hand, field);
        }

        public static void SaveRoiSnaps(RoiConfig? cfg = null, string dir = "Assets/RoiSnaps")
        {
            cfg ??= RoiConfig.CreateDefault();
            Directory.CreateDirectory(dir);
            using var bmp = CaptureScreen();
            using var big = BitmapConverter.ToMat(bmp);

            int i = 1;
            foreach (var r in cfg.HandDigits.Concat(cfg.FieldDigits))
            {
                using var crop = CropAndPrep(big, r);
                Cv2.ImWrite(Path.Combine(dir, $"snap_{i:D2}.png"), crop);
                i++;
            }
        }

        public static void ApplyToGameState(HanafudaAdvisor.Wpf.Models.GameState g, ReadResult r)
        {
            g.MyHand.Clear(); g.Field.Clear();
            var used = new List<HanafudaAdvisor.Wpf.Models.Card>();

            foreach (var m in r.HandMonths.Where(x => 1 <= x && x <= 12))
            { var c = PickUnused(m, used); g.MyHand.Add(c); used.Add(c); }

            foreach (var m in r.FieldMonths.Where(x => 1 <= x && x <= 12))
            { var c = PickUnused(m, used); g.Field.Add(c); used.Add(c); }
        }

        // ========== Core ==========
        private static System.Drawing.Bitmap CaptureScreen()
        {
            var b = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            var bmp = new System.Drawing.Bitmap(b.Width, b.Height, PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(b.Left, b.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            return bmp;
        }

        private static Dictionary<int, Mat> LoadTemplates(string dir)
        {
            var dict = new Dictionary<int, Mat>();
            if (!Directory.Exists(dir)) return dict;

            foreach (var path in Directory.EnumerateFiles(dir, "*.png"))
            {
                if (int.TryParse(Path.GetFileNameWithoutExtension(path), out int n) && 1 <= n && n <= 12)
                {
                    var m = Cv2.ImRead(path, ImreadModes.Grayscale);
                    Cv2.Threshold(m, m, 0, 255, ThresholdTypes.Otsu | ThresholdTypes.BinaryInv);
                    dict[n] = m;
                }
            }
            return dict;
        }

        private static Mat CropAndPrep(Mat big, RatioRect r)
        {
            var roi = new CvRect(
                (int)(r.X * big.Width), (int)(r.Y * big.Height),
                (int)(r.W * big.Width), (int)(r.H * big.Height));
            roi = roi & new CvRect(0, 0, big.Width, big.Height);

            var cut = new Mat(big, roi);
            var gray = new Mat();
            Cv2.CvtColor(cut, gray, ColorConversionCodes.BGRA2GRAY);
            Cv2.GaussianBlur(gray, gray, new CvSize(3, 3), 0);
            Cv2.Threshold(gray, gray, 0, 255, ThresholdTypes.Otsu | ThresholdTypes.BinaryInv);
            Cv2.MorphologyEx(gray, gray, MorphTypes.Open, Cv2.GetStructuringElement(MorphShapes.Rect, new CvSize(2, 2)));
            return gray;
        }

        private static int RecognizeOne(Mat screen, RatioRect r, Dictionary<int, Mat> templates)
        {
            using var probe = CropAndPrep(screen, r);

            double best = 0; int bestN = -1;
            foreach (var (n, templRaw) in templates)
            {
                using var templ = ResizeKeepWithin(templRaw, probe.Size());
                if (templ.Empty()) continue;

                using var res = new Mat();
                Cv2.MatchTemplate(probe, templ, res, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(res, out _, out double maxVal, out _, out _);
                if (maxVal > best) { best = maxVal; bestN = n; }
            }
            return (best >= 0.62) ? bestN : -1;
        }

        private static Mat ResizeKeepWithin(Mat templ, CvSize maxSize)
        {
            double s = Math.Min((double)maxSize.Width / templ.Width, (double)maxSize.Height / templ.Height);
            if (s <= 0) return templ.Clone();
            var dst = new Mat();
            Cv2.Resize(templ, dst,
                new CvSize(Math.Max(1, (int)(templ.Width * s)), Math.Max(1, (int)(templ.Height * s))),
                0, 0, InterpolationFlags.Area);
            return dst;
        }

        private static HanafudaAdvisor.Wpf.Models.Card PickUnused(int month, List<HanafudaAdvisor.Wpf.Models.Card> already)
        {
            var cand = HanafudaAdvisor.Wpf.Models.Deck.Full.Where(c => c.Month == month).ToList();
            foreach (var c in cand) if (!already.Contains(c)) return c;
            return cand.First();
        }
    }
}
