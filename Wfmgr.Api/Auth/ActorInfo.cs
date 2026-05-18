using System.Security.Claims;

namespace Wfmgr.Api.Auth;

/// <summary>
/// Extracted identity information from the authenticated user's JWT token.
/// </summary>
public sealed record ActorInfo(string UserId, IReadOnlyCollection<string> Roles)
{
    /// <summary>
    /// The constant used for system-triggered operations in JWT-substitute scenarios.
    /// </summary>
    public const string SystemUserId = "system";

    /// <summary>
    /// System actor singleton — used for internal/hosted-service operations that
    /// do not run in an HTTP request context.
    /// </summary>
    public static readonly ActorInfo System = new(SystemUserId, ["System"]);

    /// <summary>
    /// Extract <see cref="ActorInfo"/> from the current HttpContext user principal.
    /// </summary>
    public static ActorInfo FromPrincipal(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return System;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? user.FindFirstValue("sub")
                     ?? "unknown";

        var roles = user.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct()
            .ToList();

        return new ActorInfo(userId, roles.AsReadOnly());
    }
}
