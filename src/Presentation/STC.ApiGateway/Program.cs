using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetRequiredSection("ReverseProxy:Yarp"));

builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.OnRejected = async (OnRejectedContext context, CancellationToken ctk) =>
    {
        context.HttpContext.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;

        await context.HttpContext.Response.WriteAsync("Too many requests.", ctk);
    };
    
    rateLimiterOptions.AddTokenBucketLimiter(
        policyName: "tokenBucket", configureOptions: tokenBucketRateLimiterOpt =>
        {
            tokenBucketRateLimiterOpt.TokenLimit = 100;
            tokenBucketRateLimiterOpt.TokensPerPeriod = 10;
            tokenBucketRateLimiterOpt.ReplenishmentPeriod = TimeSpan.FromSeconds(10);
            tokenBucketRateLimiterOpt.QueueLimit = 0;
            tokenBucketRateLimiterOpt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });
});

WebApplication app = builder.Build();

app.UseRateLimiter();
app.MapReverseProxy();
app.Run();