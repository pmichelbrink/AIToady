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
    public class MainViewModel : BaseViewModel
    {
        protected override async Task ExtractForumName(bool skipCategoryPrompt = false)
        {
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
                    {
                        ForumName = string.Join("_", result.Split(Path.GetInvalidFileNameChars()));
                        if (Url.Contains("akfiles", StringComparison.InvariantCultureIgnoreCase))
                            SiteName = "The AK Files";
                        else if(Url.Contains("falfiles", StringComparison.InvariantCultureIgnoreCase))
                            SiteName = "The FAL Files";
                        else if (Url.Contains("snipershide", StringComparison.InvariantCultureIgnoreCase))
                            SiteName = "Snipers Hide";
                        else if (Url.Contains("thehighroad", StringComparison.InvariantCultureIgnoreCase))
                            SiteName = "The High Road";
                        else if (Url.Contains("thefiringline", StringComparison.InvariantCultureIgnoreCase))
                            SiteName = "The Firing Line";
                        else if (Url.Contains("accurateshooter", StringComparison.InvariantCultureIgnoreCase))
                            SiteName = "Accurate Shooter";
                    }
                }

                Category = string.Empty;
            }
            catch { }
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
                        let nextButton = document.querySelector('.pageNav-jump.pageNav-jump--next');
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

        protected async override Task<List<ForumMessage>> HarvestPage()
        {
            string messageSelector = string.IsNullOrEmpty(MessageElement) ? ".message-inner" : MessageElement;
            string extractScript = $@"
                let messages = [];
                document.querySelectorAll('{messageSelector}').forEach(messageDiv => {{
                    let userElement = messageDiv.querySelector('.message-name a') || messageDiv.querySelector('.message-name .username');
                    let messageBodyElement = messageDiv.querySelector('.message-body');
                    let timeElement = messageDiv.querySelector('.u-dt');
                    let images = [];
                    let attachments = [];
                    
                    if (messageBodyElement) {{
                        messageBodyElement.querySelectorAll('img.bbImage').forEach(img => {{
                            let imageUrl = img.getAttribute('data-url') || img.src;
                            if (imageUrl && !imageUrl.includes('data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP')) {{
                                images.push(imageUrl);
                            }}
                        }});
                        
                        // Extract all attachment files
                        let attachmentUrls = new Set();
                        messageDiv.querySelectorAll('.attachmentList .attachment a, .attachmentList .file-preview').forEach(element => {{
                            let attachmentUrl = element.href;
                            if (attachmentUrl && attachmentUrl.includes('/attachments/')) {{
                                attachmentUrls.add(attachmentUrl);
                            }}
                        }});
                        attachments = Array.from(attachmentUrls);
                        }}
                    
                    if (userElement && messageBodyElement) {{
                        let postId = '';
                        let currentElement = messageDiv;
                        while (currentElement && !postId) {{
                            postId = currentElement.getAttribute('data-lb-id') || currentElement.getAttribute('id') || '';
                            currentElement = currentElement.parentElement;
                        }}
                        if (postId.startsWith('js-')) {{
                            postId = postId.substring(3);
                        }}
                        messages.push({{
                            postId: postId,
                            username: userElement.textContent.trim(),
                            message: messageBodyElement.textContent.trim().replace(/\s+/g, ' '),
                            timestamp: timeElement ? timeElement.getAttribute('datetime') : '',
                            images: images,
                            attachments: attachments
                        }});
                    }}
                }});
                JSON.stringify(messages);
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