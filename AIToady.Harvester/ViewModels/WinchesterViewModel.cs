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
    public class WinchesterViewModel : BaseViewModel
    {
        protected override async Task ExtractForumName(bool skipCategoryPrompt = false)
        {
            try
            {
                string script = @"
                    (function() {
                        let forumTitle = document.querySelector('div#spForumHeaderName.spHeaderName');
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
                        document.querySelectorAll('div.spForumTopicSection').forEach(div => {
                            let anchor = div.querySelector('a.spRowName');
                            if (anchor) {
                                let lastPostLabels = div.querySelectorAll('.spInRowPostLink .spInRowLabel');
                                let dateEl = lastPostLabels.length ? lastPostLabels[lastPostLabels.length - 1] : null;
                                threads.push({ url: anchor.href, lastPostDate: dateEl ? dateEl.textContent.trim() : null });
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
                    let titleElement = document.querySelector('div#spTopicHeaderName.spHeaderName');
                    let title = titleElement ? titleElement.textContent.trim() : document.title;
                    let topicEl = document.querySelector('div.spTopicViewSection[id^=""eachTopic""]');
                    let topicId = topicEl ? topicEl.id.replace('eachTopic', '') : '';
                    return JSON.stringify({ title, topicId });
                })()
            ";
            string titleResult = await InvokeExecuteScriptRequested(titleScript);
            var parsed = JsonSerializer.Deserialize<System.Text.Json.JsonElement>(JsonSerializer.Deserialize<string>(titleResult));
            string title = parsed.GetProperty("title").GetString();
            string topicId = parsed.GetProperty("topicId").GetString();
            _threadName = string.Join("_", title.Split(System.IO.Path.GetInvalidFileNameChars()));
            if (!string.IsNullOrEmpty(topicId))
                _threadName += $"_{topicId}";
            return _threadName;
        }
        protected override async Task<bool> LoadNextForumPage()
        {
            //return await CheckIfNextPageExists(999);
            //await Task.Delay(2000);
            return await ClickNextForumPageButton();
        }
        private async Task<bool> ClickNextForumPageButton()
        {
            try
            {
                string script = @"
                    (function() {
                        let jump = document.querySelector('a.spForumPageJump[data-site]');
                        if (!jump) return 'not_found';
                        let maxMatch = jump.getAttribute('data-site').match(/max=(\d+)/);
                        if (!maxMatch) return 'not_found';
                        let max = parseInt(maxMatch[1]);
                        let pageMatch = location.pathname.match(/page-(\d+)/);
                        let current = pageMatch ? parseInt(pageMatch[1]) : 1;
                        if (current >= max) return 'not_found';
                        let base = location.pathname.replace(/\/page-\d+\/?$/, '').replace(/\/$/, '');
                        let nextUrl = location.origin + base + '/page-' + (current + 1) + '/';
                        location.href = nextUrl;
                        return 'clicked';
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
                        let jump = document.querySelector('a.spTopicPageJump[data-site]');
                        if (!jump) return 'not_found';
                        let maxMatch = jump.getAttribute('data-site').match(/max=(\d+)/);
                        if (!maxMatch) return 'not_found';
                        let max = parseInt(maxMatch[1]);
                        let pageMatch = location.pathname.match(/page-(\d+)/);
                        let current = pageMatch ? parseInt(pageMatch[1]) : 1;
                        if (current >= max) return 'not_found';
                        let base = location.pathname.replace(/\/page-\d+\/?$/, '').replace(/\/$/, '');
                        let nextUrl = location.origin + base + '/page-' + (current + 1) + '/';
                        location.href = nextUrl;
                        return 'clicked';
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
                document.querySelectorAll('div.spTopicPostSection[id^=""eachPost""]').forEach(post => {
                    let postId = post.id.replace('eachPost', '');

                    let nameEl = post.querySelector('.spPostUserName');
                    let username = nameEl ? nameEl.textContent.trim() : '';

                    let timeEl = post.querySelector('.spPostUserDate');
                    let timestamp = timeEl ? timeEl.textContent.trim() : '';

                    let contentEl = post.querySelector('.spPostContent');
                    let message = contentEl ? contentEl.textContent.trim().replace(/\s+/g, ' ') : '';

                    let images = [];
                    let attachments = [];
                    if (contentEl) {
                        contentEl.querySelectorAll('img[src]').forEach(img => {
                            let src = img.src;
                            if (!src || src.includes('/sp-resources/forum-themes/') || src.includes('/sp-resources/forum-plugins/')) return;
                            let title = img.getAttribute('title') || '';
                            images.push(title ? src + '|' + title : src);
                        });
                        post.querySelectorAll('.spPostIndexAttachments a[href]').forEach(a => {
                            if (!a.querySelector('img')) attachments.push(a.href);
                        });
                    }

                    messages.push({ postId, username, message, timestamp, images, attachments });
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