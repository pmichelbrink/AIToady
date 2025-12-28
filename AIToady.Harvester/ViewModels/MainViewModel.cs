using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Wpf;
using AIToady.Harvester.Models;

namespace AIToady.Harvester.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private string _url = "akfiles.com";
        private string _nextElement = ".pageNav-jump--next";
        private string _threadElement = "";
        private string _pageLoadDelay = "6";
        private bool _isHarvesting = false;
        private int _currentThreadIndex = 0;
        private bool _isCapturingElement = false;
        private List<string> _threadLinks = new List<string>();

        public string Url
        {
            get => _url;
            set => SetProperty(ref _url, value);
        }

        public string NextElement
        {
            get => _nextElement;
            set => SetProperty(ref _nextElement, value);
        }

        public string ThreadElement
        {
            get => _threadElement;
            set => SetProperty(ref _threadElement, value);
        }

        public string PageLoadDelay
        {
            get => _pageLoadDelay;
            set => SetProperty(ref _pageLoadDelay, value);
        }

        public bool IsHarvesting
        {
            get => _isHarvesting;
            set => SetProperty(ref _isHarvesting, value);
        }

        public bool IsCapturingElement
        {
            get => _isCapturingElement;
            set => SetProperty(ref _isCapturingElement, value);
        }

        public List<string> ThreadLinks => _threadLinks;

        public ICommand GoCommand { get; }
        public ICommand NextCommand { get; }
        public ICommand LoadThreadsCommand { get; }
        public ICommand StartHarvestingCommand { get; }

        public event Action<string> NavigateRequested;
        public event Func<string, System.Threading.Tasks.Task<string>> ExecuteScriptRequested;
        public event Func<string, string, System.Threading.Tasks.Task> ExtractImageRequested;

        public MainViewModel()
        {
            GoCommand = new RelayCommand(ExecuteGo);
            NextCommand = new RelayCommand(ExecuteNext);
            LoadThreadsCommand = new RelayCommand(ExecuteLoadThreads);
            StartHarvestingCommand = new RelayCommand(ExecuteStartHarvesting, () => !_isHarvesting || _threadLinks.Count > 0);
        }

        private void ExecuteGo()
        {
            if (!string.IsNullOrEmpty(Url))
            {
                string url = Url.Trim();
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    url = "https://" + url;
                }
                NavigateRequested?.Invoke(url);
            }
        }

        private async void ExecuteNext()
        {
            if (!string.IsNullOrEmpty(NextElement))
            {
                await ExecuteScriptRequested?.Invoke($"document.querySelector('{NextElement}').click();");
            }
        }

        private async void ExecuteLoadThreads()
        {
            try
            {
                string className = ThreadElement.Trim();
                if (!string.IsNullOrEmpty(className))
                {
                    string script = $@"
                        let links = [];
                        let divs = document.querySelectorAll('.{className.TrimStart('.')}');
                        divs.forEach(div => {{
                            let anchors = div.querySelectorAll('a');
                            anchors.forEach(a => {{
                                if (a.href) links.push(a.href);
                            }});
                        }});
                        JSON.stringify(links);
                    ";
                    
                    string result = await ExecuteScriptRequested?.Invoke(script);
                    result = System.Text.Json.JsonSerializer.Deserialize<string>(result);
                    var links = System.Text.Json.JsonSerializer.Deserialize<string[]>(result);
                    _threadLinks.Clear();
                    _threadLinks.AddRange(links);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading threads: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ExecuteStartHarvesting()
        {
            if (_isHarvesting)
            {
                _isHarvesting = false;
                return;
            }

            if (_threadLinks.Count == 0)
            {
                MessageBox.Show("No threads loaded. Click 'Load Threads' first.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _isHarvesting = true;

            if (!int.TryParse(PageLoadDelay, out int delay))
                delay = 60;

            int totalThreads = 0;
            int totalMessages = 0;

            // Outer loop for forum pages
            bool hasNextForumPage = true;
            while (_isHarvesting && hasNextForumPage)
            {
                // Process all threads on current page
                for (int i = 0; i < _threadLinks.Count && _isHarvesting; i++)
                {
                    ++i;
                    //++i;
                    //++i;
                    //++i;
                    //++i;
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var thread = await HarvestThread(_threadLinks[i], delay, timestamp);
                    if (thread != null)
                    {
                        string safeThreadName = string.Join("_", thread.ThreadName.Split(System.IO.Path.GetInvalidFileNameChars()));
                        string threadFolder = $"{safeThreadName}_{timestamp}";
                        System.IO.Directory.CreateDirectory(threadFolder);
                        
                        string json = System.Text.Json.JsonSerializer.Serialize(thread, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        string fileName = System.IO.Path.Combine(threadFolder, "thread.json");
                        await System.IO.File.WriteAllTextAsync(fileName, json);
                        totalThreads++;
                        totalMessages += thread.Messages.Count;
                    }
                }

                NavigateRequested?.Invoke(Url);
                
                // Wait for navigation to complete by checking URL
                string currentUrl;
                do
                {
                    await System.Threading.Tasks.Task.Delay(500);
                    currentUrl = await ExecuteScriptRequested?.Invoke("window.location.href");
                    currentUrl = System.Text.Json.JsonSerializer.Deserialize<string>(currentUrl);
                } while (currentUrl != Url && _isHarvesting);

                // Check if Next element exists and click it
                string nextScript = $"document.querySelector('{NextElement}') ? 'found' : 'not_found'";
                string nextResult = await ExecuteScriptRequested?.Invoke(nextScript);
                nextResult = System.Text.Json.JsonSerializer.Deserialize<string>(nextResult);

                if (nextResult == "found")
                {
                    await ExecuteScriptRequested?.Invoke($"document.querySelector('{NextElement}').click();");
                    await System.Threading.Tasks.Task.Delay(delay * 1000);
                    Url = await ExecuteScriptRequested?.Invoke("window.location.href");
                    Url = System.Text.Json.JsonSerializer.Deserialize<string>(Url);
                    ExecuteLoadThreads();
                    await System.Threading.Tasks.Task.Delay(delay * 1000);
                }
                else
                {
                    hasNextForumPage = false;
                }
            }

            MessageBox.Show($"Harvesting complete. {totalThreads} threads with {totalMessages} messages saved as individual files", "Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            _isHarvesting = false;
        }

        private async Task<ForumThread> HarvestThread(string threadUrl, int delay, string timestamp)
        {
            NavigateRequested?.Invoke(threadUrl);
            await System.Threading.Tasks.Task.Delay(delay * 1000);
            
            var thread = new ForumThread();
            int globalImageCounter = 1;
            
            // Get thread name from page title
            string titleScript = "document.title";
            string titleResult = await ExecuteScriptRequested?.Invoke(titleScript);
            thread.ThreadName = System.Text.Json.JsonSerializer.Deserialize<string>(titleResult) ?? "Unknown Thread";
            
            string safeThreadName = string.Join("_", thread.ThreadName.Split(System.IO.Path.GetInvalidFileNameChars()));
            string threadFolder = $"{safeThreadName}_{timestamp}";
            string imagesFolder = System.IO.Path.Combine(threadFolder, "Images");
            
            // Loop through all pages in the thread
            bool hasNextPage = true;
            while (_isHarvesting && hasNextPage)
            {
                var pageMessages = await HarvestPage();
                
                // Extract images for each message
                foreach (var message in pageMessages)
                {
                    var imageNames = new List<string>();
                    for (int i = 0; i < message.Images.Count; i++)
                    {
                        try
                        {
                            string imageUrl = message.Images[i];
                            string fileName = System.IO.Path.GetFileName(new Uri(imageUrl).LocalPath);
                            if (string.IsNullOrEmpty(fileName) || !fileName.Contains("."))
                                fileName = $"image_{globalImageCounter}.jpg";
                            
                            if (!System.IO.Directory.Exists(imagesFolder))
                                System.IO.Directory.CreateDirectory(imagesFolder);
                            
                            string imagePath = System.IO.Path.Combine(imagesFolder, fileName);
                            await ExtractImageRequested?.Invoke(imageUrl, imagePath);
                            imageNames.Add(fileName);
                            globalImageCounter++;
                        }
                        catch { }
                    }
                    message.Images = imageNames;
                }
                
                thread.Messages.AddRange(pageMessages);
                
                // Check if NextElement exists and click it
                string nextScript = $"document.querySelector('{NextElement}') ? 'found' : 'not_found'";
                string nextResult = await ExecuteScriptRequested?.Invoke(nextScript);
                nextResult = System.Text.Json.JsonSerializer.Deserialize<string>(nextResult);
                
                if (nextResult == "found")
                {
                    await ExecuteScriptRequested?.Invoke($"document.querySelector('{NextElement}').click();");
                    await System.Threading.Tasks.Task.Delay(delay * 1000);
                }
                else
                {
                    hasNextPage = false;
                }
            }
            
            return thread;
        }

        private async Task<List<ForumMessage>> HarvestPage()
        {
            string extractScript = @"
                let messages = [];
                document.querySelectorAll('.message-inner').forEach(messageDiv => {
                    let userElement = messageDiv.querySelector('.message-name a');
                    let messageBodyElement = messageDiv.querySelector('.message-body');
                    let timeElement = messageDiv.querySelector('.u-dt');
                    let images = [];
                    
                    if (messageBodyElement) {
                        messageBodyElement.querySelectorAll('img.bbImage').forEach(img => {
                            let imageUrl = img.getAttribute('data-url') || img.src;
                            if (imageUrl && !imageUrl.includes('data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP')) {
                                images.push(imageUrl);
                            }
                        });
                    }
                    
                    if (userElement && messageBodyElement) {
                        let postId = '';
                        let currentElement = messageDiv;
                        while (currentElement && !postId) {
                            postId = currentElement.getAttribute('data-lb-id') || currentElement.getAttribute('id') || '';
                            currentElement = currentElement.parentElement;
                        }
                        if (postId.startsWith('js-')) {
                            postId = postId.substring(3);
                        }
                        messages.push({
                            postId: postId,
                            username: userElement.textContent.trim(),
                            message: messageBodyElement.textContent.trim().replace(/\s+/g, ' '),
                            timestamp: timeElement ? timeElement.getAttribute('datetime') : '',
                            images: images
                        });
                    }
                });
                JSON.stringify(messages);
            ";
            
            string result = await ExecuteScriptRequested?.Invoke(extractScript);
            if (!string.IsNullOrEmpty(result))
            {
                try
                {
                    result = System.Text.Json.JsonSerializer.Deserialize<string>(result);
                    return System.Text.Json.JsonSerializer.Deserialize<List<ForumMessage>>(result) ?? new List<ForumMessage>();
                }
                catch
                {
                    return new List<ForumMessage>();
                }
            }
            
            return new List<ForumMessage>();
        }

        public void HandleElementCapture(string result, bool isThreadElement)
        {
            if (isThreadElement && result.Contains("."))
            {
                string[] parts = result.Split('.');
                result = parts[parts.Length - 1];
                ThreadElement = result;
            }
            else if (!isThreadElement)
            {
                NextElement = result;
            }
        }
    }
}