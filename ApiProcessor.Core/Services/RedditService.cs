using ApiProcessor.Core.Models.Reddit;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
//using static System.Net.WebRequestMethods;

namespace ApiProcessor.Core.Services
{
    public class RedditService
    {
        private string? token = null;
        private string? lastPost = null;
        private string user_agent = "sample-api-processor";
        private JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        private HttpClient http;
        private readonly ILogger<RedditService> _logger;

        public RedditService(ILogger<RedditService> logger)
        {
            _logger = logger;
            http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd(user_agent);
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

            var tokenResponse = await http.SendAsync(message);

            var tokenContent = tokenResponse.Content.ReadAsStringAsync().Result;

            token = JsonDocument.Parse(tokenContent).RootElement.GetProperty("access_token").GetString();
        }
        
        public async Task PollApi()
        {
            var baseUrl = "https://oauth.reddit.com/r/funny/new";
            var parameters = new Dictionary<string, string>() { { "limit", "100" } };
            if (!String.IsNullOrEmpty(lastPost))
            {
                parameters.Add("after", lastPost);
            }
            var apiUrl = new Uri(QueryHelpers.AddQueryString(baseUrl, parameters));

            var apiMessage = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            apiMessage.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                token);

            var apiResponse = http.SendAsync(apiMessage).Result;

            var apiContent = await apiResponse.Content.ReadAsStringAsync();
            var subRedditRoot = JsonSerializer.Deserialize<RedditResponse>(apiContent, jsonOptions);
            if (subRedditRoot != null)
            {
                var posts = subRedditRoot.Data?.Children;

                if (posts != null && posts.Any())
                {
                    //TODO - ILogger
                    _logger.LogDebug(posts.First().Data?.Author);
                    _logger.LogDebug(posts.First().Data?.UpVotes.ToString());
                }
            }
        }
    }
}
