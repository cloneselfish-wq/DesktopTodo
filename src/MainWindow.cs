using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DesktopTodo
{
    public partial class MainWindow : Window
    {
        private TrayController tray;
        private bool closeTipShown;
        private bool allowClose;

        private Border titleBar;
        private Border pinBtn;
        private TextBlock pinGlyph;

        private Border addPanel;
        private TextBox inputBox;
        private TextBlock inputPlaceholder;
        private DatePicker duePicker;

        private ScrollViewer activeScroller;
        private StackPanel activeList;
        private Border emptyState;

        private bool embedded;
        private bool embedDragging;
        private Point embedDragStart;
        private Rect embedDragOrigin;

        public bool IsTopmost { get { return Topmost; } }
        public bool IsEmbedded { get { return embedded; } }

        public MainWindow()
        {
            Title = "桌面待办";
            Width = 380;
            Height = 640;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowActivated = false;
            Background = BrushFrom(0xF6, 0xF7, 0xFA);
            FontFamily = new FontFamily("Microsoft YaHei UI");
            FontSize = 13;

            Settings s = Store.Data.Settings;
            embedded = s.DesktopEmbed;
            Topmost = embedded ? false : s.Topmost;
            ShowInTaskbar = !embedded;

            InitPosition();
            BuildUi();
            UpdatePinGlyph();

            try
            {
                System.Drawing.Icon ico = TrayController.MakeIcon();
                BitmapSource bs = Imaging.CreateBitmapSourceFromHIcon(
                    ico.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bs.Freeze();
                Icon = bs;
            }
            catch { }

            SourceInitialized += delegate
            {
                if (embedded)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ApplyEmbed));
                }
            };
            Loaded += delegate { RefreshAll(); };
            Closing += OnClosing;
        }

        public void AttachTray(TrayController t) { tray = t; }

        // ---------- layout ----------

        private void BuildUi()
        {
            Grid root = new Grid();
            RowDefinition r0 = new RowDefinition(); r0.Height = new GridLength(42);
            RowDefinition r1 = new RowDefinition(); r1.Height = GridLength.Auto;
            RowDefinition r2 = new RowDefinition(); r2.Height = new GridLength(1, GridUnitType.Star);
            RowDefinition r3 = new RowDefinition(); r3.Height = GridLength.Auto;
            root.RowDefinitions.Add(r0);
            root.RowDefinitions.Add(r1);
            root.RowDefinitions.Add(r2);
            root.RowDefinitions.Add(r3);
            Content = root;

            Border tb = BuildTitleBar();
            Grid.SetRow(tb, 0);
            root.Children.Add(tb);

            Border ap = BuildAddPanel();
            Grid.SetRow(ap, 1);
            root.Children.Add(ap);

            activeScroller = new ScrollViewer();
            activeScroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            activeScroller.Margin = new Thickness(10, 8, 4, 0);
            activeList = new StackPanel();
            activeScroller.Content = activeList;
            Grid.SetRow(activeScroller, 2);
            root.Children.Add(activeScroller);

            Border ds = BuildDoneSection();
            Grid.SetRow(ds, 3);
            root.Children.Add(ds);
        }

        private Border BuildTitleBar()
        {
            titleBar = new Border();
            titleBar.Background = new LinearGradientBrush(
                Color.FromRgb(0x4F, 0x6B, 0xED),
                Color.FromRgb(0x6C, 0x88, 0xF4),
                90);
            titleBar.MouseLeftButtonDown += TitleBar_MouseLeftButtonDown;
            titleBar.MouseMove += TitleBar_MouseMove;
            titleBar.MouseLeftButtonUp += TitleBar_MouseUp;

            Grid g = new Grid();
            ColumnDefinition c0 = new ColumnDefinition();
            ColumnDefinition c1 = new ColumnDefinition(); c1.Width = GridLength.Auto;
            g.ColumnDefinitions.Add(c0);
            g.ColumnDefinitions.Add(c1);
            titleBar.Child = g;

            TextBlock title = new TextBlock();
            title.Text = "待办清单";
            title.Foreground = Brushes.White;
            title.FontWeight = FontWeights.Bold;
            title.FontSize = 14;
            title.VerticalAlignment = VerticalAlignment.Center;
            title.Margin = new Thickness(14, 0, 0, 0);
            g.Children.Add(title);
            Grid.SetColumn(title, 0);

            StackPanel btns = new StackPanel();
            btns.Orientation = Orientation.Horizontal;
            btns.VerticalAlignment = VerticalAlignment.Center;
            btns.Margin = new Thickness(0, 0, 8, 0);
            g.Children.Add(btns);
            Grid.SetColumn(btns, 1);

            pinBtn = MakeTitleIcon("\uE718", "窗口置顶", delegate { SetTopmost(!Topmost); });
            pinGlyph = (TextBlock)pinBtn.Child;
            btns.Children.Add(pinBtn);

            btns.Children.Add(MakeTitleIcon("\uE921", "最小化", delegate { MinimizeOrHide(); }));
            btns.Children.Add(MakeTitleIcon("\uE710", "隐藏到托盘", delegate { HideToTray(); }));

            return titleBar;
        }

        private Border MakeTitleIcon(string glyph, string tooltip, Action onClick)
        {
            Border b = new Border();
            b.Width = 34;
            b.Height = 28;
            b.CornerRadius = new CornerRadius(6);
            b.Background = Brushes.Transparent;
            b.Cursor = Cursors.Hand;
            b.ToolTip = tooltip;

            TextBlock t = new TextBlock();
            t.Text = glyph;
            t.FontFamily = new FontFamily("Segoe MDL2 Assets");
            t.FontSize = 12;
            t.Foreground = Brushes.White;
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.VerticalAlignment = VerticalAlignment.Center;
            b.Child = t;

            b.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; };
            b.MouseEnter += delegate { b.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)); };
            b.MouseLeave += delegate { b.Background = Brushes.Transparent; };
            b.MouseLeftButtonUp += delegate { onClick(); };
            return b;
        }

        private Border BuildAddPanel()
        {
            addPanel = new Border();
            addPanel.Background = Brushes.White;
            addPanel.CornerRadius = new CornerRadius(10);
            addPanel.Margin = new Thickness(10, 8, 10, 0);
            addPanel.Padding = new Thickness(10, 8, 10, 8);
            addPanel.BorderBrush = BrushFrom(0xE8, 0xEA, 0xF0);
            addPanel.BorderThickness = new Thickness(1);

            Grid g = new Grid();
            RowDefinition r0 = new RowDefinition(); r0.Height = GridLength.Auto;
            RowDefinition r1 = new RowDefinition(); r1.Height = GridLength.Auto;
            g.RowDefinitions.Add(r0);
            g.RowDefinitions.Add(r1);
            addPanel.Child = g;

            Grid inputGrid = new Grid();
            inputBox = new TextBox();
            inputBox.FontSize = 14;
            inputBox.BorderThickness = new Thickness(0);
            inputBox.Background = Brushes.Transparent;
            inputBox.VerticalContentAlignment = VerticalAlignment.Center;
            inputBox.Padding = new Thickness(0, 2, 0, 2);
            inputBox.TextChanged += delegate
            {
                inputPlaceholder.Visibility = string.IsNullOrEmpty(inputBox.Text)
                    ? Visibility.Visible : Visibility.Collapsed;
            };
            inputBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) AddItem();
            };
            inputPlaceholder = new TextBlock();
            inputPlaceholder.Text = "输入待办事项，回车或点击添加…";
            inputPlaceholder.Foreground = BrushFrom(0xB0, 0xB7, 0xC3);
            inputPlaceholder.VerticalAlignment = VerticalAlignment.Center;
            inputPlaceholder.IsHitTestVisible = false;
            inputGrid.Children.Add(inputBox);
            inputGrid.Children.Add(inputPlaceholder);
            g.Children.Add(inputGrid);
            Grid.SetRow(inputGrid, 0);

            Grid row2 = new Grid();
            ColumnDefinition d0 = new ColumnDefinition(); d0.Width = GridLength.Auto;
            ColumnDefinition d1 = new ColumnDefinition(); d1.Width = GridLength.Auto;
            ColumnDefinition d2 = new ColumnDefinition(); d2.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition d3 = new ColumnDefinition(); d3.Width = GridLength.Auto;
            row2.ColumnDefinitions.Add(d0);
            row2.ColumnDefinitions.Add(d1);
            row2.ColumnDefinitions.Add(d2);
            row2.ColumnDefinitions.Add(d3);
            row2.Margin = new Thickness(0, 8, 0, 0);

            TextBlock dueLabel = new TextBlock();
            dueLabel.Text = "截止(可选):";
            dueLabel.Foreground = BrushFrom(0x6B, 0x72, 0x80);
            dueLabel.FontSize = 12;
            dueLabel.VerticalAlignment = VerticalAlignment.Center;
            row2.Children.Add(dueLabel);
            Grid.SetColumn(dueLabel, 0);

            duePicker = new DatePicker();
            duePicker.Width = 138;
            duePicker.Margin = new Thickness(6, 0, 0, 0);
            duePicker.VerticalAlignment = VerticalAlignment.Center;
            row2.Children.Add(duePicker);
            Grid.SetColumn(duePicker, 1);

            Border addBtn = new Border();
            addBtn.Background = BrushFrom(0x4F, 0x6B, 0xED);
            addBtn.CornerRadius = new CornerRadius(7);
            addBtn.Padding = new Thickness(14, 5, 14, 5);
            addBtn.Cursor = Cursors.Hand;
            addBtn.VerticalAlignment = VerticalAlignment.Center;
            TextBlock addTxt = new TextBlock();
            addTxt.Text = "＋ 添加";
            addTxt.Foreground = Brushes.White;
            addTxt.FontWeight = FontWeights.SemiBold;
            addBtn.Child = addTxt;
            addBtn.MouseEnter += delegate { addBtn.Background = BrushFrom(0x43, 0x5C, 0xD6); };
            addBtn.MouseLeave += delegate { addBtn.Background = BrushFrom(0x4F, 0x6B, 0xED); };
            addBtn.MouseLeftButtonUp += delegate { AddItem(); };
            row2.Children.Add(addBtn);
            Grid.SetColumn(addBtn, 3);

            g.Children.Add(row2);
            Grid.SetRow(row2, 1);

            return addPanel;
        }

        // ---------- window behaviors ----------

        private void InitPosition()
        {
            Settings s = Store.Data.Settings;
            bool ok = false;
            if (s.HasPos)
            {
                double vl = SystemParameters.VirtualScreenLeft;
                double vt = SystemParameters.VirtualScreenTop;
                double vr = vl + SystemParameters.VirtualScreenWidth;
                double vb = vt + SystemParameters.VirtualScreenHeight;
                if (s.PosX >= vl - 60 && s.PosY >= vt - 10 && s.PosX + Width <= vr + 60 && s.PosY + 90 <= vb + 60)
                {
                    Left = s.PosX;
                    Top = s.PosY;
                    ok = true;
                }
            }
            if (!ok)
            {
                Rect wa = SystemParameters.WorkArea;
                Left = wa.Right - Width - 28;
                Top = wa.Bottom - Height - 28;
            }
        }

        public void SavePosition()
        {
            try
            {
                if (embedded)
                {
                    Rect r = DesktopHost.GetBounds(this);
                    Store.Data.Settings.PosX = r.Left;
                    Store.Data.Settings.PosY = r.Top;
                }
                else
                {
                    Store.Data.Settings.PosX = Left;
                    Store.Data.Settings.PosY = Top;
                }
                Store.Data.Settings.HasPos = true;
                Store.Save();
            }
            catch (Exception ex) { Logger.Log("SavePosition: " + ex.Message); }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount > 1) return;
            if (embedded)
            {
                embedDragStart = Mouse.GetPosition(titleBar);
                embedDragOrigin = DesktopHost.GetBounds(this);
                embedDragging = true;
                titleBar.CaptureMouse();
            }
            else
            {
                try
                {
                    DragMove();
                    SavePosition();
                }
                catch { }
            }
        }

        private void TitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (!embedDragging) return;
            Point p = Mouse.GetPosition(titleBar);
            double dx = p.X - embedDragStart.X;
            double dy = p.Y - embedDragStart.Y;
            Matrix m = Matrix.Identity;
            PresentationSource ps = PresentationSource.FromVisual(this);
            if (ps != null && ps.CompositionTarget != null) m = ps.CompositionTarget.TransformToDevice;
            DesktopHost.SetEmbedPosition(this,
                embedDragOrigin.Left + dx * m.M11,
                embedDragOrigin.Top + dy * m.M22);
        }

        private void TitleBar_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (embedDragging)
            {
                embedDragging = false;
                titleBar.ReleaseMouseCapture();
                SavePosition();
            }
        }

        private void MinimizeOrHide()
        {
            if (embedded) HideToTray();
            else WindowState = WindowState.Minimized;
        }

        public void ToggleVisible()
        {
            if (IsVisible) HideToTray();
            else ShowFromTray();
        }

        public void HideToTray()
        {
            SavePosition();
            Hide();
            if (tray != null && !closeTipShown)
            {
                closeTipShown = true;
                tray.ShowTip("桌面待办", "已隐藏到托盘，双击托盘图标可再次打开");
            }
        }

        public void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        public void SetTopmost(bool on)
        {
            if (on && embedded) UnembedNow();
            Topmost = on;
            Store.Data.Settings.Topmost = on;
            Store.Save();
            UpdatePinGlyph();
        }

        private void UpdatePinGlyph()
        {
            if (pinGlyph != null) pinGlyph.Opacity = Topmost ? 1.0 : 0.55;
            if (pinBtn != null) pinBtn.ToolTip = Topmost ? "已置顶（点击取消）" : "窗口置顶";
        }

        // ---------- desktop embed mode ----------

        public void ToggleEmbed()
        {
            if (embedded) UnembedNow();
            else EmbedNow();
        }

        private void EmbedNow()
        {
            try
            {
                SavePosition();
                if (Topmost)
                {
                    Topmost = false;
                    Store.Data.Settings.Topmost = false;
                }
                ShowInTaskbar = false; // recreates the hwnd, so must happen before Embed()
                UpdatePinGlyph();
                bool ok = DesktopHost.Embed(this);
                if (ok)
                {
                    embedded = true;
                    Store.Data.Settings.DesktopEmbed = true;
                    Store.Save();
                    if (tray != null) tray.ShowTip("桌面待办", "已贴入桌面壁纸层，可通过托盘菜单取消");
                }
                else
                {
                    ShowInTaskbar = true;
                    if (tray != null) tray.ShowTip("桌面待办", "贴入桌面失败，已保持普通窗口模式");
                }
            }
            catch (Exception ex)
            {
                Logger.Log("EmbedNow: " + ex);
                embedded = false;
                ShowInTaskbar = true;
                Store.Data.Settings.DesktopEmbed = false;
                Store.Save();
            }
        }

        private void UnembedNow()
        {
            try { DesktopHost.Unembed(this); }
            catch (Exception ex) { Logger.Log("UnembedNow: " + ex); }
            embedded = false;
            ShowInTaskbar = true;
            Store.Data.Settings.DesktopEmbed = false;
            Store.Save();
        }

        private void ApplyEmbed()
        {
            try
            {
                bool ok = DesktopHost.Embed(this);
                if (ok)
                {
                    Settings s = Store.Data.Settings;
                    if (s.HasPos) DesktopHost.SetEmbedPosition(this, s.PosX, s.PosY);
                }
                else
                {
                    embedded = false;
                    ShowInTaskbar = true;
                    Topmost = Store.Data.Settings.Topmost;
                    Store.Data.Settings.DesktopEmbed = false;
                    Store.Save();
                    UpdatePinGlyph();
                }
            }
            catch (Exception ex)
            {
                Logger.Log("ApplyEmbed: " + ex);
            }
        }

        // ---------- lifecycle ----------

        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (allowClose) return;
            e.Cancel = true;
            HideToTray();
        }

        public void PrepareExit()
        {
            allowClose = true;
            if (embedded)
            {
                try { DesktopHost.Unembed(this); } catch { }
                embedded = false;
            }
        }

        // ---------- helpers ----------

        internal static SolidColorBrush BrushFrom(byte r, byte g, byte b)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
