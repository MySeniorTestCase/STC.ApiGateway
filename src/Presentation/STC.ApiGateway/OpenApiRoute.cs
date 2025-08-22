using System.Text.Json.Nodes;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Model;

namespace STC.ApiGateway;

public static class OpenApiRoute
{
    public static IEndpointRouteBuilder MapOpenApiRoute(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(pattern: "/openapi/{id}", async (string id, IProxyStateLookup proxyStateLookup,
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

        return endpoints;
    }
}