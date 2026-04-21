namespace TrailScout.Models
{
    public class Trail
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Difficulty { get; set; } = string.Empty;
        public double DistanceMiles { get; set; }
        public int ElevationGainFeet { get; set; }
        public string Features { get; set; } = string.Empty; // Store as comma-separated or JSON
        public string Description { get; set; } = string.Empty;
        public bool ScenicViews { get; set; }
        public bool Waterfalls { get; set; }
        public bool Lakes { get; set; }
        public string CrowdLevel { get; set; } = string.Empty;
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public List<ChatMessage> History { get; set; } = new();
    }

    public class ChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
