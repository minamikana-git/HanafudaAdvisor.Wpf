using HanafudaAdvisor.Wpf.Models;
using HanafudaAdvisor.Wpf.Services;
using System.Windows;
using SDRect = System.Drawing.Rectangle;


namespace HanafudaAdvisor.Wpf
{
    public partial class MainWindow : Window
    {
        private readonly GameState _g = new();
        private readonly Advisor _advisor = new();
        private TransparentHudWindow? _hud;

        public MainWindow()
        {
            InitializeComponent();
            RefreshLists();
        }

        private void RefreshLists()
        {
            HandList.ItemsSource = null; HandList.ItemsSource = _g.MyHand.ToList();
            FieldList.ItemsSource = null; FieldList.ItemsSource = _g.Field.ToList();
        }

        private void OnAddHand(object s, RoutedEventArgs e)
        {
            var dlg = new CardPickerDialog();
            if (dlg.ShowDialog() == true) { _g.MyHand.Add(dlg.Selected!); RefreshLists(); }
        }

        private void OnRemoveHand(object s, RoutedEventArgs e)
        {
            if (HandList.SelectedItem is Card c) { _g.MyHand.Remove(c); RefreshLists(); }
        }

        private void OnAddField(object s, RoutedEventArgs e)
        {
            var dlg = new CardPickerDialog();
            if (dlg.ShowDialog() == true) { _g.Field.Add(dlg.Selected!); RefreshLists(); }
        }

        private void OnRemoveField(object s, RoutedEventArgs e)
        {
            if (FieldList.SelectedItem is Card c) { _g.Field.Remove(c); RefreshLists(); }
        }

        private void OnCalcBest(object s, RoutedEventArgs e)
        {
            ResultList.Items.Clear();
            foreach (var x in _advisor.BestMoves(_g, 1200).OrderByDescending(x => x.Ev).Take(6))
                ResultList.Items.Add($"出す {x.Play} | 期待値={x.Ev:F2} | {x.Reason}");

            YakuList.Items.Clear();
            foreach (var y in Scoring.ListCompletedYaku(_g.MyPile))
                YakuList.Items.Add($"{y.name} +{y.points}");

            // （配札役を出している場合は）
            DealYakuList.Items.Clear();
            foreach (var y in Scoring.ListDealYaku(_g.MyHand))
                DealYakuList.Items.Add($"{y.name} +{y.points}");
        }

        private void OnPredictOpp(object s, RoutedEventArgs e)
        {
            ResultList.Items.Clear();
            foreach (var p in _advisor.PredictOpponentNext(_g, 1500))
                ResultList.Items.Add($"相手: 次は{p.Month}月を出す確率 {p.Probability:P1} | {p.Reason}");
        }

        private void OnShowHud(object s, RoutedEventArgs e)
        {
            _hud ??= new TransparentHudWindow();
            _hud.Owner = this;
            _hud.Topmost = true;
            _hud.Show();
            _hud.UpdateFromState(_g, _advisor);
        }

        private void OnReadFromScreen(object sender, RoutedEventArgs e)
        {
            try
            {
                var cfg = ScreenDigitReaderCv.RoiConfig.CreateDefault();
                var r = ScreenDigitReaderCv.ReadMonths(cfg);
                ScreenDigitReaderCv.ApplyToGameState(_g, r);
                RefreshLists();
                System.Windows.MessageBox.Show(
                    $"手札: {string.Join(",", r.HandMonths)}\n場: {string.Join(",", r.FieldMonths)}",
                    "読み取り結果");
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show("画面の読み取りに失敗: " + ex.Message);
            }
        }

        // ★ROIスナップ保存
        private void OnSaveRoiSnaps(object sender, RoutedEventArgs e)
        {
            try
            {
                var outDir = @"E:\HanafudaAdvisor.Wpf\Assets\RoiSnaps"; // ★固定先
                System.IO.Directory.CreateDirectory(outDir);

                var cfg = HanafudaAdvisor.Wpf.Services.ScreenDigitReaderCv.RoiConfig.CreateDefault();
                HanafudaAdvisor.Wpf.Services.ScreenDigitReaderCv.SaveRoiSnaps(cfg, outDir);

                System.Diagnostics.Process.Start("explorer.exe", outDir);
                System.Windows.MessageBox.Show($"ROIスナップを保存しました:\n{outDir}");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("ROIスナップ保存に失敗: " + ex.Message);
            }
        }

        private static SDRect AdjustToAspect(SDRect r, double target = 16.0 / 9.0)  // 16:9 に寄せる（中心維持）
        {
            double cur = (double)r.Width / r.Height;
            if (Math.Abs(cur - target) < 0.02) return r; // ほぼ16:9ならそのまま
            if (cur > target)  // 横長すぎ → 幅を詰める
            {
                int w = (int)Math.Round(r.Height * target);
                int x = r.Left + (r.Width - w) / 2;
                return new SDRect(x, r.Top, Math.Max(1, w), r.Height);
            }
            else               // 縦長すぎ → 高さを詰める
            {
                int h = (int)Math.Round(r.Width / target);
                int y = r.Top + (r.Height - h) / 2;
                return new SDRect(r.Left, Math.Max(0, y), r.Width, Math.Max(1, h));
            }
        }

        private void OnReadWithDrag(object sender, RoutedEventArgs e)
        {
            // 1) ドラッグで矩形取得
            var snip = new SnipOverlayWindow();
            if (snip.ShowDialog() != true || snip.SelectedRect is null) return;

            var rect = AdjustToAspect(snip.SelectedRect.Value); // 16:9 に寄せる

            // 2) 矩形キャプチャ → 読み取り
            using var bmp = ScreenDigitReaderCv.CaptureRect(rect);
            var cfg = ScreenDigitReaderCv.CreateFor1600x900();
            var r = ScreenDigitReaderCv.ReadMonthsFromBitmap(bmp, cfg);

            // 3) 反映
            ScreenDigitReaderCv.ApplyToGameState(_g, r);
            RefreshLists();
            System.Windows.MessageBox.Show($"手札: {string.Join(",", r.HandMonths)}\n場: {string.Join(",", r.FieldMonths)}",
                            "ドラッグ読取");
        }
    }
}
