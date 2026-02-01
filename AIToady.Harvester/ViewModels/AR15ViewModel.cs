using AIToady.Harvester.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AIToady.Harvester.ViewModels
{
    public class AR15ViewModel : BaseViewModel
    {
        public AR15ViewModel()
        {
            
        }

        protected override bool IsBoardPage(string url)
        {
            if (url.Contains("page=", StringComparison.OrdinalIgnoreCase))
                return false;
            if (url.Contains("/board.html", StringComparison.OrdinalIgnoreCase))
                return true;
            if (url.Contains("/forum.html", StringComparison.OrdinalIgnoreCase))
                return false;
            if (url.TrimEnd('/').Split('/').LastOrDefault()?.All(char.IsDigit) == true)
                return false;
            else
                return true;
        }

        protected override async Task LoadForumLinksFromBoard()
        {
            try
            {
                string script = @"
                    let forumLinks = [];
                    document.querySelectorAll('a[href*=""/forums/""]').forEach(a => {
                        let href = a.getAttribute('href');
                        if (href && (href.match(/\/forums\/[^\/]+\/[^\/]+\/\d+\/$/) || href.includes('/archive/forum.html'))) {
                            forumLinks.push(a.href);
                        }
                    });
                    JSON.stringify([...new Set(forumLinks)]);
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
        protected override async Task ExtractForumName(bool skipCategoryPrompt = false)
        {
            try
            {
                string script;
                if (Url.Contains("/archive/"))
                {
                    script = @"
                        (function() {
                            let parentDiv = document.querySelector('div.expanded.column.row.skin-dark.white.barpad');
                            if (parentDiv) {
                                let childDiv = parentDiv.querySelector('div.column');
                                return childDiv ? childDiv.textContent.trim() : '';
                            }
                            return '';
                        })()
                    ";
                }
                else
                {
                    script = @"
                        (function() {
                            let h5 = document.querySelector('h5.tw-text-base.tw-font-semibold.lg\\:tw-h5');
                            return h5 ? h5.textContent.trim() : '';
                        })()
                    ";
                }

                string result = await InvokeExecuteScriptRequested(script);
                if (!string.IsNullOrEmpty(result))
                {
                    result = JsonSerializer.Deserialize<string>(result);
                    if (!string.IsNullOrEmpty(result))
                    {
                        if (!skipCategoryPrompt)
                            Category = await PromptUserInput("Enter Category (optional):");

                        ForumName = string.Join("_", result.Split(Path.GetInvalidFileNameChars()));
                    }
                }
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

                // Debug: Get HTML
                //string htmlScript = "document.documentElement.outerHTML";
                //string html = await InvokeExecuteScriptRequested(htmlScript);
                //html = JsonSerializer.Deserialize<string>(html);
                //string debugPath = Path.Combine(Path.GetTempPath(), "webview_debug.html");
                //File.WriteAllText(debugPath, html);
                //AddLogEntry($"HTML saved to: {debugPath}");

                string script;
                if (!Url.Contains("/archive/"))
                {
                    script = @"
                        let threads = [];
                        let anchors = document.querySelectorAll('a.tw-align-middle.tw-text-\\[0\\.9rem\\]');
                        anchors.forEach(a => {
                            if (a.href && a.href.includes('/forums/')) {
                                let threadUrl = a.href;
                                let lastPostDate = null;
                                let container = a.closest('div.tw-relative.tw-rounded');
                                if (container) {
                                    let divs = container.querySelectorAll('div');
                                    divs.forEach(div => {
                                        if (div.textContent.includes('Last Post:')) {
                                            let span = div.querySelector('span.tw-font-bold');
                                            if (span) {
                                                lastPostDate = span.textContent.trim();
                                            }
                                        }
                                    });
                                }
                                threads.push({ url: threadUrl, lastPostDate: lastPostDate });
                            }
                        });
                        JSON.stringify(threads);
                    ";
                }
                else
                {
                    script = @"
                    let threads = [];
                    let uls = document.querySelectorAll('ul');
                    uls.forEach(ul => {
                        let anchors = ul.querySelectorAll('li a');
                        anchors.forEach(a => {
                            if (a.href && a.href.includes('/forums/')) {
                                let threadUrl = a.href;
                                let lastPostDate = null;
                                let li = a.closest('li');
                                if (li) {
                                    let italic = li.querySelector('i');
                                    if (italic) {
                                        let text = italic.textContent.trim();
                                        let match = text.match(/\((.*?)\s+-/);
                                        if (match) {
                                            lastPostDate = match[1];
                                        }
                                    }
                                }
                                threads.push({ url: threadUrl, lastPostDate: lastPostDate });
                            }
                        });
                    });
                    JSON.stringify(threads);
                ";
                }

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
            // Get thread name from page title
            string titleScript = "document.title";
            string titleResult = await InvokeExecuteScriptRequested(titleScript);
            titleResult = JsonSerializer.Deserialize<string>(titleResult) ?? "Unknown Thread";

            // Extract only the first part before " > "
            if (titleResult.Contains(" > "))
                titleResult = titleResult.Split(" > ")[0].Trim();

            // Extract thread ID from URL (e.g., "51-170096" from the last segment)
            var threadId = threadUrl.TrimEnd('/').Split('/').LastOrDefault();
            if (!string.IsNullOrEmpty(threadId))
                titleResult += $"_{threadId}";

            return string.Join("_", titleResult.Split(System.IO.Path.GetInvalidFileNameChars()));
        }



        protected async override Task<bool> CheckIfNextPageExists()
        {
            try
            {
                string script = @"
            (function() {
                let links = document.querySelectorAll('a[href*=""?page=""]');
                for (let link of links) {
                    let span = link.querySelector('span');
                    if (span && span.textContent.trim() === 'Next Page') {
                        return link.getAttribute('href');
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
                    // It's a link URL, construct full URL and navigate
                    string currentUrl = await InvokeExecuteScriptRequested("window.location.origin");
                    currentUrl = JsonSerializer.Deserialize<string>(currentUrl);
                    string fullUrl = currentUrl + result;
                    InvokeNavigateRequested(fullUrl);
                    return true;
                }
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error checking next page: {ex.Message}");
                return false;
            }
        }
        protected override async Task<bool> LoadNextForumPage()
        {
            string nextScript = @"
                (function() {
                    let links = document.querySelectorAll('a');
                    for (let link of links) {
                        if (link.textContent.trim() === 'Next »' || link.textContent.includes('Next')) {
                            return link.getAttribute('href');
                        }
                    }
                    return 'not_found';
                })()
            ";
            string nextResult = await InvokeExecuteScriptRequested(nextScript);
            nextResult = JsonSerializer.Deserialize<string>(nextResult);

            if (nextResult == "not_found")
            {
                return false;
            }

            // Navigate to the next page
            string currentUrl = await InvokeExecuteScriptRequested("window.location.origin");
            currentUrl = JsonSerializer.Deserialize<string>(currentUrl);
            string fullUrl = currentUrl + nextResult;
            InvokeNavigateRequested(fullUrl);
            return true;
        }
        protected async override Task<List<ForumMessage>> HarvestPage()
        {
            string extractScript = @"
                (function() {
                    let messages = [];

                    document.querySelectorAll('div.tw-rounded-sm.tw-p-2').forEach(messageDiv => {
                        let timeElement = messageDiv.querySelector('strong [itemprop=""datePublished""]');
                        let messageBodyElement = messageDiv.querySelector('td.tw-thread__body');
                        let postIdLink = messageDiv.querySelector('a[id^=""i""]');
                        let images = [];
                        let attachments = [];

                        if (messageBodyElement) {
                            // Extract images
                            messageBodyElement.querySelectorAll('img').forEach(img => {
                                let imageUrl = img.src;
                                if (imageUrl && !imageUrl.includes('data:image') && !imageUrl.includes('clear.gif')) {
                                    imageUrl = imageUrl.split('?')[0];
                                    if (!images.includes(imageUrl)) {
                                        images.push(imageUrl);
                                    }
                                }
                            });

                            let timestamp = timeElement ? timeElement.getAttribute('content') : '';
                            let postId = postIdLink ? postIdLink.getAttribute('id') : '';

                            messages.push({
                                postId: postId,
                                username: 'ArchivedUser',
                                message: messageBodyElement.textContent.trim().replace(/\\s+/g, ' '),
                                timestamp: timestamp,
                                images: images,
                                attachments: attachments
                            });
                        }
                    });

                    return JSON.stringify(messages);
                })()
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
