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
            _viewModel.ExtractImageRequested += ExtractImageFromWebView;
            
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

        private async System.Threading.Tasks.Task ExtractImageFromWebView(string imageUrl, string filePath)
        {
            try
            {
                // First try simple HttpClient approach
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    var userAgent = await WebView.ExecuteScriptAsync("navigator.userAgent");
                    userAgent = userAgent.Trim('"');
                    httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
                    
                    var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
                    
                    // Check if content is HTML (captcha page)
                    string content = System.Text.Encoding.UTF8.GetString(imageBytes.Take(100).ToArray());
                    if (content.Contains("<html>") || content.Contains("<HTML>"))
                    {
                        // Use WebView2 navigation for captcha pages
                        string currentUrl = WebView.Source?.ToString();
                        WebView.Source = new Uri(imageUrl);
                        await System.Threading.Tasks.Task.Delay(2000);
                        
                        string script = @"
                            var img = document.querySelector('img');
                            if (img && img.complete) {
                                var canvas = document.createElement('canvas');
                                canvas.width = img.naturalWidth || img.width;
                                canvas.height = img.naturalHeight || img.height;
                                var ctx = canvas.getContext('2d');
                                ctx.drawImage(img, 0, 0);
                                canvas.toDataURL().split(',')[1];
                            } else null;
                        ";
                        
                        string result = await WebView.ExecuteScriptAsync(script);
                        if (!string.IsNullOrEmpty(currentUrl))
                            WebView.Source = new Uri(currentUrl);
                        
                        if (!string.IsNullOrEmpty(result) && result != "null")
                        {
                            result = result.Trim('"');
                            imageBytes = Convert.FromBase64String(result);
                        }
                        else return;
                    }
                    
                    await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);
                }
            }
            catch { }
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