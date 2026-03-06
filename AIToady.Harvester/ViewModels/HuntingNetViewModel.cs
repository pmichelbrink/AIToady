using AIToady.Harvester.Models;
using System.IO;
using System.Text.Json;

namespace AIToady.Harvester.ViewModels
{
    /// <summary>
    /// Main view model for forum harvesting operations.
    /// Handles thread extraction, page navigation, and content processing for forum sites.
    /// This view model can be used some PHP forums, for customization create a new
    /// view model that inherits from BaseViewModel
    /// </summary>
    public class HuntingNetViewModel : BaseViewModel
    {
        public HuntingNetViewModel()
        {
            MessagesPerPage = 15;
        }
        protected override async Task ExtractForumName(bool skipCategoryPrompt = false)
        {
            try
            {
                string script = @"
                    (function() {
                        let h1Element = document.querySelector('h1.style-inherit.iblock');
                        return h1Element ? h1Element.textContent.trim() : '';
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
        protected override async Task<bool> LoadNextForumPage()
        {
            try
            {
                string script = @"
                    (function() {
                        let nextLink = document.querySelector('div.trow.pagenav a[rel=""next""]');
                        if (nextLink && nextLink.href) {
                            return nextLink.href;
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

                if (result == "not_found")
                {
                    return false;
                }
                else
                {
                    InvokeNavigateRequested(result);
                    return true;
                }
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error checking next page: {ex.Message}");
                return false;
            }
        }
        protected async override Task<bool> CheckIfNextPageExists(int currentPageMessageCount)
        {
            if (currentPageMessageCount < MessagesPerPage)
            {
                AddLogEntry($"Only {currentPageMessageCount} messages on current page, assuming no next page");
                return false;
            }

            try
            {
                string script = @"
                    (function() {
                        let nextLink = document.querySelector('a#mb_pagenext[rel=""next""]');
                        if (nextLink && nextLink.href && !nextLink.href.includes('javascript:void')) {
                            return nextLink.href;
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

                if (result == "not_found")
                {
                    return false;
                }
                else
                {
                    InvokeNavigateRequested(result);
                    return true;
                }
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error checking next page: {ex.Message}");
                return false;
            }
        }
        public override async Task ExecuteLoadThreads()
        {
            try
            {
                // Ensure WebView2 is initialized before executing scripts
                if (InvokeExecuteScriptRequested == null)
                {
                    AddLogEntry("WebView2 not ready. Please navigate to a page first.");
                    return;
                }

                string script = @"
                    (function() {
                        let threads = [];
                        document.querySelectorAll('div.trow.text-center').forEach(row => {
                            let link = row.querySelector('h4.style-inherit a');
                            if (link) {
                                let threadUrl = link.href;
                                let lastPostDate = null;
                                let dateText = row.querySelector('div.tcell.alt2.smallfont div.text-right');
                                if (dateText) {
                                    let dateMatch = dateText.textContent.match(/(\d{2}-\d{2}-\d{4})/);
                                    if (dateMatch) {
                                        lastPostDate = dateMatch[1];
                                    }
                                }
                                threads.push({ url: threadUrl, lastPostDate: lastPostDate });
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
        protected async override Task<string> GetThreadName(string threadUrl)
        {
            string titleScript = @"
                (function() {
                    let h1 = document.querySelector('h1.threadtitle');
                    return h1 ? h1.textContent.trim() : '';
                })()
            ";

            string titleResult = await InvokeExecuteScriptRequested(titleScript);
            if (!string.IsNullOrEmpty(titleResult))
            {
                titleResult = JsonSerializer.Deserialize<string>(titleResult);
            }

            if (string.IsNullOrEmpty(titleResult))
                titleResult = "Unknown Thread";

            var urlParts = threadUrl.TrimEnd('/').Split('/');
            var threadSegment = urlParts.FirstOrDefault(part => part.Contains('-') && part.Split('-')[0].All(char.IsDigit));
            var threadId = threadSegment.Substring(0, threadSegment.IndexOf('-'));
            if (!string.IsNullOrEmpty(threadId))
                titleResult += $"_{threadId}";

            return string.Join("_", titleResult.Split(System.IO.Path.GetInvalidFileNameChars()));
        }
        protected async override Task<List<ForumMessage>> HarvestPage()
        {
            string extractScript = @"
                (function() {
                    let messages = [];
                    document.querySelectorAll('div[id^=""post""]').forEach(postDiv => {
                        let postId = postDiv.id.replace('post', '');
                        let usernameElement = postDiv.querySelector('a.bigusername');
                        let username = usernameElement ? usernameElement.textContent.trim() : 'Unknown';
                        let dateElement = postDiv.querySelector('.trow.thead.smallfont .tcell');
                        let timestamp = dateElement ? dateElement.textContent.trim() : '';
                        let messageBodyElement = postDiv.querySelector('div[id^=""post_message_""]');
                        let images = [];
                        let attachments = [];
                        
                        if (messageBodyElement) {
                            messageBodyElement.querySelectorAll('img').forEach(img => {
                                let imageUrl = img.src;
                                if (imageUrl && !imageUrl.includes('statusicon') && !imageUrl.includes('icons/icon')) {
                                    images.push(imageUrl);
                                }
                            });
                        }
                        
                        if (messageBodyElement) {
                            messages.push({
                                postId: postId,
                                username: username,
                                message: messageBodyElement.textContent.trim().replace(/\s+/g, ' '),
                                timestamp: timestamp,
                                images: images,
                                attachments: attachments
                            });
                        }
                    });
                    return JSON.stringify(messages);
                })()
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
    }
} 