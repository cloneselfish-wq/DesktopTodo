using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopTodo
{
    // Edit popup: change an existing item's text and due date.
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
                if (e.Key == Key.Enter) SaveEdit(w, item, tb, dp);
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
            saveBtn.MouseLeftButtonUp += delegate { SaveEdit(w, item, tb, dp); };
            btns.Children.Add(saveBtn);

            w.Loaded += delegate
            {
                UpdateChipText(chipText, chipClear, dp);
                tb.Focus();
                tb.SelectAll();
            };
            w.Closed += delegate { editWindow = null; };
            w.Show();
        }

        private void SaveEdit(Window w, TodoItem item, TextBox tb, DatePicker dp)
        {
            string text = tb.Text == null ? "" : tb.Text.Trim();
            if (text.Length > 0) item.Text = text;
            item.DueDate = dp.SelectedDate.HasValue ? dp.SelectedDate.Value.Date : (DateTime?)null;
            Store.Save();
            RefreshAll();
            w.Close();
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
