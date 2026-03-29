using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;

namespace MinimalFirewall
{
    public partial class App : Application
    {
        private const string AppGuid = "Global\\6326C497-403B-F991-2F6A-A5FBA67C364C";

        private MainWindow? _mainWindow;
        private TrayIconManager? _trayIconManager;

        // Tray icon native
        private static App? _instance;
        public static App Instance => _instance!;

        // Startup flag
        public bool StartMinimized { get; private set; }

        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                HandleException(e.ExceptionObject as Exception);

            using var mutex = new Mutex(true, AppGuid, out bool createdNew);
            if (!createdNew)
            {
                NativeMessageBox("Minimal Firewall is already running.", "Application Already Running");
                return;
            }

            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            bool startMinimized = false;
            foreach (var arg in args)
            {
                if (arg.Equals("-tray", StringComparison.OrdinalIgnoreCase))
                    startMinimized = true;
            }

            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start((_) =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                var app = new App { StartMinimized = startMinimized };
                _instance = app;
            });

            // Keep mutex alive until app exits
            GC.KeepAlive(mutex);
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _mainWindow = new MainWindow(StartMinimized);
            _mainWindow.Activate();
        }

        internal MainWindow? MainWindow => _mainWindow;

        private static void HandleException(Exception? ex)
        {
            NativeMessageBox($"An unexpected error occurred: {ex?.Message}", "Error");
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);

        private static void NativeMessageBox(string text, string caption)
            => MessageBox(IntPtr.Zero, text, caption, 0x00000000);
    }
}
