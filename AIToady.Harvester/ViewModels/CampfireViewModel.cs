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
    public class CampfireViewModel : BaseViewModel
    {
        public CampfireViewModel()
        {
            MessagesPerPage = 15;
        }
        protected override async Task ExtractForumName(bool skipCategoryPrompt = false)
        {
            try
            {
                string script = @"
                    (function() {
                        let breadcrumbs = document.querySelector('#breadcrumbs h1');
                        if (breadcrumbs) {
                            let links = breadcrumbs.querySelectorAll('a');
                            if (links.length > 0) {
                                return links[links.length - 1].textContent.trim();
                            }
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

                Category = string.Empty;
            }
            catch { }
        }
        //protected override async Task<bool> LoadNextForumPage()
        //{
        //    try
        //    {
        //        string script = @"
        //            (function() {
        //                let nextLink = document.querySelector('div.trow.pagenav a[rel=""next""]');
        //                if (nextLink && nextLink.href) {
        //                    return nextLink.href;
        //                }
        //                return 'not_found';
        //            })()
        //        ";
        //        string result = await InvokeExecuteScriptRequested(script);

        //        if (string.IsNullOrEmpty(result))
        //        {
        //            AddLogEntry("Script returned null result, assuming no next page");
        //            return false;
        //        }

        //        result = JsonSerializer.Deserialize<string>(result);

        //        if (result == "not_found")
        //        {
        //            return false;
        //        }
        //        else
        //        {
        //            InvokeNavigateRequested(result);
        //            return true;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        AddLogEntry($"Error checking next page: {ex.Message}");
        //        return false;
        //    }
        //}
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
                        let nextTd = document.querySelector('td.page-n i.fa-angle-right');
                        if (nextTd && nextTd.closest('td').onclick) {
                            let onclickAttr = nextTd.closest('td').getAttribute('onclick');
                            let match = onclickAttr.match(/location\.href='([^']+)'/);
                            if (match) {
                                return match[1];
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
                        document.querySelectorAll('tr[id^=""postrow-inline-""]').forEach(row => {
                            let subjectTd = row.querySelector('td[class*=""topicsubject""]');
                            if (subjectTd) {
                                let link = subjectTd.querySelector('div:first-child a');
                                let dateSpan = subjectTd.querySelector('.fa-clock').closest('div').querySelector('span.date');
                                if (link && link.href) {
                                    threads.push({ 
                                        url: link.href, 
                                        lastPostDate: dateSpan ? dateSpan.textContent.trim() : null 
                                    });
                                }
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
                    let titleDiv = document.querySelector('div.truncate.bold');
                    return titleDiv ? titleDiv.getAttribute('title') : '';
                })()
            ";

            string titleResult = await InvokeExecuteScriptRequested(titleScript);
            if (!string.IsNullOrEmpty(titleResult))
            {
                titleResult = JsonSerializer.Deserialize<string>(titleResult);
            }

            if (string.IsNullOrEmpty(titleResult))
                titleResult = "Unknown Thread";

            var match = System.Text.RegularExpressions.Regex.Match(threadUrl, @"/topics/(\d+)/");
            if (match.Success)
            {
                titleResult += $"_{match.Groups[1].Value}";
            }

            return string.Join("_", titleResult.Split(System.IO.Path.GetInvalidFileNameChars()));
        }
        protected async override Task<List<ForumMessage>> HarvestPage()
        {
            string extractScript = @"
                (function() {
                    let messages = [];
                    document.querySelectorAll('table.fw').forEach(table => {
                        let authorTd = table.querySelector('td.author-content[rowspan=""2""]');
                        let postContentTd = table.querySelector('td.post-content');
                        
                        if (authorTd && postContentTd) {
                            let usernameElement = authorTd.querySelector('.author-name .username');
                            let username = usernameElement ? usernameElement.textContent.trim() : 'Unknown';
                            
                            let subjectTd = table.querySelector('td.subjecttable');
                            let postId = '';
                            let timestamp = '';
                            
                            if (subjectTd) {
                                let postIdLink = subjectTd.querySelector('a[id^=""number""]');
                                if (postIdLink) {
                                    postId = postIdLink.textContent.trim();
                                }
                                let dateSpan = subjectTd.querySelector('span.date');
                                if (dateSpan) {
                                    timestamp = dateSpan.textContent.trim();
                                }
                            }
                            
                            let bodyDiv = postContentTd.querySelector('div[id^=""body""]');
                            if (bodyDiv) {
                                let images = [];
                                postContentTd.querySelectorAll('img.post-image').forEach(img => {
                                    if (img.src) {
                                        images.push(img.src);
                                    }
                                });
                                
                                messages.push({
                                    postId: postId,
                                    username: username,
                                    message: bodyDiv.textContent.trim().replace(/\s+/g, ' '),
                                    timestamp: timestamp,
                                    images: images,
                                    attachments: []
                                });
                            }
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