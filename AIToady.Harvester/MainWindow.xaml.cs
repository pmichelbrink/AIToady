using AIToady.Harvester.ViewModels;
using System;
using System.IO;
using System.Security.Policy;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

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
            
            LoadWindowSettings();

            _viewModel.NavigateRequested += url => WebView.Source = new Uri(url);
            _viewModel.ExecuteScriptRequested += async script => await WebView.ExecuteScriptAsync(script);
            _viewModel.ExtractImageRequested += ExtractImageFromWebView;
            _viewModel.ExtractAttachmentRequested += ExtractAttachmentFromWebView;


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
            
            Closing += MainWindow_Closing;
        }

        private void LoadWindowSettings()
        {
            Width = Properties.Settings.Default.WindowWidth;
            Height = Properties.Settings.Default.WindowHeight;
            Left = Properties.Settings.Default.WindowLeft;
            Top = Properties.Settings.Default.WindowTop;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_viewModel.IsHarvesting)
            {
                var result = MessageBox.Show("Harvesting is in progress. Are you sure you want to exit?", "Confirm Exit", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }
            
            Properties.Settings.Default.WindowWidth = Width;
            Properties.Settings.Default.WindowHeight = Height;
            Properties.Settings.Default.WindowLeft = Left;
            Properties.Settings.Default.WindowTop = Top;
            Properties.Settings.Default.Save();
            
            _viewModel.SaveSettings();
            _viewModel.Dispose();
        }

        private async Task ExtractAttachmentFromWebView(string attachmentUrl, string filePath)
        {
            try
            {
                string currentUrl = WebView.Source?.ToString();
                WebView.Source = new Uri(attachmentUrl);
                await Task.Delay(3000);
                
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

                do
                    await Task.Delay(500);
                while (currentUrl != WebView.Source?.ToString());

                if (!string.IsNullOrEmpty(result) && result != "null")
                {
                    result = result.Trim('"');
                    var attachmentBytes = Convert.FromBase64String(result);
                    await System.IO.File.WriteAllBytesAsync(filePath, attachmentBytes);
                }



                if (Path.GetExtension(filePath).ToLower().Contains("mov") ||
                    Path.GetExtension(filePath).ToLower().Contains("pdf"))
                {
                    // Check Downloads folder for the file
                    string downloadsPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                    string fileName = System.IO.Path.GetFileName(filePath);
                    string downloadedFile = System.IO.Path.Combine(downloadsPath, fileName);

                    if (System.IO.Directory.Exists(downloadedFile))
                    {
                        System.IO.File.Move(downloadedFile, filePath, true);
                    }
                }
            }
            catch { }
        }

        private async Task ExtractImageFromWebView(string imageUrl, string filePath)
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
                        await Task.Delay(2000);

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

                        do
                            await Task.Delay(500);
                        while (currentUrl != WebView.Source?.ToString());

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
            catch 
            {
                try
                {
                    string currentUrl = WebView.Source?.ToString();
                    WebView.Source = new Uri(imageUrl);
                    await Task.Delay(2000);

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

                    do
                        await Task.Delay(500);
                    while (currentUrl != WebView.Source?.ToString());

                    if (!string.IsNullOrEmpty(result) && result != "null")
                    {
                        result = result.Trim('"');
                        var imageBytes = Convert.FromBase64String(result);
                        await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);
                    }
                }
                catch { }
            }
        }

        private async void WebView_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            if (_viewModel.IsHarvesting)
                return;

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

        private void PageLoadDelayTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text, 0) || (sender as TextBox)?.Text == "0";
        }

        private void ThreadsToSkipTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text, 0);
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Folder Selection"
            };
            
            if (dialog.ShowDialog() == true)
            {
                _viewModel.RootFolder = System.IO.Path.GetDirectoryName(dialog.FileName);
            }
        }

        private void StartTimePickerButton_Click(object sender, RoutedEventArgs e)
        {
            ShowTimePicker(StartTimeTextBox, _viewModel.StartTime, time => _viewModel.StartTime = time);
        }

        private void EndTimePickerButton_Click(object sender, RoutedEventArgs e)
        {
            ShowTimePicker(EndTimeTextBox, _viewModel.EndTime, time => _viewModel.EndTime = time);
        }

        private void ShowTimePicker(TextBox textBox, string currentTime, Action<string> updateTime)
        {
            var popup = new System.Windows.Controls.Primitives.Popup
            {
                PlacementTarget = textBox,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                StaysOpen = false
            };

            var stackPanel = new StackPanel { Background = System.Windows.Media.Brushes.White, Margin = new Thickness(5) };
            var border = new Border { BorderBrush = System.Windows.Media.Brushes.Gray, BorderThickness = new Thickness(1), Child = stackPanel };

            var hourCombo = new ComboBox { Width = 50, Margin = new Thickness(2) };
            var minuteCombo = new ComboBox { Width = 50, Margin = new Thickness(2) };

            for (int i = 0; i < 24; i++) hourCombo.Items.Add(i.ToString("D2"));
            for (int i = 0; i < 60; i += 15) minuteCombo.Items.Add(i.ToString("D2"));

            if (TimeSpan.TryParse(currentTime, out var time))
            {
                hourCombo.SelectedItem = time.Hours.ToString("D2");
                minuteCombo.SelectedItem = (time.Minutes / 15 * 15).ToString("D2");
            }

            var okButton = new Button { Content = "OK", Width = 50, Margin = new Thickness(2) };
            okButton.Click += (s, e) => {
                updateTime($"{hourCombo.SelectedItem}:{minuteCombo.SelectedItem}");
                popup.IsOpen = false;
            };

            stackPanel.Children.Add(new Label { Content = "Hour:" });
            stackPanel.Children.Add(hourCombo);
            stackPanel.Children.Add(new Label { Content = "Minute:" });
            stackPanel.Children.Add(minuteCombo);
            stackPanel.Children.Add(okButton);

            popup.Child = border;
            popup.IsOpen = true;
        }

        private string _lastSortColumn = "";
        private bool _lastSortAscending = true;

        private void LogColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var columnName = button?.Tag?.ToString();
            
            var view = CollectionViewSource.GetDefaultView(_viewModel.LogEntries);
            view.SortDescriptions.Clear();
            
            bool ascending = _lastSortColumn != columnName || !_lastSortAscending;
            _lastSortColumn = columnName;
            _lastSortAscending = ascending;
            
            var direction = ascending ? System.ComponentModel.ListSortDirection.Ascending : System.ComponentModel.ListSortDirection.Descending;
            view.SortDescriptions.Add(new System.ComponentModel.SortDescription(columnName, direction));
        }
    }
}