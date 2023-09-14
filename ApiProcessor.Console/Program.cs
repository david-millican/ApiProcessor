using ApiProcessor.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

//TODO - take list of subreddits from command line or config
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services => { services.AddTransient<RedditService>(); })
    .Build();

var reddit = host.Services.GetRequiredService<RedditService>();
string[] subs = { "funny", "dadjokes" };
await reddit.ExecuteAsync(subs);