// Dev diagnostic: verifies whether the DesktopTodo widget actually RENDERS on screen.
// Captures the app window's screen region, hides the window, captures again, shows it,
// then reports how many pixels changed (= the widget really is composited on screen).
// Also saves shot_with.png / shot_without.png for visual inspection.
// Usage: DesktopShot.exe
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

class DesktopShot
{
    delegate bool EnumProc(IntPtr h, IntPtr lp);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr lp);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern IntPtr FindWindow(string c, string w);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string w);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hh, IntPtr after, int x, int y, int cx, int cy, uint flags);
    const uint SWP_NOSIZE_F = 0x0001, SWP_NOZORDER_F = 0x0004, SWP_NOACTIVATE_F = 0x0010;

    static IntPtr bestHwnd;
    static long bestArea;

    static void Consider(IntPtr h)
    {
        uint p;
        GetWindowThreadProcessId(h, out p);
        if (p != appPid) return;
        StringBuilder sb = new StringBuilder(256);
        GetClassName(h, sb, 256);
        string cls = sb.ToString();
        if (!cls.StartsWith("HwndWrapper")) return;
        RECT r;
        GetWindowRect(h, out r);
        long area = (long)(r.R - r.L) * (r.B - r.T);
        StringBuilder tb = new StringBuilder(256);
        GetWindowText(h, tb, 256);
        Console.WriteLine("  hwnd=" + h.ToInt64() + " visible=" + IsWindowVisible(h)
            + " rect=(" + r.L + "," + r.T + ")-(" + r.R + "," + r.B + ")"
            + " area=" + area + " text=" + tb);
        bool helper = tb.ToString() == "Hidden Window"; // WinForms parking window, not ours
        if (helper && IsWindowVisible(h)) ShowWindow(h, 0); // re-hide if a previous run showed it
        if (!helper && IsWindowVisible(h) && area > bestArea) { bestArea = area; bestHwnd = h; }
    }

    static Bitmap Capture(int x, int y, int w, int h)
    {
        Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
            g.CopyFromScreen(x, y, 0, 0, new Size(w, h));
        return bmp;
    }

    static int Diff(Bitmap a, Bitmap b)
    {
        Rectangle rect = new Rectangle(0, 0, a.Width, a.Height);
        BitmapData da = a.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        BitmapData db = b.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        int diff = 0;
        unsafe
        {
            byte* pa = (byte*)da.Scan0;
            byte* pb = (byte*)db.Scan0;
            for (int y = 0; y < a.Height; y++)
            {
                byte* ra = pa + y * da.Stride;
                byte* rb = pb + y * db.Stride;
                for (int x = 0; x < a.Width; x++)
                {
                    int o = x * 4;
                    if (ra[o] != rb[o] || ra[o + 1] != rb[o + 1] || ra[o + 2] != rb[o + 2]) diff++;
                }
            }
        }
        a.UnlockBits(da);
        b.UnlockBits(db);
        return diff;
    }

    static uint appPid;

    static void Main(string[] args)
    {
        foreach (System.Diagnostics.Process p in System.Diagnostics.Process.GetProcessesByName("DesktopTodo"))
        { appPid = (uint)p.Id; break; }
        if (appPid == 0) { Console.WriteLine("APP_NOT_RUNNING"); return; }

        if (args.Length > 0 && args[0] == "full")
        {
            Rectangle vsf = SystemInformation.VirtualScreen;
            using (Bitmap f = Capture(vsf.Left, vsf.Top, vsf.Width, vsf.Height))
                f.Save("shot_full.png", ImageFormat.Png);
            Console.WriteLine("full capture " + vsf.Width + "x" + vsf.Height + " at (" + vsf.Left + "," + vsf.Top + ") -> shot_full.png");
            return;
        }

        Console.WriteLine("== pid " + appPid + " windows ==");
        bestHwnd = IntPtr.Zero;
        bestArea = 0;

        // top-level windows of the process
        EnumWindows(delegate(IntPtr hw, IntPtr lp) { Consider(hw); return true; }, IntPtr.Zero);

        // children of Progman and of every WorkerW (top-level and Progman-child)
        IntPtr progman = FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
        {
            Consider(progman);
            IntPtr c = IntPtr.Zero;
            while (true) { c = FindWindowEx(progman, c, null, null); if (c == IntPtr.Zero) break; Consider(c); }
        }
        IntPtr w = IntPtr.Zero;
        while (true)
        {
            w = FindWindowEx(IntPtr.Zero, w, "WorkerW", null);
            if (w == IntPtr.Zero) break;
            Consider(w);
            IntPtr c2 = IntPtr.Zero;
            while (true) { c2 = FindWindowEx(w, c2, null, null); if (c2 == IntPtr.Zero) break; Consider(c2); }
        }
        IntPtr w2 = IntPtr.Zero;
        if (progman != IntPtr.Zero)
        {
            while (true)
            {
                w2 = FindWindowEx(progman, w2, "WorkerW", null);
                if (w2 == IntPtr.Zero) break;
                Consider(w2);
                IntPtr c3 = IntPtr.Zero;
                while (true) { c3 = FindWindowEx(w2, c3, null, null); if (c3 == IntPtr.Zero) break; Consider(c3); }
            }
        }

        if (bestHwnd == IntPtr.Zero) { Console.WriteLine("NO_APP_WINDOW_FOUND"); return; }
        IntPtr h = bestHwnd;

        if (args.Length > 0 && args[0] == "move" && args.Length >= 3)
        {
            int mx = int.Parse(args[1]), my = int.Parse(args[2]);
            SetWindowPos(h, IntPtr.Zero, mx, my, 0, 0, SWP_NOSIZE_F | SWP_NOZORDER_F | SWP_NOACTIVATE_F);
            Console.WriteLine("widget moved to (" + mx + "," + my + ")");
            Thread.Sleep(500);
        }

        RECT r;
        GetWindowRect(h, out r);
        IntPtr root = GetAncestor(h, 2); // GA_ROOT
        StringBuilder rb = new StringBuilder(256);
        GetClassName(root, rb, 256);

        Rectangle vs = SystemInformation.VirtualScreen;
        int x = Math.Max(r.L, vs.Left);
        int y = Math.Max(r.T, vs.Top);
        int wd = Math.Min(r.R, vs.Right) - x;
        int ht = Math.Min(r.B, vs.Bottom) - y;
        if (wd <= 0 || ht <= 0) { Console.WriteLine("WINDOW_OFFSCREEN rect=(" + r.L + "," + r.T + ")-(" + r.R + "," + r.B + ") virtual=" + vs); return; }

        Console.WriteLine("== diff test on hwnd=" + h.ToInt64()
            + " visible=" + IsWindowVisible(h)
            + " root=" + root.ToInt64() + " " + rb
            + " rect=(" + r.L + "," + r.T + ")-(" + r.R + "," + r.B + ") capture=" + wd + "x" + ht + " at (" + x + "," + y + ")");

        Bitmap a = Capture(x, y, wd, ht);
        a.Save("shot_with.png", ImageFormat.Png);
        ShowWindow(h, 0); // SW_HIDE
        Thread.Sleep(600);
        Bitmap b = Capture(x, y, wd, ht);
        b.Save("shot_without.png", ImageFormat.Png);
        ShowWindow(h, 8); // SW_SHOWNA

        int diff = Diff(a, b);
        int total = wd * ht;
        Console.WriteLine("DIFF_PIXELS=" + diff + "/" + total + " (" + (100.0 * diff / total).ToString("F1") + "%)"
            + " -> " + (diff > total / 20 ? "WIDGET RENDERS ON SCREEN" : "WIDGET NOT VISIBLE ON SCREEN"));

        // Capture the window's OWN surface (works even when other windows overlap it):
        // shows whether the WPF visual tree renders or the window is a blank background.
        using (Bitmap pb = new Bitmap(r.R - r.L, r.B - r.T, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(pb))
            {
                IntPtr hdc = g.GetHdc();
                PrintWindow(h, hdc, 2); // PW_RENDERFULLCONTENT
                g.ReleaseHdc(hdc);
            }
            pb.Save("shot_pw.png", ImageFormat.Png);
            Console.WriteLine("PrintWindow capture saved (shot_pw.png)");
        }
    }
}
