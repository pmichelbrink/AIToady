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
    public class BrianEnosViewModel : BaseViewModel
    {
        public BrianEnosViewModel()
        {
            MessagesPerPage = 15;
        }
        protected override async Task ExtractForumName(bool skipCategoryPrompt = false)
        {
            try
            {
                string script = @"
                    (function() {
                        let headerElement = document.querySelector('h1.ipsType_pageTitle');
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
                        let nextLi = document.querySelector('li.ipsPagination_next:not(.ipsPagination_inactive)');
                        if (nextLi) {
                            let nextLink = nextLi.querySelector('a[rel=""next""]');
                            if (nextLink && nextLink.href) {
                                return nextLink.href;
                            }
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
                        document.querySelectorAll('li.ipsDataItem[data-rowid]').forEach(li => {
                            let link = li.querySelector('.ipsDataItem_title .ipsType_break a[href*=""/topic/""]');
                            if (link) {
                                let threadUrl = link.href;
                                let lastPostDate = null;
                                let timeElement = li.querySelector('.ipsDataItem_lastPoster time');
                                if (timeElement) {
                                    lastPostDate = timeElement.getAttribute('datetime');
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
                    let span = document.querySelector('span.ipsType_break.ipsContained span');
                    return span ? span.textContent.trim() : '';
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
                    document.querySelectorAll('article.cPost[id^=""elComment_""]').forEach(article => {
                        let postId = article.id.replace('elComment_', '');
                        let usernameElement = article.querySelector('.cAuthorPane_author a');
                        let username = usernameElement ? usernameElement.textContent.trim() : 'Unknown';
                        let timeElement = article.querySelector('time[datetime]');
                        let timestamp = timeElement ? timeElement.getAttribute('datetime') : '';
                        let messageBodyElement = article.querySelector('[data-role=""commentContent""]');
                        let images = [];
                        let attachments = [];
                        
                        if (messageBodyElement) {
                            messageBodyElement.querySelectorAll('a.ipsAttachLink').forEach(link => {
                                let attachmentUrl = link.href;
                                if (attachmentUrl) {
                                    attachments.push(attachmentUrl);
                                }
                            });
                            
                            messageBodyElement.querySelectorAll('img').forEach(img => {
                                if (!img.closest('a.ipsAttachLink')) {
                                    let imageUrl = img.getAttribute('data-fullurl') || img.src;
                                    if (imageUrl && !imageUrl.includes('data:image')) {
                                        images.push(imageUrl);
                                    }
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