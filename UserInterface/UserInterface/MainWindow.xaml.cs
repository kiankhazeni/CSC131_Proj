using System;
using System.Drawing;
using System.IO;
using System.Windows;
using UserInterface.ViewModels;
using Application = System.Windows.Application;
using FormsNotifyIcon = System.Windows.Forms.NotifyIcon;
using FormsContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using FormsScreen = System.Windows.Forms.Screen;
using System.Windows.Interop;

namespace UserInterface
{
    public partial class MainWindow : Window
    {
        private FormsNotifyIcon? _notifyIcon;
        private bool _isExitRequested;
        private bool _isPseudoMaximized;
        private Rect _restoreBounds;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            InitializeTrayIcon();
            SourceInitialized += (_, _) => UpdateMaximizedBounds();
            StateChanged += (_, _) =>
            {
                if (WindowState == WindowState.Maximized)
                {
                    WindowState = WindowState.Normal;
                    PseudoMaximizeToWorkingArea();
                }
            };
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isPseudoMaximized)
                RestoreFromPseudoMaximize();
            else
                PseudoMaximizeToWorkingArea();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ExitMenuButton_Click(object sender, RoutedEventArgs e)
        {
            ExitApplication();
        }

        private void InitializeTrayIcon()
        {
            var iconPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "resources/assets",
                "vitals_icon.ico");

            _notifyIcon = new FormsNotifyIcon
            {
                Icon = File.Exists(iconPath)
                    ? new Icon(iconPath)
                    : SystemIcons.Application,
                Visible = true,
                Text = "Vitals | CPR Lifeline"
            };

            _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;

            var contextMenu = new FormsContextMenuStrip();
            contextMenu.Items.Add("Open", null, (_, _) => ShowMainWindow());
            contextMenu.Items.Add("Exit", null, (_, _) => ExitApplication());

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void NotifyIcon_DoubleClick(object? sender, EventArgs e)
        {
            ShowMainWindow();
        }

        private void ShowMainWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void PseudoMaximizeToWorkingArea()
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;

            _restoreBounds = new Rect(Left, Top, Width, Height);

            Rect workingArea = GetCurrentWorkingAreaInDips();
            Left = workingArea.Left;
            Top = workingArea.Top;
            Width = Math.Max(MinWidth, workingArea.Width);
            Height = Math.Max(MinHeight, workingArea.Height);
            _isPseudoMaximized = true;
        }

        private void RestoreFromPseudoMaximize()
        {
            Left = _restoreBounds.Left;
            Top = _restoreBounds.Top;
            Width = _restoreBounds.Width;
            Height = _restoreBounds.Height;
            _isPseudoMaximized = false;
        }

        private FormsScreen GetCurrentScreen()
        {
            var handle = new WindowInteropHelper(this).Handle;
            return handle == IntPtr.Zero
                ? FormsScreen.PrimaryScreen
                : FormsScreen.FromHandle(handle);
        }

        private Rect GetCurrentWorkingAreaInDips()
        {
            try
            {
                var screen = GetCurrentScreen();
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    var transform = source.CompositionTarget.TransformFromDevice;
                    var topLeft = transform.Transform(new System.Windows.Point(screen.WorkingArea.Left, screen.WorkingArea.Top));
                    var bottomRight = transform.Transform(new System.Windows.Point(screen.WorkingArea.Right, screen.WorkingArea.Bottom));
                    return new Rect(topLeft, bottomRight);
                }
            }
            catch
            {
                // Next
            }

            return SystemParameters.WorkArea;
        }

        private void UpdateMaximizedBounds()
        {
            Rect workingArea = GetCurrentWorkingAreaInDips();
            MaxWidth = workingArea.Width;
            MaxHeight = workingArea.Height;
        }

        private void ExitApplication()
        {
            _isExitRequested = true;
            DisposeViewModel();

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            Application.Current.Shutdown();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isExitRequested)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            DisposeViewModel();

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            base.OnClosing(e);
        }

        private void DisposeViewModel()
        {
            if (DataContext is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
