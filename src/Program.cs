using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace DesktopTodo
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "DesktopTodo_SingleInstance_Mutex", out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }
                GC.KeepAlive(mutex);

                Store.Load();

                Application app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                app.DispatcherUnhandledException += OnDispatcherException;

                MainWindow win = new MainWindow();
                TrayController tray = new TrayController(win);
                win.AttachTray(tray);
                win.Show();

                app.Run();
            }
        }

        private static void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.Log("Unhandled exception: " + e.Exception);
            try
            {
                MessageBox.Show("出错了：" + e.Exception.Message, "桌面待办",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
            e.Handled = true;
        }
    }
}
