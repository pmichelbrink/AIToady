using AIToady.Harvester.Models;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace AIToady.Harvester.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        protected override async Task<ForumThread> HarvestThread(string threadUrl)
        {
            _threadImageCounter = 1;
            _threadPageNumber = 1;
            InvokeNavigateRequested(threadUrl);
            await Task.Delay(GetRandomizedDelay());
            
            var thread = new ForumThread();
            
            // Get thread name from page title
            string titleScript = "document.title";
            string titleResult = await InvokeExecuteScriptRequested(titleScript);
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
                    nextPageExists = await CheckIfNextPageExists();
                }

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

        private async Task<bool> CheckAKForumNextPageExists()
        {
            try
            {
                string script = @"
                    (function() {
                        let nextButton = document.querySelector('.pageNav-jump--next[aria-disabled=""false""]');
                        if (nextButton) {
                            nextButton.click();
                            return 'clicked';
                        }
                        return 'not_found';
                    })()
                ";
                string result = await InvokeExecuteScriptRequested(script);
                
                if (string.IsNullOrEmpty(result))
                {
                    AddLogEntry("Script returned null result, assuming no next page");
                    return false;
                }
                
                result = JsonSerializer.Deserialize<string>(result);
                return result == "clicked";
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error checking next page: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> CheckIfNextPageExists()
        {
            try
            {
                string script = @"
                    (function() {
                        let nextButton = document.querySelector('.pageNav-jump.pageNav-jump--next');
                        if (nextButton) {
                            nextButton.click();
                            return 'clicked';
                        }
                        return 'not_found';
                    })()
                ";
                string result = await InvokeExecuteScriptRequested(script);
                
                if (string.IsNullOrEmpty(result))
                {
                    return false;
                }
                
                result = JsonSerializer.Deserialize<string>(result);
                return result == "clicked";
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error checking next page: {ex.Message}");
                return false;
            }
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
            
            string result = await InvokeExecuteScriptRequested(extractScript);
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
                document.querySelectorAll('.MessageCard__container').forEach(messageDiv => {
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
            
            try
            {
                string result = await InvokeExecuteScriptRequested(extractScript);
                if (!string.IsNullOrEmpty(result))
                {
                    result = System.Text.Json.JsonSerializer.Deserialize<string>(result);
                    return System.Text.Json.JsonSerializer.Deserialize<List<ForumMessage>>(result) ?? new List<ForumMessage>();
                }
            }
            catch (TaskCanceledException)
            {
                AddLogEntry("Script execution timeout in HarvestAKForumPage, returning empty list");
            }
            catch
            {
                // Silent catch for other exceptions
            }
            
            return new List<ForumMessage>();
        }
    }
} 