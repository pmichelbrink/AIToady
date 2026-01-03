using AIToady.Harvester.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Timers;

namespace AIToady.Harvester.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        public event Action<string> NavigateRequested;
        public event Func<string, Task<string>> ExecuteScriptRequested;
        public event Func<string, string, Task> ExtractImageRequested;
        public event Func<string, string, Task> ExtractAttachmentRequested;


        public MainViewModel()
        {
        }


        protected override void ExecuteGo()
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

        protected async void ExecuteNext()
        {
            if (!string.IsNullOrEmpty(NextElement))
            {
                await ExecuteScriptRequested?.Invoke($"document.querySelector('{NextElement}').click();");
            }
        }

        private async Task ExecuteLoadThreads()
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
                                if (a.href && a.href.includes('/threads/')) links.push(a.href);
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

        private int GetPageNumberFromUrl(string url)
        {
            var match = System.Text.RegularExpressions.Regex.Match(url, @"/page-(\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 1;
        }

        protected override async void ExecuteStartHarvesting()
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

            _isHarvesting = true;
            HarvestingButtonText = "Stop Harvesting";
            AddLogEntry($"- - - - - Starting Forum Page {GetPageNumberFromUrl(Url)} - - - - -");

            // Get forum name from h1.p-title-value element
            string forumScript = "document.querySelector('h1.p-title-value')?.textContent?.trim() || 'Unknown Forum'";
            string forumResult = await ExecuteScriptRequested?.Invoke(forumScript);
            _forumName = JsonSerializer.Deserialize<string>(forumResult) ?? "Unknown Forum";
            _forumName = string.Join("_", _forumName.Split(System.IO.Path.GetInvalidFileNameChars()));

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
                        NavigateRequested?.Invoke(Url);
                        return;
                    }
                }

                ThreadsToSkip = 0;

                await LoadForumPage();

                // Check if Next element exists and click it, if it 
                // doesn't exist, we are on the last page
                string nextScript = $"document.querySelector('{NextElement}') ? 'found' : 'not_found'";
                string nextResult = await ExecuteScriptRequested?.Invoke(nextScript);
                nextResult = JsonSerializer.Deserialize<string>(nextResult);

                if (nextResult == "found")
                {
                    if (_stopAfterCurrentPage)
                    {
                        hasNextForumPage = false;
                        AddLogEntry("Stopping after current page as requested");
                    }
                    else
                    {
                        //Load the next page by clicking the Next element
                        await ExecuteScriptRequested?.Invoke($"document.querySelector('{NextElement}').click();");

                        //Wait for navigation to complete
                        await Task.Delay(GetRandomizedDelay());

                        Url = await ExecuteScriptRequested?.Invoke("window.location.href");
                        Url = JsonSerializer.Deserialize<string>(Url);
                        AddLogEntry($"- - - - - Starting Forum Page {GetPageNumberFromUrl(Url)} - - - - -");
                        SaveSettings();
                    }
                }
                else
                {
                    hasNextForumPage = false;
                }
            }

            if (_isHarvesting)
            {
                MessageBox.Show($"Harvesting complete.", "Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                _isHarvesting = false;
                HarvestingButtonText = "Start Harvesting";
            }
        }

        private async Task LoadForumPage()
        {
            //Load the forum page (a thread page is currently loaded) and
            //wait for navigation to complete by checking URL
            NavigateRequested?.Invoke(Url);
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
            _threadPageNumber = 1;
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
                _threadName = $"{thread.ThreadName.Split(" | ").First().Trim()}";
                _siteName = thread.ThreadName.Split(" | ").Last().Trim();
            }

            // Extract thread ID from URL and append to thread name
            var threadId = threadUrl.TrimEnd('/').Split('.').LastOrDefault();
            if (!string.IsNullOrEmpty(threadId))
                _threadName += $"_{threadId}";

            _threadName = string.Join("_", _threadName.Split(System.IO.Path.GetInvalidFileNameChars()));
            string threadFolder = Path.Combine(_rootFolder, _siteName, _forumName, _threadName);
            
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
                var pageMessages = await HarvestPage();

                if (pageMessages.Count == 0)
                {
                }

                // Extract images and attachments for each message
                await ExtractImagesAndAttachments(thread, threadFolder, pageMessages);

                AddLogEntry($"Page {_threadPageNumber} Harvested");

                // Check if NextElement exists and click it
                string nextScript = $"document.querySelector('{NextElement}') ? 'found' : 'not_found'";
                string nextResult = await ExecuteScriptRequested?.Invoke(nextScript);
                nextResult = JsonSerializer.Deserialize<string>(nextResult);

                if (nextResult == "found")
                {
                    _threadPageNumber++;
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

        private async Task ExtractImagesAndAttachments(ForumThread thread, string threadFolder, List<ForumMessage> pageMessages)
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
                        string fileName = GetFileNameFromUrl(i, imageUrl);

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
                
                // Process attachments
                var attachmentNames = new List<string>();
                for (int i = 0; i < message.Attachments.Count; i++)
                {
                    try
                    {
                        string attachmentUrl = message.Attachments[i];
                        string fileName = GetFileNameFromUrl(i, attachmentUrl);

                        if (!System.IO.Directory.Exists(attachmentsFolder))
                            System.IO.Directory.CreateDirectory(attachmentsFolder);

                        string attachmentPath = System.IO.Path.Combine(attachmentsFolder, fileName);
                        await ExtractAttachmentRequested?.Invoke(attachmentUrl, attachmentPath);
                        attachmentNames.Add(fileName);
                    }
                    catch { }
                }
                message.Attachments = attachmentNames;
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
                    let attachments = [];
                    
                    if (messageBodyElement) {
                        messageBodyElement.querySelectorAll('img.bbImage').forEach(img => {
                            let imageUrl = img.getAttribute('data-url') || img.src;
                            if (imageUrl && !imageUrl.includes('data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP')) {
                                images.push(imageUrl);
                            }
                        });
                        
                        // Extract all attachment files
                        let attachmentUrls = new Set();
                        messageDiv.querySelectorAll('.attachmentList .attachment a').forEach(element => {
                            let attachmentUrl = element.href;
                            if (attachmentUrl && attachmentUrl.includes('/attachments/')) {
                                attachmentUrls.add(attachmentUrl);
                            }
                        });
                        attachments = Array.from(attachmentUrls);
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
                            images: images,
                            attachments: attachments
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
    }
}