using System;
using System.Windows;
using WinForms = System.Windows.Forms;
using SD = System.Drawing;
using SD2 = System.Drawing.Drawing2D;

namespace DesktopTodo
{
    public class TrayController : IDisposable
    {
        private readonly MainWindow win;
        private WinForms.NotifyIcon tray;
        private WinForms.ContextMenuStrip menu;
        private WinForms.ToolStripMenuItem pinItem;
        private WinForms.ToolStripMenuItem embedItem;
        private WinForms.ToolStripMenuItem autoStartItem;
        private bool exiting;

        public TrayController(MainWindow mainWindow)
        {
            win = mainWindow;

            tray = new WinForms.NotifyIcon();
            tray.Icon = MakeIcon();
            tray.Text = "桌面待办";
            tray.Visible = true;

            menu = new WinForms.ContextMenuStrip();

            menu.Items.Add(new WinForms.ToolStripMenuItem("显示 / 隐藏", null,
                delegate { win.ToggleVisible(); }));
            menu.Items.Add(new WinForms.ToolStripSeparator());

            pinItem = new WinForms.ToolStripMenuItem("窗口置顶", null,
                delegate { win.SetTopmost(!win.IsTopmost); });
            menu.Items.Add(pinItem);

            embedItem = new WinForms.ToolStripMenuItem("贴入桌面(壁纸层)", null,
                delegate { win.ToggleEmbed(); });
            menu.Items.Add(embedItem);

            autoStartItem = new WinForms.ToolStripMenuItem("开机自动启动", null,
                delegate { AutoStart.SetEnabled(!AutoStart.IsEnabled()); });
            menu.Items.Add(autoStartItem);

            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add(new WinForms.ToolStripMenuItem("退出", null,
                delegate { Exit(); }));

            menu.Opening += delegate { RefreshMenu(); };
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { win.ToggleVisible(); };
        }

        private void RefreshMenu()
        {
            pinItem.Checked = win.IsTopmost;
            embedItem.Checked = win.IsEmbedded;
            autoStartItem.Checked = AutoStart.IsEnabled();
        }

        public void ShowTip(string title, string text)
        {
            if (tray == null) return;
            try
            {
                tray.BalloonTipTitle = title;
                tray.BalloonTipText = text;
                tray.ShowBalloonTip(1800);
            }
            catch { }
        }

        private void Exit()
        {
            if (exiting) return;
            exiting = true;
            try { win.SavePosition(); } catch { }
            try { win.PrepareExit(); } catch { }
            Dispose();
            Application.Current.Shutdown();
        }

        public void Dispose()
        {
            if (tray != null)
            {
                tray.Visible = false;
                tray.Dispose();
                tray = null;
            }
            if (menu != null)
            {
                menu.Dispose();
                menu = null;
            }
        }

        // Draws a rounded square with a white check mark (used for tray + taskbar icon)
        public static SD.Icon MakeIcon()
        {
            using (SD.Bitmap bmp = new SD.Bitmap(32, 32))
            {
                using (SD.Graphics g = SD.Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SD2.SmoothingMode.AntiAlias;
                    using (SD2.GraphicsPath path = new SD2.GraphicsPath())
                    {
                        int r = 7;
                        int w = 31;
                        path.AddArc(0, 0, 2 * r, 2 * r, 180, 90);
                        path.AddArc(w - 2 * r, 0, 2 * r, 2 * r, 270, 90);
                        path.AddArc(w - 2 * r, w - 2 * r, 2 * r, 2 * r, 0, 90);
                        path.AddArc(0, w - 2 * r, 2 * r, 2 * r, 90, 90);
                        path.CloseFigure();
                        using (SD.SolidBrush b = new SD.SolidBrush(SD.Color.FromArgb(79, 107, 237)))
                        {
                            g.FillPath(b, path);
                        }
                    }
                    using (SD.Pen p = new SD.Pen(SD.Color.White, 3f))
                    {
                        p.StartCap = SD2.LineCap.Round;
                        p.EndCap = SD2.LineCap.Round;
                        p.LineJoin = SD2.LineJoin.Round;
                        g.DrawLine(p, 9, 17, 14, 22);
                        g.DrawLine(p, 14, 22, 23, 10);
                    }
                }
                return SD.Icon.FromHandle(bmp.GetHicon());
            }
        }
    }
}
