using AIToady.Harvester.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace AIToady.Harvester.ViewModels
{
    public class AR15ViewModel : BaseViewModel
    {
        public AR15ViewModel()
        {
            
        }
        protected override async Task ExtractForumName()
        {
            try
            {
                string script = @"
                    (function() {
                        let parentDiv = document.querySelector('div.expanded.column.row.skin-dark.white.barpad');
                        if (parentDiv) {
                            let childDiv = parentDiv.querySelector('div.column');
                            return childDiv ? childDiv.textContent.trim() : '';
                        }
                        return '';
                    })()
                ";
                string result = await InvokeExecuteScriptRequested(script);
                if (!string.IsNullOrEmpty(result))
                {
                    result = JsonSerializer.Deserialize<string>(result);
                    if (!string.IsNullOrEmpty(result))
                        ForumName = result;
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

                string script = @"
                    let linkSet = new Set();
                    let uls = document.querySelectorAll('ul');
                    uls.forEach(ul => {
                        let anchors = ul.querySelectorAll('li a');
                        anchors.forEach(a => {
                            if (a.href && a.href.includes('/forums/')) linkSet.add(a.href);
                        });
                    });
                    JSON.stringify(Array.from(linkSet));
                ";

                string result = await InvokeExecuteScriptRequested(script);
                result = JsonSerializer.Deserialize<string>(result);
                var links = JsonSerializer.Deserialize<string[]>(result);
                _threadLinks.Clear();
                _threadLinks.AddRange(links);
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
                let nextLink = document.querySelector('a[href*=""?page=""]');
                if (nextLink && nextLink.textContent.includes('Next Page')) {
                    return nextLink.getAttribute('href');
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
                    let nextLink = document.querySelector('a[href*=""page=""]');
                    if (nextLink && nextLink.textContent.includes('Next')) {
                        return nextLink.getAttribute('href');
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
                        let images = [];
                        let attachments = [];
            
                        if (messageBodyElement) {
                            // Extract images
                            messageBodyElement.querySelectorAll('img').forEach(img => {
                                let imageUrl = img.src;
                                if (imageUrl && !imageUrl.includes('data:image')) {
                                    imageUrl = imageUrl.split('?')[0];
                                    if (!images.includes(imageUrl)) {
                                        images.push(imageUrl);
                                    }
                                }
                            });
                
                            let timestamp = timeElement ? timeElement.getAttribute('content') : '';
                
                            messages.push({
                                postId: '',
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
