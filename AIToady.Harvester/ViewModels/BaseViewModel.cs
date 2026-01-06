using AIToady.Harvester.Models;
using System;
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
        protected string _url = "akfiles.com";
        protected string _nextElement = ".pageNav-jump--next";
        protected string _threadElement = "";
        protected int _pageLoadDelay = 6;
        protected bool _isHarvesting = false;
        protected bool _isCapturingElement = false;
        protected string _rootFolder = GetDriveWithMostFreeSpace();
        protected string _startTime = "09:00";
        protected string _endTime = "17:00";
        protected string _harvestingButtonText = "Start Harvesting";
        protected bool _stopAfterCurrentPage = false;
        protected bool _skipExistingThreads = true;
        protected bool _hoursOfOperationEnabled = true;
        protected List<string> _threadLinks = new List<string>();
        protected HashSet<string> _badDomains = new HashSet<string>();
        protected Random _random = new Random();
        protected int _forumPageNumber = 1;
        protected int _threadPageNumber = 1;
        protected int _currentThreadIndex = 0;
        protected int _threadImageCounter = 1;
        protected string _threadName;
        protected System.Timers.Timer _operatingHoursTimer;
        public event Func<string, string, Task<string>> ExtractImageRequested;
        public event Func<string, string, Task> ExtractAttachmentRequested;
        public event Action<string> NavigateRequested;
        public event Func<string, Task<string>> ExecuteScriptRequested;

        protected virtual async Task<ForumThread> HarvestThread(string threadUrl) { return new ForumThread(); }
        protected void InvokeNavigateRequested(string url) => NavigateRequested?.Invoke(url);
        protected async Task<string> InvokeExecuteScriptRequested(string script) => await ExecuteScriptRequested?.Invoke(script);
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

        public List<string> ThreadLinks => _threadLinks;

        public ICommand GoCommand { get; protected set; }
        public ICommand NextCommand { get; protected set; }
        public ICommand LoadThreadsCommand { get; protected set; }
        public ICommand StartHarvestingCommand { get; protected set; }
        
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        public async void ExecuteStartHarvesting()
        {
            if (_isHarvesting)
            {
                _isHarvesting = false;
                HarvestingButtonText = "Start Harvesting";
                return;
            }

            if (!IsWithinOperatingHours())
            {
                MessageBox.Show("Outside operating hours. Harvesting will start automatically during operating hours.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                HarvestingButtonText = "Sleeping";
                return;
            }

            if (string.IsNullOrEmpty(ThreadElement) || string.IsNullOrEmpty(NextElement))
            {
                MessageBox.Show("Thread Element and Next Element must be specified before harvesting.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _isHarvesting = false;
                HarvestingButtonText = "Start Harvesting";
                return;
            }

            if (string.IsNullOrEmpty(SiteName) || string.IsNullOrEmpty(ForumName))
            {
                MessageBox.Show("Site Name and Forum Name must be specified before harvesting.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _isHarvesting = false;
                HarvestingButtonText = "Start Harvesting";
                return;
            }

            _isHarvesting = true;
            HarvestingButtonText = "Stop Harvesting";
            AddLogEntry($"- - - - - Starting Forum Page {GetPageNumberFromUrl(Url)} - - - - -");

            bool hasNextForumPage = true;
            while (_isHarvesting && hasNextForumPage)
            {
                await ExecuteLoadThreads();

                // Check operating hours after each page
                if (!IsWithinOperatingHours())
                {
                    _isHarvesting = false;
                    HarvestingButtonText = "Sleeping";
                    break;
                }

                // Process all threads on current page
                for (int i = ThreadsToSkip; i < _threadLinks.Count; i++)
                {
                    AddLogEntry(_threadLinks[i]);
                    var thread = await HarvestThread(_threadLinks[i]);
                    await WriteThreadInfo(thread);

                    if (!_isHarvesting)
                    {
                        InvokeNavigateRequested(Url);
                        return;
                    }
                }

                ThreadsToSkip = 0;

                if (_stopAfterCurrentPage)
                {
                    hasNextForumPage = false;
                    AddLogEntry("Stopping after current page as requested");
                    break;
                }

                await LoadForumPage();

                if (Url.Contains("akforum.net"))
                {
                    hasNextForumPage = await LoadNextAKForumsPage();
                }
                else
                {
                    hasNextForumPage = await LoadNextForumPage();          
                }

                if (hasNextForumPage)
                {
                    await Task.Delay(GetRandomizedDelay());
                    Url = await InvokeExecuteScriptRequested("window.location.href");
                    Url = JsonSerializer.Deserialize<string>(Url);
                    AddLogEntry($"- - - - - Starting Forum Page {GetPageNumberFromUrl(Url)} - - - - -");
                    SaveSettings();

                }
            }

            if (_isHarvesting)
            {
                MessageBox.Show($"Harvesting complete.", "Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                _isHarvesting = false;
                HarvestingButtonText = "Start Harvesting";
            }
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

        private async Task<bool> LoadNextForumPage()
        {
            string nextScript = @"
                        (function() {
                            let nextButton = document.querySelector('.pageNav-jump.pageNav-jump--next');
                            if (nextButton) {
                                nextButton.click();
                                return 'clicked';
                            }
                            return 'not_found';
                        })()
                    ";
            string nextResult = await InvokeExecuteScriptRequested(nextScript);
            nextResult = JsonSerializer.Deserialize<string>(nextResult);
            return nextResult == "clicked";
        }

        public void ExecuteGo()
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

        public async void ExecuteNext()
        {
            if (!string.IsNullOrEmpty(NextElement))
            {
                await ExecuteScriptRequested?.Invoke($"document.querySelector('{NextElement}').click();");
            }
        }

        public async Task ExecuteLoadThreads()
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
                        let linkSet = new Set();
                        let divs = document.querySelectorAll('.{className.TrimStart('.')}');
                        divs.forEach(div => {{
                            let anchors = div.querySelectorAll('a');
                            anchors.forEach(a => {{
                                if (a.href && a.href.includes('/threads/')) linkSet.add(a.href);
                            }});
                        }});
                        JSON.stringify(Array.from(linkSet));
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
                AddLogEntry($"Error loading threads: {ex.Message}");
            }
        }
        public async Task LoadForumPage()
        {
            //Load the forum page (a thread page is currently loaded) and
            //wait for navigation to complete by checking URL
            InvokeNavigateRequested(Url);
            string currentUrl;
            do
            {
                await Task.Delay(5000);
                currentUrl = await ExecuteScriptRequested?.Invoke("window.location.href");
                currentUrl = JsonSerializer.Deserialize<string>(currentUrl);
            } while (currentUrl != Url && _isHarvesting);
        }
        public int GetPageNumberFromUrl(string url)
        {
            var match = System.Text.RegularExpressions.Regex.Match(url, @"/page-(\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 1;
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

                        // Handle relative URLs by prepending domain from Url property
                        if (imageUrl.StartsWith("/") && !string.IsNullOrEmpty(Url))
                        {
                            var imageUri = new Uri(Url.StartsWith("http") ? Url : "https://" + Url);
                            imageUrl = $"{imageUri.Scheme}://{imageUri.Host}{imageUrl}";
                        }

                        // Skip URLs without proper domain (e.g., "http://IMG_20170901_195125885.jpg")
                        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) && !uri.Host.Contains("."))
                        {
                            AddLogEntry($"Skipping URL without domain: {imageUrl}");
                            continue;
                        }

                        // Skip URLs containing BBCode tags
                        if (imageUrl.Contains("[url]") || imageUrl.Contains("[/url]"))
                        {
                            AddLogEntry($"Skipping URL with BBCode tags: {imageUrl}");
                            continue;
                        }

                        // Skip domains that have previously failed
                        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var checkUri) && _badDomains.Contains(checkUri.Host))
                        {
                            AddLogEntry($"Skipping known bad domain: {checkUri.Host}");
                            continue;
                        }

                        if (imageUrl.Contains("tinypic.com"))
                        {
                            AddLogEntry($"Skipping tinypic.com image {imageUrl}");
                            continue;
                        }
                        if (imageUrl.Contains("imgsafe.org"))
                        {
                            AddLogEntry($"Skipping imgsafe.org image {imageUrl}");
                            continue;
                        }
                        if (imageUrl.Contains("postimg.org"))
                        {
                            AddLogEntry($"Skipping postimg.org image {imageUrl}");
                            continue;
                        }
                        if (imageUrl.Contains("carbinecreations.com"))
                        {
                            AddLogEntry($"Skipping carbinecreations.com image {imageUrl}");
                            continue;
                        }
                        if (imageUrl.Contains("picturetrail.com"))
                        {
                            AddLogEntry($"Skipping picturetrail.com image {imageUrl}");
                            continue;
                        }
                        if (imageUrl.Contains("hillarymilesproductions.com"))
                        {
                            AddLogEntry($"Skipping hillarymilesproductions.com image {imageUrl}");
                            continue;
                        }
                        if (imageUrl.Contains("pbsrc.com"))
                        {
                            AddLogEntry($"Skipping pbsrc.com image {imageUrl}");
                            continue;
                        }
                        if (imageUrl.Contains("fearlessmen.com"))
                        {
                            AddLogEntry($"Skipping fearlessmen.com image {imageUrl}");
                            continue;
                        }
                        if (imageUrl.Contains("allbackgrounds.com"))
                        {
                            AddLogEntry($"Skipping allbackgrounds.com image {imageUrl}");
                            continue;
                        }
                        if (imageUrl.Contains("freeimagehosting.net"))
                        {
                            AddLogEntry($"Skipping freeimagehosting.net image {imageUrl}");
                            continue;
                        }
                        if (imageUrl.Contains("novarata.net"))
                        {
                            AddLogEntry($"Skipping novarata.net image {imageUrl}");
                            continue;
                        }
                        if (imageUrl.Contains("combatmachine.net"))
                        {
                            AddLogEntry($"Skipping combatmachine.net image {imageUrl}");
                            continue;
                        }
                        if (imageUrl.Contains("hostingpics.net"))
                        {
                            AddLogEntry($"Skipping hostingpics.net image {imageUrl}");
                            continue;
                        }
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

                        string imagePath = System.IO.Path.Combine(imagesFolder, fileName);
                        string result = await ExtractImageRequested?.Invoke(imageUrl, imagePath);

                        if (File.Exists(imagePath))
                        {
                            if (IsHtmlFile(imagePath))
                            {
                                File.Delete(imagePath);
                                AddLogEntry($"Deleted HTML file masquerading as image: {fileName}");
                            }
                            else
                            {
                                imageNames.Add(fileName);
                                _threadImageCounter++;
                            }
                        }
                        else if (result.Contains("SSL") || result.Contains("403") || result.Contains("404"))
                        {
                            AddLogEntry($"Failed to find image {imageUrl}, skipping");
                        }
                        else if (result.Contains("504") || result.Contains("522"))
                        {
                            AddLogEntry($"Image timeout {imageUrl}, skipping");
                            if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var failedUri))
                            {
                                _badDomains.Add(failedUri.Host);
                                AddLogEntry($"Added {failedUri.Host} to bad domains list");
                            }
                        }
                        else
                        {

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
                        await ExtractAttachmentRequested?.Invoke(attachmentUrl, attachmentPath);
                        attachmentNames.Add(fileName);
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
            GoCommand = new RelayCommand(ExecuteGo);
            NextCommand = new RelayCommand(ExecuteNext);
            StartHarvestingCommand = new RelayCommand(ExecuteStartHarvesting, () => !_isHarvesting || _threadLinks.Count > 0);

            InitializeOperatingHoursTimer();
        }
        public int GetRandomizedDelay()
        {
            int delay = _random.Next(PageLoadDelay, PageLoadDelay * 3 + 1) * 1000;
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
            Properties.Settings.Default.Save();
        }

        public void SaveBadDomains()
        {
            try
            {
                string badDomainsFile = Path.Combine(_rootFolder, "bad_domains.txt");
                File.WriteAllLines(badDomainsFile, _badDomains);
            }
            catch { }
        }

        public void LoadBadDomains()
        {
            try
            {
                string badDomainsFile = Path.Combine(_rootFolder, "bad_domains.txt");
                if (File.Exists(badDomainsFile))
                {
                    var domains = File.ReadAllLines(badDomainsFile);
                    _badDomains.Clear();
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

            Url = string.IsNullOrEmpty(Properties.Settings.Default.Url) ? "akfiles.com" : Properties.Settings.Default.Url;
            NextElement = string.IsNullOrEmpty(Properties.Settings.Default.NextElement) ? ".pageNav-jump--next" : Properties.Settings.Default.NextElement;
            ThreadElement = Properties.Settings.Default.ThreadElement;
            ThreadsToSkip = Properties.Settings.Default.ThreadsToSkip;
            PageLoadDelay = Properties.Settings.Default.PageLoadDelay == 0 ? 6 : Properties.Settings.Default.PageLoadDelay;
            RootFolder = string.IsNullOrEmpty(Properties.Settings.Default.RootFolder) ? GetDriveWithMostFreeSpace() : Properties.Settings.Default.RootFolder;
            StartTime = string.IsNullOrEmpty(Properties.Settings.Default.StartTime) ? "09:00" : Properties.Settings.Default.StartTime;
            EndTime = string.IsNullOrEmpty(Properties.Settings.Default.EndTime) ? "17:00" : Properties.Settings.Default.EndTime;
            SkipExistingThreads = Properties.Settings.Default.SkipExistingThreads;
            SiteName = Properties.Settings.Default.SiteName ?? "";
            ForumName = Properties.Settings.Default.ForumName ?? "";
            MessageElement = Properties.Settings.Default.MessageElement ?? "";
            ImageElement = Properties.Settings.Default.ImageElement ?? "";
            AttachmentElement = Properties.Settings.Default.AttachmentElement ?? "";
            HoursOfOperationEnabled = Properties.Settings.Default.HoursOfOperationEnabled;
        }
        public void InitializeOperatingHoursTimer()
        {
            _operatingHoursTimer = new System.Timers.Timer(60000); // Check every minute
            _operatingHoursTimer.Elapsed += (s, e) => CheckOperatingHours();
            _operatingHoursTimer.Start();
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

        public void CheckOperatingHours()
        {
            if (_isHarvesting && !IsWithinOperatingHours())
            {
                _isHarvesting = false;
                HarvestingButtonText = "Sleeping";
            }
            else if (!_isHarvesting && IsWithinOperatingHours() && HarvestingButtonText == "Sleeping" && _threadLinks.Count > 0)
            {
                HarvestingButtonText = "Start Harvesting";
                // Auto-resume harvesting
                Application.Current.Dispatcher.Invoke(() => ExecuteStartHarvesting());
            }
            else if (HarvestingButtonText == "Sleeping" && IsWithinOperatingHours())
            {
                HarvestingButtonText = "Start Harvesting";
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
                    string forumFolder = Path.Combine(_rootFolder, SiteName, ForumName);
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
                
                string threadFolder = System.IO.Path.Combine(_rootFolder, SiteName, ForumName, _threadName);
                System.IO.Directory.CreateDirectory(threadFolder);

                string json = JsonSerializer.Serialize(thread, new JsonSerializerOptions { WriteIndented = true });
                string fileName = System.IO.Path.Combine(threadFolder, "thread.json");
                await System.IO.File.WriteAllTextAsync(fileName, json);
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

        public void Dispose()
        {
            _operatingHoursTimer?.Stop();
            _operatingHoursTimer?.Dispose();
        }
    }
}