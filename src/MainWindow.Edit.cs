using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopTodo
{
    // Edit popup: change an existing item's text and due date, and manage its subtask checklist.
    // Opened from the hover ✎ button on a card or by double-clicking the item text.
    public partial class MainWindow
    {
        private Window editWindow;

        private Border BuildEditButton(TodoItem item)
        {
            Border eb = new Border();
            eb.Width = 24;
            eb.Height = 24;
            eb.CornerRadius = new CornerRadius(12);
            eb.Background = Brushes.Transparent;
            eb.Cursor = Cursors.Hand;
            eb.VerticalAlignment = VerticalAlignment.Center;
            eb.ToolTip = "编辑这条待办（双击条目文字也可以）";

            TextBlock eTxt = Theme.Glyph("\uE70F", 11, Theme.TextTertiary);
            eTxt.HorizontalAlignment = HorizontalAlignment.Center;
            eTxt.VerticalAlignment = VerticalAlignment.Center;
            eb.Child = eTxt;

            eb.MouseEnter += delegate
            {
                eb.Background = BrushFrom(0xEE, 0xF1, 0xF9);
                eTxt.Foreground = Theme.Accent;
            };
            eb.MouseLeave += delegate
            {
                eb.Background = Brushes.Transparent;
                eTxt.Foreground = Theme.TextTertiary;
            };
            eb.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; };
            eb.MouseLeftButtonUp += delegate { ShowEditPopup(item); };
            return eb;
        }

        private void ShowEditPopup(TodoItem item)
        {
            if (editWindow != null) { editWindow.Activate(); return; }

            Window w = new Window();
            editWindow = w;
            w.Title = "编辑待办";
            w.Width = 316;
            w.Height = 196;
            w.SizeToContent = SizeToContent.Height; // the checklist grows/shrinks the popup
            w.WindowStyle = WindowStyle.None;
            w.ResizeMode = ResizeMode.NoResize;
            w.AllowsTransparency = true;
            w.Background = Brushes.Transparent;
            w.Topmost = true;
            w.ShowInTaskbar = false;
            w.WindowStartupLocation = WindowStartupLocation.Manual;
            w.Left = Left + (Width - w.Width) / 2;
            w.Top = Top + (Height - w.Height) / 2 - 30;

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

            // Header: title + ✕
            Border header = new Border();
            header.Padding = new Thickness(14, 12, 8, 6);
            header.Background = Brushes.Transparent;
            sp.Children.Add(header);

            Grid hg = new Grid();
            ColumnDefinition h0 = new ColumnDefinition(); h0.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition h1 = new ColumnDefinition(); h1.Width = GridLength.Auto;
            hg.ColumnDefinitions.Add(h0);
            hg.ColumnDefinitions.Add(h1);
            header.Child = hg;

            TextBlock title = new TextBlock();
            title.Text = "编辑待办";
            title.FontSize = 13.5;
            title.FontWeight = FontWeights.SemiBold;
            title.Foreground = Theme.TextPrimary;
            title.VerticalAlignment = VerticalAlignment.Center;
            hg.Children.Add(title);
            Grid.SetColumn(title, 0);

            hg.Children.Add(Theme.GhostButton("\uE711", 11, null, false, delegate { w.Close(); }));
            Grid.SetColumn((Border)hg.Children[hg.Children.Count - 1], 1);

            // The DatePicker is declared before the text box because the
            // Enter-to-save handler below captures it.
            DatePicker dp = null;

            // Subtask working copy + controls: declared early for the same reason as dp.
            // Edits stay local until 保存, so 取消 discards everything.
            List<SubTask> workSubs = new List<SubTask>();
            foreach (SubTask s in item.CleanSubs())
            {
                SubTask copy = new SubTask();
                copy.Id = s.Id;
                copy.Text = s.Text;
                copy.Done = s.Done;
                workSubs.Add(copy);
            }
            TextBox addBox = null;
            List<TextBox> subBoxes = new List<TextBox>();

            // Text input
            TextBox tb = new TextBox();
            tb.Margin = new Thickness(14, 4, 14, 0);
            tb.Text = item.Text;
            tb.FontSize = 13.5;
            tb.Padding = new Thickness(10, 8, 10, 8);
            tb.BorderBrush = Theme.ChipBorder;
            tb.BorderThickness = new Thickness(1);
            tb.Background = BrushFrom(0xFA, 0xFB, 0xFD);
            tb.GotKeyboardFocus += delegate { tb.BorderBrush = Theme.Accent; };
            tb.LostKeyboardFocus += delegate { tb.BorderBrush = Theme.ChipBorder; };
            tb.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) SaveEdit(w, item, tb, dp, workSubs, addBox, subBoxes);
                if (e.Key == Key.Escape) w.Close();
            };
            sp.Children.Add(tb);

            // Due-date chip + hidden DatePicker (same pattern as the add panel)
            Grid dueRow = new Grid();
            dueRow.Margin = new Thickness(14, 10, 14, 0);
            ColumnDefinition c0 = new ColumnDefinition(); c0.Width = GridLength.Auto;
            ColumnDefinition c1 = new ColumnDefinition(); c1.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition c2 = new ColumnDefinition(); c2.Width = GridLength.Auto;
            dueRow.ColumnDefinitions.Add(c0);
            dueRow.ColumnDefinitions.Add(c1);
            dueRow.ColumnDefinitions.Add(c2);
            sp.Children.Add(dueRow);

            Grid dueCell = new Grid();

            Border chip = new Border();
            chip.Background = Theme.ChipBg;
            chip.BorderBrush = Theme.ChipBorder;
            chip.BorderThickness = new Thickness(1);
            chip.CornerRadius = new CornerRadius(13);
            chip.Padding = new Thickness(10, 0, 8, 0);
            chip.Height = 26;
            chip.Cursor = Cursors.Hand;
            chip.VerticalAlignment = VerticalAlignment.Center;
            chip.HorizontalAlignment = HorizontalAlignment.Left;
            StackPanel chipSp = new StackPanel();
            chipSp.Orientation = Orientation.Horizontal;
            TextBlock cal = Theme.Glyph("\uE787", 11, Theme.TextSecondary);
            cal.VerticalAlignment = VerticalAlignment.Center;
            TextBlock chipText = new TextBlock();
            chipText.FontSize = 11.5;
            chipText.Foreground = Theme.TextSecondary;
            chipText.VerticalAlignment = VerticalAlignment.Center;
            chipText.Margin = new Thickness(5, 0, 0, 0);
            Border chipClear = new Border();
            chipClear.Width = 18;
            chipClear.Height = 18;
            chipClear.CornerRadius = new CornerRadius(9);
            chipClear.Background = Brushes.Transparent;
            chipClear.Margin = new Thickness(4, 0, 0, 0);
            chipClear.VerticalAlignment = VerticalAlignment.Center;
            TextBlock clr = Theme.Glyph("\uE711", 8, Theme.TextSecondary);
            clr.HorizontalAlignment = HorizontalAlignment.Center;
            clr.VerticalAlignment = VerticalAlignment.Center;
            chipClear.Child = clr;
            chipClear.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; };
            chipClear.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                e.Handled = true;
                dp.SelectedDate = null;
            };
            chipSp.Children.Add(cal);
            chipSp.Children.Add(chipText);
            chipSp.Children.Add(chipClear);
            chip.Child = chipSp;
            chip.MouseEnter += delegate { chip.Background = BrushFrom(0xEF, 0xF1, 0xF7); };
            chip.MouseLeave += delegate { chip.Background = Theme.ChipBg; };
            chip.MouseLeftButtonUp += delegate
            {
                try { dp.IsDropDownOpen = true; }
                catch { }
            };
            dueCell.Children.Add(chip);

            dp = new DatePicker();
            dp.Width = 150;
            dp.Height = 26;
            dp.Opacity = 0;
            dp.IsHitTestVisible = false;
            dp.VerticalAlignment = VerticalAlignment.Center;
            dp.HorizontalAlignment = HorizontalAlignment.Left;
            if (item.DueDate.HasValue) dp.SelectedDate = item.DueDate.Value.Date;
            dp.SelectedDateChanged += delegate { UpdateChipText(chipText, chipClear, dp); };
            dueCell.Children.Add(dp);

            dueRow.Children.Add(dueCell);
            Grid.SetColumn(dueCell, 0);

            TextBlock hint = new TextBlock();
            hint.Text = "回车保存";
            hint.FontSize = 11;
            hint.Foreground = Theme.TextTertiary;
            hint.VerticalAlignment = VerticalAlignment.Center;
            hint.HorizontalAlignment = HorizontalAlignment.Right;
            dueRow.Children.Add(hint);
            Grid.SetColumn(hint, 2);

            // ---- Subtask checklist ----
            TextBlock subsTitle = new TextBlock();
            subsTitle.FontSize = 11.5;
            subsTitle.FontWeight = FontWeights.SemiBold;
            subsTitle.Foreground = Theme.TextSecondary;
            subsTitle.Margin = new Thickness(14, 12, 14, 0);
            sp.Children.Add(subsTitle);

            ScrollViewer subScroller = new ScrollViewer();
            subScroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            subScroller.Margin = new Thickness(14, 6, 14, 0);
            subScroller.MaxHeight = 116;
            Style subSbStyle = Theme.SlimScrollBar();
            if (subSbStyle != null) subScroller.Resources.Add(typeof(ScrollBar), subSbStyle);
            StackPanel subHost = new StackPanel();
            subScroller.Content = subHost;
            sp.Children.Add(subScroller);

            addBox = new TextBox();
            addBox.Margin = new Thickness(14, 6, 14, 0);
            addBox.FontSize = 12.5;
            addBox.Padding = new Thickness(9, 5, 9, 5);
            addBox.BorderBrush = Theme.ChipBorder;
            addBox.BorderThickness = new Thickness(1);
            addBox.Background = BrushFrom(0xFA, 0xFB, 0xFD);
            addBox.GotKeyboardFocus += delegate { addBox.BorderBrush = Theme.Accent; };
            addBox.LostKeyboardFocus += delegate { addBox.BorderBrush = Theme.ChipBorder; };
            sp.Children.Add(addBox);

            Action rebuildSubs = null;

            Action addSub = delegate
            {
                string t = addBox.Text == null ? "" : addBox.Text.Trim();
                if (t.Length == 0) return;
                SubTask s = new SubTask();
                s.Id = Guid.NewGuid().ToString("N");
                s.Text = t;
                s.Done = false;
                workSubs.Add(s);
                addBox.Text = "";
                rebuildSubs();
                addBox.Focus();
            };

            addBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) { e.Handled = true; addSub(); }
                if (e.Key == Key.Escape) w.Close();
            };

            rebuildSubs = delegate
            {
                // Keep typed-but-unconfirmed row text before re-creating the controls.
                for (int i = 0; i < workSubs.Count && i < subBoxes.Count; i++)
                {
                    workSubs[i].Text = subBoxes[i].Text == null ? "" : subBoxes[i].Text.Trim();
                }
                subHost.Children.Clear();
                subBoxes.Clear();
                foreach (SubTask s in workSubs)
                {
                    Grid row = new Grid();
                    row.Margin = new Thickness(0, 1, 0, 1);
                    ColumnDefinition r0 = new ColumnDefinition(); r0.Width = GridLength.Auto;
                    ColumnDefinition r1 = new ColumnDefinition(); r1.Width = new GridLength(1, GridUnitType.Star);
                    ColumnDefinition r2 = new ColumnDefinition(); r2.Width = GridLength.Auto;
                    row.ColumnDefinitions.Add(r0);
                    row.ColumnDefinitions.Add(r1);
                    row.ColumnDefinitions.Add(r2);

                    TextBox rowBox = new TextBox();
                    rowBox.Text = s.Text;
                    rowBox.FontSize = 12.5;
                    rowBox.Padding = new Thickness(7, 4, 7, 4);
                    rowBox.Margin = new Thickness(8, 0, 0, 0);
                    rowBox.Background = Brushes.Transparent;
                    rowBox.BorderThickness = new Thickness(0);
                    rowBox.VerticalAlignment = VerticalAlignment.Center;
                    rowBox.Foreground = s.Done ? Theme.TextTertiary : Theme.TextPrimary;
                    rowBox.GotKeyboardFocus += delegate { rowBox.Background = BrushFrom(0xF3, 0xF4, 0xF8); };
                    rowBox.LostKeyboardFocus += delegate { rowBox.Background = Brushes.Transparent; };
                    rowBox.ToolTip = "可直接修改这行的文字";
                    subBoxes.Add(rowBox);

                    Border cbx = MiniCheck(s.Done, 16);
                    cbx.VerticalAlignment = VerticalAlignment.Center;
                    cbx.ToolTip = "勾选这个子事项";
                    cbx.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
                    {
                        e.Handled = true;
                        s.Done = !s.Done;
                        ((TextBlock)cbx.Child).Visibility = s.Done ? Visibility.Visible : Visibility.Collapsed;
                        cbx.Background = s.Done ? Theme.Accent : Brushes.White;
                        cbx.BorderBrush = s.Done ? Theme.Accent : Theme.CheckBorder;
                        rowBox.Foreground = s.Done ? Theme.TextTertiary : Theme.TextPrimary;
                        UpdateSubsTitle(subsTitle, workSubs);
                    };

                    Border rmv = new Border();
                    rmv.Width = 20;
                    rmv.Height = 20;
                    rmv.CornerRadius = new CornerRadius(10);
                    rmv.Background = Brushes.Transparent;
                    rmv.Cursor = Cursors.Hand;
                    rmv.VerticalAlignment = VerticalAlignment.Center;
                    rmv.ToolTip = "移除这个子事项";
                    TextBlock rmvTxt = Theme.Glyph("\uE711", 8, Theme.TextTertiary);
                    rmvTxt.HorizontalAlignment = HorizontalAlignment.Center;
                    rmvTxt.VerticalAlignment = VerticalAlignment.Center;
                    rmv.Child = rmvTxt;
                    rmv.MouseEnter += delegate { rmv.Background = Theme.DangerFill; rmvTxt.Foreground = Theme.Danger; };
                    rmv.MouseLeave += delegate { rmv.Background = Brushes.Transparent; rmvTxt.Foreground = Theme.TextTertiary; };
                    rmv.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; };
                    rmv.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
                    {
                        e.Handled = true;
                        workSubs.Remove(s);
                        rebuildSubs();
                    };

                    row.Children.Add(cbx);
                    Grid.SetColumn(cbx, 0);
                    row.Children.Add(rowBox);
                    Grid.SetColumn(rowBox, 1);
                    row.Children.Add(rmv);
                    Grid.SetColumn(rmv, 2);
                    subHost.Children.Add(row);
                }

                subScroller.Visibility = workSubs.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
                UpdateSubsTitle(subsTitle, workSubs);
            };

            // Buttons
            StackPanel btns = new StackPanel();
            btns.Orientation = Orientation.Horizontal;
            btns.HorizontalAlignment = HorizontalAlignment.Right;
            btns.Margin = new Thickness(14, 12, 14, 12);
            sp.Children.Add(btns);

            Border cancelBtn = new Border();
            cancelBtn.Background = Theme.ChipBg;
            cancelBtn.BorderBrush = Theme.ChipBorder;
            cancelBtn.BorderThickness = new Thickness(1);
            cancelBtn.CornerRadius = new CornerRadius(8);
            cancelBtn.Padding = new Thickness(14, 6, 14, 7);
            cancelBtn.Cursor = Cursors.Hand;
            TextBlock cancelTxt = new TextBlock();
            cancelTxt.Text = "取消";
            cancelTxt.FontSize = 12.5;
            cancelTxt.Foreground = Theme.TextSecondary;
            cancelBtn.Child = cancelTxt;
            cancelBtn.MouseEnter += delegate { cancelBtn.Background = BrushFrom(0xEE, 0xF0, 0xF6); };
            cancelBtn.MouseLeave += delegate { cancelBtn.Background = Theme.ChipBg; };
            cancelBtn.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; };
            cancelBtn.MouseLeftButtonUp += delegate { w.Close(); };
            btns.Children.Add(cancelBtn);

            Border saveBtn = new Border();
            saveBtn.Background = Theme.Accent;
            saveBtn.CornerRadius = new CornerRadius(8);
            saveBtn.Padding = new Thickness(16, 6, 16, 7);
            saveBtn.Cursor = Cursors.Hand;
            saveBtn.Margin = new Thickness(8, 0, 0, 0);
            TextBlock saveTxt = new TextBlock();
            saveTxt.Text = "保存";
            saveTxt.FontSize = 12.5;
            saveTxt.FontWeight = FontWeights.SemiBold;
            saveTxt.Foreground = Brushes.White;
            saveBtn.Child = saveTxt;
            saveBtn.MouseEnter += delegate { saveBtn.Background = Theme.AccentDark; };
            saveBtn.MouseLeave += delegate { saveBtn.Background = Theme.Accent; };
            saveBtn.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; };
            saveBtn.MouseLeftButtonUp += delegate { SaveEdit(w, item, tb, dp, workSubs, addBox, subBoxes); };
            btns.Children.Add(saveBtn);

            w.Loaded += delegate
            {
                UpdateChipText(chipText, chipClear, dp);
                // SizeToContent has measured by now; center the fully-grown popup over the main window.
                w.Top = Top + (Height - w.ActualHeight) / 2 - 30;
                tb.Focus();
                tb.SelectAll();
            };
            w.Closed += delegate { editWindow = null; };
            rebuildSubs();
            w.Show();
        }

        private void SaveEdit(Window w, TodoItem item, TextBox tb, DatePicker dp,
            List<SubTask> workSubs, TextBox addBox, List<TextBox> subBoxes)
        {
            string text = tb.Text == null ? "" : tb.Text.Trim();
            if (text.Length > 0) item.Text = text;
            item.DueDate = dp.SelectedDate.HasValue ? dp.SelectedDate.Value.Date : (DateTime?)null;

            // Pull the latest text back from the row boxes (they were edited in place).
            for (int i = 0; i < workSubs.Count && i < subBoxes.Count; i++)
            {
                workSubs[i].Text = subBoxes[i].Text == null ? "" : subBoxes[i].Text.Trim();
            }
            // A step typed into the add box but not confirmed with Enter is still honored.
            if (addBox != null)
            {
                string extra = addBox.Text == null ? "" : addBox.Text.Trim();
                if (extra.Length > 0)
                {
                    SubTask s = new SubTask();
                    s.Id = Guid.NewGuid().ToString("N");
                    s.Text = extra;
                    workSubs.Add(s);
                }
            }

            List<SubTask> clean = new List<SubTask>();
            foreach (SubTask s in workSubs)
            {
                if (s != null && !string.IsNullOrEmpty(s.Text)) clean.Add(s);
            }
            item.Subs = clean.Count > 0 ? clean : null;

            Store.Save();
            RefreshAll();
            w.Close();
        }

        private static void UpdateSubsTitle(TextBlock title, List<SubTask> subs)
        {
            int done = 0;
            foreach (SubTask s in subs) if (s != null && s.Done) done++;
            if (subs.Count == 0) title.Text = "子事项（还没有，在下面添加具体步骤）";
            else title.Text = "子事项 " + done + "/" + subs.Count;
        }

        private static void UpdateChipText(TextBlock chipText, Border chipClear, DatePicker dp)
        {
            if (dp.SelectedDate.HasValue)
            {
                chipText.Text = "截止 " + FmtDate(dp.SelectedDate.Value);
                chipText.Foreground = Theme.Accent;
                chipClear.Visibility = Visibility.Visible;
            }
            else
            {
                chipText.Text = "截止日期";
                chipText.Foreground = Theme.TextSecondary;
                chipClear.Visibility = Visibility.Collapsed;
            }
        }
    }
}
