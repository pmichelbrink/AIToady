using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Wpf;

namespace AIToady.Harvester.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private string _url = "akfiles.com";
        private string _nextElement = ".pageNav-jump--next";
        private string _threadElement = "";
        private string _pageLoadDelay = "60";
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
            _currentThreadIndex = 0;

            if (!int.TryParse(PageLoadDelay, out int delay))
            {
                delay = 60;
            }

            while (_isHarvesting && _currentThreadIndex < _threadLinks.Count)
            {
                NavigateRequested?.Invoke(_threadLinks[_currentThreadIndex]);
                await System.Threading.Tasks.Task.Delay(delay * 1000);
                
                if (_isHarvesting && !string.IsNullOrEmpty(NextElement))
                {
                    await ExecuteScriptRequested?.Invoke($"document.querySelector('{NextElement}')?.click();");
                }
                
                _currentThreadIndex++;
            }

            _isHarvesting = false;
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