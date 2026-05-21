using System.Security.Claims;
using Wfmgr.Application.Abstractions;

namespace Wfmgr.Api.Auth;

/// <summary>
/// Resolves the current <see cref="Actor"/> from the HTTP request's
/// <see cref="ClaimsPrincipal"/>. Falls back to <see cref="Actor.System"/> when
/// no authenticated user is present (e.g. background tasks reusing the scope).
/// </summary>
public sealed class HttpContextActorAccessor : IActorAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextActorAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Actor Current
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return Actor.System;
            }

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? user.FindFirstValue("sub")
                         ?? "unknown";

            var roles = user.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .Distinct()
                .ToList();

            return new Actor(userId, roles.AsReadOnly());
        }
    }
}
