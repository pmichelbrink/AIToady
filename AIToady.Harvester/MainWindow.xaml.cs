using AIToady.Harvester.ViewModels;
using AIToady.Infrastructure;
using System;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace AIToady.Harvester
{
    public enum ViewModelType
    {
        TheAKForum,
        AR15
    }

    public partial class MainWindow : Window
    {
        private BaseViewModel _viewModel;
        private bool _isCapturingElement = false;
        private bool _isThreadElementCapture = false;
        private bool _isMessageElementCapture = false;
        private HashSet<string> _domains409 = new HashSet<string>();

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
            _viewModel.ViewModelSwitchRequested += viewModelType => SwitchViewModel(viewModelType);
            _viewModel.PromptUserInputRequested += PromptUserForInput;
            _viewModel.ClearCacheRequested += async () => await WebView.CoreWebView2?.Profile.ClearBrowsingDataAsync();
            _viewModel.SetDownloadFolderRequested += SetDownloadFolder;
            _viewModel.PropertyChanged += (s, e) => { if (e.PropertyName == "DarkMode") ApplyTheme(); };


            WebView.NavigationCompleted += WebView_NavigationCompleted;
            WebView.CoreWebView2InitializationCompleted += async (s, e) => {
                //await WebView.CoreWebView2.Profile.ClearBrowsingDataAsync();

                WebView.CoreWebView2.ProcessFailed += (sender, args) => {
                    Dispatcher.Invoke(() => WebView.Reload());
                };

                WebView.CoreWebView2.ScriptDialogOpening += async (sender, args) => {
                    await Task.Delay(5000);
                    _viewModel.AddLogEntry($"Clicking OK to this alert message: {args.Message}.");
                    args.Accept();
                };

                WebView.CoreWebView2.WebMessageReceived += (sender, args) => {
                    if (_isCapturingElement) {
                        string result = args.TryGetWebMessageAsString();
                        if (_isThreadElementCapture)
                            _viewModel.ThreadElement = result;
                        else if (_isMessageElementCapture)
                            _viewModel.MessageElement = result;
                        else
                            _viewModel.NextElement = result;
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

            // Set password box value after loading settings
            EmailPasswordBox.Password = _viewModel.EmailPassword;

            // Apply initial theme
            Loaded += (s, e) => ApplyTheme();
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

                if (File.Exists(filePath))
                {
                    if (!string.IsNullOrEmpty(currentUrl))
                        await WaitForNavigation(currentUrl);

                    return;
                }

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
                    await WaitForNavigation(currentUrl);

                if (!string.IsNullOrEmpty(result) && result != "null")
                {
                    result = result.Trim('"');
                    var attachmentBytes = Convert.FromBase64String(result);
                    await File.WriteAllBytesAsync(filePath, attachmentBytes);
                }

                if (!ConsolidateDuplicateDownloads(filePath))
                    CheckDownloadsFolderForFile(filePath);
            }
            catch { }
        }

        private void CheckDownloadsFolderForFile(string filePath)
        {
            try
            {
                string downloadsPath = WebView.CoreWebView2.Profile.DefaultDownloadFolderPath;
                string fileName = Path.GetFileName(filePath);
                string downloadedFile = Path.Combine(downloadsPath, fileName);

                if (File.Exists(downloadedFile) && !downloadedFile.Equals(filePath))
                {
                    File.Move(downloadedFile, filePath, true);
                }
                else
                {
                    string normalizedFileName = Path.GetFileNameWithoutExtension(fileName).Replace(" ", ".").Replace("-", ".").ToLower() + Path.GetExtension(fileName).ToLower();
                    string normalizedDownloadedFile = Path.Combine(downloadsPath, normalizedFileName);
                    if (normalizedDownloadedFile.Equals(filePath))
                        return;

                    if (!normalizedDownloadedFile.Equals(filePath) &&  File.Exists(normalizedDownloadedFile))
                    {
                        File.Move(normalizedDownloadedFile, filePath, true);
                    }
                    else
                    {
                        var recentFiles = Directory.GetFiles(downloadsPath)
                            .Where(f => File.GetCreationTime(f) > DateTime.Now.AddMinutes(-1))
                            .OrderByDescending(f => File.GetCreationTime(f));

                        var prefix = Path.GetFileNameWithoutExtension(fileName).Substring(0, Math.Min(2, Path.GetFileNameWithoutExtension(fileName).Length));
                        var matchingFile = recentFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

                        if (matchingFile != null)
                        {
                            File.Move(matchingFile, filePath, true);
                            _viewModel.AddLogEntry($"Found attachment in Downloads folder: {matchingFile} and moved to target location: {filePath}.");
                        }
                        else if (recentFiles.Any())
                        {
                            _viewModel.AddLogEntry($"CheckDownloadsFolderForFile found {recentFiles.Count()} file but didn't any it. This this the first file found: {recentFiles.First()}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _viewModel.AddLogEntry($"****** Error checking Downloads folder for file: {ex.Message}");
            }
        }

        private bool ConsolidateDuplicateDownloads(string filePath)
        {
            try
            {
                if (!filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(filePath) ||
                    new FileInfo(filePath).Length > 10240)
                    return false;

                //await Task.Delay(1000);

                var files = Directory.GetFiles(WebView.CoreWebView2.Profile.DefaultDownloadFolderPath, "*.pdf", SearchOption.TopDirectoryOnly)
                    .Select(f => new { Path = f, Info = new FileInfo(f) })
                    .OrderBy(f => f.Info.CreationTime)
                    .ToList();

                for (int i = 0; i < files.Count - 1; i++)
                {
                    for (int j = i + 1; j < files.Count; j++)
                    {
                        var file1 = files[i];
                        var file2 = files[j];

                        if ((file2.Info.CreationTime - file1.Info.CreationTime).TotalSeconds <= 5 &&
                            file1.Info.Extension.Equals(file2.Info.Extension, StringComparison.OrdinalIgnoreCase) &&
                            char.ToLower(Path.GetFileNameWithoutExtension(file1.Path)[0]) == char.ToLower(Path.GetFileNameWithoutExtension(file2.Path)[0]))
                        {
                            var smaller = file1.Info.Length < file2.Info.Length ? file1 : file2;
                            var larger = file1.Info.Length >= file2.Info.Length ? file1 : file2;

                            File.Copy(larger.Path, filePath, true);
                            File.Delete(larger.Path);
                            _viewModel.AddLogEntry($"Consolidated duplicate downloads: kept {Path.GetFileName(smaller.Path)}, removed {Path.GetFileName(larger.Path)}");
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _viewModel.AddLogEntry($"ConsolidateDuplicateDownloads: {ex.Message}");
                return false;
            }
        }

        private async Task<string> ExtractFlickrAlbum(string albumUrl, string filePath)
        {
            try
            {
                string currentUrl = WebView.Source?.ToString();
                WebView.Source = new Uri(albumUrl);
                await Task.Delay(3000);

                string script = @"
                    (function() {
                        var imageUrls = [];
                        var imgs = document.querySelectorAll('img[src*=""live.staticflickr.com""]');
                        imgs.forEach(img => {
                            var src = img.src;
                            if (src && !imageUrls.includes(src)) {
                                src = src.replace('_m.jpg', '_b.jpg').replace('_n.jpg', '_b.jpg').replace('_z.jpg', '_b.jpg');
                                imageUrls.push(src);
                            }
                        });
                        return JSON.stringify(imageUrls);
                    })();
                ";

                string result = await WebView.ExecuteScriptAsync(script);
                if (!string.IsNullOrEmpty(currentUrl))
                    await WaitForNavigation(currentUrl);

                if (!string.IsNullOrEmpty(result) && result != "null")
                {
                    result = result.Trim('"').Replace("\\\"", "\"");
                    var imageUrls = System.Text.Json.JsonSerializer.Deserialize<string[]>(result);
                    
                    if (imageUrls != null && imageUrls.Length > 0)
                    {
                        string directory = Path.GetDirectoryName(filePath);
                        string baseFileName = Path.GetFileNameWithoutExtension(filePath);
                        
                        for (int i = 0; i < imageUrls.Length; i++)
                        {
                            string imageUrl = imageUrls[i];
                            string extension = Path.GetExtension(imageUrl).Split('?')[0];
                            string newFilePath = Path.Combine(directory, $"{baseFileName}_{i + 1}{extension}");
                            
                            using (var httpClient = new System.Net.Http.HttpClient())
                            {
                                var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
                                await File.WriteAllBytesAsync(newFilePath, imageBytes);
                            }
                        }
                        return "success";
                    }
                }
            }
            catch { }
            return "404";
        }

        private async Task WaitForNavigation(string targetUrl, int maxRetries = 5)
        {
            WebView.Source = new Uri(targetUrl);
            await Task.Delay(500);

            int retries = 0;
            while (WebView.Source?.ToString() != targetUrl)
            {
                await Task.Delay(500);
                if (++retries >= maxRetries)
                {
                    WebView.Source = new Uri(targetUrl);
                    retries = 0;
                }
            }
        }

        private async Task<string> ExtractImageFromWebView(string imageUrl, string filePath)
        {
            try
            {
                // Handle Flickr album links
                if (imageUrl.Contains("flickr.com") && imageUrl.Contains("/in/set-"))
                    return await ExtractFlickrAlbum(imageUrl, filePath);

                // Skip HttpClient for domains that previously returned 409
                if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) && _domains409.Contains(uri.Host))
                {
                    string result = await ExtractImageWithBrowser(imageUrl, filePath);
                    if (result == "success")
                        return result;
                }

                // First try simple HttpClient approach
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    var userAgent = await WebView.ExecuteScriptAsync("navigator.userAgent");
                    userAgent = userAgent.Trim('"');
                    httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
                    
                    // Add Referer header to mimic browser behavior
                    string currentUrl = WebView.Source?.ToString();
                    if (!string.IsNullOrEmpty(currentUrl))
                        httpClient.DefaultRequestHeaders.Add("Referer", currentUrl);
                    
                    // Add other common browser headers
                    httpClient.DefaultRequestHeaders.Add("Accept", "image/webp,image/apng,image/*,*/*;q=0.8");
                    httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
                    httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

                    var response = await httpClient.GetAsync(imageUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        int statusCode = (int)response.StatusCode;
                        if (statusCode == 409 && Uri.TryCreate(imageUrl, UriKind.Absolute, out var errorUri))
                        {
                            _domains409.Add(errorUri.Host);
                            string result = await ExtractImageWithBrowser(imageUrl, filePath);
                            if (result == "success")
                                return result;
                        }

                        throw new System.Net.Http.HttpRequestException($"HTTP {statusCode}");
                    }
                    var imageBytes = await response.Content.ReadAsByteArrayAsync();

                    // Check if content is HTML (captcha page)
                    string content = System.Text.Encoding.UTF8.GetString(imageBytes.Take(100).ToArray());
                    if (content.Contains("<html>") || content.Contains("<HTML>"))
                    {
                        // Use WebView2 navigation for captcha pages
                        currentUrl = WebView.Source?.ToString();
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
                            await WaitForNavigation(currentUrl);

                        if (!string.IsNullOrEmpty(result) && result != "null")
                        {
                            result = result.Trim('"');
                            imageBytes = Convert.FromBase64String(result);
                        }
                        else return "404";
                    }

                    await File.WriteAllBytesAsync(filePath, imageBytes);
                    return "success";
                }
            }
            catch (Exception ex) 
            {
                try
                {
                    return await ExtractImageWithBrowser(imageUrl, filePath);
                }
                catch (Exception ex2)
                { 
                    return ex2.Message; 
                }
            }
        }

        private async Task<string> ExtractImageWithBrowser(string imageUrl, string filePath)
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
            {
                WebView.Source = new Uri(currentUrl);
                await WaitForNavigation(currentUrl);
            }

            if (!string.IsNullOrEmpty(result) && result != "null")
            {
                result = result.Trim('"');
                var imageBytes = Convert.FromBase64String(result);
                await File.WriteAllBytesAsync(filePath, imageBytes);
                return "success";
            }
            return "ExtractImageWithBrowser Failed";
        }

        private async void WebView_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            if (_viewModel.IsHarvesting)
                return;

            var newUrl = WebView.Source?.ToString() ?? string.Empty;
            if (Utilities.IsValidForumUrl(newUrl))
                _viewModel.Url = newUrl;
            
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
            _isThreadElementCapture = false;
            WebView.ExecuteScriptAsync("window.capturingElement = false;");
        }

        private void MessageElementTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _isCapturingElement = true;
            _isMessageElementCapture = true;
            WebView.ExecuteScriptAsync("window.capturingElement = true;");
        }

        private void MessageElementTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            _isCapturingElement = false;
            _isMessageElementCapture = false;
            WebView.ExecuteScriptAsync("window.capturingElement = false;");
        }

        private void UrlTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _viewModel.GoCommand.Execute(null);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            WebView.CoreWebView2?.ExecuteScriptAsync("window.history.back();");
        }

        private void PageLoadDelayTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text, 0) || (sender as TextBox)?.Text == "0";
        }

        private void ThreadsToSkipTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text, 0);
        }

        private void MessagesPerPageTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text, 0) || (sender as TextBox)?.Text == "0";
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

        private void HarvestSinceDatePickerButton_Click(object sender, RoutedEventArgs e)
        {
            var calendar = new Calendar
            {
                SelectedDate = _viewModel.HarvestSince,
                DisplayDate = _viewModel.HarvestSince ?? DateTime.Now
            };

            var popup = new System.Windows.Controls.Primitives.Popup
            {
                PlacementTarget = HarvestSinceTextBox,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                StaysOpen = false,
                Child = calendar
            };

            calendar.SelectedDatesChanged += (s, args) =>
            {
                _viewModel.HarvestSince = calendar.SelectedDate;
                popup.IsOpen = false;
            };

            popup.IsOpen = true;
        }

        private void ClearHarvestSinceButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.HarvestSince = null;
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

        private Task<string> PromptUserForInput(string prompt)
        {
            var inputDialog = new Window
            {
                Title = "Input Required",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var stackPanel = new StackPanel { Margin = new Thickness(10) };
            var label = new Label { Content = prompt };
            var textBox = new TextBox { Margin = new Thickness(0, 5, 0, 10), Text = _viewModel.Category };
            var button = new Button { Content = "OK", Width = 75, HorizontalAlignment = HorizontalAlignment.Right };

            button.Click += (s, e) => inputDialog.DialogResult = true;
            textBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) inputDialog.DialogResult = true; };

            stackPanel.Children.Add(label);
            stackPanel.Children.Add(textBox);
            stackPanel.Children.Add(button);
            inputDialog.Content = stackPanel;

            textBox.Focus();
            return Task.FromResult(inputDialog.ShowDialog() == true ? textBox.Text : string.Empty);
        }

        private string _lastSortColumn = "";
        private bool _lastSortAscending = true;

        private void SwitchViewModel(ViewModelType viewModelType)
        {
            BaseViewModel newViewModel;
            if (viewModelType == ViewModelType.TheAKForum)
                newViewModel = _viewModel.CloneToViewModel<TheAKForumViewModel>();
            else
                newViewModel = _viewModel.CloneToViewModel<AR15ViewModel>();
            
            _viewModel.Dispose();
            _viewModel = newViewModel;
            DataContext = _viewModel;
            
            _viewModel.NavigateRequested += url => WebView.Source = new Uri(url);
            _viewModel.ExecuteScriptRequested += async script => await WebView.ExecuteScriptAsync(script);
            _viewModel.ExtractImageRequested += ExtractImageFromWebView;
            _viewModel.ExtractAttachmentRequested += ExtractAttachmentFromWebView;
            _viewModel.ViewModelSwitchRequested += viewModelType => SwitchViewModel(viewModelType);
            _viewModel.PromptUserInputRequested += PromptUserForInput;
            _viewModel.ClearCacheRequested += async () => await WebView.CoreWebView2?.Profile.ClearBrowsingDataAsync();
            _viewModel.SetDownloadFolderRequested += SetDownloadFolder;
            
            _viewModel.ExecuteGo();
        }

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

        private void EmailPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            _viewModel.EmailPassword = passwordBox?.Password ?? "";
        }

        private void ScheduleForumsButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new ScheduleForumsWindow(_viewModel.ScheduledForums, _viewModel.DarkMode)
            {
                Owner = this
            };
            window.ShowDialog();
        }

        private async void TestEmailButton_Click(object sender, RoutedEventArgs e)
        {
            var success = await _viewModel.TestEmail();
            MessageBox.Show(this, success ? "Test email sent successfully!" : "Failed to send test email. Check your credentials.",
                "Email Test", MessageBoxButton.OK, success ? MessageBoxImage.Information : MessageBoxImage.Error);
        }

        private void ApplyTheme()
        {
            var bg = _viewModel.DarkMode ? "#1E1E1E" : "#FFFFFF";
            var fg = _viewModel.DarkMode ? "#FFFFFF" : "#000000";
            var controlBg = _viewModel.DarkMode ? "#2D2D30" : "#FFFFFF";
            var borderBrush = _viewModel.DarkMode ? "#3F3F46" : "#ABADB3";
            
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom(bg);
            Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom(fg);
            
            LogListView.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom(controlBg);
            LogListView.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom(fg);
            LogListView.BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom(borderBrush);
            
            if (_viewModel.DarkMode)
                SetWindowChromeDark();
            else
                SetWindowChromeLight();
            
            foreach (var element in FindVisualChildren<Control>(this))
            {
                if (element is TextBox || element is ComboBox)
                {
                    element.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom(controlBg);
                    element.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom(fg);
                    element.BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom(borderBrush);
                }
                else if (element is Label || element is CheckBox || element is GroupBox)
                {
                    element.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom(fg);
                }
            }
            
            foreach (var header in FindVisualChildren<GridViewColumnHeader>(this))
            {
                header.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom(controlBg);
                header.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom(fg);
            }
            
            foreach (var button in FindVisualChildren<Button>(LogListView))
            {
                button.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom(fg);
            }
        }

        private void SetDownloadFolder(string folder)
        {
            if (WebView.CoreWebView2 != null && !string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
                WebView.CoreWebView2.Profile.DefaultDownloadFolderPath = folder;
            }
        }
        
        private void SetWindowChromeDark()
        {
            if (System.Environment.OSVersion.Version.Build >= 22000)
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int value = 1;
                DwmSetWindowAttribute(hwnd, 20, ref value, sizeof(int));
            }
        }
        
        private void SetWindowChromeLight()
        {
            if (System.Environment.OSVersion.Version.Build >= 22000)
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int value = 0;
                DwmSetWindowAttribute(hwnd, 20, ref value, sizeof(int));
            }
        }
        
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                    if (child is T t) yield return t;
                    foreach (var childOfChild in FindVisualChildren<T>(child)) yield return childOfChild;
                }
            }
        }
    }
}