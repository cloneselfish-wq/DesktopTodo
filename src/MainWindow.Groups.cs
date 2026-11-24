using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopTodo
{
    // Grouping: a filter chip bar above the list, per-group sections in the list,
    // and the "移入分组" picker on each card.
    public partial class MainWindow
    {
        private const string GroupAll = "*";     // filter token: no group filter
        private const string GroupNone = "";     // filter token / item GroupId: 未分组

        private string groupFilter = GroupAll;
        private Border groupBar;
        private WrapPanel groupChips;
        private TextBlock addHint;

        // Groups the user has collapsed, keyed by group id (GroupNone for the ungrouped bucket).
        private readonly HashSet<string> groupCollapsed = new HashSet<string>();

        private Window groupPicker;
        private Window groupDialog;

        // ---------- chip bar ----------

        private Border BuildGroupBar()
        {
            groupBar = new Border();
            groupBar.Background = Brushes.Transparent;
            groupBar.Margin = new Thickness(12, 4, 8, 0);

            // A WrapPanel instead of a single scrolling row: with a handful of groups the
            // chips wrap onto a second line instead of being clipped on the right.
            groupChips = new WrapPanel();
            groupChips.Margin = new Thickness(0, 1, 0, 0);
            groupBar.Child = groupChips;

            return groupBar;
        }

        // Rebuilds the chip row; counts follow the current search so the numbers
        // always match what the list below actually shows.
        private void RefreshGroupBar(List<TodoItem> visible)
        {
            if (groupChips == null) return;
            groupChips.Children.Clear();

            groupChips.Children.Add(BuildGroupChip(GroupAll, "全部", visible.Count, null));

            foreach (TodoGroup g in Store.Groups())
            {
                int n = Store.CountInGroup(visible, g.Id);
                if (n == 0 && groupFilter != g.Id) continue;
                groupChips.Children.Add(BuildGroupChip(g.Id, g.Name, n, GroupDot(GroupIndex(g.Id))));
            }

            int none = Store.CountInGroup(visible, GroupNone);
            if (none > 0 || groupFilter == GroupNone)
            {
                groupChips.Children.Add(BuildGroupChip(GroupNone, "未分组", none,
                    BrushFrom(0xB0, 0xB7, 0xC3)));
            }

            groupChips.Children.Add(BuildNewGroupChip());

            if (addHint != null)
            {
                if (groupFilter == GroupAll) addHint.Text = "回车快速添加";
                else addHint.Text = "添加到 " + FilterName();
            }
        }

        private Border BuildGroupChip(string key, string label, int count, Brush dot)
        {
            bool on = groupFilter == key;

            Border chip = new Border();
            chip.Height = 23;
            chip.CornerRadius = new CornerRadius(11.5);
            chip.Padding = new Thickness(8, 0, 8, 0);
            chip.Margin = new Thickness(0, 2, 5, 2);
            chip.Cursor = Cursors.Hand;
            chip.VerticalAlignment = VerticalAlignment.Center;
            chip.Background = on ? Theme.Accent : Brushes.White;
            chip.BorderThickness = new Thickness(1);
            chip.BorderBrush = on ? Theme.Accent : Theme.ChipBorder;
            chip.ToolTip = on ? "已在「" + label + "」中" : "只看「" + label + "」";

            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.VerticalAlignment = VerticalAlignment.Center;

            if (dot != null)
            {
                Border d = new Border();
                d.Width = 7;
                d.Height = 7;
                d.CornerRadius = new CornerRadius(3.5);
                d.Background = on ? Brushes.White : dot;
                d.VerticalAlignment = VerticalAlignment.Center;
                d.Margin = new Thickness(0, 0, 5, 0);
                sp.Children.Add(d);
            }

            TextBlock txt = new TextBlock();
            txt.Text = label;
            txt.FontSize = 11.5;
            txt.Foreground = on ? Brushes.White : Theme.TextSecondary;
            txt.VerticalAlignment = VerticalAlignment.Center;
            sp.Children.Add(txt);

            TextBlock num = new TextBlock();
            num.Text = count.ToString();
            num.FontSize = 10.5;
            num.Foreground = on ? BrushFrom(0xDC, 0xE3, 0xFF) : Theme.TextTertiary;
            num.VerticalAlignment = VerticalAlignment.Center;
            num.Margin = new Thickness(4, 0, 0, 0);
            sp.Children.Add(num);

            chip.Child = sp;

            chip.MouseEnter += delegate
            {
                if (groupFilter != key) chip.Background = Theme.HoverFill;
            };
            chip.MouseLeave += delegate
            {
                chip.Background = groupFilter == key ? Theme.Accent : Brushes.White;
            };
            chip.MouseLeftButtonUp += delegate
            {
                groupFilter = key;
                RefreshAll();
            };

            return chip;
        }

        private Border BuildNewGroupChip()
        {
            Border chip = new Border();
            chip.Height = 23;
            chip.CornerRadius = new CornerRadius(11.5);
            chip.Padding = new Thickness(8, 0, 8, 0);
            chip.Margin = new Thickness(0, 2, 5, 2);
            chip.Cursor = Cursors.Hand;
            chip.VerticalAlignment = VerticalAlignment.Center;
            chip.Background = Brushes.Transparent;
            chip.BorderThickness = new Thickness(1);
            chip.BorderBrush = Theme.ChipBorder;
            chip.ToolTip = "新建一个分组";

            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.VerticalAlignment = VerticalAlignment.Center;
            TextBlock plus = Theme.Glyph("\uE710", 9, Theme.TextTertiary);
            plus.VerticalAlignment = VerticalAlignment.Center;
            TextBlock txt = new TextBlock();
            txt.Text = "分组";
            txt.FontSize = 11.5;
            txt.Foreground = Theme.TextTertiary;
            txt.VerticalAlignment = VerticalAlignment.Center;
            txt.Margin = new Thickness(4, 0, 0, 0);
            sp.Children.Add(plus);
            sp.Children.Add(txt);
            chip.Child = sp;

            chip.MouseEnter += delegate
            {
                chip.Background = Theme.HoverFill;
                plus.Foreground = Theme.Accent;
                txt.Foreground = Theme.Accent;
            };
            chip.MouseLeave += delegate
            {
                chip.Background = Brushes.Transparent;
                plus.Foreground = Theme.TextTertiary;
                txt.Foreground = Theme.TextTertiary;
            };
            chip.MouseLeftButtonUp += delegate
            {
                ShowGroupDialog("新建分组", "", "创建", delegate(string name)
                {
                    TodoGroup g = Store.AddGroup(name);
                    if (g == null) return;
                    groupFilter = g.Id;
                    Store.Save();
                    RefreshAll();
                });
            };

            return chip;
        }

        // ---------- list sections ----------

        private string FilterName()
        {
            return groupFilter == GroupNone ? "未分组" : Store.GroupName(groupFilter);
        }

        // True when the item survives the current group filter.
        private bool GroupFilterMatch(TodoItem it)
        {
            if (groupFilter == GroupAll) return true;
            return Store.InGroup(it, groupFilter);
        }

        // Renders the active list as one section per group (used when no group filter is on).
        // The incoming list is already sorted, so each section keeps the global order.
        private void RenderActiveGrouped(List<TodoItem> active)
        {
            foreach (TodoGroup g in Store.Groups())
            {
                List<TodoItem> sub = active.FindAll(delegate(TodoItem i) { return Store.InGroup(i, g.Id); });
                if (sub.Count == 0) continue;
                AddGroupSection(g.Id, g.Name, sub.Count, GroupDot(GroupIndex(g.Id)), true, sub);
            }

            List<TodoItem> none = active.FindAll(delegate(TodoItem i) { return Store.InGroup(i, GroupNone); });
            if (none.Count > 0)
            {
                AddGroupSection(GroupNone, "未分组", none.Count, BrushFrom(0xB0, 0xB7, 0xC3), false, none);
            }
        }

        private void AddGroupSection(string id, string name, int count, Brush dot, bool editable,
            List<TodoItem> items)
        {
            bool collapsed = groupCollapsed.Contains(id);
            activeList.Children.Add(BuildGroupHeader(id, name, count, dot, editable, collapsed));
            if (collapsed) return;
            foreach (TodoItem it in items) activeList.Children.Add(BuildActiveCard(it));
        }

        private Border BuildGroupHeader(string id, string name, int count, Brush dot, bool editable,
            bool collapsed)
        {
            Border h = new Border();
            h.Padding = new Thickness(6, 5, 6, 5);
            h.Margin = new Thickness(2, 4, 2, 2);
            h.CornerRadius = new CornerRadius(8);
            h.Background = Brushes.Transparent;
            h.Cursor = Cursors.Hand;
            h.ToolTip = collapsed ? "展开这个分组" : "收起这个分组";

            Grid g = new Grid();
            ColumnDefinition c0 = new ColumnDefinition(); c0.Width = GridLength.Auto;
            ColumnDefinition c1 = new ColumnDefinition(); c1.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition c2 = new ColumnDefinition(); c2.Width = GridLength.Auto;
            g.ColumnDefinitions.Add(c0);
            g.ColumnDefinitions.Add(c1);
            g.ColumnDefinitions.Add(c2);
            h.Child = g;

            StackPanel left = new StackPanel();
            left.Orientation = Orientation.Horizontal;
            left.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(left);
            Grid.SetColumn(left, 0);

            Border d = new Border();
            d.Width = 8;
            d.Height = 8;
            d.CornerRadius = new CornerRadius(4);
            d.Background = dot;
            d.VerticalAlignment = VerticalAlignment.Center;
            left.Children.Add(d);

            TextBlock t = new TextBlock();
            t.Text = name;
            t.FontSize = 12;
            t.FontWeight = FontWeights.SemiBold;
            t.Foreground = BrushFrom(0x59, 0x60, 0x70);
            t.VerticalAlignment = VerticalAlignment.Center;
            t.Margin = new Thickness(7, 0, 0, 0);
            left.Children.Add(t);

            TextBlock n = new TextBlock();
            n.Text = count.ToString();
            n.FontSize = 11;
            n.Foreground = Theme.TextTertiary;
            n.VerticalAlignment = VerticalAlignment.Center;
            n.Margin = new Thickness(6, 1, 0, 0);
            left.Children.Add(n);

            TextBlock chev = Theme.Glyph(collapsed ? "\uE70E" : "\uE70D", 9, Theme.TextTertiary);
            chev.VerticalAlignment = VerticalAlignment.Center;
            chev.Margin = new Thickness(0, 0, 4, 0);
            g.Children.Add(chev);
            Grid.SetColumn(chev, 2);

            h.MouseEnter += delegate { h.Background = Theme.HoverFill; };
            h.MouseLeave += delegate { h.Background = Brushes.Transparent; };
            h.MouseLeftButtonUp += delegate
            {
                if (groupCollapsed.Contains(id)) groupCollapsed.Remove(id);
                else groupCollapsed.Add(id);
                RefreshAll();
            };

            if (editable)
            {
                ContextMenu cm = new ContextMenu();

                MenuItem rename = new MenuItem();
                rename.Header = "重命名分组";
                rename.Click += delegate
                {
                    ShowGroupDialog("重命名分组", name, "保存", delegate(string v)
                    {
                        if (!Store.RenameGroup(id, v)) return;
                        Store.Save();
                        RefreshAll();
                    });
                };
                cm.Items.Add(rename);

                MenuItem del = new MenuItem();
                del.Header = "删除分组";
                del.Click += delegate { ConfirmDeleteGroup(id, name); };
                cm.Items.Add(del);

                h.ContextMenu = cm;
            }

            return h;
        }

        private void ConfirmDeleteGroup(string id, string name)
        {
            int n = Store.CountInGroup(Store.Data.Items, id);
            string msg = n == 0
                ? "确定删除分组「" + name + "」吗？"
                : "确定删除分组「" + name + "」吗？\n组内 " + n + " 条待办会移到「未分组」，不会被删除。";
            MessageBoxResult r = MessageBox.Show(this, msg, "删除分组",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            Store.DeleteGroup(id);
            if (groupFilter == id) groupFilter = GroupAll;
            Store.Save();
            RefreshAll();
        }

        // ---------- move-to-group button + picker ----------

        private Border BuildGroupButton(TodoItem item)
        {
            Border gb = new Border();
            gb.Width = 24;
            gb.Height = 24;
            gb.CornerRadius = new CornerRadius(12);
            gb.Background = Brushes.Transparent;
            gb.Cursor = Cursors.Hand;
            gb.VerticalAlignment = VerticalAlignment.Center;
            gb.ToolTip = "移入分组（当前：" + Store.GroupName(item.GroupId) + "）";

            TextBlock gTxt = Theme.Glyph("\uE8B7", 11, Theme.TextTertiary);
            gTxt.HorizontalAlignment = HorizontalAlignment.Center;
            gTxt.VerticalAlignment = VerticalAlignment.Center;
            gb.Child = gTxt;

            gb.MouseEnter += delegate
            {
                gb.Background = BrushFrom(0xEE, 0xF1, 0xF9);
                gTxt.Foreground = Theme.Accent;
            };
            gb.MouseLeave += delegate
            {
                gb.Background = Brushes.Transparent;
                gTxt.Foreground = Theme.TextTertiary;
            };
            gb.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; };
            gb.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                e.Handled = true;
                ShowGroupPicker(gb, item);
            };

            return gb;
        }

        // Small floating list: 未分组 + every group + 新建分组. Clicking a row moves the item.
        private void ShowGroupPicker(FrameworkElement anchor, TodoItem item)
        {
            if (groupPicker != null)
            {
                groupPicker.Close();
                groupPicker = null;
                return;
            }

            Window w = new Window();
            groupPicker = w;
            w.Title = "移入分组";
            w.Width = 190;
            w.SizeToContent = SizeToContent.Height;
            w.WindowStyle = WindowStyle.None;
            w.ResizeMode = ResizeMode.NoResize;
            w.AllowsTransparency = true;
            w.Background = Brushes.Transparent;
            w.Topmost = true;
            w.ShowInTaskbar = false;
            w.WindowStartupLocation = WindowStartupLocation.Manual;

            Border root = new Border();
            root.Background = Brushes.White;
            root.CornerRadius = new CornerRadius(12);
            root.BorderBrush = Theme.CardBorder;
            root.BorderThickness = new Thickness(1);
            root.Margin = new Thickness(9);
            root.Padding = new Thickness(4, 4, 4, 4);
            root.Effect = Theme.CardShadow();
            w.Content = root;

            StackPanel sp = new StackPanel();
            root.Child = sp;

            TextBlock cap = new TextBlock();
            cap.Text = "移入分组";
            cap.FontSize = 10.5;
            cap.Foreground = Theme.TextTertiary;
            cap.Margin = new Thickness(8, 4, 8, 5);
            sp.Children.Add(cap);

            List<string> ids = new List<string>();
            ids.Add(GroupNone);
            foreach (TodoGroup g in Store.Groups()) ids.Add(g.Id);

            foreach (string id in ids)
            {
                bool current = Store.InGroup(item, id);
                string label = id == GroupNone ? "未分组" : Store.GroupName(id);
                sp.Children.Add(BuildPickerRow(w, label, current, id == GroupNone ? null : id, item));
            }

            Border divider = new Border();
            divider.Height = 1;
            divider.Background = Theme.CardBorder;
            divider.Margin = new Thickness(6, 4, 6, 4);
            sp.Children.Add(divider);

            Border newRow = new Border();
            newRow.CornerRadius = new CornerRadius(8);
            newRow.Padding = new Thickness(8, 6, 8, 6);
            newRow.Cursor = Cursors.Hand;
            StackPanel nsp = new StackPanel();
            nsp.Orientation = Orientation.Horizontal;
            TextBlock plus = Theme.Glyph("\uE710", 9, Theme.Accent);
            plus.VerticalAlignment = VerticalAlignment.Center;
            TextBlock ntxt = new TextBlock();
            ntxt.Text = "新建分组…";
            ntxt.FontSize = 12;
            ntxt.Foreground = Theme.Accent;
            ntxt.VerticalAlignment = VerticalAlignment.Center;
            ntxt.Margin = new Thickness(6, 0, 0, 0);
            nsp.Children.Add(plus);
            nsp.Children.Add(ntxt);
            newRow.Child = nsp;
            newRow.MouseEnter += delegate { newRow.Background = BrushFrom(0xF3, 0xF5, 0xFC); };
            newRow.MouseLeave += delegate { newRow.Background = Brushes.Transparent; };
            newRow.MouseLeftButtonUp += delegate
            {
                w.Close();
                ShowGroupDialog("新建分组", "", "创建并移入", delegate(string name)
                {
                    TodoGroup g = Store.AddGroup(name);
                    if (g == null) return;
                    Store.MoveItem(item.Id, g.Id);
                    Store.Save();
                    RefreshAll();
                });
            };
            sp.Children.Add(newRow);

            // Anchor below the button, right-aligned to it, clamped to the work area.
            Point scr = anchor.PointToScreen(new Point(0, anchor.ActualHeight));
            Point dip = scr;
            PresentationSource ps = PresentationSource.FromVisual(this);
            if (ps != null && ps.CompositionTarget != null)
                dip = ps.CompositionTarget.TransformFromDevice.Transform(scr);
            Rect wa = SystemParameters.WorkArea;
            double left = dip.X + anchor.ActualWidth - 190 + 9;
            if (left + 190 > wa.Right - 6) left = wa.Right - 6 - 190;
            if (left < wa.Left + 6) left = wa.Left + 6;
            w.Left = left;
            w.Top = dip.Y - 6;

            w.Loaded += delegate
            {
                if (w.Top + w.ActualHeight > wa.Bottom - 6)
                    w.Top = Math.Max(wa.Top + 6, dip.Y - 6 - w.ActualHeight);
            };
            w.Closed += delegate { if (groupPicker == w) groupPicker = null; };
            // Click anywhere else (including back on the widget) dismisses the picker.
            w.Deactivated += delegate { w.Close(); };
            w.Show();
        }

        private Border BuildPickerRow(Window w, string label, bool current, string groupId, TodoItem item)
        {
            Border row = new Border();
            row.CornerRadius = new CornerRadius(8);
            row.Padding = new Thickness(8, 6, 8, 6);
            row.Cursor = Cursors.Hand;
            row.Background = Brushes.Transparent;

            Grid g = new Grid();
            ColumnDefinition c0 = new ColumnDefinition(); c0.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition c1 = new ColumnDefinition(); c1.Width = GridLength.Auto;
            g.ColumnDefinitions.Add(c0);
            g.ColumnDefinitions.Add(c1);
            row.Child = g;

            TextBlock t = new TextBlock();
            t.Text = label;
            t.FontSize = 12;
            t.Foreground = current ? Theme.Accent : Theme.TextPrimary;
            t.FontWeight = current ? FontWeights.SemiBold : FontWeights.Normal;
            t.VerticalAlignment = VerticalAlignment.Center;
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            g.Children.Add(t);
            Grid.SetColumn(t, 0);

            if (current)
            {
                TextBlock ck = Theme.Glyph("\uE73E", 10, Theme.Accent);
                ck.VerticalAlignment = VerticalAlignment.Center;
                g.Children.Add(ck);
                Grid.SetColumn(ck, 1);
            }

            row.MouseEnter += delegate { row.Background = BrushFrom(0xF3, 0xF5, 0xFC); };
            row.MouseLeave += delegate { row.Background = Brushes.Transparent; };
            row.MouseLeftButtonUp += delegate
            {
                w.Close();
                if (current) return;
                Store.MoveItem(item.Id, groupId);
                Store.Save();
                RefreshAll();
            };

            return row;
        }

        // ---------- new / rename group dialog ----------

        private void ShowGroupDialog(string caption, string initial, string okLabel, Action<string> onOk)
        {
            if (groupDialog != null)
            {
                groupDialog.Close();
                groupDialog = null;
            }

            Window w = new Window();
            groupDialog = w;
            w.Title = caption;
            w.Width = 300;
            w.SizeToContent = SizeToContent.Height;
            w.WindowStyle = WindowStyle.None;
            w.ResizeMode = ResizeMode.NoResize;
            w.AllowsTransparency = true;
            w.Background = Brushes.Transparent;
            w.Topmost = true;
            w.ShowInTaskbar = false;
            w.WindowStartupLocation = WindowStartupLocation.Manual;
            w.Left = Left + (Width - w.Width) / 2;
            w.Top = Top + 120;

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

            TextBlock title = new TextBlock();
            title.Text = caption;
            title.FontSize = 13;
            title.FontWeight = FontWeights.SemiBold;
            title.Foreground = Theme.TextPrimary;
            title.Margin = new Thickness(14, 12, 14, 8);
            sp.Children.Add(title);

            TextBox tb = new TextBox();
            tb.Margin = new Thickness(14, 0, 14, 0);
            tb.FontSize = 13;
            tb.Padding = new Thickness(9, 7, 9, 7);
            tb.Text = initial == null ? "" : initial;
            tb.BorderBrush = Theme.ChipBorder;
            tb.BorderThickness = new Thickness(1);
            tb.Background = BrushFrom(0xFA, 0xFB, 0xFD);
            tb.GotKeyboardFocus += delegate { tb.BorderBrush = Theme.Accent; };
            tb.LostKeyboardFocus += delegate { tb.BorderBrush = Theme.ChipBorder; };
            sp.Children.Add(tb);

            TextBlock tip = new TextBlock();
            tip.Text = "同名分组不会重复创建";
            tip.FontSize = 10.5;
            tip.Foreground = Theme.TextTertiary;
            tip.Margin = new Thickness(15, 5, 14, 0);
            sp.Children.Add(tip);

            StackPanel btns = new StackPanel();
            btns.Orientation = Orientation.Horizontal;
            btns.HorizontalAlignment = HorizontalAlignment.Right;
            btns.Margin = new Thickness(14, 10, 14, 12);
            sp.Children.Add(btns);

            Border cancel = new Border();
            cancel.Background = Theme.ChipBg;
            cancel.BorderBrush = Theme.ChipBorder;
            cancel.BorderThickness = new Thickness(1);
            cancel.CornerRadius = new CornerRadius(8);
            cancel.Padding = new Thickness(14, 6, 14, 7);
            cancel.Cursor = Cursors.Hand;
            TextBlock cancelTxt = new TextBlock();
            cancelTxt.Text = "取消";
            cancelTxt.FontSize = 12.5;
            cancelTxt.Foreground = Theme.TextSecondary;
            cancel.Child = cancelTxt;
            cancel.MouseEnter += delegate { cancel.Background = BrushFrom(0xEE, 0xF0, 0xF6); };
            cancel.MouseLeave += delegate { cancel.Background = Theme.ChipBg; };
            cancel.MouseLeftButtonUp += delegate { w.Close(); };
            btns.Children.Add(cancel);

            Border ok = new Border();
            ok.Background = Theme.Accent;
            ok.CornerRadius = new CornerRadius(8);
            ok.Padding = new Thickness(16, 6, 16, 7);
            ok.Cursor = Cursors.Hand;
            ok.Margin = new Thickness(8, 0, 0, 0);
            TextBlock okTxt = new TextBlock();
            okTxt.Text = okLabel;
            okTxt.FontSize = 12.5;
            okTxt.FontWeight = FontWeights.SemiBold;
            okTxt.Foreground = Brushes.White;
            ok.Child = okTxt;
            ok.MouseEnter += delegate { ok.Background = Theme.AccentDark; };
            ok.MouseLeave += delegate { ok.Background = Theme.Accent; };
            btns.Children.Add(ok);

            Action commit = delegate
            {
                string v = tb.Text == null ? "" : tb.Text.Trim();
                if (v.Length == 0)
                {
                    tb.BorderBrush = Theme.Danger;
                    tb.Focus();
                    return;
                }
                w.Close();
                onOk(v);
            };

            tb.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) { e.Handled = true; commit(); }
                if (e.Key == Key.Escape) w.Close();
            };
            ok.MouseLeftButtonUp += delegate { commit(); };

            w.Loaded += delegate { tb.Focus(); tb.SelectAll(); };
            w.Closed += delegate { if (groupDialog == w) groupDialog = null; };
            w.Show();
        }

        // ---------- helpers ----------

        private int GroupIndex(string id)
        {
            List<TodoGroup> list = Store.Groups();
            for (int i = 0; i < list.Count; i++) if (list[i].Id == id) return i;
            return 0;
        }

        // Stable per-position palette so a group keeps its color across restarts.
        private static Brush GroupDot(int index)
        {
            switch (((index % 8) + 8) % 8)
            {
                case 0: return BrushFrom(0x4F, 0x6B, 0xED);
                case 1: return BrushFrom(0xF0, 0x8C, 0x4B);
                case 2: return BrushFrom(0x2B, 0xAE, 0x7F);
                case 3: return BrushFrom(0x8B, 0x6B, 0xE0);
                case 4: return BrushFrom(0xD9, 0x60, 0x8C);
                case 5: return BrushFrom(0x3D, 0x9B, 0xD9);
                case 6: return BrushFrom(0xC7, 0x9A, 0x2E);
                default: return BrushFrom(0x6B, 0x7A, 0x8F);
            }
        }
    }
}
