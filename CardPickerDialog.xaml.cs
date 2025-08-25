using System.Linq;
using System.Windows;
using HanafudaAdvisor.Wpf.Models;

namespace HanafudaAdvisor.Wpf
{
    public partial class CardPickerDialog : Window
    {
        public Card? Selected { get; private set; }

        public CardPickerDialog()
        {
            InitializeComponent();
            MonthBox.ItemsSource = Deck.Months.ToList();
            TypeBox.ItemsSource = Deck.Types.ToList();
            MonthBox.SelectedIndex = 0;
            TypeBox.SelectedIndex = 0;
        }

        private void OnPreview(object s, RoutedEventArgs e)
        {
            int month = (int)MonthBox.SelectedItem!;
            var type = (CardType)TypeBox.SelectedItem!;
            PreviewList.ItemsSource = Deck.Full.Where(c => c.Month == month && c.Type == type).ToList();
        }

        private void OnOk(object s, RoutedEventArgs e)
        {
            if (PreviewList.SelectedItem is Card c) { Selected = c; DialogResult = true; }
            else System.Windows.MessageBox.Show("候補を選択してください");
        }

        private void OnCancel(object s, RoutedEventArgs e) => DialogResult = false;
    }
}
