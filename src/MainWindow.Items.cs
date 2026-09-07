using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace DesktopTodo
{
    // Item cards, add/completion/restore logic, and list rendering
    public partial class MainWindow
    {
        private Border doneSection;
        private Border doneHeader;
        private TextBlock doneHeaderText;
        private TextBlock doneArrow;
        private Border doneBody;
        private StackPanel doneList;
        private bool doneOpen;

        private Border BuildDoneSection()
        {
            doneSection = new Border();
            doneSection.Margin = new Thickness(12, 6, 12, 12);
            doneSection.Background = BrushFrom(0xEE, 0xF0, 0xF6);
            doneSection.CornerRadius = new CornerRadius(12);
            doneSection.Padding = new Thickness(5);

            StackPanel outer = new StackPanel();
            doneSection.Child = outer;

            doneHeader = new Border();
            doneHeader.Padding = new Thickness(8, 7, 10, 7);
            doneHeader.CornerRadius = new CornerRadius(8);
            doneHeader.Cursor = Cursors.Hand;
            doneHeader.Background = Brushes.Transparent;
            doneHeader.ToolTip = "点击展开，查看并恢复已划掉的待办";
            doneHeader.MouseEnter += delegate { doneHeader.Background = BrushFrom(0xE3, 0xE6, 0xEF); };
            doneHeader.MouseLeave += delegate { doneHeader.Background = Brushes.Transparent; };
            doneHeader.MouseLeftButtonUp += delegate { doneOpen = !doneOpen; RefreshAll(); };
            outer.Children.Add(doneHeader);

            Grid hg = new Grid();
            ColumnDefinition h0 = new ColumnDefinition(); h0.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition h1 = new ColumnDefinition(); h1.Width = GridLength.Auto;
            hg.ColumnDefinitions.Add(h0);
            hg.ColumnDefinitions.Add(h1);
            doneHeader.Child = hg;

            doneHeaderText = new TextBlock();
            doneHeaderText.Text = "已划掉 (0)";
            doneHeaderText.FontSize = 12;
            doneHeaderText.FontWeight = FontWeights.SemiBold;
            doneHeaderText.Foreground = BrushFrom(0x6B, 0x72, 0x80);
            hg.Children.Add(doneHeaderText);
            Grid.SetColumn(doneHeaderText, 0);

            doneArrow = new TextBlock();
            doneArrow.FontFamily = new FontFamily("Segoe MDL2 Assets");
            doneArrow.FontSize = 10;
            doneArrow.Foreground = BrushFrom(0x6B, 0x72, 0x80);
            doneArrow.VerticalAlignment = VerticalAlignment.Center;
            hg.Children.Add(doneArrow);
            Grid.SetColumn(doneArrow, 1);

            doneBody = new Border();
            doneBody.Padding = new Thickness(2, 0, 2, 4);
            outer.Children.Add(doneBody);

            ScrollViewer scroller = new ScrollViewer();
            scroller.MaxHeight = 240;
            scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            Style sbStyle = Theme.SlimScrollBar();
            if (sbStyle != null) scroller.Resources.Add(typeof(ScrollBar), sbStyle);
            doneList = new StackPanel();
            scroller.Content = doneList;
            doneBody.Child = scroller;

            return doneSection;
        }

        public void RefreshAll()
        {
            if (activeList == null || doneHeaderText == null) return;
            activeList.Children.Clear();

            List<TodoItem> active = Store.GetActiveSorted();

            if (titleCount != null)
            {
                titleCount.Text = active.Count == 0 ? "" : "· " + active.Count + " 项";
            }

            if (active.Count == 0)
            {
                if (emptyState == null)
                {
                    emptyState = new Border();
                    emptyState.Padding = new Thickness(0, 34, 0, 14);
                    StackPanel esp = new StackPanel();
                    esp.HorizontalAlignment = HorizontalAlignment.Center;
                    Border ico = new Border();
                    ico.Width = 46;
                    ico.Height = 46;
                    ico.CornerRadius = new CornerRadius(23);
                    ico.Background = BrushFrom(0xE9, 0xEB, 0xF4);
                    TextBlock ig = Theme.Glyph("\uE73A", 18, BrushFrom(0xB6, 0xBD, 0xCC));
                    ig.HorizontalAlignment = HorizontalAlignment.Center;
                    ig.VerticalAlignment = VerticalAlignment.Center;
                    ico.Child = ig;
                    TextBlock t1 = new TextBlock();
                    t1.Text = "今天没有待办";
                    t1.FontSize = 13;
                    t1.FontWeight = FontWeights.SemiBold;
                    t1.Foreground = BrushFrom(0x8A, 0x93, 0xA6);
                    t1.HorizontalAlignment = HorizontalAlignment.Center;
                    t1.Margin = new Thickness(0, 12, 0, 0);
                    TextBlock t2 = new TextBlock();
                    t2.Text = "在上方输入，回车添加一条吧";
                    t2.FontSize = 11.5;
                    t2.Foreground = BrushFrom(0xB0, 0xB7, 0xC3);
                    t2.HorizontalAlignment = HorizontalAlignment.Center;
                    t2.Margin = new Thickness(0, 4, 0, 0);
                    esp.Children.Add(ico);
                    esp.Children.Add(t1);
                    esp.Children.Add(t2);
                    emptyState.Child = esp;
                }
                activeList.Children.Add(emptyState);
            }
            else
            {
                foreach (TodoItem it in active)
                {
                    activeList.Children.Add(BuildActiveCard(it));
                }
            }

            List<TodoItem> done = Store.GetDoneSorted();
            doneHeaderText.Text = "已划掉 (" + done.Count + ")";
            doneArrow.Text = doneOpen ? "\uE70D" : "\uE70E";
            doneBody.Visibility = (doneOpen && done.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
            doneList.Children.Clear();
            foreach (TodoItem it in done)
            {
                doneList.Children.Add(BuildDoneCard(it));
            }
        }

        private void AddItem()
        {
            string text = inputBox.Text == null ? "" : inputBox.Text.Trim();
            if (text.Length == 0) return;

            TodoItem it = new TodoItem();
            it.Id = Guid.NewGuid().ToString("N");
            it.Text = text;
            it.CreatedAt = DateTime.Now;
            it.DueDate = duePicker.SelectedDate.HasValue
                ? duePicker.SelectedDate.Value.Date
                : (DateTime?)null;

            Store.Data.Items.Add(it);
            Store.Save();

            inputBox.Text = "";
            duePicker.SelectedDate = null;
            inputBox.Focus();
            RefreshAll();
        }

        // Small ghost ✕ button on a card; asks for confirmation, then permanently removes the item.
        private Border BuildDeleteButton(TodoItem item)
        {
            Border db = new Border();
            db.Width = 24;
            db.Height = 24;
            db.CornerRadius = new CornerRadius(12);
            db.Background = Brushes.Transparent;
            db.Cursor = Cursors.Hand;
            db.VerticalAlignment = VerticalAlignment.Center;
            db.ToolTip = "删除这条待办";

            TextBlock dTxt = new TextBlock();
            dTxt.Text = "\uE711";
            dTxt.FontFamily = new FontFamily("Segoe MDL2 Assets");
            dTxt.FontSize = 11;
            dTxt.Foreground = BrushFrom(0xB3, 0xB9, 0xC4);
            dTxt.HorizontalAlignment = HorizontalAlignment.Center;
            dTxt.VerticalAlignment = VerticalAlignment.Center;
            db.Child = dTxt;

            db.MouseEnter += delegate
            {
                db.Background = BrushFrom(0xFB, 0xEC, 0xEC);
                dTxt.Foreground = BrushFrom(0xE5, 0x48, 0x4D);
            };
            db.MouseLeave += delegate
            {
                db.Background = Brushes.Transparent;
                dTxt.Foreground = BrushFrom(0xB3, 0xB9, 0xC4);
            };
            db.MouseLeftButtonUp += delegate
            {
                MessageBoxResult r = MessageBox.Show(this,
                    "确定删除这条待办吗？\n「" + item.Text + "」\n删除后无法恢复。",
                    "删除待办", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (r != MessageBoxResult.Yes) return;
                Store.DeleteItem(item.Id);
                Store.Save();
                RefreshAll();
            };

            return db;
        }

        private UIElement BuildActiveCard(TodoItem item)
        {
            Border card = new Border();
            card.Background = Brushes.White;
            card.CornerRadius = new CornerRadius(12);
            card.Margin = new Thickness(4, 0, 4, 9);
            card.Padding = new Thickness(12, 11, 9, 10);
            card.BorderBrush = Theme.CardBorder;
            card.BorderThickness = new Thickness(1);
            card.Tag = item;

            Grid g = new Grid();
            ColumnDefinition c0 = new ColumnDefinition(); c0.Width = GridLength.Auto;
            ColumnDefinition c1 = new ColumnDefinition(); c1.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition c2 = new ColumnDefinition(); c2.Width = GridLength.Auto;
            g.ColumnDefinitions.Add(c0);
            g.ColumnDefinitions.Add(c1);
            g.ColumnDefinitions.Add(c2);
            card.Child = g;

            // Rounded-square checkbox
            Border cb = Theme.CheckCircle();
            cb.VerticalAlignment = VerticalAlignment.Top;
            cb.Margin = new Thickness(0, 1, 10, 0);
            TextBlock check = (TextBlock)cb.Child;
            cb.MouseEnter += delegate { if (!item.Completed) cb.BorderBrush = Theme.Accent; };
            cb.MouseLeave += delegate { if (!item.Completed) cb.BorderBrush = Theme.CheckBorder; };
            g.Children.Add(cb);
            Grid.SetColumn(cb, 0);

            StackPanel content = new StackPanel();
            g.Children.Add(content);
            Grid.SetColumn(content, 1);

            // Text with an animated strike-through line drawn over it.
            // Transparent background makes the whole row hit-testable for double-click edit.
            Grid textGrid = new Grid();
            textGrid.Background = Brushes.Transparent;
            TextBlock txt = new TextBlock();
            txt.Text = item.Text;
            txt.TextWrapping = TextWrapping.Wrap;
            txt.FontSize = 14;
            txt.Foreground = Theme.TextPrimary;
            txt.HorizontalAlignment = HorizontalAlignment.Left;
            textGrid.Children.Add(txt);
            textGrid.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    ShowEditPopup(item);
                }
            };

            Rectangle strike = new Rectangle();
            strike.Height = 2;
            strike.RadiusX = 1;
            strike.RadiusY = 1;
            strike.Fill = BrushFrom(0xA6, 0xAD, 0xB9);
            strike.VerticalAlignment = VerticalAlignment.Center;
            ScaleTransform st = new ScaleTransform(0, 1);
            strike.RenderTransform = st;
            strike.RenderTransformOrigin = new Point(0, 0.5);
            textGrid.Children.Add(strike);
            content.Children.Add(textGrid);

            // Badges: optional due-date pill + creation time
            StackPanel badges = new StackPanel();
            badges.Orientation = Orientation.Horizontal;
            badges.Margin = new Thickness(0, 6, 0, 0);

            if (item.DueDate.HasValue)
            {
                Border duePill = new Border();
                duePill.Background = DuePillBg(item.DueDate.Value);
                duePill.CornerRadius = new CornerRadius(9);
                duePill.Padding = new Thickness(7, 2, 7, 3);
                duePill.VerticalAlignment = VerticalAlignment.Center;
                StackPanel dp = new StackPanel();
                dp.Orientation = Orientation.Horizontal;
                TextBlock clock = Theme.Glyph("\uE823", 10, DueBrush(item.DueDate.Value));
                clock.VerticalAlignment = VerticalAlignment.Center;
                TextBlock due = new TextBlock();
                due.Text = DueText(item.DueDate.Value);
                due.FontSize = 11;
                due.Foreground = DueBrush(item.DueDate.Value);
                due.VerticalAlignment = VerticalAlignment.Center;
                due.Margin = new Thickness(4, 0, 0, 0);
                dp.Children.Add(clock);
                dp.Children.Add(due);
                duePill.Child = dp;
                badges.Children.Add(duePill);
            }

            TextBlock created = new TextBlock();
            created.Text = "创建 " + FmtTime(item.CreatedAt);
            created.FontSize = 10.5;
            created.Foreground = Theme.TextTertiary;
            created.VerticalAlignment = VerticalAlignment.Center;
            if (item.DueDate.HasValue) created.Margin = new Thickness(8, 0, 0, 0);
            badges.Children.Add(created);

            content.Children.Add(badges);

            cb.MouseLeftButtonUp += delegate
            {
                if (item.Completed) return;
                AnimateComplete(item, card, cb, check, txt, strike, st);
            };

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.VerticalAlignment = VerticalAlignment.Center;
            actions.Opacity = 0;
            actions.Children.Add(BuildEditButton(item));
            Border del = BuildDeleteButton(item);
            del.Margin = new Thickness(2, 0, 0, 0);
            actions.Children.Add(del);
            g.Children.Add(actions);
            Grid.SetColumn(actions, 2);

            card.MouseEnter += delegate
            {
                card.BorderBrush = Theme.CardBorderHover;
                card.Effect = Theme.CardShadow();
                actions.Opacity = 1;
            };
            card.MouseLeave += delegate
            {
                card.BorderBrush = Theme.CardBorder;
                card.Effect = null;
                actions.Opacity = 0;
            };

            return card;
        }

        // Check -> draw the line through the text -> fade & collapse the card -> re-render lists.
        // Data is persisted immediately, so nothing is lost even if the app closes mid-animation.
        private void AnimateComplete(TodoItem item, Border card, Border cb, TextBlock check,
            TextBlock txt, Rectangle strike, ScaleTransform st)
        {
            item.Completed = true;
            item.CompletedAt = DateTime.Now;
            Store.Save();

            card.IsHitTestVisible = false;
            cb.Background = Theme.Accent;
            cb.BorderBrush = Theme.Accent;
            check.Visibility = Visibility.Visible;

            DoubleAnimation checkIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160));
            check.BeginAnimation(OpacityProperty, checkIn);

            DoubleAnimation txtFade = new DoubleAnimation(1, 0.5, TimeSpan.FromMilliseconds(480));
            txt.BeginAnimation(OpacityProperty, txtFade);

            DoubleAnimation grow = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(480));
            grow.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut };
            grow.Completed += delegate
            {
                DoubleAnimation fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                card.BeginAnimation(OpacityProperty, fade);

                card.Height = card.ActualHeight;
                DoubleAnimation collapse = new DoubleAnimation(card.ActualHeight, 0, TimeSpan.FromMilliseconds(300));
                collapse.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn };
                collapse.Completed += delegate { RefreshAll(); };

                ThicknessAnimation margin = new ThicknessAnimation(
                    card.Margin, new Thickness(6, 0, 6, 0), TimeSpan.FromMilliseconds(300));

                card.BeginAnimation(HeightProperty, collapse);
                card.BeginAnimation(MarginProperty, margin);
            };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        }

        private void Restore(TodoItem item)
        {
            item.Completed = false;
            item.CompletedAt = null;
            Store.Save();
            RefreshAll();
        }

        private UIElement BuildDoneCard(TodoItem item)
        {
            Border card = new Border();
            card.Background = Brushes.White;
            card.CornerRadius = new CornerRadius(10);
            card.Margin = new Thickness(2, 0, 2, 6);
            card.Padding = new Thickness(10, 8, 8, 8);

            Grid g = new Grid();
            ColumnDefinition c0 = new ColumnDefinition(); c0.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition c1 = new ColumnDefinition(); c1.Width = GridLength.Auto;
            ColumnDefinition c2 = new ColumnDefinition(); c2.Width = GridLength.Auto;
            g.ColumnDefinitions.Add(c0);
            g.ColumnDefinitions.Add(c1);
            g.ColumnDefinitions.Add(c2);
            card.Child = g;

            StackPanel sp = new StackPanel();
            g.Children.Add(sp);
            Grid.SetColumn(sp, 0);

            TextBlock txt = new TextBlock();
            txt.Text = item.Text;
            txt.TextWrapping = TextWrapping.Wrap;
            txt.FontSize = 12.5;
            txt.Foreground = BrushFrom(0x8A, 0x93, 0xA6);
            txt.TextDecorations = TextDecorations.Strikethrough;
            sp.Children.Add(txt);

            TextBlock times = new TextBlock();
            string doneAt = item.CompletedAt.HasValue ? FmtTime(item.CompletedAt.Value) : "-";
            times.Text = "创建 " + FmtTime(item.CreatedAt) + "    划掉 " + doneAt;
            times.FontSize = 10.5;
            times.Foreground = Theme.TextTertiary;
            times.Margin = new Thickness(0, 3, 0, 0);
            sp.Children.Add(times);

            Border rb = new Border();
            rb.Width = 26;
            rb.Height = 26;
            rb.CornerRadius = new CornerRadius(13);
            rb.Background = BrushFrom(0xEE, 0xF1, 0xF8);
            rb.Cursor = Cursors.Hand;
            rb.VerticalAlignment = VerticalAlignment.Center;
            rb.Margin = new Thickness(6, 0, 0, 0);
            rb.ToolTip = "恢复这条待办";

            TextBlock rTxt = Theme.Glyph("\uE7A7", 12, Theme.Accent);
            rTxt.HorizontalAlignment = HorizontalAlignment.Center;
            rTxt.VerticalAlignment = VerticalAlignment.Center;
            rb.Child = rTxt;

            rb.MouseEnter += delegate { rb.Background = BrushFrom(0xE1, 0xE6, 0xF3); };
            rb.MouseLeave += delegate { rb.Background = BrushFrom(0xEE, 0xF1, 0xF8); };
            rb.MouseLeftButtonUp += delegate { Restore(item); };

            g.Children.Add(rb);
            Grid.SetColumn(rb, 1);

            Border del = BuildDeleteButton(item);
            del.Margin = new Thickness(2, 0, 0, 0);
            del.Opacity = 0;
            g.Children.Add(del);
            Grid.SetColumn(del, 2);

            card.MouseEnter += delegate { del.Opacity = 1; };
            card.MouseLeave += delegate { del.Opacity = 0; };

            return card;
        }

        private static Brush DuePillBg(DateTime d)
        {
            DateTime today = DateTime.Now.Date;
            if (d.Date < today) return BrushFrom(0xFD, 0xEC, 0xEC);
            if (d.Date == today) return BrushFrom(0xFD, 0xF1, 0xDE);
            return BrushFrom(0xF1, 0xF3, 0xF8);
        }

        private static string FmtTime(DateTime d)
        {
            DateTime now = DateTime.Now;
            return d.Year == now.Year ? d.ToString("MM-dd HH:mm") : d.ToString("yyyy-MM-dd HH:mm");
        }

        private static string FmtDate(DateTime d)
        {
            DateTime now = DateTime.Now;
            return d.Year == now.Year ? d.ToString("MM-dd") : d.ToString("yyyy-MM-dd");
        }

        private static string DueText(DateTime d)
        {
            DateTime today = DateTime.Now.Date;
            if (d.Date < today) return "已过期 " + FmtDate(d);
            if (d.Date == today) return "今天截止";
            if (d.Date == today.AddDays(1)) return "明天截止";
            return FmtDate(d);
        }

        private static SolidColorBrush DueBrush(DateTime d)
        {
            DateTime today = DateTime.Now.Date;
            if (d.Date < today) return BrushFrom(0xE5, 0x48, 0x4D);
            if (d.Date == today) return BrushFrom(0xD9, 0x77, 0x06);
            return BrushFrom(0x6B, 0x72, 0x80);
        }
    }
}
