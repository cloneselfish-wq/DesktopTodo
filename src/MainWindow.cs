using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        private TextBlock titleCount;

        private Border addPanel;
        private TextBox inputBox;
        private TextBlock inputPlaceholder;
        private DatePicker duePicker;
        private Border dueChip;
        private TextBlock dueChipText;
        private Border dueChipClear;

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
            Background = Theme.PageBg;
            FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI");
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
            RowDefinition r0 = new RowDefinition(); r0.Height = new GridLength(44);
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
            Style sbStyle = Theme.SlimScrollBar();
            if (sbStyle != null) activeScroller.Resources.Add(typeof(ScrollBar), sbStyle);
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
            titleBar.Background = Brushes.White;
            titleBar.BorderBrush = Theme.CardBorder;
            titleBar.BorderThickness = new Thickness(0, 0, 0, 1);
            titleBar.MouseLeftButtonDown += TitleBar_MouseLeftButtonDown;
            titleBar.MouseMove += TitleBar_MouseMove;
            titleBar.MouseLeftButtonUp += TitleBar_MouseUp;

            Grid g = new Grid();
            ColumnDefinition c0 = new ColumnDefinition();
            ColumnDefinition c1 = new ColumnDefinition(); c1.Width = GridLength.Auto;
            g.ColumnDefinitions.Add(c0);
            g.ColumnDefinitions.Add(c1);
            titleBar.Child = g;

            StackPanel left = new StackPanel();
            left.Orientation = Orientation.Horizontal;
            left.VerticalAlignment = VerticalAlignment.Center;
            left.Margin = new Thickness(14, 0, 0, 0);
            g.Children.Add(left);
            Grid.SetColumn(left, 0);

            Border dot = new Border();
            dot.Width = 9;
            dot.Height = 9;
            dot.CornerRadius = new CornerRadius(4.5);
            dot.Background = Theme.Accent;
            dot.VerticalAlignment = VerticalAlignment.Center;
            left.Children.Add(dot);

            TextBlock title = new TextBlock();
            title.Text = "待办清单";
            title.Foreground = Theme.TextPrimary;
            title.FontWeight = FontWeights.SemiBold;
            title.FontSize = 13;
            title.VerticalAlignment = VerticalAlignment.Center;
            title.Margin = new Thickness(9, 0, 0, 0);
            left.Children.Add(title);

            titleCount = new TextBlock();
            titleCount.Text = "";
            titleCount.FontSize = 11.5;
            titleCount.Foreground = Theme.TextTertiary;
            titleCount.VerticalAlignment = VerticalAlignment.Center;
            titleCount.Margin = new Thickness(8, 1, 0, 0);
            left.Children.Add(titleCount);

            StackPanel btns = new StackPanel();
            btns.Orientation = Orientation.Horizontal;
            btns.VerticalAlignment = VerticalAlignment.Center;
            btns.Margin = new Thickness(0, 0, 8, 0);
            g.Children.Add(btns);
            Grid.SetColumn(btns, 1);

            pinBtn = Theme.GhostButton("\uE718", 12, "窗口置顶", false, delegate { SetTopmost(!Topmost); });
            pinGlyph = (TextBlock)pinBtn.Child;
            btns.Children.Add(pinBtn);

            btns.Children.Add(Theme.GhostButton("\uE713", 12, "设置", false, delegate { ShowSettings(); }));
            btns.Children.Add(Theme.GhostButton("\uE921", 12, "最小化", false, delegate { MinimizeOrHide(); }));
            btns.Children.Add(Theme.GhostButton("\uE8BB", 12, "隐藏到托盘", true, delegate { HideToTray(); }));

            return titleBar;
        }

        private Border BuildAddPanel()
        {
            addPanel = new Border();
            addPanel.Background = Brushes.White;
            addPanel.CornerRadius = new CornerRadius(14);
            addPanel.Margin = new Thickness(12, 10, 12, 2);
            addPanel.Padding = new Thickness(12, 11, 10, 10);
            addPanel.BorderBrush = Theme.CardBorder;
            addPanel.BorderThickness = new Thickness(1);
            addPanel.Effect = Theme.CardShadow();

            Grid g = new Grid();
            RowDefinition r0 = new RowDefinition(); r0.Height = GridLength.Auto;
            RowDefinition r1 = new RowDefinition(); r1.Height = GridLength.Auto;
            g.RowDefinitions.Add(r0);
            g.RowDefinitions.Add(r1);
            addPanel.Child = g;

            // Input row: borderless text box + round accent add button
            Grid inputGrid = new Grid();
            ColumnDefinition i0 = new ColumnDefinition(); i0.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition i1 = new ColumnDefinition(); i1.Width = GridLength.Auto;
            inputGrid.ColumnDefinitions.Add(i0);
            inputGrid.ColumnDefinitions.Add(i1);

            inputBox = new TextBox();
            inputBox.FontSize = 14;
            inputBox.BorderThickness = new Thickness(0);
            inputBox.Background = Brushes.Transparent;
            inputBox.VerticalContentAlignment = VerticalAlignment.Center;
            inputBox.Padding = new Thickness(2, 4, 2, 4);
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
            inputPlaceholder.Text = "输入待办事项，回车添加…";
            inputPlaceholder.Foreground = Theme.TextTertiary;
            inputPlaceholder.VerticalAlignment = VerticalAlignment.Center;
            inputPlaceholder.IsHitTestVisible = false;
            inputGrid.Children.Add(inputBox);
            inputGrid.Children.Add(inputPlaceholder);

            Border addBtn = new Border();
            addBtn.Width = 34;
            addBtn.Height = 34;
            addBtn.CornerRadius = new CornerRadius(17);
            addBtn.Background = Theme.Accent;
            addBtn.Cursor = Cursors.Hand;
            addBtn.Margin = new Thickness(8, 0, 0, 0);
            addBtn.VerticalAlignment = VerticalAlignment.Center;
            TextBlock addTxt = Theme.Glyph("\uE710", 13, Brushes.White);
            addTxt.FontWeight = FontWeights.SemiBold;
            addTxt.HorizontalAlignment = HorizontalAlignment.Center;
            addTxt.VerticalAlignment = VerticalAlignment.Center;
            addBtn.Child = addTxt;
            addBtn.MouseEnter += delegate { addBtn.Background = Theme.AccentDark; };
            addBtn.MouseLeave += delegate { addBtn.Background = Theme.Accent; };
            addBtn.MouseLeftButtonUp += delegate { AddItem(); };
            inputGrid.Children.Add(addBtn);
            Grid.SetColumn(addBtn, 1);

            g.Children.Add(inputGrid);
            Grid.SetRow(inputGrid, 0);

            // Chips row: due-date chip opens the (invisible) DatePicker's own popup
            Grid chips = new Grid();
            ColumnDefinition k0 = new ColumnDefinition(); k0.Width = GridLength.Auto;
            ColumnDefinition k1 = new ColumnDefinition(); k1.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition k2 = new ColumnDefinition(); k2.Width = GridLength.Auto;
            chips.ColumnDefinitions.Add(k0);
            chips.ColumnDefinitions.Add(k1);
            chips.ColumnDefinitions.Add(k2);
            chips.Margin = new Thickness(0, 9, 0, 0);

            Grid dueCell = new Grid();

            dueChip = new Border();
            dueChip.Background = Theme.ChipBg;
            dueChip.BorderBrush = Theme.ChipBorder;
            dueChip.BorderThickness = new Thickness(1);
            dueChip.CornerRadius = new CornerRadius(13);
            dueChip.Padding = new Thickness(10, 0, 8, 0);
            dueChip.Height = 26;
            dueChip.Cursor = Cursors.Hand;
            dueChip.VerticalAlignment = VerticalAlignment.Center;
            dueChip.HorizontalAlignment = HorizontalAlignment.Left;
            StackPanel chipSp = new StackPanel();
            chipSp.Orientation = Orientation.Horizontal;
            TextBlock cal = Theme.Glyph("\uE787", 11, Theme.TextSecondary);
            cal.VerticalAlignment = VerticalAlignment.Center;
            dueChipText = new TextBlock();
            dueChipText.Text = "截止日期";
            dueChipText.FontSize = 11.5;
            dueChipText.Foreground = Theme.TextSecondary;
            dueChipText.VerticalAlignment = VerticalAlignment.Center;
            dueChipText.Margin = new Thickness(5, 0, 0, 0);
            dueChipClear = new Border();
            dueChipClear.Width = 18;
            dueChipClear.Height = 18;
            dueChipClear.CornerRadius = new CornerRadius(9);
            dueChipClear.Background = Brushes.Transparent;
            dueChipClear.Margin = new Thickness(4, 0, 0, 0);
            dueChipClear.VerticalAlignment = VerticalAlignment.Center;
            dueChipClear.Visibility = Visibility.Collapsed;
            TextBlock clr = Theme.Glyph("\uE711", 8, Theme.TextSecondary);
            clr.HorizontalAlignment = HorizontalAlignment.Center;
            clr.VerticalAlignment = VerticalAlignment.Center;
            dueChipClear.Child = clr;
            dueChipClear.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; };
            dueChipClear.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                e.Handled = true;
                duePicker.SelectedDate = null;
            };
            chipSp.Children.Add(cal);
            chipSp.Children.Add(dueChipText);
            chipSp.Children.Add(dueChipClear);
            dueChip.Child = chipSp;
            dueChip.MouseEnter += delegate { dueChip.Background = BrushFrom(0xEF, 0xF1, 0xF7); };
            dueChip.MouseLeave += delegate { dueChip.Background = Theme.ChipBg; };
            dueChip.MouseLeftButtonUp += delegate
            {
                try { duePicker.IsDropDownOpen = true; }
                catch { }
            };
            dueCell.Children.Add(dueChip);

            duePicker = new DatePicker();
            duePicker.Width = 150;
            duePicker.Height = 26;
            duePicker.Opacity = 0;
            duePicker.IsHitTestVisible = false;
            duePicker.VerticalAlignment = VerticalAlignment.Center;
            duePicker.HorizontalAlignment = HorizontalAlignment.Left;
            duePicker.SelectedDateChanged += delegate { UpdateDueChip(); };
            dueCell.Children.Add(duePicker);

            chips.Children.Add(dueCell);
            Grid.SetColumn(dueCell, 0);

            TextBlock hint = new TextBlock();
            hint.Text = "回车快速添加";
            hint.FontSize = 11;
            hint.Foreground = Theme.TextTertiary;
            hint.VerticalAlignment = VerticalAlignment.Center;
            hint.HorizontalAlignment = HorizontalAlignment.Right;
            chips.Children.Add(hint);
            Grid.SetColumn(hint, 2);

            g.Children.Add(chips);
            Grid.SetRow(chips, 1);

            return addPanel;
        }

        private void UpdateDueChip()
        {
            if (dueChipText == null) return;
            if (duePicker.SelectedDate.HasValue)
            {
                dueChipText.Text = "截止 " + FmtDate(duePicker.SelectedDate.Value);
                dueChipText.Foreground = Theme.Accent;
                dueChipClear.Visibility = Visibility.Visible;
            }
            else
            {
                dueChipText.Text = "截止日期";
                dueChipText.Foreground = Theme.TextSecondary;
                dueChipClear.Visibility = Visibility.Collapsed;
            }
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
                    NudgeRender();
                    if (tray != null) tray.ShowTip("桌面待办", "已贴入桌面壁纸层：按 Win+D 显示桌面即可看到；托盘菜单可取消");
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
                // Embed() preserves the window's current on-screen position by mapping it
                // into the host layer's own coordinates. Do NOT re-apply the saved PosX/PosY
                // afterwards: those are in DIPs while SetEmbedPosition works in raw pixels,
                // which misplaces the widget on displays with DPI scaling != 100%.
                if (!DesktopHost.Embed(this))
                {
                    embedded = false;
                    ShowInTaskbar = true;
                    Topmost = Store.Data.Settings.Topmost;
                    Store.Data.Settings.DesktopEmbed = false;
                    Store.Save();
                    UpdatePinGlyph();
                }
                else
                {
                    NudgeRender();
                }
            }
            catch (Exception ex)
            {
                Logger.Log("ApplyEmbed: " + ex);
            }
        }

        // After reparenting into the wallpaper layer, WPF keeps compositing the stale
        // top-level surface: only the background brush paints and the whole UI looks
        // blank ("widget disappeared"). A hide/show cycle forces WPF to rebuild its
        // render target for the child-window state. (DesktopHost.Embed additionally
        // nudges the window size by 1px to raise a WM_SIZE.)
        private void NudgeRender()
        {
            try
            {
                Hide();
                Show();
                InvalidateVisual();
            }
            catch (Exception ex) { Logger.Log("NudgeRender: " + ex.Message); }
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
