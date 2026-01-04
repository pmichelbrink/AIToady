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
        protected List<string> _threadLinks = new List<string>();
        protected Random _random = new Random();
        protected int _forumPageNumber = 1;
        protected int _threadPageNumber = 1;
        protected int _currentThreadIndex = 0;
        protected int _threadImageCounter = 1;
        protected string _threadName;
        protected System.Timers.Timer _operatingHoursTimer;

        protected virtual void ExecuteStartHarvesting() { }
        protected virtual void ExecuteGo() { }
        protected virtual void ExecuteNext() { }
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
            Properties.Settings.Default.Save();
        }
        public void LoadSettings()
        {
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
        }
        public void InitializeOperatingHoursTimer()
        {
            _operatingHoursTimer = new System.Timers.Timer(60000); // Check every minute
            _operatingHoursTimer.Elapsed += (s, e) => CheckOperatingHours();
            _operatingHoursTimer.Start();
        }

        public bool IsWithinOperatingHours()
        {
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
        public static string GetFileNameFromUrl(int fileIndex, string attachmentUrl)
        {
            // Extract filename from URL path
            string fileName = System.IO.Path.GetFileName(attachmentUrl.TrimEnd('/'));
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = $"attachment_{fileIndex + 1}.jpg";
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