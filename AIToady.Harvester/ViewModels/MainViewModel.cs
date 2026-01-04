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

            _threadName = thread.ThreadName;

            if (_threadName.Contains('|'))
                _threadName = _threadName.Split('|')[0].Trim();

            // Extract thread ID from URL and append to thread name
            var threadId = threadUrl.TrimEnd('/').Split('.').LastOrDefault();
            if (!string.IsNullOrEmpty(threadId))
                _threadName += $"_{threadId}";

            _threadName = string.Join("_", _threadName.Split(System.IO.Path.GetInvalidFileNameChars()));
            string threadFolder = Path.Combine(_rootFolder, SiteName, ForumName, _threadName);
            
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
                List<ForumMessage> pageMessages = null;
                
                if (threadUrl.Contains("akforum.net"))
                    pageMessages = await HarvestAKForumPage();
                else
                    pageMessages = await HarvestPage();

                if (pageMessages.Count == 0)
                {
                }

                // Extract images and attachments for each message
                await ExtractImagesAndAttachments(thread, threadFolder, pageMessages);

                AddLogEntry($"Page {_threadPageNumber} Harvested");

                bool nextPageExists = false;

                if (threadUrl.Contains("akforum.net"))
                {
                    nextPageExists = await CheckAKForumNextPageExists();
                }
                else
                {
                    // Check if NextElement exists and click it
                    string nextResult = null;
                    string nextScript = $"document.querySelector('{NextElement}') ? 'found' : 'not_found'";
                    try
                    {
                        nextResult = await ExecuteScriptRequested?.Invoke(nextScript);
                        nextResult = JsonSerializer.Deserialize<string>(nextResult);
                        if (nextResult == "found")
                            nextPageExists = true;
                    }
                    catch (TaskCanceledException)
                    {
                        AddLogEntry("Script execution timeout, retrying...");
                        await Task.Delay(2000);
                        try
                        {
                            nextResult = await ExecuteScriptRequested?.Invoke(nextScript);
                            nextResult = JsonSerializer.Deserialize<string>(nextResult);
                        }
                        catch
                        {
                            AddLogEntry("Failed to check next page, assuming no more pages");
                            nextResult = "not_found";
                        }
                    }
                }

                if (nextPageExists)
                {
                    _threadPageNumber++;
                    try
                    {
                        await ExecuteScriptRequested?.Invoke($"document.querySelector('{NextElement}').click();");
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

        private async Task<bool> CheckAKForumNextPageExists()
        {
            try
            {
                string script = "document.querySelector('.pageNav-jump--next[aria-disabled=\"false\"]') ? 'found' : 'not_found'";
                string result = await ExecuteScriptRequested?.Invoke(script);
                result = JsonSerializer.Deserialize<string>(result);
                return result == "found";
            }
            catch
            {
                return false;
            }
        }

        private async Task ExtractImagesAndAttachments(ForumThread thread, string threadFolder, List<ForumMessage> pageMessages)
        {
            // Store current URL to return to after extraction
            string currentUrl = await ExecuteScriptRequested?.Invoke("window.location.href");
            currentUrl = JsonSerializer.Deserialize<string>(currentUrl);
            
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
                        string fileName = GetFileNameFromUrl(i, attachmentUrl);

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

            // Return to the original page after extraction
            NavigateRequested?.Invoke(currentUrl);
            await Task.Delay(1000); // Wait for navigation to complete

            thread.Messages.AddRange(pageMessages);
        }


        private async Task<List<ForumMessage>> HarvestPage()
        {
            string messageSelector = string.IsNullOrEmpty(MessageElement) ? ".message-inner" : MessageElement;
            string extractScript = $@"
                let messages = [];
                document.querySelectorAll('{messageSelector}').forEach(messageDiv => {{
                    let userElement = messageDiv.querySelector('.message-name a');
                    let messageBodyElement = messageDiv.querySelector('.message-body');
                    let timeElement = messageDiv.querySelector('.u-dt');
                    let images = [];
                    let attachments = [];
                    
                    if (messageBodyElement) {{
                        messageBodyElement.querySelectorAll('img.bbImage').forEach(img => {{
                            let imageUrl = img.getAttribute('data-url') || img.src;
                            if (imageUrl && !imageUrl.includes('data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP')) {{
                                images.push(imageUrl);
                            }}
                        }});
                        
                        // Extract all attachment files
                        let attachmentUrls = new Set();
                        messageDiv.querySelectorAll('.attachmentList .attachment a').forEach(element => {{
                            let attachmentUrl = element.href;
                            if (attachmentUrl && attachmentUrl.includes('/attachments/')) {{
                                attachmentUrls.add(attachmentUrl);
                            }}
                        }});
                        attachments = Array.from(attachmentUrls);
                        }}
                    
                    if (userElement && messageBodyElement) {{
                        let postId = '';
                        let currentElement = messageDiv;
                        while (currentElement && !postId) {{
                            postId = currentElement.getAttribute('data-lb-id') || currentElement.getAttribute('id') || '';
                            currentElement = currentElement.parentElement;
                        }}
                        if (postId.startsWith('js-')) {{
                            postId = postId.substring(3);
                        }}
                        messages.push({{
                            postId: postId,
                            username: userElement.textContent.trim(),
                            message: messageBodyElement.textContent.trim().replace(/\s+/g, ' '),
                            timestamp: timeElement ? timeElement.getAttribute('datetime') : '',
                            images: images,
                            attachments: attachments
                        }});
                    }}
                }});
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

        private async Task<List<ForumMessage>> HarvestAKForumPage()
        {
            string extractScript = @"
                let messages = [];
                document.querySelectorAll('.js-quickEditTarget.message-cell-content-wrapper').forEach(messageDiv => {
                    let userElement = messageDiv.querySelector('.MessageCard__user-info__name');
                    let messageBodyElement = messageDiv.querySelector('.message-body');
                    let timeElement = messageDiv.querySelector('.u-dt, time');
                    let images = [];
                    let attachments = [];
                    
                    if (messageBodyElement) {
                        // Extract images from bbImage elements
                        messageBodyElement.querySelectorAll('img.bbImage').forEach(img => {
                            let imageUrl = img.getAttribute('data-url') || img.getAttribute('data-src') || img.src;
                            if (imageUrl && !imageUrl.includes('data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP')) {
                                // Remove query parameters to get clean image URL
                                imageUrl = imageUrl.split('?')[0];
                                if (imageUrl && !images.includes(imageUrl)) {
                                    images.push(imageUrl);
                                }
                            }
                        });
                        
                        // Extract images from lbContainer elements
                        messageDiv.querySelectorAll('.lbContainer-zoomer').forEach(zoomer => {
                            let imageUrl = zoomer.getAttribute('data-src');
                            if (imageUrl) {
                                // Remove query parameters to get clean image URL
                                imageUrl = imageUrl.split('?')[0];
                                if (imageUrl && !images.includes(imageUrl)) {
                                    images.push(imageUrl);
                                }
                            }
                        });
                        
                        // Extract attachment files from attachmentList
                        let attachmentUrls = new Set();
                        messageDiv.querySelectorAll('.attachmentList .attachment .attachment-name a').forEach(element => {
                            let attachmentUrl = element.href;
                            if (attachmentUrl && attachmentUrl.includes('/attachments/')) {
                                attachmentUrls.add(attachmentUrl);
                            }
                        });
                        attachments = Array.from(attachmentUrls);
                    }
                    
                    if (messageBodyElement) {
                        let postId = '';
                        let currentElement = messageDiv;
                        while (currentElement && !postId) {
                            let dataLbId = currentElement.getAttribute('data-lb-id');
                            if (dataLbId && dataLbId.startsWith('post-')) {
                                postId = dataLbId.replace('post-', '');
                                break;
                            }
                            postId = currentElement.getAttribute('id') || '';
                            currentElement = currentElement.parentElement;
                        }
                        if (postId.startsWith('js-')) {
                            postId = postId.substring(3);
                        }
                        
                        let username = userElement ? userElement.textContent.trim() : 'Unknown User';
                        let timestamp = timeElement ? (timeElement.getAttribute('datetime') || timeElement.getAttribute('title') || timeElement.textContent) : '';
                        
                        messages.push({
                            postId: postId,
                            username: username,
                            message: messageBodyElement.textContent.trim().replace(/\\s+/g, ' '),
                            timestamp: timestamp,
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