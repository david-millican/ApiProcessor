namespace ApiProcessor.Core.Models.Reddit
{
    internal class RedditResponse
    {
        public string? Kind { get; set; }
        public RootData? Data { get; set; }
    }

    class RootData
    {
        public List<Child>? Children { get; set; }
    }

    class Child
    {
        public string? Kind { get; set; }
        public RedditPost? Data { get; set; }
    }
}
