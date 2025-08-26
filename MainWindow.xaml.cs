using HanafudaAdvisor.Wpf.Models;
using HanafudaAdvisor.Wpf.Services;
using System.Linq;
using System.Windows;

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
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("画面の読み取りに失敗: " + ex.Message);
            }
        }

        private void OnSaveRoiSnaps(object sender, RoutedEventArgs e)
        {
            ScreenDigitReaderCv.SaveRoiSnaps();
            System.Windows.MessageBox.Show("Assets\\RoiSnaps に切り出しを保存しました。\n1.png～12.png に手動でリネームし、Assets\\Digits に移動してください。");
        }

    }
}
