using AIToady.Harvester.Models;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace AIToady.Harvester.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private string _url = "akfiles.com";
        private string _nextElement = ".pageNav-jump--next";
        private string _threadElement = "";
        private int _pageLoadDelay = 6;
        private bool _isHarvesting = false;
        private int _currentThreadIndex = 0;
        private bool _isCapturingElement = false;
        private List<string> _threadLinks = new List<string>();
        int _threadImageCounter = 1;
        string _forumName;
        private string _threadName;
        string _siteName = "The AK Files";
        private Random _random = new Random();
        private string _rootFolder = GetDriveWithMostFreeSpace();
        
        private static string GetDriveWithMostFreeSpace()
        {
            return DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .OrderByDescending(d => d.AvailableFreeSpace)
                .FirstOrDefault()?.Name ?? "C:\\";
        }
        private int GetRandomizedDelay()
        {
            int delay = _random.Next(PageLoadDelay, PageLoadDelay * 3 + 1) * 1000;
            return delay;
        }
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

        public int PageLoadDelay
        {
            get => _pageLoadDelay;
            set => SetProperty(ref _pageLoadDelay, value);
        }

        public string RootFolder
        {
            get => _rootFolder;
            set => SetProperty(ref _rootFolder, value);
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
        public event Func<string, Task<string>> ExecuteScriptRequested;
        public event Func<string, string, Task> ExtractImageRequested;

        public MainViewModel()
        {
            LoadSettings();
            GoCommand = new RelayCommand(ExecuteGo);
            NextCommand = new RelayCommand(ExecuteNext);
            LoadThreadsCommand = new RelayCommand(ExecuteLoadThreads);
            StartHarvestingCommand = new RelayCommand(ExecuteStartHarvesting, () => !_isHarvesting || _threadLinks.Count > 0);
        }

        private void LoadSettings()
        {
            Url = string.IsNullOrEmpty(Properties.Settings.Default.Url) ? "akfiles.com" : Properties.Settings.Default.Url;
            NextElement = string.IsNullOrEmpty(Properties.Settings.Default.NextElement) ? ".pageNav-jump--next" : Properties.Settings.Default.NextElement;
            ThreadElement = Properties.Settings.Default.ThreadElement;
            PageLoadDelay = Properties.Settings.Default.PageLoadDelay == 0 ? 6 : Properties.Settings.Default.PageLoadDelay;
            RootFolder = string.IsNullOrEmpty(Properties.Settings.Default.RootFolder) ? GetDriveWithMostFreeSpace() : Properties.Settings.Default.RootFolder;
        }

        public void SaveSettings()
        {
            Properties.Settings.Default.Url = Url;
            Properties.Settings.Default.NextElement = NextElement;
            Properties.Settings.Default.ThreadElement = ThreadElement;
            Properties.Settings.Default.PageLoadDelay = PageLoadDelay;
            Properties.Settings.Default.RootFolder = RootFolder;
            Properties.Settings.Default.Save();
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
                    result = JsonSerializer.Deserialize<string>(result);
                    var links = JsonSerializer.Deserialize<string[]>(result);
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

            // Get forum name from h1.p-title-value element
            string forumScript = "document.querySelector('h1.p-title-value')?.textContent?.trim() || 'Unknown Forum'";
            string forumResult = await ExecuteScriptRequested?.Invoke(forumScript);
            _forumName = JsonSerializer.Deserialize<string>(forumResult) ?? "Unknown Forum";
            _forumName = string.Join("_", _forumName.Split(System.IO.Path.GetInvalidFileNameChars()));

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
                    var thread = await HarvestThread(_threadLinks[i]);
                    await WriteThreadInfo(thread);
                }

                NavigateRequested?.Invoke(Url);

                await LoadForumPage();

                // Check if Next element exists and click it, if it 
                // doesn't exist, we are on the last page
                string nextScript = $"document.querySelector('{NextElement}') ? 'found' : 'not_found'";
                string nextResult = await ExecuteScriptRequested?.Invoke(nextScript);
                nextResult = JsonSerializer.Deserialize<string>(nextResult);

                if (nextResult == "found")
                {
                    //Load the next page by clicking the Next element
                    await ExecuteScriptRequested?.Invoke($"document.querySelector('{NextElement}').click();");

                    //Wait for navigation to complete
                    await Task.Delay(GetRandomizedDelay());

                    Url = await ExecuteScriptRequested?.Invoke("window.location.href");
                    Url = JsonSerializer.Deserialize<string>(Url);

                    ExecuteLoadThreads();
                }
                else
                {
                    hasNextForumPage = false;
                }
            }

            MessageBox.Show($"Harvesting complete.", "Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            _isHarvesting = false;
        }

        private async Task WriteThreadInfo(ForumThread thread)
        {
            if (thread != null)
            {
                string threadFolder = System.IO.Path.Combine(_rootFolder, _siteName, _forumName, _threadName);
                System.IO.Directory.CreateDirectory(threadFolder);

                string json = JsonSerializer.Serialize(thread, new JsonSerializerOptions { WriteIndented = true });
                string fileName = System.IO.Path.Combine(threadFolder, "thread.json");
                await System.IO.File.WriteAllTextAsync(fileName, json);
            }
        }

        private async Task LoadForumPage()
        {
            //Load the forum page (a thread page is currently loaded) and
            //wait for navigation to complete by checking URL
            string currentUrl;
            do
            {
                await Task.Delay(500);
                currentUrl = await ExecuteScriptRequested?.Invoke("window.location.href");
                currentUrl = JsonSerializer.Deserialize<string>(currentUrl);
            } while (currentUrl != Url && _isHarvesting);
        }

        private async Task<ForumThread> HarvestThread(string threadUrl)
        {
            _threadImageCounter = 1;
            NavigateRequested?.Invoke(threadUrl);
            await Task.Delay(GetRandomizedDelay());
            
            var thread = new ForumThread();
            
            // Get thread name from page title
            string titleScript = "document.title";
            string titleResult = await ExecuteScriptRequested?.Invoke(titleScript);
            thread.ThreadName = JsonSerializer.Deserialize<string>(titleResult) ?? "Unknown Thread";
            
            // Extract site name from thread title (assuming format like "ThreadName | The AK Files")
            if (thread.ThreadName.Contains(" | "))
            {
                _threadName = $"{thread.ThreadName.Split(" | ").First().Trim()}_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}";
                _siteName = thread.ThreadName.Split(" | ").Last().Trim();
            }

            _threadName = string.Join("_", _threadName.Split(System.IO.Path.GetInvalidFileNameChars()));
            string threadFolder = Path.Combine(_rootFolder, _siteName, _forumName, _threadName);
            string imagesFolder = Path.Combine(threadFolder, "Images");
            
            // Loop through all pages in the thread
            bool hasNextPage = true;
            while (_isHarvesting && hasNextPage)
            {
                var pageMessages = await HarvestPage();

                // Extract images for each message
                await ExtractImages(thread, imagesFolder, pageMessages);

                // Check if NextElement exists and click it
                string nextScript = $"document.querySelector('{NextElement}') ? 'found' : 'not_found'";
                string nextResult = await ExecuteScriptRequested?.Invoke(nextScript);
                nextResult = JsonSerializer.Deserialize<string>(nextResult);

                if (nextResult == "found")
                {
                    await ExecuteScriptRequested?.Invoke($"document.querySelector('{NextElement}').click();");
                    await Task.Delay(GetRandomizedDelay());
                }
                else
                {
                    hasNextPage = false;
                }
            }

            return thread;
        }

        private async Task ExtractImages(ForumThread thread, string imagesFolder, List<ForumMessage> pageMessages)
        {
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
                            fileName = $"image_{_threadImageCounter}.jpg";

                        if (!System.IO.Directory.Exists(imagesFolder))
                            System.IO.Directory.CreateDirectory(imagesFolder);

                        string imagePath = System.IO.Path.Combine(imagesFolder, fileName);
                        await ExtractImageRequested?.Invoke(imageUrl, imagePath);
                        imageNames.Add(fileName);
                        _threadImageCounter++;
                    }
                    catch { }
                }
                message.Images = imageNames;
            }

            thread.Messages.AddRange(pageMessages);
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