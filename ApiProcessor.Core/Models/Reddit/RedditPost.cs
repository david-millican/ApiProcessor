using System.Text.Json.Serialization;

namespace ApiProcessor.Core.Models.Reddit
{
    internal class RedditPost : IPost
    {
        [JsonPropertyName("name")]
        public string? Id { get; set; }
        [JsonPropertyName("author_fullname")]
        public string? Author { get; set; }
        [JsonPropertyName("ups")]
        public int? UpVotes { get; set; }
    }
}