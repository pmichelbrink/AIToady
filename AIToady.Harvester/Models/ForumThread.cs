using System.Collections.Generic;

namespace AIToady.Harvester.Models
{
    public class ForumThread
    {
        public string ThreadName { get; set; } = string.Empty;
        public List<ForumMessage> Messages { get; set; } = new List<ForumMessage>();
    }
}