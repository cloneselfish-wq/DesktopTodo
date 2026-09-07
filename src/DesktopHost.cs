using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DesktopTodo
{
    // Hosts the widget window inside the desktop wallpaper layer (behind desktop icons, above the wallpaper)
    // using the standard Progman/WorkerW technique. Falls back gracefully if the desktop layer is not found.
    public static class DesktopHost
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int GWL_STYLE = -16;
        private const int WS_CHILD = 0x40000000;
        private const int WS_POPUP = unchecked((int)0x80000000);

        // Returns the WorkerW window that renders the wallpaper, or IntPtr.Zero if not found.
        public static IntPtr GetDesktopWorkerW()
        {
            IntPtr progman = FindWindow("Progman", null);
            if (progman == IntPtr.Zero) return IntPtr.Zero;

            IntPtr defview = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defview == IntPtr.Zero)
            {
                IntPtr w = IntPtr.Zero;
                while (true)
                {
                    w = FindWindowEx(progman, w, "WorkerW", null);
                    if (w == IntPtr.Zero) break;
                    defview = FindWindowEx(w, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (defview != IntPtr.Zero) break;
                }
            }

            // Ask Progman (0x052C) to spawn the wallpaper WorkerW layer
            IntPtr result;
            SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out result);

            // 1) Classic (Win10 / early Win11): a TOP-LEVEL WorkerW without the icons view
            //    is the live wallpaper layer. Never fall back to hidden ones — Windows keeps
            //    stale HIDDEN WorkerW layers around (measured: 16 on Win11 25H2) and
            //    parenting into those makes the widget invisible.
            IntPtr worker = IntPtr.Zero;
            IntPtr w2 = IntPtr.Zero;
            while (true)
            {
                w2 = FindWindowEx(IntPtr.Zero, w2, "WorkerW", null);
                if (w2 == IntPtr.Zero) break;
                if (FindWindowEx(w2, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero) continue;
                if (worker == IntPtr.Zero && IsWindowVisible(w2)) worker = w2;
            }
            if (worker != IntPtr.Zero) return worker;

            // 2) Windows 11 24H2+/25H2: 0x052C no longer spawns a top-level layer; the live
            //    wallpaper WorkerW is a visible CHILD of Progman (sibling of the icons view).
            IntPtr w3 = IntPtr.Zero;
            while (true)
            {
                w3 = FindWindowEx(progman, w3, "WorkerW", null);
                if (w3 == IntPtr.Zero) break;
                if (FindWindowEx(w3, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero) continue;
                if (worker == IntPtr.Zero && IsWindowVisible(w3)) worker = w3;
            }
            return worker;
        }

        public static bool Embed(Window win)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(win).EnsureHandle();
                IntPtr worker = GetDesktopWorkerW();
                if (worker == IntPtr.Zero) return false;

                RECT wr, cr;
                bool haveWorkerRect = GetWindowRect(worker, out wr);
                bool haveWinRect = GetWindowRect(hwnd, out cr);
                Logger.Log("Embed: worker=" + worker.ToInt64() + " visible=" + IsWindowVisible(worker));

                int style = GetWindowLong(hwnd, GWL_STYLE);
                SetWindowLong(hwnd, GWL_STYLE, (style & ~WS_POPUP) | WS_CHILD);
                if (!SetParent(hwnd, worker))
                {
                    SetWindowLong(hwnd, GWL_STYLE, style);
                    Logger.Log("Embed: SetParent failed, err=" + Marshal.GetLastWin32Error());
                    return false;
                }
                Logger.Log("Embed: parented into " + worker.ToInt64());

                if (haveWorkerRect && haveWinRect)
                {
                    // Coordinates become relative to the host layer after SetParent;
                    // keep the same on-screen spot and force the window visible.
                    SetWindowPos(hwnd, IntPtr.Zero,
                        cr.Left - wr.Left, cr.Top - wr.Top,
                        cr.Right - cr.Left, cr.Bottom - cr.Top,
                        SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
                }
                else
                {
                    SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log("DesktopHost.Embed: " + ex.Message);
                return false;
            }
        }

        public static bool Unembed(Window win)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(win).EnsureHandle();
                int style = GetWindowLong(hwnd, GWL_STYLE);
                SetWindowLong(hwnd, GWL_STYLE, (style & ~WS_CHILD) | WS_POPUP);
                SetParent(hwnd, IntPtr.Zero);
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED | SWP_NOACTIVATE);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log("DesktopHost.Unembed: " + ex.Message);
                return false;
            }
        }

        // Window bounds in physical pixels (screen coords in normal mode, WorkerW coords when embedded)
        public static Rect GetBounds(Window win)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(win).EnsureHandle();
                RECT r;
                if (GetWindowRect(hwnd, out r))
                {
                    return new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
                }
            }
            catch { }
            return new Rect(win.Left, win.Top, win.Width, win.Height);
        }

        public static void SetEmbedPosition(Window win, double x, double y)
        {
            IntPtr hwnd = new WindowInteropHelper(win).EnsureHandle();
            SetWindowPos(hwnd, IntPtr.Zero, (int)Math.Round(x), (int)Math.Round(y), 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }
}
