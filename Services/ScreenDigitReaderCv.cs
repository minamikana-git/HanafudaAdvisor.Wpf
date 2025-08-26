// File: Services/ScreenDigitReaderCv.cs
// OpenCvSharp を使った「数字バッジ」テンプレ一致リーダー（ウィンドウのクライアント領域をキャプチャ）
// 依存: OpenCvSharp4 / OpenCvSharp4.runtime.win / OpenCvSharp4.Extensions

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using OpenCvSharp;
using OpenCvSharp.Extensions;

// エイリアス（System.Drawing と OpenCvSharp の型衝突回避）
using SDRect = System.Drawing.Rectangle;
using SDSize = System.Drawing.Size;
using CvSize = OpenCvSharp.Size;
using CvRect = OpenCvSharp.Rect;

namespace HanafudaAdvisor.Wpf.Services
{
    public static class ScreenDigitReaderCv
    {
        // ==========================
        // Win32: アクティブウィンドウのクライアント領域キャプチャ
        // ==========================
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }

        private static class Win32
        {
            [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
            [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
            [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
        }

        /// <summary>前面アクティブなウィンドウのクライアント領域を丸ごと取得（1600x900のウィンドウ想定）</summary>
        private static Bitmap CaptureActiveWindowClient()
        {
            var h = Win32.GetForegroundWindow();
            if (h == IntPtr.Zero) throw new InvalidOperationException("前面ウィンドウが取得できません。");

            if (!Win32.GetClientRect(h, out var rc)) throw new InvalidOperationException("ClientRect取得失敗");

            var lt = new POINT { X = rc.Left, Y = rc.Top };
            var rb = new POINT { X = rc.Right, Y = rc.Bottom };
            Win32.ClientToScreen(h, ref lt);
            Win32.ClientToScreen(h, ref rb);

            var rect = new SDRect(lt.X, lt.Y, Math.Max(1, rb.X - lt.X), Math.Max(1, rb.Y - lt.Y));
            var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(rect.Left, rect.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            return bmp;
        }

        // ==========================
        // ROI（0..1 の相対座標）
        // ==========================
        public record RatioRect(double X, double Y, double W, double H)
        {
            public SDRect ToPixelRect(int w, int h)
                => new SDRect((int)(X * w), (int)(Y * h), (int)(W * w), (int)(H * h));
        }

        public sealed class RoiConfig
        {
            public List<RatioRect> HandDigits { get; } = new();
            public List<RatioRect> FieldDigits { get; } = new();

            /// <summary>汎用の当たり（環境によりズレる場合あり）</summary>
            public static RoiConfig CreateDefault()
            {
                var c = new RoiConfig();
                for (int i = 0; i < 8; i++)
                    c.HandDigits.Add(new RatioRect(0.30 + i * 0.055, 0.88, 0.045, 0.075));

                var starts = new (double x, double y)[] {
                    (0.36,0.46),(0.46,0.46),(0.56,0.46),(0.66,0.46),
                    (0.36,0.60),(0.46,0.60),(0.56,0.60),(0.66,0.60)
                };
                foreach (var s in starts) c.FieldDigits.Add(new RatioRect(s.x, s.y, 0.045, 0.075));
                return c;
            }
        }

        /// <summary>1600x900 のウィンドウ用プリセット（数字バッジを広めに囲む）</summary>
        public static RoiConfig CreateFor1600x900()
        {
            var c = new RoiConfig();

            double handStartX = 0.295;
            double stepX = 0.0615;
            for (int i = 0; i < 8; i++)
                c.HandDigits.Add(new RatioRect(handStartX + i * stepX, 0.865, 0.055, 0.095));

            (double x, double y)[] pos =
            {
                (0.355,0.455),(0.455,0.455),(0.555,0.455),(0.655,0.455),
                (0.355,0.600),(0.455,0.600),(0.555,0.600),(0.655,0.600),
            };
            foreach (var p in pos) c.FieldDigits.Add(new RatioRect(p.x, p.y, 0.055, 0.095));
            return c;
        }

        // ==========================
        // 公開 API
        // ==========================
        public sealed record ReadResult(int[] HandMonths, int[] FieldMonths);

        public static ReadResult ReadMonths(RoiConfig? cfg = null)
        {
            cfg ??= CreateFor1600x900();
            using var bmp = CaptureActiveWindowClient();
            using var mat = BitmapConverter.ToMat(bmp);

            var templates = LoadTemplates("Assets/Digits");
            if (templates.Count == 0)
                throw new InvalidOperationException(
                    "テンプレがありません。まず ROIスナップ保存 を実行し、" +
                    "Assets\\RoiSnaps の画像を 1.png〜12.png にリネームして Assets\\Digits に置いてください。");

            int[] hand = cfg.HandDigits.Select(r => RecognizeOne(mat, r, templates)).ToArray();
            int[] field = cfg.FieldDigits.Select(r => RecognizeOne(mat, r, templates)).ToArray();
            return new ReadResult(hand, field);
        }

        public static void SaveRoiSnaps(RoiConfig? cfg = null, string? dir = null)
        {
            cfg ??= CreateFor1600x900();
            dir ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "RoiSnaps");
            Directory.CreateDirectory(dir);

            using var bmp = CaptureActiveWindowClient();
            using var big = BitmapConverter.ToMat(bmp);

            int i = 1;
            foreach (var r in cfg.HandDigits.Concat(cfg.FieldDigits))
            {
                using var crop = CropAndPrep(big, r);
                Cv2.ImWrite(Path.Combine(dir, $"snap_{i:D2}.png"), crop);
                i++;
            }
        }

        /// <summary>ROI の当たり確認用に矩形を描いた画像を保存</summary>
        public static void SaveRoiPreview(RoiConfig? cfg, string outPath)
        {
            cfg ??= CreateFor1600x900();
            using var bmp = CaptureActiveWindowClient();
            using var mat = BitmapConverter.ToMat(bmp);

            foreach (var r in cfg.HandDigits.Concat(cfg.FieldDigits))
            {
                var rc = new CvRect(
                    (int)(r.X * mat.Width), (int)(r.Y * mat.Height),
                    (int)(r.W * mat.Width), (int)(r.H * mat.Height));
                rc = rc & new CvRect(0, 0, mat.Width, mat.Height);
                Cv2.Rectangle(mat, rc, Scalar.Lime, 2);
            }
            Cv2.ImWrite(outPath, mat);
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

        // ==========================
        // 内部実装
        // ==========================
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
            // BGRA → Gray, ぼかし, Otsu二値化, 軽いオープンでノイズ除去
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
            return (best >= 0.62) ? bestN : -1; // 閾値は必要に応じて調整
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

        // 選択された画面矩形をキャプチャ
        public static Bitmap CaptureRect(SDRect r)
        {
            var bmp = new Bitmap(r.Width, r.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(r.Left, r.Top, 0, 0, bmp.Size, System.Drawing.CopyPixelOperation.SourceCopy);
            return bmp;
        }

        // Bitmap を直接読み取る
        public static ReadResult ReadMonthsFromBitmap(Bitmap bmp, RoiConfig? cfg = null)
        {
            cfg ??= CreateFor1600x900();            // 16:9 基準のROI
            using var mat = BitmapConverter.ToMat(bmp);
            var templates = LoadTemplates("Assets/Digits");
            if (templates.Count == 0)
                throw new InvalidOperationException("Assets\\Digits に 1.png〜12.png を置いてください。");

            int[] hand = cfg.HandDigits.Select(r => RecognizeOne(mat, r, templates)).ToArray();
            int[] field = cfg.FieldDigits.Select(r => RecognizeOne(mat, r, templates)).ToArray();
            return new ReadResult(hand, field);
        }
    }
}


