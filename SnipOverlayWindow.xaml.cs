using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SDRect = System.Drawing.Rectangle;

namespace HanafudaAdvisor.Wpf
{
    public partial class SnipOverlayWindow : Window
    {
        private System.Windows.Point _start; private bool _drag;
        public SDRect? SelectedRect { get; private set; }

        public SnipOverlayWindow() { InitializeComponent(); Cursor = System.Windows.Input.Cursors.Cross; }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 全仮想スクリーンを覆う
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
            Focus();
        }

        private void OnDown(object sender, MouseButtonEventArgs e)
        {
            _drag = true; _start = e.GetPosition(Root);
            Box.Visibility = Visibility.Visible;
            Canvas.SetLeft(Box, _start.X); Canvas.SetTop(Box, _start.Y);
            Box.Width = 0; Box.Height = 0; Root.CaptureMouse();
        }

        private void OnMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_drag) return;
            var p = e.GetPosition(Root);
            var x = Math.Min(p.X, _start.X);
            var y = Math.Min(p.Y, _start.Y);
            var w = Math.Abs(p.X - _start.X);
            var h = Math.Abs(p.Y - _start.Y);
            Canvas.SetLeft(Box, x); Canvas.SetTop(Box, y);
            Box.Width = w; Box.Height = h;
        }

        private void OnUp(object sender, MouseButtonEventArgs e)
        {
            if (!_drag) return; _drag = false; Root.ReleaseMouseCapture();
            if (Box.Width < 2 || Box.Height < 2) { DialogResult = false; return; }

            // DIPs → 物理解像度(px) + 仮想スクリーン原点を加味
            var src = PresentationSource.FromVisual(this);
            var m = src.CompositionTarget.TransformToDevice; // M11/M22 が倍率
            double l = Left + Canvas.GetLeft(Box);
            double t = Top + Canvas.GetTop(Box);
            int pxL = (int)Math.Round(l * m.M11);
            int pxT = (int)Math.Round(t * m.M22);
            int pxW = (int)Math.Round(Box.Width * m.M11);
            int pxH = (int)Math.Round(Box.Height * m.M22);

            SelectedRect = new SDRect(pxL, pxT, pxW, pxH);
            DialogResult = true;
        }

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { DialogResult = false; }
            else if (e.Key == Key.Enter && Box.Width > 1 && Box.Height > 1) { OnUp(sender, null); }
        }
    }
}
