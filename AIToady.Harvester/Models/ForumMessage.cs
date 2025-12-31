using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace AIToady.Harvester.Models
{
    public class ForumMessage
    {
        [JsonPropertyName("postId")]
        public string PostId { get; set; } = string.Empty;
        
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;
        
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
        
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;
        
        [JsonPropertyName("images")]
        public List<string> Images { get; set; } = new List<string>();
        
        [JsonPropertyName("attachments")]
        public List<string> Attachments { get; set; } = new List<string>();
    }
}