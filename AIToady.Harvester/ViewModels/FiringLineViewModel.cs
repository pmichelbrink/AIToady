using AIToady.Harvester.Models;
using System.IO;
using System.Text.Json;

namespace AIToady.Harvester.ViewModels
{
    public class FiringLineViewModel : BaseViewModel
    {
        public FiringLineViewModel()
        {
            MessagesPerPage = 25;
        }
        protected override async Task ExtractForumName(bool skipCategoryPrompt = false)
        {
            SiteName = "The Firing Line";
            try
            {
                string script = @"
                    (function() {
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
                    let threads = [];
                    document.querySelectorAll('a[id^=""thread_title_""]').forEach(a => {
                        threads.push({ url: a.href, lastPostDate: null });
                    });
                    JSON.stringify(threads);
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
            // Get thread name from page title
            string titleScript = "document.title";
            string titleResult = await InvokeExecuteScriptRequested(titleScript);
            _threadName = JsonSerializer.Deserialize<string>(titleResult) ?? "Unknown Thread";


            if (_threadName.Contains(" - The Firing Line Forums"))
                _threadName = _threadName.Split(" - The Firing Line Forums")[0].Trim();

            // Extract thread ID from URL and append to thread name
            var threadId = threadUrl.Substring(threadUrl.LastIndexOf("t=") + 2);
            if (!string.IsNullOrEmpty(threadId))
                _threadName += $"_{threadId}";

            _threadName = string.Join("_", _threadName.Split(System.IO.Path.GetInvalidFileNameChars()));

            return _threadName;
        }
        protected async override Task<List<ForumMessage>> HarvestPage()
        {
            string extractScript = @"
                let messages = [];
                document.querySelectorAll('table[id^=""post""]').forEach(table => {
                    let postId = table.id.replace('post', '');
                    let userElement = table.querySelector('.bigusername');
                    let messageBodyElement = table.querySelector('[id^=""post_message_""]');
                    let dateElement = table.querySelector('.thead');
                    let images = [];
                    let attachments = [];
                    
                    if (messageBodyElement) {
                        messageBodyElement.querySelectorAll('img').forEach(img => {
                            if (img.src && !img.src.includes('/images/') && !img.src.includes('data:image')) {
                                images.push(img.src);
                            }
                        });
                        
                        table.querySelectorAll('a[href*=""attachment.php""]').forEach(a => {
                            if (a.href.includes('attachmentid=')) {
                                attachments.push(a.href);
                            }
                        });
                    }
                    
                    if (userElement && messageBodyElement) {
                        messages.push({
                            postId: postId,
                            username: userElement.textContent.trim(),
                            message: messageBodyElement.textContent.trim().replace(/\s+/g, ' '),
                            timestamp: dateElement ? dateElement.textContent.trim() : '',
                            images: images,
                            attachments: attachments
                        });
                    }
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
                        let nextButton = document.querySelector('a[rel=""next""][class=""smallfont""]');
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
