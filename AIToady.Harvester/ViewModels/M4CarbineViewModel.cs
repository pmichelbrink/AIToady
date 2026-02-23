using AIToady.Harvester.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AIToady.Harvester.ViewModels
{
    public class M4CarbineViewModel : BaseViewModel
    {
        public M4CarbineViewModel()
        {
            MessagesPerPage = 50;
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
                string script;
                if (Url.Contains("/archive/"))
                {
                    script = @"
                        let forumLinks = [];
                        document.querySelectorAll('ul li a[href*=""/archive/forum.html""]').forEach(a => {
                            forumLinks.push(a.href);
                        });
                        JSON.stringify([...new Set(forumLinks)]);
                    ";
                }
                else
                {
                    script = @"
                        let forumLinks = [];
                        document.querySelectorAll('a[href*=""/forums/""]').forEach(a => {
                            let href = a.getAttribute('href');
                            if (href && (href.match(/\/forums\/[^\/]+\/[^\/]+\/\d+\/$/) || href.includes('/archive/forum.html'))) {
                                forumLinks.push(a.href);
                            }
                        });
                        JSON.stringify([...new Set(forumLinks)]);
                    ";
                }

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
                string script = @"
                    (function() {
                        let details = document.querySelector('details.category-breadcrumb__subcategory-selector');
                        if (details) {
                            let summary = details.querySelector('summary[data-name]');
                            return summary ? summary.getAttribute('data-name') : '';
                        }
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

                string getThreadsScript = @"
                    (function() {
                        let threads = [];
                        let rows = document.querySelectorAll('tr.topic-list-item[data-topic-id]');
                        rows.forEach(row => {
                            let link = row.querySelector('a.raw-topic-link[href]');
                            if (link) {
                                let threadUrl = link.href;
                                let lastPostDate = null;
                                let activityCell = row.querySelector('td.activity span.relative-date');
                                if (activityCell) {
                                    lastPostDate = activityCell.textContent.trim();
                                }
                                threads.push({ url: threadUrl, lastPostDate: lastPostDate });
                            }
                        });
                        return JSON.stringify(threads);
                    })()
                ";

                int previousCount = 0;
                int unchangedCount = 0;
                
                while (true)
                {
                    await InvokeExecuteScriptRequested("window.scrollTo(0, document.body.scrollHeight);");
                    await Task.Delay(3000);
                    
                    string result = await InvokeExecuteScriptRequested(getThreadsScript);
                    if (string.IsNullOrEmpty(result))
                    {
                        AddLogEntry("Script returned null, stopping scroll");
                        break;
                    }
                    
                    result = JsonSerializer.Deserialize<string>(result);
                    var threads = JsonSerializer.Deserialize<ThreadInfo[]>(result);
                    
                    if (threads.Length == previousCount)
                    {
                        unchangedCount++;
                        if (unchangedCount >= 2)
                            break;
                    }
                    else
                    {
                        unchangedCount = 0;
                        AddLogEntry($"Loaded {threads.Length} threads...");
                    }
                    
                    previousCount = threads.Length;
                }

                string finalResult = await InvokeExecuteScriptRequested(getThreadsScript);
                if (!string.IsNullOrEmpty(finalResult))
                {
                    finalResult = JsonSerializer.Deserialize<string>(finalResult);
                    var finalThreads = JsonSerializer.Deserialize<ThreadInfo[]>(finalResult);
                    _threadInfos.Clear();
                    _threadInfos.AddRange(finalThreads);
                    AddLogEntry($"Finished loading {finalThreads.Length} threads");
                }
                else
                {
                    AddLogEntry("Final script returned null");
                }
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
                    let h1 = document.querySelector('h1[data-topic-id]');
                    if (h1) {
                        let link = h1.querySelector('a.fancy-title');
                        if (link) {
                            return link.textContent.trim();
                        }
                    }
                    return '';
                })()
            ";
            
            string titleResult = await InvokeExecuteScriptRequested(titleScript);
            if (!string.IsNullOrEmpty(titleResult))
            {
                titleResult = JsonSerializer.Deserialize<string>(titleResult);
            }
            
            if (string.IsNullOrEmpty(titleResult))
                titleResult = "Unknown Thread";

            var threadId = threadUrl.TrimEnd('/').Split('/').LastOrDefault();
            if (!string.IsNullOrEmpty(threadId))
                titleResult += $"_{threadId}";

            return string.Join("_", titleResult.Split(System.IO.Path.GetInvalidFileNameChars()));
        }



        protected async override Task<bool> CheckIfNextPageExists(int currentPageMessageCount)
        {
            //This forum has infinite scrolling, so we rely on HarvestPage to scroll and load more messages
            //until it detects no new messages are loaded. Therefore, we can just return false here to
            //indicate that there are no traditional "pages" to navigate through.
            return false;
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

                    document.querySelectorAll('article.boxed[data-post-id]').forEach(article => {
                        let postId = article.getAttribute('data-post-id');
                        let usernameElement = article.querySelector('.names a[data-user-card]');
                        let username = usernameElement ? usernameElement.textContent.trim() : 'Unknown';
                        let timeElement = article.querySelector('.post-date span.relative-date');
                        let timestamp = timeElement ? timeElement.getAttribute('title') : '';
                        let messageBodyElement = article.querySelector('.cooked');
                        let images = [];
                        let attachments = [];

                        if (messageBodyElement) {
                            messageBodyElement.querySelectorAll('img').forEach(img => {
                                let imageUrl = img.src;
                                if (imageUrl && !imageUrl.includes('data:image')) {
                                    imageUrl = imageUrl.split('?')[0];
                                    if (!images.includes(imageUrl)) {
                                        images.push(imageUrl);
                                    }
                                }
                            });

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

            try
            {
                var allMessagesDict = new Dictionary<string, ForumMessage>();
                
                while (true)
                {
                    string result = await InvokeExecuteScriptRequested(extractScript);
                    if (!string.IsNullOrEmpty(result))
                    {
                        result = JsonSerializer.Deserialize<string>(result);
                        var messages = JsonSerializer.Deserialize<List<ForumMessage>>(result);
                        
                        foreach (var msg in messages)
                        {
                            if (!string.IsNullOrEmpty(msg.PostId) && !allMessagesDict.ContainsKey(msg.PostId))
                            {
                                allMessagesDict[msg.PostId] = msg;
                            }
                        }
                    }
                    
                    string progressScript = @"
                        (function() {
                            let div = document.querySelector('div.timeline-replies');
                            if (div) {
                                let text = div.textContent.trim();
                                let parts = text.split('/');
                                if (parts.length === 2) {
                                    return JSON.stringify({ current: parseInt(parts[0].trim()), total: parseInt(parts[1].trim()) });
                                }
                            }
                            return 'null';
                        })()
                    ";
                    
                    string progressResult = await InvokeExecuteScriptRequested(progressScript);
                    if (!string.IsNullOrEmpty(progressResult) && !progressResult.Contains("null"))
                    {
                        progressResult = JsonSerializer.Deserialize<string>(progressResult);
                        if (progressResult != "null")
                        {
                            var progress = JsonSerializer.Deserialize<Dictionary<string, int>>(progressResult);
                            
                            if (progress != null && progress.ContainsKey("current") && progress.ContainsKey("total"))
                            {
                                int current = progress["current"];
                                int total = progress["total"];
                                
                                if (current >= total)
                                {
                                    AddLogEntry($"Loaded all {total} messages");
                                    break;
                                }
                                else if (allMessagesDict.Count >= total)
                                {
                                    AddLogEntry($"Loaded {allMessagesDict.Count} messages even though the value of 'current' was only {current}");
                                    break;
                                }

                                if (allMessagesDict.Count % 50 == 0)
                                {
                                    AddLogEntry($"Progress: {current}/{total}, Collected {allMessagesDict.Count} unique messages");
                                }
                            }
                        }
                    }
                    else
                    {
                        AddLogEntry($"progressResult is null or empty, assuming that we loaded all of the messages.");
                        break;
                    }

                        
                    await InvokeExecuteScriptRequested("window.scrollBy(0, 300);");
                    await Task.Delay(200);
                }

                var finalMessages = allMessagesDict.Values.ToList();
                return finalMessages;
            }
            catch (TaskCanceledException)
            {
                AddLogEntry("Script execution timeout in HarvestPage, returning empty list");
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error in HarvestPage: {ex.Message}");
            }

            return new List<ForumMessage>();
        }
    }
}
