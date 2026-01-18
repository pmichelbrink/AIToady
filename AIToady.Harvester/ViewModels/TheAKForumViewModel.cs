using AIToady.Harvester.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace AIToady.Harvester.ViewModels
{
    public class TheAKForumViewModel : BaseViewModel
    {
        public TheAKForumViewModel()
        {
            
        }
        protected override async Task ExtractForumName()
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
                        ForumName = result;
                }

                Category = string.Empty;
            }
            catch { }
        }

        protected async override Task<bool> CheckIfNextPageExists()
        {
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
            string extractScript = @"
                (function() {
                    let messages = [];
                    
                    // Handle MessageCard__container elements
                    document.querySelectorAll('.MessageCard__container').forEach(messageDiv => {
                        let userElement = messageDiv.querySelector('.MessageCard__user-info__name');
                        let messageBodyElement = messageDiv.querySelector('.message-body');
                        let timeElement = messageDiv.querySelector('.u-dt, time');
                        let images = [];
                        let attachments = [];
                        
                        if (messageBodyElement) {
                            // Extract images from bbImage elements
                            messageBodyElement.querySelectorAll('img.bbImage').forEach(img => {
                                let imageUrl = img.getAttribute('data-url') || img.getAttribute('data-src') || img.src;
                                if (imageUrl && !imageUrl.includes('data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP')) {
                                    // Remove query parameters to get clean image URL
                                    imageUrl = imageUrl.split('?')[0];
                                    if (imageUrl && !images.includes(imageUrl)) {
                                        images.push(imageUrl);
                                    }
                                }
                            });
                            
                            // Extract images from lbContainer elements
                            messageDiv.querySelectorAll('.lbContainer-zoomer').forEach(zoomer => {
                                let imageUrl = zoomer.getAttribute('data-src');
                                if (imageUrl) {
                                    // Remove query parameters to get clean image URL
                                    imageUrl = imageUrl.split('?')[0];
                                    if (imageUrl && !images.includes(imageUrl)) {
                                        images.push(imageUrl);
                                    }
                                }
                            });
                            
                            // Extract attachment files from attachmentList
                            let attachmentUrls = new Set();
                            messageDiv.querySelectorAll('.attachmentList .attachment .attachment-name a').forEach(element => {
                                let attachmentUrl = element.href;
                                if (attachmentUrl && attachmentUrl.includes('/attachments/')) {
                                    attachmentUrls.add(attachmentUrl);
                                }
                            });
                            attachments = Array.from(attachmentUrls);
                        }
                        
                        if (messageBodyElement) {
                            let postId = '';
                            let currentElement = messageDiv;
                            while (currentElement && !postId) {
                                let dataLbId = currentElement.getAttribute('data-lb-id');
                                if (dataLbId && dataLbId.startsWith('post-')) {
                                    postId = dataLbId.replace('post-', '');
                                    break;
                                }
                                postId = currentElement.getAttribute('id') || '';
                                currentElement = currentElement.parentElement;
                            }
                            if (postId.startsWith('js-')) {
                                postId = postId.substring(3);
                            }
                            
                            let username = userElement ? userElement.textContent.trim() : 'Unknown User';
                            let timestamp = timeElement ? (timeElement.getAttribute('datetime') || timeElement.getAttribute('title') || timeElement.textContent) : '';
                            
                            messages.push({
                                postId: postId,
                                username: username,
                                message: messageBodyElement.textContent.trim().replace(/\\s+/g, ' '),
                                timestamp: timestamp,
                                images: images,
                                attachments: attachments
                            });
                        }
                    });
                    
                    // Handle message-content elements
                    document.querySelectorAll('.message-content').forEach(messageDiv => {
                        let messageBodyElement = messageDiv.querySelector('.message-body');
                        let timeElement = messageDiv.querySelector('.u-dt, time');
                        let images = [];
                        let attachments = [];
                        
                        if (messageBodyElement) {
                            // Extract images
                            messageBodyElement.querySelectorAll('img.bbImage').forEach(img => {
                                let imageUrl = img.getAttribute('data-url') || img.getAttribute('data-src') || img.src;
                                if (imageUrl && !imageUrl.includes('data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP')) {
                                    imageUrl = imageUrl.split('?')[0];
                                    if (imageUrl && !images.includes(imageUrl)) {
                                        images.push(imageUrl);
                                    }
                                }
                            });
                            
                            messageDiv.querySelectorAll('.lbContainer-zoomer').forEach(zoomer => {
                                let imageUrl = zoomer.getAttribute('data-src');
                                if (imageUrl) {
                                    imageUrl = imageUrl.split('?')[0];
                                    if (imageUrl && !images.includes(imageUrl)) {
                                        images.push(imageUrl);
                                    }
                                }
                            });
                            
                            // Extract attachments
                            let attachmentUrls = new Set();
                            messageDiv.querySelectorAll('.attachmentList .attachment .attachment-name a').forEach(element => {
                                let attachmentUrl = element.href;
                                if (attachmentUrl && attachmentUrl.includes('/attachments/')) {
                                    attachmentUrls.add(attachmentUrl);
                                }
                            });
                            attachments = Array.from(attachmentUrls);
                            
                            // Extract post ID from data-lb-id
                            let postId = '';
                            let lbContainer = messageDiv.querySelector('[data-lb-id]');
                            if (lbContainer) {
                                let dataLbId = lbContainer.getAttribute('data-lb-id');
                                if (dataLbId && dataLbId.startsWith('post-')) {
                                    postId = dataLbId.replace('post-', '');
                                }
                            }
                            
                            let timestamp = timeElement ? (timeElement.getAttribute('datetime') || timeElement.getAttribute('title') || timeElement.textContent) : '';
                            
                            messages.push({
                                postId: postId,
                                username: 'Unknown User',
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
