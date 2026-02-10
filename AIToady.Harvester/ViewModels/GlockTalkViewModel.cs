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
    public class GlockTalkViewModel : BaseViewModel
    {
        public GlockTalkViewModel()
        {
            MessagesPerPage = 15;
        }
        protected override async Task ExtractForumName(bool skipCategoryPrompt = false)
        {
            try
            {
                string script = @"
                    (function() {
                        let headerElement = document.querySelector('h1[qid=""page-header""]');
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
                        let nextButton = document.querySelector('.pageNav-jump--next');
                        if (nextButton && nextButton.getAttribute('aria-disabled') !== 'true') {
                            if (nextButton.tagName === 'A' && nextButton.href) {
                                return nextButton.getAttribute('href');
                            } else {
                                nextButton.click(); 
                                return 'clicked';
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

                if (result == "clicked")
                {
                    return true;
                }
                else if (result == "not_found")
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

        protected async override Task<List<ForumMessage>> HarvestPage()
        {
            string messageSelector = string.IsNullOrEmpty(MessageElement) ? ".message-cell--main" : MessageElement;
            string extractScript = $@"
                let messages = [];
                document.querySelectorAll('{messageSelector}').forEach(messageDiv => {{
                    let userElement = messageDiv.querySelector('.message-name a') || messageDiv.querySelector('.message-name .username') || messageDiv.closest('.message-cell').previousElementSibling?.querySelector('.message-name a');
                    let messageBodyElement = messageDiv.querySelector('.message-body');
                    let timeElement = messageDiv.querySelector('.u-dt');
                    let images = [];
                    let attachments = [];
                    
                    if (messageBodyElement) {{
                        messageBodyElement.querySelectorAll('img.bbImage').forEach(img => {{
                            let imageUrl = img.getAttribute('data-url') || img.getAttribute('data-src') || img.src;
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
                    
                    if (messageBodyElement) {{
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
                            username: userElement ? userElement.textContent.trim() : 'Unknown',
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