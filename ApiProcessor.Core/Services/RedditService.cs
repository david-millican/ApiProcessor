using ApiProcessor.Core.Models;
using ApiProcessor.Core.Models.Reddit;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ApiProcessor.Core.Services
{
    public class RedditService
    {
        private string? _token = null;
        private double _delaySeconds = 0.0;
        private int _taskCount = 0;
        private string _user_agent = "sample-api-processor";
        private JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        private HttpClient _http;
        private readonly ILogger<RedditService> _logger;
        //keeping this here rather than per sub polling task as it seems that the instructions are looking for awareness of this
        private readonly ConcurrentDictionary<string, int> _allPosts = new();
        private readonly ConcurrentDictionary<string, int> _allUsers = new();

        public RedditService(ILogger<RedditService> logger)
        {
            //TODO - inject HttpClient so that it can be mocked
            _logger = logger;
            _http = new HttpClient();
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(_user_agent);
        }

        public async Task ExecuteAsync(string[] subreddits)
        {
            _taskCount = subreddits.Length;
            await RetrieveToken();
            Timer timer = new Timer(state =>
            {
                Task.Run(() => PublishStatistics());
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            Parallel.ForEach(subreddits, async subreddit =>
                {
                    await PollApi(subreddit);
                }
            );
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
        
        public async Task PollApi(string subReddit)
        {
            _logger.LogInformation($"start polling subreddit - {subReddit}");
            string? _lastPost = null;
            //TODO - allow for starting multiple polling tasks for tracking multiple subreddits
            var baseUrl = $"https://oauth.reddit.com/r/{subReddit}/new";
            while (true)
            {
                var parameters = new Dictionary<string, string>() { { "limit", "100" } };
                if (!String.IsNullOrEmpty(_lastPost))
                {
                    parameters.Add("after", _lastPost);
                }
                var apiUrl = new Uri(QueryHelpers.AddQueryString(baseUrl, parameters));

                var apiMessage = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                apiMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

                var apiResponse = _http.SendAsync(apiMessage).Result;

                ProcessResponseHeaders(apiResponse.Headers);

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
                                if (_allPosts.ContainsKey(post.Data.Id.ToString()))
                                {
                                    _logger.LogWarning($"duplicate post encountered...");
                                }
                                else
                                {
                                    if (!_allPosts.TryAdd(post.Data.Id.ToString(), (int)post.Data.UpVotes))
                                    {
                                        _logger.LogError($"failed to add post {post.Data.Id} to topPosts");
                                    }
                                }
                                if (post.Data.Author == null)
                                {
                                    _logger.LogError($"author missing from post {post.Data.Id}");
                                    continue;
                                }
                                if (_allUsers.ContainsKey(post.Data.Author.ToString()))
                                {
                                    _allUsers[post.Data.Author.ToString()]++;
                                }
                                else
                                {
                                    if (!_allUsers.TryAdd(post.Data.Author.ToString(), 1))
                                    {
                                        _logger.LogError($"failed to add post {post.Data.Id} to topUsers");
                                    }
                                }
                            }
                            _lastPost = post?.Data?.Id;
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"no posts returned");
                    }
                }
                _logger.LogInformation($"copmleted an iteration of subreddit - {subReddit}");
                Thread.Sleep((int)(_delaySeconds * 1000));
            }
        }

        private void ProcessResponseHeaders(HttpResponseHeaders headers)
        {
            if (headers != null && headers.Any())
            {
                var remaining = double.Parse(headers.GetValues("X-Ratelimit-Remaining").First()); 
                var reset = double.Parse(headers.GetValues("X-Ratelimit-Reset").First());

                _delaySeconds = ((int)remaining == 0) ? reset : (reset / remaining / _taskCount);
            }
        }

        public IEnumerable<KeyValuePair<string, int>> GetTopPosts(int count) {
            return _allPosts.OrderByDescending(x => x.Value).Take(count);
        }

        public IEnumerable<KeyValuePair<string, int>> GetTopUsers(int count)
        {
            return _allUsers.OrderByDescending(x => x.Value).Take(count);
        }

        public void PublishStatistics()
        {
            //TODO - change the formatting of this
            _logger.LogInformation($"--------{DateTime.Now.ToString()}-----------");
            _logger.LogInformation("--------top posts-----------");
            GetTopPosts(3).ToList().ForEach(kvp => _logger.LogInformation($"{kvp.Key} - {kvp.Value}"));
            _logger.LogInformation("--------top users-----------");
            GetTopUsers(3).ToList().ForEach(kvp => _logger.LogInformation($"{kvp.Key} - {kvp.Value}"));
        }
    }
}
