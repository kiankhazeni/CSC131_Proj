using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;

namespace UserInterface.Views
{
    public partial class AuthPromptWindow : Window
    {
        private readonly string _url;
        private readonly string _code;

        public AuthPromptWindow(string moduleName, string url, string code)
        {
            InitializeComponent();
            _url = string.IsNullOrWhiteSpace(url) ? "https://login.microsoft.com/device" : url;
            _code = code ?? string.Empty;

            ModuleTextBlock.Inlines.Clear();

            if (string.IsNullOrWhiteSpace(moduleName))
            {
                ModuleTextBlock.Inlines.Add("A module needs permission to access Microsoft services");
            }
            else
            {
                ModuleTextBlock.Inlines.Add("For ");
                ModuleTextBlock.Inlines.Add(new System.Windows.Documents.Run(moduleName) {FontWeight = FontWeights.SemiBold});
                // ModuleTextBlock.Inlines.Add(" needs permission to access Microsoft services");
            }

            CodeTextBox.Text = string.IsNullOrWhiteSpace(_code) ? "See module log" : _code;
            UrlTextBlock.Text = _url;
        }

        private void CopyCode_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_code))
                Clipboard.SetText(_code);
        }

        private void OpenLink_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _url,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore; URL is visible in dialog
            }
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
