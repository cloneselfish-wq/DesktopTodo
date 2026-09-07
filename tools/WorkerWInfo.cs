// Dev diagnostic: dumps the Progman/WorkerW desktop window hierarchy so we can
// see which wallpaper layers exist, their Z-order, visibility and rects.
// Usage: WorkerWInfo.exe
using System;
using System.Runtime.InteropServices;
using System.Text;

class WorkerWInfo
{
    delegate bool EnumProc(IntPtr h, IntPtr lp);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern IntPtr FindWindow(string c, string w);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string w);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr lp);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll", SetLastError = true)] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint m, IntPtr wp, IntPtr lp, uint f, uint t, out IntPtr r);

    static void Main()
    {
        Dump("BEFORE 0x052C");
        IntPtr progman = FindWindow("Progman", null);
        IntPtr res;
        SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out res);
        Dump("AFTER 0x052C");
    }

    static void Dump(string tag)
    {
        Console.WriteLine("== " + tag + " ==");
        EnumWindows(delegate(IntPtr h, IntPtr lp)
        {
            StringBuilder sb = new StringBuilder(256);
            GetClassName(h, sb, 256);
            string cls = sb.ToString();
            if (cls == "Progman" || cls == "WorkerW") Print(h, "  ", 2);
            return true;
        }, IntPtr.Zero);
        Console.WriteLine();
    }

    static void Print(IntPtr h, string indent, int depth)
    {
        StringBuilder sb = new StringBuilder(256);
        GetClassName(h, sb, 256);
        RECT r;
        GetWindowRect(h, out r);
        long style = (long)(uint)GetWindowLong(h, -16);
        Console.WriteLine(indent + h.ToInt64() + " " + sb + " visible=" + IsWindowVisible(h)
            + " rect=(" + r.L + "," + r.T + ")-(" + r.R + "," + r.B + ")"
            + " style=0x" + style.ToString("X"));
        if (depth <= 0) return;
        IntPtr c = IntPtr.Zero;
        while (true)
        {
            c = FindWindowEx(h, c, null, null);
            if (c == IntPtr.Zero) break;
            Print(c, indent + "    ", depth - 1);
        }
    }
}
