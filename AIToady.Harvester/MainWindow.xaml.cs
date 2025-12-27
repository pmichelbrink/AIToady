using System;
using System.Windows;
using System.Windows.Input;
using AIToady.Harvester.ViewModels;

namespace AIToady.Harvester
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private bool _isCapturingElement = false;
        private bool _isThreadElementCapture = false;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            
            _viewModel.NavigateRequested += url => WebView.Source = new Uri(url);
            _viewModel.ExecuteScriptRequested += async script => await WebView.ExecuteScriptAsync(script);
            
            WebView.NavigationCompleted += WebView_NavigationCompleted;
            WebView.CoreWebView2InitializationCompleted += (s, e) => {
                WebView.CoreWebView2.WebMessageReceived += (sender, args) => {
                    if (_isCapturingElement) {
                        string result = args.TryGetWebMessageAsString();
                        _viewModel.HandleElementCapture(result, _isThreadElementCapture);
                        _isCapturingElement = false;
                    }
                };
            };
        }

        private async void WebView_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!_viewModel.IsHarvesting)
                _viewModel.Url = WebView.Source?.ToString() ?? string.Empty;
            
            await WebView.ExecuteScriptAsync(@"
                function getSelector(element) {
                    if (element.id) {
                        return '#' + element.id;
                    }
                    let path = [];
                    let current = element;
                    while (current && current.nodeType === 1) {
                        let selector = current.tagName.toLowerCase();
                        if (current.className) {
                            selector += '.' + current.className.trim().replace(/\s+/g, '.');
                        }
                        path.unshift(selector);
                        current = current.parentElement;
                        if (current && current.id) {
                            path.unshift('#' + current.id);
                            break;
                        }
                    }
                    return path.join(' > ');
                }
                
                document.addEventListener('click', function(event) {
                    if (window.capturingElement) {
                        event.preventDefault();
                        event.stopPropagation();
                        let result = getSelector(event.target.parentElement);
                        window.chrome.webview.postMessage(result);
                        window.capturingElement = false;
                    }
                }, true);
            ");
        }

        private void NextElementTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _isCapturingElement = true;
            _isThreadElementCapture = false;
            WebView.ExecuteScriptAsync("window.capturingElement = true;");
        }

        private void NextElementTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            _isCapturingElement = false;
            WebView.ExecuteScriptAsync("window.capturingElement = false;");
        }

        private void ThreadElementTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _isCapturingElement = true;
            _isThreadElementCapture = true;
            WebView.ExecuteScriptAsync("window.capturingElement = true;");
        }

        private void ThreadElementTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            _isCapturingElement = false;
            WebView.ExecuteScriptAsync("window.capturingElement = false;");
        }

        private void UrlTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _viewModel.GoCommand.Execute(null);
            }
        }
    }
}