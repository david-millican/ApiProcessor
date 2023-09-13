namespace ApiProcessor.Core.Models
{
    internal interface IPost
    {
        public string? Id { get; set; }
        public int? UpVotes { get; set; }
        public string? Author { get; set; }
    }
}
