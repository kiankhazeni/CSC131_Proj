using System;
using System.Drawing;
using System.Windows;
using UserInterface.ViewModels;
using Application = System.Windows.Application;
using FormsNotifyIcon = System.Windows.Forms.NotifyIcon;
using FormsContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using FormsToolTipIcon = System.Windows.Forms.ToolTipIcon;

namespace UserInterface
{
    public partial class MainWindow : Window
    {
        private FormsNotifyIcon? _notifyIcon;
        private bool _isExitRequested;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new FormsNotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "Vitals"
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

        private void ExitApplication()
        {
            _isExitRequested = true;

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            Application.Current.Shutdown();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isExitRequested)
            {
                e.Cancel = true;
                Hide();

                _notifyIcon?.ShowBalloonTip(
                    1500,
                    "Vitals",
                    "The app is still running in the system tray.",
                    FormsToolTipIcon.Info);

                return;
            }

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            base.OnClosing(e);
        }
    }
}