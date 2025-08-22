using System.Net;
using System.Text.Json.Nodes;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Scalar.AspNetCore;
using STC.Shared.JwtAuthentication;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Model;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetRequiredSection("ReverseProxy:Yarp"));
builder.Services.AddHttpClient();
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
{
    authOpt.AddPolicy("Authenticated", policy => { policy.RequireAuthenticatedUser(); });
});

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
app.MapScalarApiReference(_ =>
{
    // _.WithOpenApiRoutePattern(pattern: "/openapi/v1.json");
    _.AddDocuments(documents:
    [
        new ScalarDocument(Name: "Auth API", Title: null, RoutePattern: "/openapi/auth_api"),
        new ScalarDocument(Name: "Product Catalog API", Title: null, RoutePattern: "/openapi/productCatalog_api"),
    ]);
});

app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();

app.MapGet(pattern: "/openapi/{id}", async (string id, IProxyStateLookup proxyStateLookup,
    IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    HttpClient httpClient = httpClientFactory.CreateClient(name: "OpenApiClient");

    foreach (RouteModel routeModel in proxyStateLookup.GetRoutes())
    {
        if (routeModel.Cluster!.ClusterId != id)
            continue;

        string targetApiUrl = routeModel.Cluster!.Destinations.First().Value.Model.Config.Address;

        JsonNode? openApi =
            await httpClient.GetFromJsonAsync<JsonNode>(requestUri: $"{targetApiUrl}/openapi/v1.json",
                cancellationToken: cancellationToken);
        if (openApi is null)
            continue;

        openApi["servers"]!.AsArray().Clear();

        var pathsObject = openApi["paths"]!.AsObject();

        var newEndpoints = new List<(string key, JsonNode value)>();
        foreach (var endpoint in pathsObject)
            newEndpoints.Add((endpoint.Key, endpoint.Value));

        string? removePrefix = routeModel.Config.Transforms?.SelectMany(_transform => _transform)
            .FirstOrDefault(x => x.Key.Equals("PathRemovePrefix", StringComparison.OrdinalIgnoreCase)).Value;

        if (string.IsNullOrEmpty(removePrefix) is false)
        {
            foreach (var (oldKey, value) in newEndpoints)
            {
                pathsObject.Remove(propertyName: oldKey);

                var newKey = removePrefix + oldKey;

                pathsObject.Add(newKey, value.DeepClone());
            }
        }

        return Results.Ok(openApi);
    }

    return Results.NotFound();
});


app.Run();