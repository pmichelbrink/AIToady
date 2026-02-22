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
    public class AccurateReloadingViewModel : BaseViewModel
    {
        protected override async Task ExtractForumName(bool skipCategoryPrompt = false)
        {
            try
            {
                string script = @"
                    (function() {
                        let breadcrumb = document.querySelector('.ev_background_txt');
                        if (breadcrumb) {
                            let text = breadcrumb.textContent.trim();
                            let parts = text.split(/\s{2,}/);
                            return parts[parts.length - 1].trim();
                        }
                        
                        let headerElement = document.querySelector('h1.p-title-value');
                        if (headerElement) return headerElement.textContent.trim();
                        
                        let navbarElement = document.querySelector('td.navbar strong');
                        if (navbarElement) {
                            return navbarElement.textContent.replace(/<!--[\s\S]*?-->/g, '').trim();
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
                        document.querySelectorAll('.ev_ubbx_frm_title a.ev_ubbx_frm_title_link').forEach(a => {
                            threads.push({ url: a.href, lastPostDate: null });
                        });
                        
                        if (threads.length === 0) {
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
                        }
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
            _threadName = JsonSerializer.Deserialize<string>(titleResult) ?? "Unknown Thread";

            if (_threadName.Contains('|'))
                _threadName = _threadName.Split('|')[0].Trim();

            // Extract thread ID from URL and append to thread name
            var match = System.Text.RegularExpressions.Regex.Match(threadUrl, @"/f/(\d+)/");
            var threadId = match.Success ? match.Groups[1].Value : threadUrl.TrimEnd('/').Split('.').LastOrDefault();
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
                        let pageContainer = document.querySelector('.ev_ubbx_pages');
                        if (!pageContainer) return 'not_found';
                        
                        let currentPageBold = pageContainer.querySelector('b');
                        if (!currentPageBold) return 'not_found';
                        
                        let currentPage = parseInt(currentPageBold.textContent);
                        let allLinks = pageContainer.querySelectorAll('a');
                        
                        for (let link of allLinks) {
                            if (parseInt(link.textContent) === currentPage + 1) {
                                link.click();
                                return 'clicked';
                            }
                        }
                        
                        return 'not_found';
                    })()
                ";

                string result = JsonSerializer.Deserialize<string>(await InvokeExecuteScriptRequested(script));
                return result == "clicked";
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error checking next page: {ex.Message}");
                return false;
            }
        }

        protected async override Task<List<ForumMessage>> HarvestPage()
        {
            string extractScript = @"
                let messages = [];
                document.querySelectorAll('table[id^=""post_""]').forEach(table => {
                    let postId = table.id.replace('post_', '');
                    let username = table.querySelector('.ev_ubbx_tpc_author a')?.textContent.trim() || '';
                    let timestampCell = table.querySelector('.ev_msg_timestamp');
                    let timestamp = '';
                    if (timestampCell) {
                        let nobr = timestampCell.querySelector('nobr');
                        timestamp = nobr ? nobr.textContent.trim() : timestampCell.textContent.replace('posted', '').trim();
                    }
                    let messageDiv = table.querySelector('.ev_ubbx_tpc');
                    
                    let images = [];
                    if (messageDiv) {
                        messageDiv.querySelectorAll('img').forEach(img => {
                            if (img.src && !img.src.includes('/blank.gif')) {
                                images.push(img.src);
                            }
                        });
                    }
                    
                    messages.push({
                        postId: postId,
                        username: username,
                        message: messageDiv ? messageDiv.textContent.trim().replace(/\s+/g, ' ') : '',
                        timestamp: timestamp,
                        images: images,
                        attachments: []
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
            if (url.Contains("/forums/a/cfrm"))
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
                    document.querySelectorAll('td.ev_forum_td_title a.ubbx_cfrm_com_title_link').forEach(a => {
                        links.push(a.href);
                    });
                    JSON.stringify(links);
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