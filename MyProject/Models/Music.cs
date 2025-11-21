using System.Text.Json.Serialization;

namespace MyProject.Models
{
    public class Music
    {
        [JsonPropertyName("Warmup")]
        public List<string> Warmup { get; set; } = [];
        [JsonPropertyName("Round")]
        public List<SoundEvent> Round { get; set; } = [];
        [JsonPropertyName("EndGame")]
        public List<string> EndGame { get; set; } = [];
        [JsonPropertyName("Loose")]
        public List<string> Loose { get; set; } = [];
        [JsonPropertyName("Win")]
        public List<string> Win { get; set; } = [];
    }

    public class SoundEvent
    {
        [JsonPropertyName("EventName")]
        public string EventName { get; set; } = string.Empty;
        [JsonPropertyName("DisplayName")]
        public string DisplayName { get; set; } = string.Empty;
    }
}
