using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace HanafudaAdvisor.Wpf.Services
{
    public static class ScreenDigitReaderCv
    {
        // 0..1 の相対矩形
        public record RatioRect(double X, double Y, double W, double H)
        {
            public Rectangle ToPixelRect(int w, int h)
                => new((int)(X * w), (int)(Y * h), (int)(W * w), (int)(H * h));
        }

        public sealed class RoiConfig
        {
            public List<RatioRect> HandDigits { get; } = new();
            public List<RatioRect> FieldDigits { get; } = new();

            public static RoiConfig CreateDefault()
            {
                var c = new RoiConfig();
                // ★あなたの解像度に合わせて後で微調整（1920x1080ベースのあたり値）
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

        // ---- public API ------------------------------------------------------
        public sealed record ReadResult(int[] HandMonths, int[] FieldMonths);

        public static ReadResult ReadMonths(RoiConfig? cfg = null)
        {
            cfg ??= RoiConfig.CreateDefault();
            using var bmp = CaptureScreen();
            using var mat = BitmapConverter.ToMat(bmp);

            // テンプレを読み込み
            var templates = LoadTemplates("Assets/Digits");
            if (templates.Count == 0)
                throw new InvalidOperationException(
                    "テンプレがありません。まず『画面から読取』横の ▼ から『ROIスナップ保存』を実行し、" +
                    "Assets\\RoiSnaps に出力された画像を 1.png〜12.png にリネームして Assets\\Digits へ置いてください。");

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

            int idx = 1;
            foreach (var r in cfg.HandDigits.Concat(cfg.FieldDigits))
            {
                using var crop = CropAndPrep(big, r);
                Cv2.ImWrite(Path.Combine(dir, $"snap_{idx:D2}.png"), crop);
                idx++;
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

        // ---- core ------------------------------------------------------------
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
                var name = Path.GetFileNameWithoutExtension(path);
                if (int.TryParse(name, out int n) && 1 <= n && n <= 12)
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
            var roi = new Rect(
                (int)(r.X * big.Width), (int)(r.Y * big.Height),
                (int)(r.W * big.Width), (int)(r.H * big.Height));
            roi = roi & new Rect(0, 0, big.Width, big.Height);
            var cut = new Mat(big, roi);
            var gray = new Mat(); Cv2.CvtColor(cut, gray, ColorConversionCodes.BGRA2GRAY);
            // 明暗差を強調して数字のみを残す（ゲームUIに合わせて軽く開閉処理）
