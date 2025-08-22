using System.Security.Claims;
using STC.Shared.Contracts.Auth;

namespace STC.ApiGateway;

/// <summary>
/// This middleware extracts user information from the JWT token and writes it to the request headers.
/// </summary>
public class WriteUserInformationsToRequestHeaderMiddleware : IMiddleware
{
    const string RoleClaim = ClaimTypes.Role;
    const string UserIdClaim = ClaimTypes.NameIdentifier;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        context.Request.Headers.Remove(key: SharedClaimConstants.UserId);
        string? userId = context.User.Claims.FirstOrDefault(_claim => _claim.Type == UserIdClaim)
            ?.Value;
        if (string.IsNullOrEmpty(userId) is false)
            context.Request.Headers.Append(key: SharedClaimConstants.UserId, value: userId);

        context.Request.Headers.Remove(key: SharedClaimConstants.Role);
        string? roleClaims = string.Join(", ",
            context.User.Claims.Where(_claim => _claim.Type == RoleClaim).Select(_claim => _claim.Value));
        if (string.IsNullOrEmpty(roleClaims) is false)
            context.Request.Headers.Append(key: SharedClaimConstants.Role, value: roleClaims);

        await next(context: context);
    }
}