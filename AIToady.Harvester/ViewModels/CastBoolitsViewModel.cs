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
    public class CastBoolitsViewModel : BaseViewModel
    {
        protected override async Task ExtractForumName(bool skipCategoryPrompt = false)
        {
            try
            {
                string script = @"
                    (function() {
                        let forumTitle = document.querySelector('span.forumtitle');
                        if (forumTitle) return forumTitle.textContent.trim();
                        return '';
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
                        document.querySelectorAll('li.threadbit').forEach(li => {
                            let anchor = li.querySelector('a.title');
                            if (anchor) {
                                let url = anchor.href;
                                let latestDate = null;
                                let timeElement = li.querySelector('.threadlastpost .time');
                                if (timeElement) {
                                    latestDate = timeElement.textContent.trim();
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
        protected override async Task<string> GetThreadName(string threadUrl)
        {
            string titleScript = @"
                (function() {
                    let titleElement = document.querySelector('.ev_container b');
                    if (titleElement) return titleElement.textContent.trim();
                    return document.title;
                })()
            ";
            string titleResult = await InvokeExecuteScriptRequested(titleScript);
            titleResult = JsonSerializer.Deserialize<string>(titleResult);
            titleResult = titleResult.Split(" - The Handloaders Bench")[0].Trim('"');
            _threadName = titleResult;

            // Extract thread ID from URL and append to thread name
            var match = System.Text.RegularExpressions.Regex.Match(threadUrl, @"\?(\d+)-");
            var threadId = match.Success ? match.Groups[1].Value : null;
            if (!string.IsNullOrEmpty(threadId))
                _threadName += $"_{threadId}";

            _threadName = string.Join("_", _threadName.Split(System.IO.Path.GetInvalidFileNameChars()));

            if (_threadName.EndsWith("_unread"))
                _threadName = _threadName.Substring(0, _threadName.Length - 7);

            return _threadName;
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
                        let nextButton = document.querySelector('span.prev_next a[rel=""next""]');
                        if (nextButton) {
                            nextButton.click();
                            return 'clicked';
                        }
                        return 'not_found';
                    })()
                ";

                string result = JsonSerializer.Deserialize<string>(await InvokeExecuteScriptRequested(script));
                return result == "clicked";
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error loading next forum page: {ex.Message}");
                return false;
            }
        }

        protected async override Task<List<ForumMessage>> HarvestPage()
        {
            string extractScript = @"
                let messages = [];
                document.querySelectorAll('li.postbitlegacy').forEach(post => {
                    let postId = post.id.replace('post_', '');
                    
                    let usernameLink = post.querySelector('.username_container a.username strong');
                    let username = usernameLink ? usernameLink.textContent.trim() : '';
                    
                    let dateElement = post.querySelector('.postdate .date');
                    let timestamp = dateElement ? dateElement.textContent.trim() : '';
                    
                    let contentDiv = post.querySelector('.postcontent');
                    let message = contentDiv ? contentDiv.textContent.trim().replace(/\s+/g, ' ') : '';
                    
                    let images = [];
                    if (contentDiv) {
                        contentDiv.querySelectorAll('img').forEach(img => {
                            if (img.src && !img.src.includes('clear.gif')) {
                                images.push(img.src);
                            }
                        });
                    }
                    
                    let attachments = [];
                    let attachmentsDiv = post.querySelector('.attachments');
                    if (attachmentsDiv) {
                        attachmentsDiv.querySelectorAll('a[href*=""attachment.php""]').forEach(a => {
                            if (!a.href.includes('thumb=1')) {
                                attachments.push(a.href);
                            }
                        });
                    }
                    
                    messages.push({
                        postId: postId,
                        username: username,
                        message: message,
                        timestamp: timestamp,
                        images: images,
                        attachments: attachments
                    });
                });
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
        protected override bool IsBoardPage(string url)
        {
            if (url.Contains("/forums/bullet-tests.41", StringComparison.OrdinalIgnoreCase))
                return true;
            else
                return false;
        }
        protected override async Task LoadForumLinksFromBoard()
        {
            try
            {
                string script = @"
                    let links = [];
                    document.querySelectorAll('a[href*=""/forums/""]').forEach(a => {
                        if (a.href.match(/\/forums\/[^\/]+\/?$/)) {
                            links.push(a.href);
                        }
                    });
                    JSON.stringify([...new Set(links)]);
                ";
                
                string result = await InvokeExecuteScriptRequested(script);
                result = JsonSerializer.Deserialize<string>(result);
                var links = JsonSerializer.Deserialize<string[]>(result);

                foreach (var link in links)
                {
                    if (!_scheduledForums.Contains(link))
                    {
                        _scheduledForums.Add(link);
                    }
                }

                SaveSettings();
                AddLogEntry($"Added {links.Length} forums to schedule");
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error loading forum links from board: {ex.Message}");
            }
        }
    }
} 