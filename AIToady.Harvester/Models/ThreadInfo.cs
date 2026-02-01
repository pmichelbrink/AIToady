using System.Text.Json.Serialization;

namespace AIToady.Harvester.Models
{
    public class ThreadInfo
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }
        
        [JsonPropertyName("lastPostDate")]
        public string LastPostDateString { get; set; }
        
        [JsonIgnore]
        public DateTime? LastPostDate
        {
            get => DateTime.TryParse(LastPostDateString, out var date) ? date : null;
            set => LastPostDateString = value?.ToString("o");
        }
    }
}
