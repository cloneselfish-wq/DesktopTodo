using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopTodo
{
    // Settings popup: the gear button on the title bar opens a small panel with
    // toggles for autostart, topmost and desktop embed. Closes on focus loss.
    public partial class MainWindow
    {
        private Window settingsWindow;

        private void ShowSettings()
        {
            if (settingsWindow != null) { settingsWindow.Activate(); return; }

            Window w = new Window();
            settingsWindow = w;
            w.Title = "设置";
            w.Width = 292;
            w.Height = 202;
            w.WindowStyle = WindowStyle.None;
            w.ResizeMode = ResizeMode.NoResize;
            w.AllowsTransparency = true;
            w.Background = Brushes.Transparent;
            w.Topmost = true;
            w.ShowInTaskbar = false;
            w.WindowStartupLocation = WindowStartupLocation.Manual;
            w.Left = Left + Width - w.Width - 12;
            w.Top = Top + 36;

            Border root = new Border();
            root.Background = Brushes.White;
            root.CornerRadius = new CornerRadius(14);
            root.BorderBrush = Theme.CardBorder;
            root.BorderThickness = new Thickness(1);
            root.Margin = new Thickness(10);
            root.Effect = Theme.CardShadow();
            w.Content = root;

            StackPanel sp = new StackPanel();
            root.Child = sp;

            // Header: drag to move, ✕ to close
            Border header = new Border();
            header.Padding = new Thickness(14, 11, 8, 7);
            header.Background = Brushes.Transparent;
            header.Cursor = Cursors.Hand;
            header.MouseLeftButtonDown += delegate
            {
                try { w.DragMove(); } catch { }
            };
            sp.Children.Add(header);

            Grid hg = new Grid();
            ColumnDefinition h0 = new ColumnDefinition(); h0.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition h1 = new ColumnDefinition(); h1.Width = GridLength.Auto;
            hg.ColumnDefinitions.Add(h0);
            hg.ColumnDefinitions.Add(h1);
            header.Child = hg;

            TextBlock title = new TextBlock();
            title.Text = "设置";
            title.FontSize = 13.5;
            title.FontWeight = FontWeights.SemiBold;
            title.Foreground = Theme.TextPrimary;
            title.VerticalAlignment = VerticalAlignment.Center;
            hg.Children.Add(title);
            Grid.SetColumn(title, 0);

            hg.Children.Add(MakeSettingsIconButton("\uE711", delegate { w.Close(); }));
            Grid.SetColumn((Border)hg.Children[hg.Children.Count - 1], 1);

            // Toggles (state is re-read after each change so failed operations revert)
            sp.Children.Add(MakeToggleRow("开机自动启动", AutoStart.IsEnabled(),
                delegate { return AutoStart.IsEnabled(); },
                delegate(bool on)
                {
                    try { AutoStart.SetEnabled(on); }
                    catch (Exception ex) { Logger.Log("Settings autostart: " + ex.Message); }
                }));
            sp.Children.Add(MakeToggleRow("窗口置顶", IsTopmost,
                delegate { return IsTopmost; },
                delegate(bool on) { SetTopmost(on); }));
            sp.Children.Add(MakeToggleRow("贴入桌面(壁纸层)", IsEmbedded,
                delegate { return IsEmbedded; },
                delegate(bool on) { if (on != IsEmbedded) ToggleEmbed(); }));

            w.Closed += delegate { settingsWindow = null; };
            w.Deactivated += delegate { w.Close(); };
            w.Show();
        }

        private Border MakeSettingsIconButton(string glyph, Action onClick)
        {
            return Theme.GhostButton(glyph, 11, null, false, onClick);
        }

        // One settings row: label on the left, pill switch on the right.
        private Border MakeToggleRow(string label, bool initial, Func<bool> getState, Action<bool> onChange)
        {
            Border row = new Border();
            row.Padding = new Thickness(14, 10, 12, 10);
            row.Background = Brushes.Transparent;
            row.MouseEnter += delegate { row.Background = BrushFrom(0xF6, 0xF7, 0xFA); };
            row.MouseLeave += delegate { row.Background = Brushes.Transparent; };

            Grid g = new Grid();
            ColumnDefinition c0 = new ColumnDefinition(); c0.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition c1 = new ColumnDefinition(); c1.Width = GridLength.Auto;
            g.ColumnDefinitions.Add(c0);
            g.ColumnDefinitions.Add(c1);
            row.Child = g;

            TextBlock txt = new TextBlock();
            txt.Text = label;
            txt.FontSize = 12.5;
            txt.Foreground = BrushFrom(0x2B, 0x2F, 0x36);
            txt.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(txt);
            Grid.SetColumn(txt, 0);

            bool on = initial;
            SolidColorBrush onBrush = BrushFrom(0x4F, 0x6B, 0xED);
            SolidColorBrush offBrush = BrushFrom(0xC9, 0xCF, 0xDB);

            Border pill = new Border();
            pill.Width = 40;
            pill.Height = 20;
            pill.CornerRadius = new CornerRadius(10);
            pill.Background = on ? onBrush : offBrush;
            pill.Cursor = Cursors.Hand;
            pill.VerticalAlignment = VerticalAlignment.Center;
            pill.ToolTip = "点击切换";

            Border knob = new Border();
            knob.Width = 16;
            knob.Height = 16;
            knob.CornerRadius = new CornerRadius(8);
            knob.Background = Brushes.White;
            knob.HorizontalAlignment = HorizontalAlignment.Left;
            knob.Margin = new Thickness(on ? 22 : 2, 0, 0, 0);
            pill.Child = knob;

            row.MouseLeftButtonUp += delegate
            {
                bool target = !on;
                onChange(target);
                bool actual;
                try { actual = getState(); } catch { actual = target; }
                on = actual;
                pill.Background = on ? onBrush : offBrush;
                knob.Margin = new Thickness(on ? 22 : 2, 0, 0, 0);
            };

            g.Children.Add(pill);
            Grid.SetColumn(pill, 1);

            return row;
        }
    }
}
