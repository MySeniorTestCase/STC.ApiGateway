using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Scalar.AspNetCore;
using STC.ApiGateway;
using STC.Shared.JwtAuthentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetRequiredSection("ReverseProxy:Yarp"));
builder.Services.AddHttpClient();
builder.Services.AddScoped<WriteUserInformationsToRequestHeaderMiddleware>();

builder.Services.AddJwtAuthenticationDependencies(options: jwtOpt =>
{
    var jwtSct = builder.Configuration.GetRequiredSection(key: "Auth:JwtSettings");

    jwtOpt.SecurityKey = jwtSct["SecurityKey"] ??
                         throw new InvalidOperationException("SecurityKey is not configured.");
    jwtOpt.Audience = jwtSct["Audience"] ??
                      throw new InvalidOperationException("Audience is not configured.");
    jwtOpt.Issuer = jwtSct["Issuer"] ??
                    throw new InvalidOperationException("Issuer is not configured.");
}).AddAuthorization(configure: authOpt =>
    authOpt.AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser()));

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
app.MapOpenApiRoute();
app.UseAuthentication();
app.UseAuthorization();

app.MapScalarApiReference(_ => _.AddDocuments(documents:
[
    new ScalarDocument(Name: "Auth API", Title: null, RoutePattern: "/openapi/auth_api"),
    new ScalarDocument(Name: "Product Catalog API", Title: null, RoutePattern: "/openapi/productCatalog_api"),
]));

app.UseMiddleware<WriteUserInformationsToRequestHeaderMiddleware>();
app.MapReverseProxy();
app.Run();