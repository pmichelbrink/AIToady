using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AIToady.Harvester
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool isCapturingElement = false;
        private TextBox activeTextBox = null;
        private List<string> threadLinks = new List<string>();
        private bool isNextElementAvailable = false;
        private System.Windows.Threading.DispatcherTimer harvestTimer;
        private int currentLinkIndex = 0;
        private bool isHarvesting = false;

        public MainWindow()
        {
            InitializeComponent();
            WebView.NavigationCompleted += WebView_NavigationCompleted;
            WebView.CoreWebView2InitializationCompleted += (s, e) => {
                WebView.CoreWebView2.WebMessageReceived += (sender, args) => {
                    if (isCapturingElement && activeTextBox != null) {
                        string result = args.TryGetWebMessageAsString();
                        if (activeTextBox == ThreadElementTextBox && result.Contains(".")) {
                            string[] parts = result.Split('.');
                            result = parts[parts.Length - 1];
                        }
                        activeTextBox.Text = result;
                        isCapturingElement = false;
                        activeTextBox = null;
                    }
                };
            };
            
            harvestTimer = new System.Windows.Threading.DispatcherTimer();
            harvestTimer.Interval = TimeSpan.FromMinutes(1);
            harvestTimer.Tick += HarvestTimer_Tick;
        }

        private async void WebView_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
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
                        let result;
                        if (window.captureMode === 'class') {
                            result = event.target.parentElement?.className || '';
                        } else {
                            // For Next Element, get the most specific class from the clicked element
                            let classes = event.target.className.split(' ');
                            result = classes.find(c => c.includes('next') || c.includes('Next')) || classes[classes.length - 1] || '';
                        }
                        window.chrome.webview.postMessage(result);
                        window.capturingElement = false;
                    }
                }, true);
            ");
            
            await CheckNextElementAvailability();
        }
        
        private async Task CheckNextElementAvailability()
        {
            string selector = NextElementTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(selector))
            {
                string result = await WebView.ExecuteScriptAsync($@"
                    let element = document.querySelector('{selector}');
                    if (!element) {{
                        'not_found';
                    }} else if (element.disabled || element.classList.contains('disabled') || element.getAttribute('aria-disabled') === 'true') {{
                        'disabled';
                    }} else {{
                        'available';
                    }}
                ");
                
                result = System.Text.Json.JsonSerializer.Deserialize<string>(result);
                isNextElementAvailable = result == "available";
            }
        }

        private void NextElementTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            isCapturingElement = true;
            activeTextBox = NextElementTextBox;
            WebView.ExecuteScriptAsync("window.capturingElement = true; window.captureMode = 'nextClass';");
        }

        private void NextElementTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            isCapturingElement = false;
            activeTextBox = null;
            WebView.ExecuteScriptAsync("window.capturingElement = false;");
        }

        private void ThreadElementTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            isCapturingElement = true;
            activeTextBox = ThreadElementTextBox;
            WebView.ExecuteScriptAsync("window.capturingElement = true; window.captureMode = 'class';");
        }

        private void ThreadElementTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            isCapturingElement = false;
            activeTextBox = null;
            WebView.ExecuteScriptAsync("window.capturingElement = false;");
        }

        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToUrl();
        }

        private void UrlTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NavigateToUrl();
            }
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            string className = NextElementTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(className))
            {
                if (!className.StartsWith("."))
                {
                    className = "." + className;
                }
                await WebView.ExecuteScriptAsync($"document.querySelector('{className}')?.click();");
            }
        }

        private async void LoadThreadsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string className = ThreadElementTextBox.Text.Trim();
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
                    
                    string result = await WebView.ExecuteScriptAsync(script);
                    result = System.Text.Json.JsonSerializer.Deserialize<string>(result);
                    var links = System.Text.Json.JsonSerializer.Deserialize<string[]>(result);
                    threadLinks.Clear();
                    threadLinks.AddRange(links);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error loading threads: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartHarvestingButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isHarvesting)
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(NextElementTextBox.Text))
                {
                    MessageBox.Show("Next Element is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(ThreadElementTextBox.Text))
                {
                    MessageBox.Show("Thread Element is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (!int.TryParse(PageLoadDelayTextBox.Text, out int delaySeconds) || delaySeconds <= 0)
                {
                    MessageBox.Show("Page Load Delay must be a positive number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (threadLinks.Count == 0)
                {
                    MessageBox.Show("No thread links loaded. Click 'Load Threads' first.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                isHarvesting = true;
                currentLinkIndex = 0;
                StartHarvestingButton.Content = "Stop Harvesting";
                harvestTimer.Interval = TimeSpan.FromSeconds(delaySeconds);
                harvestTimer.Start();
                LoadNextLink(); // Load first link immediately
            }
            else
            {
                isHarvesting = false;
                StartHarvestingButton.Content = "Start Harvesting";
                harvestTimer.Stop();
            }
        }

        private void HarvestTimer_Tick(object sender, EventArgs e)
        {
            LoadNextLink();
        }

        private void LoadNextLink()
        {
            if (currentLinkIndex < threadLinks.Count)
            {
                WebView.Source = new Uri(threadLinks[currentLinkIndex]);
                currentLinkIndex++;
            }
            else
            {
                // Finished harvesting all links
                isHarvesting = false;
                StartHarvestingButton.Content = "Start Harvesting";
                harvestTimer.Stop();
            }
        }

        private void NavigateToUrl()
        {
            string url = UrlTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(url))
            {
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    url = "https://" + url;
                }
                WebView.Source = new Uri(url);
            }
        }
    }
}