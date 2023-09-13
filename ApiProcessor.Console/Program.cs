using ApiProcessor.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services => { services.AddTransient<RedditService>(); })
    .Build();

var reddit = host.Services.GetRequiredService<RedditService>();
await reddit.ExecuteAsync();
