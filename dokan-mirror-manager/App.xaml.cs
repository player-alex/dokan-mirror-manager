using System.Windows;
using Caliburn.Micro;
using DokanMirrorManager.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;

namespace DokanMirrorManager
{
    public partial class App : Application
    {
        private static Mutex? _mutex = null;
        private const int WM_SHOWWINDOW_CUSTOM = 0x8001;

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        public App()
        {
            // Check for single instance
            bool createdNew;
            _mutex = new Mutex(true, "DokanMirrorManager_SingleInstance", out createdNew);

            if (!createdNew)
            {
                // Try to acquire the mutex in case the previous instance crashed
                try
                {
                    // Wait for 1 second to acquire the mutex
                    if (_mutex.WaitOne(TimeSpan.FromSeconds(1), false))
                    {
                        // Successfully acquired the mutex - previous instance crashed
                        // Continue with normal startup
                    }
                    else
                    {
                        // Another instance is actually running - activate it
                        ActivateExistingInstance();
                        Current.Shutdown();
                        return;
                    }
                }
                catch (AbandonedMutexException)
                {
                    // Previous instance crashed and left the mutex abandoned
                    // The mutex is now owned by this thread, continue with normal startup
                }
            }

            // Global exception handlers
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            new Bootstrapper();
        }

        private void ActivateExistingInstance()
        {
            // Find the existing window by title
            IntPtr hwnd = FindWindow(null, "Dokan Mirror Manager");
            if (hwnd != IntPtr.Zero)
            {
                // Send custom message to show the window
                PostMessage(hwnd, WM_SHOWWINDOW_CUSTOM, IntPtr.Zero, IntPtr.Zero);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] DispatcherUnhandledException:\n{e.Exception}\n\n");

            MessageBox.Show($"An error occurred:\n\n{e.Exception.GetType().Name}: {e.Exception.Message}\n\nSee crash.log for details",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] UnhandledException:\n{e.ExceptionObject}\n\n");
        }
    }

    public class Bootstrapper : BootstrapperBase
    {
        private SimpleContainer _container = new SimpleContainer();

        public Bootstrapper()
        {
            Initialize();
        }

        protected override void Configure()
        {
            _container.Singleton<IWindowManager, WindowManager>();
            _container.Singleton<IEventAggregator, EventAggregator>();
            _container.PerRequest<ShellViewModel>();
        }

        protected override object GetInstance(Type service, string key)
        {
            return _container.GetInstance(service, key);
        }

        protected override IEnumerable<object> GetAllInstances(Type service)
        {
            return _container.GetAllInstances(service);
        }

        protected override void BuildUp(object instance)
        {
            _container.BuildUp(instance);
        }

        protected override async void OnStartup(object sender, StartupEventArgs e)
        {
            await DisplayRootViewForAsync<ShellViewModel>();
        }
    }
}
