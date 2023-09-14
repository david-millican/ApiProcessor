using ApiProcessor.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ApiProcessor.Test.Services
{
    public class RedditServiceTest
    {
        public readonly Mock<ILogger<RedditService>> _mockLoger = new();

        [Fact]
        public void ServiceReturnsPostsWithMostUpvotes()
        {
            var reddit = new RedditService(_mockLoger.Object);
            string[] subs = { "funny" };
            reddit.ExecuteAsync(subs).Wait();
            var requestedCount = 3;
            var topPosts = reddit.GetTopPosts(requestedCount);

            Assert.True(topPosts.Count() <= requestedCount);
            //TODO - mock response and assert proper top posts
        }

        [Fact]
        public void ServiceReturnsUsersWithMostPosts()
        {
            var reddit = new RedditService(_mockLoger.Object);

            string[] subs = { "funny" };
            reddit.ExecuteAsync(subs).Wait();
            var requestedCount = 3;
            var topUsers = reddit.GetTopUsers(requestedCount);

            Assert.True(topUsers.Count() <= requestedCount);
            //TODO - mock response and assert proper top users
        }
    }
}
