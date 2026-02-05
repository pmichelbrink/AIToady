using AIToady.Harvester.Models;
using AIToady.Infrastructure;
using AIToady.Infrastructure.Services;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
namespace AIToady.Harvester.ViewModels
{
    public class BaseViewModel : INotifyPropertyChanged
    {
        protected string _siteName = "";
        protected string _forumName = "";
        protected string _messageElement = "";
        protected string _imageElement = "";
        protected string _attachmentElement = "";
        protected ObservableCollection<LogEntry> _logEntries = new ObservableCollection<LogEntry>();
        protected int _threadsToSkip = 0;
        protected int _messagesPerPage = 25;
        protected string _url = string.Empty;
        protected string _nextElement = ".pageNav-jump--next";
        protected string _threadElement = "structItem-title";
        protected int _pageLoadDelay = 6;
        protected bool _isHarvesting = false;
        protected bool _isCapturingElement = false;
        protected string _rootFolder = GetDriveWithMostFreeSpace();
        protected string _startTime = "09:00";
        protected string _endTime = "23:00";
        protected string _harvestingButtonText = "Start Harvesting";
        protected bool _stopAfterCurrentPage = false;
        protected bool _skipExistingThreads = true;
        protected bool _hoursOfOperationEnabled = true;
        protected bool _darkMode = true;
        protected ObservableCollection<string> _scheduledForums = new ObservableCollection<string>();
        protected List<ThreadInfo> _threadInfos = new List<ThreadInfo>();
        protected HashSet<string> _badDomains = new HashSet<string>
        {
            "tinypic.com", "imgsafe.org", "postimg.org", "carbinecreations.com",
            "picturetrail.com", "hillarymilesproductions.com", "pbsrc.com",
            "fearlessmen.com", "allbackgrounds.com", "freeimagehosting.net",
            "novarata.net", "combatmachine.net", "hostingpics.net", "gunbroker.com", 
            "funnywebsite.com", "weaponarts.com", "bing.net", "sightpusher.com",
            "groundedparents.com", "adrenaljunkie.com", "kgcoatings.com", "arco-iris.com",
            "picyard.com", "nodakspud.com", "vcmedia.vn", "villagephotos.com", "geocities.com",
            "gunscience.com", "picfury.com", "handgunblog.com", "htmlsitedesign.com", "blackwellindustries.com",
            "62x54r.net", "zombieboxes.com", "hunt101.com", "nothingbutguns.com", "360WD.com", "photos.smugmug.com",
            "community.webshots.com"
        };
        protected Random _random = new Random();
        protected int _forumPageNumber = 1;
        protected int _threadPageNumber = 1;
        protected int _currentThreadIndex = 0;
        protected int _threadImageCounter = 1;
        protected string _threadName;
        protected System.Timers.Timer _timer;
        protected ConnectionMonitor _connectionMonitor;
        protected string _emailAccount = "";
        protected string _emailPassword = "";
        protected bool _pausedForConnection = false;
        protected string _category = "";
        protected DateTime? _harvestSince = null;
        protected DateTime _lastHarvestPageCall = DateTime.Now;
        public event Func<string, string, Task<string>> ExtractImageRequested;
        public event Func<string, string, Task> ExtractAttachmentRequested;
        public event Action<string> NavigateRequested;
        public event Func<string, Task<string>> ExecuteScriptRequested;
        public event Action<ViewModelType> ViewModelSwitchRequested;
        public event Func<string, Task<string>> PromptUserInputRequested;
        public event Func<Task> ClearCacheRequested;
        public event Action<string> SetDownloadFolderRequested;
        EmailService _emailService;
        protected virtual bool IsBoardPage(string url) { return false; }
        protected virtual async Task LoadForumLinksFromBoard() { }
        protected virtual async Task<bool> CheckIfNextPageExists(int count) { return false; }
        protected virtual async Task<List<ForumMessage>> HarvestPage() { return new List<ForumMessage>(); }
        protected void InvokeNavigateRequested(string url) => NavigateRequested?.Invoke(url);
        protected async Task<string> InvokeExecuteScriptRequested(string script) => await ExecuteScriptRequested?.Invoke(script);
        protected async Task<string> PromptUserInput(string prompt) => await PromptUserInputRequested?.Invoke(prompt) ?? string.Empty;
        protected static string GetDriveWithMostFreeSpace()
        {
            return DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .OrderByDescending(d => d.AvailableFreeSpace)
                .FirstOrDefault()?.Name ?? "C:\\";
        }
        public string SiteName
        {
            get => _siteName;
            set => SetProperty(ref _siteName, value);
        }

        public string ForumName
        {
            get => _forumName;
            set => SetProperty(ref _forumName, value);
        }
        public string MessageElement
        {
            get => _messageElement;
            set => SetProperty(ref _messageElement, value);
        }

        public string ImageElement
        {
            get => _imageElement;
            set => SetProperty(ref _imageElement, value);
        }

        public string AttachmentElement
        {
            get => _attachmentElement;
            set => SetProperty(ref _attachmentElement, value);
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

        public ObservableCollection<LogEntry> LogEntries => _logEntries;

        public int ThreadsToSkip
        {
            get => _threadsToSkip;
            set => SetProperty(ref _threadsToSkip, value);
        }

        public int MessagesPerPage
        {
            get => _messagesPerPage;
            set => SetProperty(ref _messagesPerPage, value < 1 ? 1 : value);
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

        public string StartTime
        {
            get => _startTime;
            set => SetProperty(ref _startTime, value);
        }

        public string EndTime
        {
            get => _endTime;
            set => SetProperty(ref _endTime, value);
        }

        public string HarvestingButtonText
        {
            get => _harvestingButtonText;
            set => SetProperty(ref _harvestingButtonText, value);
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

        public bool StopAfterCurrentPage
        {
            get => _stopAfterCurrentPage;
            set => SetProperty(ref _stopAfterCurrentPage, value);
        }

        public bool SkipExistingThreads
        {
            get => _skipExistingThreads;
            set => SetProperty(ref _skipExistingThreads, value);
        }

        public bool HoursOfOperationEnabled
        {
            get => _hoursOfOperationEnabled;
            set => SetProperty(ref _hoursOfOperationEnabled, value);
        }

        public bool DarkMode
        {
            get => _darkMode;
            set => SetProperty(ref _darkMode, value);
        }

        public ObservableCollection<string> ScheduledForums => _scheduledForums;

        public List<ThreadInfo> ThreadInfos => _threadInfos;

        public string EmailAccount
        {
            get => _emailAccount;
            set => SetProperty(ref _emailAccount, value);
        }

        public string EmailPassword
        {
            get => _emailPassword;
            set => SetProperty(ref _emailPassword, value);
        }

        public string Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
        }

        public DateTime? HarvestSince
        {
            get => _harvestSince;
            set => SetProperty(ref _harvestSince, value);
        }

        public ICommand GoCommand { get; protected set; }
        public ICommand NextCommand { get; protected set; }
        public ICommand LoadThreadsCommand { get; protected set; }
        public ICommand StartHarvestingCommand { get; protected set; }
        public ICommand ScheduleCommand { get; protected set; }

        public async Task<bool> TestEmail()
        {
            try
            {
                var emailService = new EmailService(EmailAccount, EmailPassword);
                await emailService.SendEmailAsync(Environment.MachineName + "@AIToady.com", "Test Email", "This is a test email from AIToady Harvester.");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        public async void ExecuteStartHarvesting()
        {
            if (!IsReadyToHarvest())
                return;

            bool hasNextForumPage = true;
            while (_isHarvesting && hasNextForumPage)
            {
                AddLogEntry($"- - - - - Starting Forum Page {GetPageNumberFromUrl(Url)} - - - - -");

                await ExecuteLoadThreads();

                if (!IsWithinOperatingHours())
                {
                    _isHarvesting = false;
                    HarvestingButtonText = "Sleeping";
                    break;
                }

                for (int i = ThreadsToSkip; i < _threadInfos.Count; i++)
                {
                    await CheckInternetConnection();

                    var threadInfo = _threadInfos[i];
                    if (HarvestSince.HasValue && threadInfo.LastPostDate.HasValue && threadInfo.LastPostDate.Value < HarvestSince.Value)
                    {
                        AddLogEntry($"Skipping thread (last post {threadInfo.LastPostDate.Value:yyyy-MM-dd} before {HarvestSince.Value:yyyy-MM-dd}): {threadInfo.Url}");
                        continue;
                    }

                    AddLogEntry(threadInfo.Url);
                    var thread = await HarvestThread(threadInfo.Url);
                    await WriteThreadInfo(thread);

                    if (!_isHarvesting)
                    {
                        InvokeNavigateRequested(Url);
                        return;
                    }
                }
                
                if (HarvestSince.HasValue && _threadInfos.All(t => t.LastPostDate.HasValue && t.LastPostDate.Value < HarvestSince.Value))
                {
                    AddLogEntry($"All threads on this page are before {HarvestSince.Value:yyyy-MM-dd}, stopping harvest");
                    hasNextForumPage = false;
                    break;
                }
                //Only skip threads on the first page (like when harvesting was stopped mid-page)
                ThreadsToSkip = 0;

                await LoadForumPage();

                hasNextForumPage = await LoadNextForumPage();

                if (hasNextForumPage)
                {
                    await Task.Delay(GetRandomizedDelay());
                    var newUrl = await InvokeExecuteScriptRequested("window.location.href");
                    newUrl = JsonSerializer.Deserialize<string>(newUrl);
                    if (Utilities.IsValidForumUrl(newUrl))
                        Url = newUrl;
                    SaveSettings();
                }
                else
                {
                    //await _emailService.SendEmailAsync(Environment.MachineName + "@AIToady.com", "Forum Extraction Complete on " + Environment.MachineName, "Body");
                    //System.Media.SystemSounds.Beep.Play();
                }

                if (_stopAfterCurrentPage)
                {
                    hasNextForumPage = false;
                    AddLogEntry("Stopping after current page as requested");
                    break;
                }
            }

            if (_isHarvesting)
            {
                if (!_stopAfterCurrentPage && _scheduledForums.Count > 0)
                {
                    await StartNextForum();
                }
                else
                {
                    StopHarvesting();
                }
            }
        }

        private async Task StartNextForum(bool skipCategoryPrompt = true)
        {
            var nextForum = _scheduledForums.First();
            _scheduledForums.RemoveAt(0);
            AddLogEntry($"Loading next scheduled forum: {nextForum}");
            //await _emailService.SendEmailAsync(Environment.MachineName + "@AIToady.com", $"Starting next scheduled forum: {nextForum}", "Body");
            if (Utilities.IsValidForumUrl(nextForum))
                Url = nextForum;

            await ExecuteGo(skipCategoryPrompt);

            await Task.Delay(3000);
            _isHarvesting = false;
            ExecuteStartHarvesting();
        }

        private bool IsReadyToHarvest()
        {
            if (_isHarvesting)
            {
                _isHarvesting = false;
                HarvestingButtonText = "Start Harvesting";
                return false;
            }

            if (!IsWithinOperatingHours())
            {
                MessageBox.Show(Application.Current.MainWindow, "Outside operating hours. Harvesting will start automatically during operating hours.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                HarvestingButtonText = "Sleeping";
                return false;
            }

            if (string.IsNullOrEmpty(ThreadElement) || string.IsNullOrEmpty(NextElement))
            {
                MessageBox.Show(Application.Current.MainWindow, "Thread Element and Next Element must be specified before harvesting.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _isHarvesting = false;
                HarvestingButtonText = "Start Harvesting";
                return false;
            }

            if (string.IsNullOrEmpty(SiteName) || string.IsNullOrEmpty(ForumName))
            {
                MessageBox.Show(Application.Current.MainWindow, "Site Name and Forum Name must be specified before harvesting.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _isHarvesting = false;
                HarvestingButtonText = "Start Harvesting";
                return false;
            }

            _isHarvesting = true;
            HarvestingButtonText = "Stop Harvesting";

            return true;
        }

        private async Task CheckInternetConnection()
        {
            while (_isHarvesting && !_connectionMonitor.IsConnected)
            {
                if (!_pausedForConnection)
                {
                    _pausedForConnection = true;
                    AddLogEntry("Internet connection lost - pausing harvesting");
                }
                await Task.Delay(1000);
            }

            if (_pausedForConnection && _connectionMonitor.IsConnected)
            {
                _pausedForConnection = false;
                AddLogEntry("Internet connection restored - resuming harvesting");
            }
        }

        private void StopHarvesting()
        {
            _isHarvesting = false;
            HarvestingButtonText = "Start Harvesting";
            MessageBox.Show(Application.Current.MainWindow, $"Harvesting complete.", "Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        protected async Task<ForumThread> HarvestThread(string threadUrl)
        {
            _threadPageNumber = 1;
            _threadImageCounter = 1;
            string firstPostId = string.Empty;
            InvokeNavigateRequested(threadUrl);
            await Task.Delay(GetRandomizedDelay());

            var thread = new ForumThread();

            _threadName = await GetThreadName(threadUrl);
            thread.ThreadName = _threadName;

            string threadFolder = GetThreadFolder();

            // Check if thread folder exists and skip if SkipExistingThreads is true
            if (SkipExistingThreads && Directory.Exists(threadFolder))
            {
                AddLogEntry($"Skipping existing thread: {_threadName}");
                return null;
            }
            string imagesFolder = Path.Combine(threadFolder, "Images");

            // Loop through all pages in the thread
            bool hasNextPage = true;
            while (_isHarvesting && hasNextPage)
            {
                _lastHarvestPageCall = DateTime.Now;
                List<ForumMessage> pageMessages = await HarvestPage();

                if (pageMessages.Count != 0)
                {
                    if (_threadPageNumber == 1)
                    {
                        firstPostId = pageMessages[0].PostId;
                    }
                    else
                    {
                        // Some forums (looking at you, Gunboards) include the first post on every page
                        if (pageMessages[0].PostId == firstPostId)
                            pageMessages.RemoveAt(0);
                    }
                }

                // Extract images and attachments for each message
                await ExtractImagesAndAttachments(thread, threadFolder, pageMessages);

                AddLogEntry($"Page {_threadPageNumber} Harvested");

                bool nextPageExists = await CheckIfNextPageExists(pageMessages.Count);

                if (nextPageExists)
                {
                    _threadPageNumber++;
                    try
                    {
                        //await InvokeExecuteScriptRequested($"document.querySelector('{NextElement}').click();");
                        await Task.Delay(GetRandomizedDelay());
                    }
                    catch (TaskCanceledException)
                    {
                        AddLogEntry("Navigation timeout, continuing to next thread");
                        hasNextPage = false;
                    }
                }
                else
                {
                    hasNextPage = false;
                }
            }

            return thread;
        }

        private string GetThreadFolder()
        {
            string threadFolder = Path.Combine(_rootFolder, SiteName);

            if (!string.IsNullOrEmpty(Category))
                threadFolder = Path.Combine(threadFolder, Category);

            threadFolder = Path.Combine(threadFolder, ForumName, _threadName);
            return threadFolder.Replace("/", "_");
        }

        protected virtual async Task<string> GetThreadName(string threadUrl)
        {
            // Get thread name from page title
            string titleScript = "document.title";
            string titleResult = await InvokeExecuteScriptRequested(titleScript);
            _threadName = JsonSerializer.Deserialize<string>(titleResult) ?? "Unknown Thread";


            if (_threadName.Contains('|'))
                _threadName = _threadName.Split('|')[0].Trim();

            // Extract thread ID from URL and append to thread name
            var threadId = threadUrl.TrimEnd('/').Split('.').LastOrDefault();
            if (!string.IsNullOrEmpty(threadId))
                _threadName += $"_{threadId}";

            _threadName = string.Join("_", _threadName.Split(System.IO.Path.GetInvalidFileNameChars()));

            return _threadName;
        }

        private async Task<bool> LoadNextAKForumsPage()
        {
            string akNextScript = @"
                        (function() {
                            let nextButton = document.querySelector('.pageNav-jump--next[aria-disabled=""false""]');
                            if (nextButton) {
                                nextButton.click();
                                return 'clicked';
                            }
                            return 'not_found';
                        })()
                    ";
            string akNextResult = await InvokeExecuteScriptRequested(akNextScript);
            akNextResult = JsonSerializer.Deserialize<string>(akNextResult);
            return akNextResult == "clicked";
        }

        protected virtual async Task<bool> LoadNextForumPage()
        {
            return await CheckIfNextPageExists(999);
        }
        protected virtual async Task ExtractForumName(bool skipCategoryPrompt = false) { }

        public virtual async Task ExecuteGo(bool skipCategoryPrompt = false)
        {
            if (string.IsNullOrEmpty(Url))
                return;

            _emailService = new EmailService(EmailAccount, EmailPassword);

            string url = Url.Trim();
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }

            Uri.TryCreate(url, UriKind.Absolute, out var uri);

            if (uri.Host.Contains("theakforum") && GetType() != typeof(TheAKForumViewModel))
            {
                SiteName = "The AK Forum";
                ViewModelSwitchRequested?.Invoke(ViewModelType.TheAKForum);
                return;
            }
            else if (uri.Host.Contains("gunboards") && GetType() != typeof(TheAKForumViewModel))
            {
                SiteName = "Gunboards";
                ViewModelSwitchRequested?.Invoke(ViewModelType.TheAKForum);
                return;
            }
            else if (uri.Host.Contains("glocktalk") && GetType() != typeof(TheAKForumViewModel))
            {
                SiteName = "Glock Talk";
                ViewModelSwitchRequested?.Invoke(ViewModelType.TheAKForum);
                return;
            }
            else if (uri.Host.Contains("ar15") && GetType() != typeof(AR15ViewModel))
            {
                ViewModelSwitchRequested?.Invoke(ViewModelType.AR15);
                return;
            }
            else if (uri.Host.Contains("thehighroad") && GetType() != typeof(HighRoadViewModel))
            {
                ViewModelSwitchRequested?.Invoke(ViewModelType.HighRoadViewModel);
                return;
            }

            NavigateRequested?.Invoke(url);
            await Task.Delay(2000);

            if (IsBoardPage(url))
            {
                await LoadForumLinksFromBoard();

                await StartNextForum(false);
            }
            else
            {
                await ExtractForumName(skipCategoryPrompt);
            }
        }

        public async void ExecuteNext()
        {
            if (!string.IsNullOrEmpty(NextElement))
            {
                await ExecuteScriptRequested?.Invoke($"document.querySelector('{NextElement}').click();");
            }
        }

        public virtual async Task ExecuteLoadThreads()
        {
            try
            {
                // Ensure WebView2 is initialized before executing scripts
                if (ExecuteScriptRequested == null)
                {
                    AddLogEntry("WebView2 not ready. Please navigate to a page first.");
                    return;
                }

                string className = ThreadElement.Trim();
                if (!string.IsNullOrEmpty(className))
                {
                    string script = $@"
                        let threads = [];
                        let divs = document.querySelectorAll('.{className.TrimStart('.')}');
                        divs.forEach(div => {{
                            let anchors = div.querySelectorAll('a');
                            let threadUrl = null;
                            anchors.forEach(a => {{
                                if (a.href && a.href.includes('/threads/') && !threadUrl) {{
                                    threadUrl = a.href;
                                }}
                            }});
                            if (threadUrl) {{
                                let latestDate = null;
                                let structItem = div.closest('.structItem');
                                if (structItem) {{
                                    let timeElement = structItem.querySelector('.structItem-latestDate');
                                    if (timeElement) {{
                                        latestDate = timeElement.getAttribute('datetime');
                                    }}
                                }}
                                threads.push({{ url: threadUrl, lastPostDate: latestDate }});
                            }}
                        }});
                        JSON.stringify(threads);
                    ";

                    string result = await ExecuteScriptRequested?.Invoke(script);
                    result = JsonSerializer.Deserialize<string>(result);
                    var threads = JsonSerializer.Deserialize<ThreadInfo[]>(result);
                    _threadInfos.Clear();
                    _threadInfos.AddRange(threads);
                }
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error loading threads: {ex.Message}");
            }
        }
        public async Task LoadForumPage()
        {
            InvokeNavigateRequested(Url);
            string currentUrl;
            int retries = 0;
            do
            {
                await Task.Delay(GetRandomizedDelay());
                currentUrl = await ExecuteScriptRequested?.Invoke("window.location.href");
                currentUrl = JsonSerializer.Deserialize<string>(currentUrl);
                if (++retries >= 5 && currentUrl?.TrimEnd('/') != Url?.TrimEnd('/'))
                {
                    InvokeNavigateRequested(Url);
                    retries = 0;
                }
            } while (Utilities.IsValidForumUrl(currentUrl) && currentUrl?.TrimEnd('/') != Url?.TrimEnd('/') && _isHarvesting);
        }
        public int GetPageNumberFromUrl(string url)
        {
            var match = System.Text.RegularExpressions.Regex.Match(url, @"/page-(\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 1;
        }
        private bool IsUrlFromBadDomain(string imageUrl)
        {
            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) || !uri.Host.Contains("."))
            {
                AddLogEntry($"Skipping invalid URL: {imageUrl}");
                return true;
            }

            if (_badDomains.Any(domain => uri.Host.Contains(domain)))
            {
                AddLogEntry($"Skipping known bad domain: {uri.Host}");
                return true;
            }
            return false;
        }

        public async Task ExtractImagesAndAttachments(ForumThread thread, string threadFolder, List<ForumMessage> pageMessages)
        {
            string imagesFolder = Path.Combine(threadFolder, "Images");
            string attachmentsFolder = Path.Combine(threadFolder, "Attachments");

            foreach (var message in pageMessages)
            {
                // Process images
                var imageNames = new List<string>();
                for (int i = 0; i < message.Images.Count; i++)
                {
                    try
                    {
                        string imageUrl = message.Images[i];

                        if (imageUrl.EndsWith(".php"))
                        {
                            AddLogEntry($"Skipping PHP URL {imageUrl}");
                            continue;
                        }

                        if (imageUrl.Contains(";base64,"))
                        {
                            AddLogEntry($"Skipping base64 URL (I haven't found a valid one yet)");
                            continue;
                        }

                        // Handle photobucket BBCode format
                        if (imageUrl.Contains("[IMG]"))
                        {
                            int imgIndex = imageUrl.IndexOf("[IMG]");
                            imageUrl = imageUrl.Substring(imgIndex + 5);
                        }

                        // Extract src from HTML img tags
                        if (imageUrl.Contains("src="))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(imageUrl, @"src=[""'](.*?)[""']");
                            if (match.Success)
                                imageUrl = match.Groups[1].Value;
                        }

                        // Skip URLs without proper domain (e.g., "http://IMG_20170901_195125885.jpg")
                        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) && !uri.Host.Contains("."))
                        {
                            AddLogEntry($"Skipping URL without domain: {imageUrl}");
                            continue;
                        }

                        // Handle relative URLs by prepending domain from Url property
                        if (imageUrl.StartsWith("/") && !string.IsNullOrEmpty(Url))
                        {
                            var imageUri = new Uri(Url.StartsWith("http") ? Url : "https://" + Url);
                            imageUrl = $"{imageUri.Scheme}://{imageUri.Host}{imageUrl}";
                        }

                        // Skip URLs containing BBCode tags
                        if (imageUrl.Contains("[url]") || imageUrl.Contains("[/url]"))
                        {
                            AddLogEntry($"Skipping URL with BBCode tags: {imageUrl}");
                            continue;
                        }

                        // Skip domains that have previously failed or are known bad domains
                        if (IsUrlFromBadDomain(imageUrl))
                            continue;

                        //if (imageUrl.Contains("photobucket.com"))
                        //{
                        //    AddLogEntry($"Skipping photobucket.com image {imageUrl}");
                        //    continue;
                        //}

                        if (imageUrl.Contains("imgur.com/a/"))
                        {
                            AddLogEntry($"Processing imgur album: {imageUrl}");
                            var albumImages = await ProcessImgurAlbum(imageUrl, imagesFolder);
                            imageNames.AddRange(albumImages);
                            continue;
                        }

                        // Clean imgur URLs by removing query parameters
                        if (imageUrl.Contains("imgur.com") && imageUrl.Contains("?"))
                            imageUrl = imageUrl.Split('?')[0];

                        // Clean Google Photos URLs by removing parameters
                        if (imageUrl.Contains("googleusercontent.com") && imageUrl.Contains("="))
                            imageUrl = imageUrl.Split('=')[0];

                        string fileName = await GetFileNameFromUrl(i, imageUrl);

                        if (!System.IO.Directory.Exists(imagesFolder))
                            System.IO.Directory.CreateDirectory(imagesFolder);

                        string imagePath = Path.Combine(imagesFolder, fileName);
                        if (File.Exists(imagePath))
                        {
                            AddLogEntry(fileName + " already exists, skipping image download");
                            continue;
                        }

                        string result = await ExtractImageRequested?.Invoke(imageUrl, imagePath);

                        if (File.Exists(imagePath))
                        {
                            if (IsHtmlFile(imagePath))
                            {
                                File.Delete(imagePath);
                                AddLogEntry($"Deleted HTML file masquerading as image: {fileName}");

                                CheckForFlickrAlbumImages(imagesFolder, imageNames, imagePath);
                            }
                            else
                            {
                                imageNames.Add(fileName);
                                _threadImageCounter++;
                            }
                        }
                        else if (result.IsUnavailableError() || result.IsTimeoutError())
                        {
                            AddLogEntry($"Failed to find image {imageUrl}, skipping");
                        }
                        else
                        {
                            AddLogEntry($"{result} - {imageUrl}");
                            //await _emailService.SendEmailAsync(Environment.MachineName + "@AIToady.com", "Image Error on " + Environment.MachineName, "Body");
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        AddLogEntry($"Image extraction timeout for image {i + 1}, skipping");
                    }
                    catch
                    {
                        AddLogEntry($"Failed to extract image {i + 1}, skipping");
                    }
                }
                message.Images = imageNames;

                if (message.Attachments.Any())
                    SetDownloadFolderRequested?.Invoke(attachmentsFolder);

                // Process attachments
                var attachmentNames = new List<string>();
                for (int i = 0; i < message.Attachments.Count; i++)
                {
                    try
                    {
                        string attachmentUrl = message.Attachments[i];

                        // Handle relative URLs by prepending domain from Url property
                        if (attachmentUrl.StartsWith("/") && !string.IsNullOrEmpty(Url))
                        {
                            var uri = new Uri(Url.StartsWith("http") ? Url : "https://" + Url);
                            attachmentUrl = $"{uri.Scheme}://{uri.Host}{attachmentUrl}";
                        }

                        string fileName = await GetFileNameFromUrl(i, attachmentUrl);

                        if (!System.IO.Directory.Exists(attachmentsFolder))
                            System.IO.Directory.CreateDirectory(attachmentsFolder);

                        string attachmentPath = System.IO.Path.Combine(attachmentsFolder, fileName);

                        if (File.Exists(attachmentPath))
                        {
                            AddLogEntry(fileName + " already exists, skipping attachment download");
                            continue;
                        }

                        await ExtractAttachmentRequested?.Invoke(attachmentUrl, attachmentPath);

                        if (File.Exists(attachmentPath))
                        {
                            attachmentNames.Add(fileName);
                        }
                        else
                        {
                            //_emailService?.SendEmailAsync(Environment.MachineName + "@AIToady.com", "Attachment Download Failed for " + fileName, "Attachment download failed for " + fileName);
                            AddLogEntry($"Attachment download failed for {fileName}");
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        AddLogEntry($"Attachment extraction timeout for attachment {i + 1}, skipping");
                    }
                    catch
                    {
                        AddLogEntry($"Failed to extract attachment {i + 1}, skipping");
                    }
                }
                message.Attachments = attachmentNames;
            }

            thread.Messages.AddRange(pageMessages);
        }

        private void CheckForFlickrAlbumImages(string imagesFolder, List<string> imageNames, string imagePath)
        {
            string baseFileName = Path.GetFileNameWithoutExtension(imagePath);
            int albumIndex = 1;
            string albumImagePath = Path.Combine(imagesFolder, $"{baseFileName}_{albumIndex}{Path.GetExtension(imagePath)}");
            while (File.Exists(albumImagePath))
            {
                imageNames.Add(Path.GetFileName(albumImagePath));
                _threadImageCounter++;
                albumIndex++;
                albumImagePath = Path.Combine(imagesFolder, $"{baseFileName}_{albumIndex}{Path.GetExtension(imagePath)}");
            }
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            CommandManager.InvalidateRequerySuggested();
            return true;
        }
        public BaseViewModel()
        {
            LoadSettings();
            GoCommand = new RelayCommand(() => ExecuteGo());
            NextCommand = new RelayCommand(ExecuteNext);
            StartHarvestingCommand = new RelayCommand(() => ExecuteStartHarvesting(), () => !_isHarvesting || _threadInfos.Count > 0);
            ScheduleCommand = new RelayCommand(ExecuteSchedule);

            InitializeTimer();
            InitializeConnectionMonitor();
        }

        public void ExecuteSchedule()
        {
            if (Utilities.IsValidForumUrl(Url) && !_scheduledForums.Contains(Url))
            {
                _scheduledForums.Add(Url);
                SaveSettings();
                ShowScheduledForumsToast();
            }
        }

        private void ShowScheduledForumsToast()
        {
            try
            {
                var builder = new ToastContentBuilder();
                
                if (_scheduledForums.Any())
                {
                    builder.AddText($"Added to scheduled forums:{Environment.NewLine}");
                    var shortened = _scheduledForums.Take(3).Select(f => f.Length > 50 ? string.Concat(f.AsSpan(0, 47), "...") : f);
                    builder.AddText(string.Join(Environment.NewLine, shortened));
                }
                
                builder.Show();
            }
            catch (Exception ex)
            {
                AddLogEntry($"{ShowScheduledForumsToast}: {ex.Message}");
            }
        }
        public int GetRandomizedDelay()
        {
            int delay = _random.Next(PageLoadDelay, PageLoadDelay * 2 + 1) * 700;
            return delay;
        }

        public void SaveSettings()
        {
            SaveBadDomains();

            Properties.Settings.Default.Url = Url;
            Properties.Settings.Default.NextElement = NextElement;
            Properties.Settings.Default.ThreadElement = ThreadElement;
            Properties.Settings.Default.ThreadsToSkip = ThreadsToSkip;
            Properties.Settings.Default.PageLoadDelay = PageLoadDelay;
            Properties.Settings.Default.RootFolder = RootFolder;
            Properties.Settings.Default.StartTime = StartTime;
            Properties.Settings.Default.EndTime = EndTime;
            Properties.Settings.Default.SkipExistingThreads = SkipExistingThreads;
            Properties.Settings.Default.SiteName = SiteName;
            Properties.Settings.Default.ForumName = ForumName;
            Properties.Settings.Default.MessageElement = MessageElement;
            Properties.Settings.Default.ImageElement = ImageElement;
            Properties.Settings.Default.AttachmentElement = AttachmentElement;
            Properties.Settings.Default.HoursOfOperationEnabled = HoursOfOperationEnabled;
            Properties.Settings.Default.EmailAccount = EmailAccount;
            Properties.Settings.Default.EmailPassword = EmailPassword;
            Properties.Settings.Default.Category = Category;
            Properties.Settings.Default.DarkMode = DarkMode;
            Properties.Settings.Default.MessagesPerPage = MessagesPerPage;
            Properties.Settings.Default.HarvestSince = HarvestSince?.ToString("o") ?? "";
            Properties.Settings.Default.ScheduleForums = string.Join("|", _scheduledForums);
            Properties.Settings.Default.Save();
        }

        public void SaveBadDomains()
        {
            try
            {
                string badDomainsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bad_domains.txt");
                File.WriteAllLines(badDomainsFile, _badDomains);
            }
            catch { }
        }

        public void LoadBadDomains()
        {
            try
            {
                string badDomainsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bad_domains.txt");
                if (File.Exists(badDomainsFile))
                {
                    var domains = File.ReadAllLines(badDomainsFile);
                    foreach (var domain in domains)
                        _badDomains.Add(domain);
                    AddLogEntry($"Loaded {_badDomains.Count} bad domains from file");
                }
            }
            catch { }
        }
        public void LoadSettings()
        {
            LoadBadDomains();

            var savedUrl = Properties.Settings.Default.Url;
            if (Utilities.IsValidForumUrl(savedUrl))
                Url = savedUrl;
            NextElement = string.IsNullOrEmpty(Properties.Settings.Default.NextElement) ? ".pageNav-jump--next" : Properties.Settings.Default.NextElement;
            ThreadElement = string.IsNullOrEmpty(Properties.Settings.Default.ThreadElement) ? "structItem-title" : Properties.Settings.Default.ThreadElement;
            ThreadsToSkip = Properties.Settings.Default.ThreadsToSkip;
            PageLoadDelay = Properties.Settings.Default.PageLoadDelay == 0 ? 4 : Properties.Settings.Default.PageLoadDelay;
            RootFolder = string.IsNullOrEmpty(Properties.Settings.Default.RootFolder) ? GetDriveWithMostFreeSpace() : Properties.Settings.Default.RootFolder;
            StartTime = string.IsNullOrEmpty(Properties.Settings.Default.StartTime) ? "09:00" : Properties.Settings.Default.StartTime;
            EndTime = string.IsNullOrEmpty(Properties.Settings.Default.EndTime) ? "23:59" : Properties.Settings.Default.EndTime;
            SkipExistingThreads = Properties.Settings.Default.SkipExistingThreads;
            SiteName = Properties.Settings.Default.SiteName ?? "";
            ForumName = Properties.Settings.Default.ForumName ?? "";
            MessageElement = Properties.Settings.Default.MessageElement ?? "";
            ImageElement = Properties.Settings.Default.ImageElement ?? "";
            AttachmentElement = Properties.Settings.Default.AttachmentElement ?? "";
            HoursOfOperationEnabled = Properties.Settings.Default.HoursOfOperationEnabled;
            EmailAccount = Properties.Settings.Default.EmailAccount ?? "";
            EmailPassword = Properties.Settings.Default.EmailPassword ?? "";
            Category = Properties.Settings.Default.Category ?? "";
            DarkMode = Properties.Settings.Default.DarkMode;
            MessagesPerPage = Properties.Settings.Default.MessagesPerPage == 0 ? 25 : Properties.Settings.Default.MessagesPerPage;
            
            var harvestSinceStr = Properties.Settings.Default.HarvestSince ?? "";
            if (!string.IsNullOrEmpty(harvestSinceStr) && DateTime.TryParse(harvestSinceStr, out var harvestSince))
                HarvestSince = harvestSince;
            
            var forums = Properties.Settings.Default.ScheduleForums ?? "";
            if (!string.IsNullOrEmpty(forums))
            {
                foreach (var forum in forums.Split('|'))
                    _scheduledForums.Add(forum);
            }
        }
        public void InitializeTimer()
        {
            _timer = new System.Timers.Timer(3600000); // Check every hour
            _timer.Elapsed += (s, e) => RunTimerOperations();
            _timer.Start();
        }

        public bool IsWithinOperatingHours()
        {
            if (!HoursOfOperationEnabled)
                return true;

            if (!TimeSpan.TryParse(StartTime, out var startTime) || !TimeSpan.TryParse(EndTime, out var endTime))
                return true;

            var currentTime = DateTime.Now.TimeOfDay;
            return currentTime >= startTime && currentTime <= endTime;
        }

        public void RunTimerOperations()
        {
            if (_isHarvesting && (DateTime.Now - _lastHarvestPageCall).TotalMinutes >= 10)
            {
                Task.Run(async () => await _emailService?.SendEmailAsync(Environment.MachineName + "@AIToady.com", 
                    $"Harvesting Stalled on {Environment.MachineName}", 
                    $"HarvestPage() has not been called in 10 minutes. Forum: {ForumName}, Thread: {_threadName}"));
                _lastHarvestPageCall = DateTime.Now;
            }

            if (_url.Contains("gunboards", StringComparison.InvariantCultureIgnoreCase))
                ClearCacheRequested?.Invoke();

            if (_isHarvesting && !IsWithinOperatingHours())
            {
                StopAfterCurrentPage = true;
                HarvestingButtonText = "Sleeping";
            }
            else if (!_isHarvesting && IsWithinOperatingHours() && HarvestingButtonText == "Sleeping" && _threadInfos.Count > 0)
            {
                HarvestingButtonText = "Start Harvesting";
                StopAfterCurrentPage = false;
                // Auto-resume harvesting
                Application.Current.Dispatcher.Invoke(() => ExecuteStartHarvesting());
            }
            else if (HarvestingButtonText == "Sleeping" && IsWithinOperatingHours())
            {
                HarvestingButtonText = "Start Harvesting";
                StopAfterCurrentPage = false;
            }
        }
        public void AddLogEntry(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
                _logEntries.Insert(0, new LogEntry { Log = message, Date = DateTime.Now }));

            // Write to log file if forum folder exists
            if (!string.IsNullOrEmpty(ForumName) && !string.IsNullOrEmpty(SiteName))
            {
                try
                {
                    string forumFolder = Path.Combine(_rootFolder, SiteName);
                    Directory.CreateDirectory(forumFolder);
                    string logFile = Path.Combine(forumFolder, "harvest.log");
                    File.AppendAllText(logFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n");
                }
                catch { }
            }
        }

        public async Task WriteThreadInfo(ForumThread thread)
        {
            if (thread != null)
            {
                // Strip newlines and normalize spaces in message content
                foreach (var message in thread.Messages)
                {
                    message.Message = message.Message?.Replace("\n", " ").Replace("\r", " ");
                    message.Message = System.Text.RegularExpressions.Regex.Replace(message.Message ?? "", @"\s+", " ");
                }

                string threadFolder = GetThreadFolder();
                System.IO.Directory.CreateDirectory(threadFolder);

                string json = JsonSerializer.Serialize(thread, new JsonSerializerOptions { WriteIndented = true });
                string fileName = System.IO.Path.Combine(threadFolder, "thread.json");
                await System.IO.File.WriteAllTextAsync(fileName, json);

                string imagePath = Path.Combine(threadFolder, "Images");
                if (Directory.Exists(imagePath) && Directory.GetFiles(imagePath).Count() == 0)
                {
                    try
                    {
                        Directory.Delete(imagePath);
                    }
                    catch { }
                }
            }
        }
        private bool IsHtmlFile(string filePath)
        {
            try
            {
                string content = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                string firstPart = content.Length > 200 ? content.Substring(0, 200) : content;
                return firstPart.Contains("<html") || firstPart.Contains("<HTML") || firstPart.Contains("<!DOCTYPE");
            }
            catch { return false; }
        }

        public async Task<string> GetFileNameFromUrl(int fileIndex, string attachmentUrl)
        {                        // Generate random filename for Google Photos URLs
            if (attachmentUrl.Contains("googleusercontent.com"))
                return $"google_photo_{Guid.NewGuid().ToString("N")[..8]}.jpg";

            // Strip query parameters
            if (attachmentUrl.Contains("attachment.php"))
                return $"attachment_{Guid.NewGuid().ToString("N")[..8]}.jpg";

            // Handle XenForo attachment URLs like "attachments/upload_2022-10-1_13-3-38-png.1106301/"
            if (Utilities.IsXenForoAttachmentUrl(attachmentUrl))
            {
                string xenForoFileName = Utilities.GetXenForoAttachmentFileName(attachmentUrl);
                if (!string.IsNullOrEmpty(xenForoFileName))
                    return xenForoFileName;
            }

            // Strip query parameters
            if (attachmentUrl.Contains("?"))
                attachmentUrl = attachmentUrl.Split('?')[0];

            // Extract filename from URL path
            string fileName = System.IO.Path.GetFileName(attachmentUrl.TrimEnd('/'));
            if (string.IsNullOrEmpty(fileName) || !fileName.Contains("."))
            {
                AddLogEntry("Trying to detect file type from HTTP headers");
                try
                {
                    using (var httpClient = new System.Net.Http.HttpClient())
                    {
                        httpClient.Timeout = TimeSpan.FromSeconds(5);
                        var response = await httpClient.SendAsync(new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, attachmentUrl));
                        var contentType = response.Content.Headers.ContentType?.MediaType;

                        string extension = contentType switch
                        {
                            "image/jpeg" => ".jpg",
                            "image/png" => ".png",
                            "image/gif" => ".gif",
                            "image/webp" => ".webp",
                            _ => ".jpg"
                        };

                        fileName = string.IsNullOrEmpty(fileName) ? $"image_{fileIndex + 1}{extension}" : $"{fileName}{extension}";
                    }
                }
                catch
                {
                    fileName = $"image_{fileIndex + 1}.jpg";
                }
            }
            else
            {
                fileName = fileName.Replace("-", ".");
                // Remove everything after the second dot (e.g. "20250413_195050.jpg.731026" -> "20250413_195050.jpg")
                var parts = fileName.Split('.');
                if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out _))
                {
                    fileName = string.Join(".", parts.Take(parts.Length - 1));
                }
            }

            fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));

            // Generate unique name if filename is just "image"
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            if (nameWithoutExt.Equals("image", StringComparison.OrdinalIgnoreCase))
            {
                var ext = Path.GetExtension(fileName);
                fileName = $"image_{Guid.NewGuid().ToString("N")[..8]}{ext}";
            }

            return fileName;
        }

        private async Task<List<string>> ProcessImgurAlbum(string albumUrl, string imagesFolder)
        {
            var albumImages = new List<string>();
            try
            {
                // Extract album ID from URL (e.g., https://imgur.com/a/nkoWLuL -> nkoWLuL)
                string albumId = albumUrl.Split('/').Last();

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    // Use Imgur API to get album data
                    string apiUrl = $"https://api.imgur.com/3/album/{albumId}";
                    httpClient.DefaultRequestHeaders.Add("Authorization", "Client-ID 546c25a59c58ad7");

                    var response = await httpClient.GetStringAsync(apiUrl);
                    var json = System.Text.Json.JsonDocument.Parse(response);

                    if (json.RootElement.GetProperty("success").GetBoolean())
                    {
                        var images = json.RootElement.GetProperty("data").GetProperty("images");
                        int imageIndex = 0;

                        foreach (var image in images.EnumerateArray())
                        {
                            try
                            {
                                string directImageUrl = image.GetProperty("link").GetString();
                                string fileName = await GetFileNameFromUrl(imageIndex, directImageUrl);
                                string imagePath = System.IO.Path.Combine(imagesFolder, fileName);

                                if (!System.IO.Directory.Exists(imagesFolder))
                                    System.IO.Directory.CreateDirectory(imagesFolder);

                                await ExtractImageRequested?.Invoke(directImageUrl, imagePath);
                                albumImages.Add(fileName);
                                imageIndex++;
                                _threadImageCounter++;
                            }
                            catch
                            {
                                AddLogEntry($"Failed to extract album image {imageIndex}");
                            }
                        }
                    }

                    AddLogEntry($"Processed imgur album: {albumImages.Count} images from {albumUrl}");
                }
            }
            catch
            {
                AddLogEntry($"Failed to process imgur album {albumUrl}");
            }
            return albumImages;
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
        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

        public T CloneToViewModel<T>() where T : BaseViewModel, new()
        {               
            // Set AppUserModelID (required for non-UWP apps)
            string appId = "AIToady.Harvester";
            SetCurrentProcessExplicitAppUserModelID(appId);

            var newViewModel = new T();
            newViewModel._siteName = _siteName;
            newViewModel._forumName = _forumName;
            newViewModel._messageElement = _messageElement;
            newViewModel._imageElement = _imageElement;
            newViewModel._attachmentElement = _attachmentElement;
            newViewModel._threadsToSkip = _threadsToSkip;
            newViewModel._url = _url;
            newViewModel._nextElement = _nextElement;
            newViewModel._threadElement = _threadElement;
            newViewModel._pageLoadDelay = _pageLoadDelay;
            newViewModel._rootFolder = _rootFolder;
            newViewModel._startTime = _startTime;
            newViewModel._endTime = _endTime;
            newViewModel._skipExistingThreads = _skipExistingThreads;
            newViewModel._hoursOfOperationEnabled = _hoursOfOperationEnabled;
            newViewModel._messagesPerPage = _messagesPerPage;
            newViewModel._badDomains = new HashSet<string>(_badDomains);
            newViewModel._category = _category;
            return newViewModel;
        }

        private void InitializeConnectionMonitor()
        {
            _connectionMonitor = new ConnectionMonitor();
            _connectionMonitor.ConnectionLost += () => AddLogEntry("Connection lost - harvesting will pause");
            _connectionMonitor.ConnectionRestored += () => AddLogEntry("Connection restored");
            _connectionMonitor.Start();
        }

        public void Dispose()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _connectionMonitor?.Stop();
        }
    }
}