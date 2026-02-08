using AIToady.Harvester.Models;
using System.Text.Json;

namespace AIToady.Harvester.ViewModels
{
    public class HighRoadViewModel : BaseViewModel
    {
        public HighRoadViewModel()
        {
            MessagesPerPage = 25;
        }
        protected override async Task ExtractForumName(bool skipCategoryPrompt = false)
        {
            SiteName = "The High Road";

            try
            {
                string script = @"
                    (function() {
                        let headerElement = document.querySelector('h1.p-title-value');
                        return headerElement ? headerElement.textContent.trim() : '';
                    })()
                ";
                string result = await InvokeExecuteScriptRequested(script);
                if (!string.IsNullOrEmpty(result))
                {
                    result = JsonSerializer.Deserialize<string>(result);
                    if (!string.IsNullOrEmpty(result))
                        ForumName = string.Join("_", result.Split(Path.GetInvalidFileNameChars()));
                }

                Category = string.Empty;
            }
            catch { }
        }
        public override async Task ExecuteLoadThreads()
        {
            try
            {
                if (InvokeExecuteScriptRequested == null)
                {
                    AddLogEntry("WebView2 not ready. Please navigate to a page first.");
                    return;
                }

                string script = @"
                    (function() {
                        let threads = [];
                        document.querySelectorAll('.structItem-title').forEach(div => {
                            let anchor = div.querySelector('a[href*=""threads""]');
                            if (anchor) {
                                let url = anchor.href;
                                let latestDate = null;
                                let structItem = div.closest('.structItem');
                                if (structItem) {
                                    let timeElement = structItem.querySelector('.structItem-latestDate');
                                    if (timeElement) {
                                        latestDate = timeElement.getAttribute('datetime');
                                    }
                                }
                                threads.push({ url: url, lastPostDate: latestDate });
                            }
                        });
                        return JSON.stringify(threads);
                    })()
                ";

                string result = await InvokeExecuteScriptRequested(script);
                result = JsonSerializer.Deserialize<string>(result);
                var threads = JsonSerializer.Deserialize<ThreadInfo[]>(result);
                _threadInfos.Clear();
                _threadInfos.AddRange(threads);
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error loading threads: {ex.Message}");
            }
        }
        protected async override Task<List<ForumMessage>> HarvestPage()
        {
            string messageSelector = string.IsNullOrEmpty(MessageElement) ? ".message-inner" : MessageElement;
            string extractScript = $@"
                let messages = [];
                document.querySelectorAll('{messageSelector}').forEach(messageDiv => {{
                    let userElement = messageDiv.querySelector('.message-name a') || messageDiv.querySelector('.message-name .username');
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
                        messageDiv.querySelectorAll('.file-preview').forEach(element => {{
                            let attachmentUrl = element.href;
                            if (attachmentUrl && attachmentUrl.includes('attachments')) {{
                                attachments.push(attachmentUrl);
                            }}
                        }});
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
                    result = JsonSerializer.Deserialize<string>(result);
                    return JsonSerializer.Deserialize<List<ForumMessage>>(result) ?? new List<ForumMessage>();
                }
                catch
                {
                    return new List<ForumMessage>();
                }
            }

            return new List<ForumMessage>();
        }
        protected async override Task<bool> CheckIfNextPageExists(int currentPageMessageCount)
        {
            // If there are fewer than MessagesPerPage messages on the current page, assume it's the last/only page
            if (currentPageMessageCount < MessagesPerPage)
            {
                AddLogEntry($"Only {currentPageMessageCount} messages on current page, assuming no next page");
                return false;
            }

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

                int[] delays = { 2000, 5000 };

                for (int i = 0; i < delays.Length; i++)
                {
                    string result = JsonSerializer.Deserialize<string>(await InvokeExecuteScriptRequested(script));

                    if (result == "clicked")
                        return true;

                    AddLogEntry($"CheckIfNextPageExists - Next button not found, refreshing page and waiting {delays[i] / 1000} seconds...");
                    await InvokeExecuteScriptRequested("location.reload();");
                    await Task.Delay(delays[i]);
                }

                AddLogEntry("CheckIfNextPageExists - Next button not found after all retries");
                return false;
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error checking next page: {ex.Message}");
                return false;
            }
        }
    }
}
