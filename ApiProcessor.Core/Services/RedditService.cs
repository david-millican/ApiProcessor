using ApiProcessor.Core.Models.Reddit;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
//using static System.Net.WebRequestMethods;

namespace ApiProcessor.Core.Services
{
    public class RedditService
    {
        private string? _token = null;
        private string? _lastPost = null;
        private string _user_agent = "sample-api-processor";
        private JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        private HttpClient _http;
        private readonly ILogger<RedditService> _logger;
        //TODO - use ConcurrentDictionary if we find a need to make this thread safe.
        private Dictionary<string, int> _topPosts = new();
        private Dictionary<string, int> _topUsers = new();

        public RedditService(ILogger<RedditService> logger)
        {
            _logger = logger;
            _http = new HttpClient();
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(_user_agent);
        }

        public async Task ExecuteAsync()
        {
            await RetrieveToken();
            await PollApi();
        }

        public async Task RetrieveToken()
        {
            //TODO - Manage Secrets
            var clientId = "dayPDhWPHzZ_oqqB3sLFTw";
            var secret = "e-Gl6fiyQP15erq06hoJvPyIUMrEWA";
            var tokenUrl = "https://www.reddit.com/api/v1/access_token";
            var device_id = "DO_NOT_TRACK_THIS_DEVICE";

            var message = new HttpRequestMessage(HttpMethod.Post, tokenUrl);

            message.Headers.Authorization = new AuthenticationHeaderValue(
         "Basic",
                 Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{secret}")));

            message.Content = new StringContent($"grant_type=https://oauth.reddit.com/grants/installed_client&device_id={device_id}", Encoding.UTF8, "application/x-www-form-urlencoded");

            var tokenResponse = await _http.SendAsync(message);

            var tokenContent = tokenResponse.Content.ReadAsStringAsync().Result;

            _token = JsonDocument.Parse(tokenContent).RootElement.GetProperty("access_token").GetString();
        }
        
        public async Task PollApi()
        {
            var baseUrl = "https://oauth.reddit.com/r/funny/new";
            var parameters = new Dictionary<string, string>() { { "limit", "100" } };
            if (!String.IsNullOrEmpty(_lastPost))
            {
                parameters.Add("after", _lastPost);
            }
            var apiUrl = new Uri(QueryHelpers.AddQueryString(baseUrl, parameters));

            var apiMessage = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            apiMessage.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                _token);

            var apiResponse = _http.SendAsync(apiMessage).Result;

            var apiContent = await apiResponse.Content.ReadAsStringAsync();
            var subRedditRoot = JsonSerializer.Deserialize<RedditResponse>(apiContent, _jsonOptions);
            if (subRedditRoot != null)
            {
                var posts = subRedditRoot.Data?.Children;

                if (posts != null)
                {
                    foreach (var post in posts)
                    {
                        if (post != null)
                        {
                            if (post.Data == null)
                            {
                                _logger.LogError($"empty post data found {post}");
                                continue;
                            }
                            if (String.IsNullOrEmpty(post.Data.Id))
                            {
                                _logger.LogError($"id missing from post {post}");
                                continue;
                            }
                            if (post.Data.UpVotes == null)
                            {
                                _logger.LogError($"upvotes missing from post {post.Data.Id}");
                                continue;
                            }
                            if (!_topPosts.TryAdd(post.Data.Id.ToString(), (int)post.Data.UpVotes))
                            {
                                _logger.LogError($"failed to add post {post.Data.Id} to topPosts");
                            }
                            if (post.Data.Author == null)
                            {
                                _logger.LogError($"author missing from post {post.Data.Id}");
                                continue;
                            }
                            if (_topUsers.ContainsKey(post.Data.Author.ToString()))
                            {
                                _topUsers[post.Data.Author.ToString()]++;
                            }
                            else
                            {
                                if (!_topUsers.TryAdd(post.Data.Author.ToString(), 1))
                                {
                                    _logger.LogError($"failed to add post {post.Data.Id} to topUsers");
                                }
                            }
                        }
                    }
                } else
                {

                }
            }
        }

        public IEnumerable<KeyValuePair<string, int>> GetTopPosts(int count) {
            return _topPosts.OrderByDescending(x => x.Value).Take(count);
        }

        public IEnumerable<KeyValuePair<string, int>> GetTopUsers(int count)
        {
            return _topUsers.OrderByDescending(x => x.Value).Take(count);
        }
    }
}
