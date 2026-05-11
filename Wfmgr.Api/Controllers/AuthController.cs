using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Wfmgr.Api.Auth;
using Wfmgr.Domain;

namespace Wfmgr.Api.Controllers;

/// <summary>
/// Development-only token endpoint.
/// <para>
/// ⚠ DEVELOPMENT ONLY — this controller issues self-signed JWT tokens using a local
/// symmetric key configured in <c>Authentication:Jwt:Secret</c>. In production, tokens
/// must come from a real identity provider (Azure AD, Auth0, Keycloak, etc.).
/// Remove or disable this controller before production deployment.
/// </para>
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly JwtSettings _settings;

    public AuthController(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    /// <summary>
    /// Issue a development JWT token for the requested role.
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/auth/dev-token
    ///     {
    ///         "userId": "daniel",
    ///         "displayName": "Daniel (Dev)",
    ///         "role": "Admin"
    ///     }
    ///
    /// Returns a signed JWT with the <c>sub</c>, <c>name</c>, <c>role</c>,
    /// and <c>permission</c> claims.
    /// </remarks>
    [HttpPost("dev-token")]
    [ProducesResponseType(typeof(DevTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<DevTokenResponse> IssueDevToken([FromBody] DevTokenRequest request)
    {
        // ⚠ DEV ONLY — no user store, no credential check.
        //     This endpoint trusts whatever userId and role the caller provides.
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest(new { error = "userId is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Role))
        {
            return BadRequest(new { error = "role is required." });
        }

        // Build claims
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.UserId),
            new(JwtRegisteredClaimNames.Name, request.DisplayName ?? request.UserId),
            new(ClaimTypes.Role, request.Role),
        };

        // Grant workflow-config.edit permission for Admin role
        if (string.Equals(request.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new("permission", "workflow-config.edit"));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_settings.TokenLifetimeHours),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new DevTokenResponse(
            Token: tokenString,
            ExpiresAt: token.ValidTo,
            Issuer: _settings.Issuer,
            Audience: _settings.Audience,
            Claims: claims.Select(c => new ClaimInfo(c.Type, c.Value)).ToList()
        ));
    }
}

public sealed record DevTokenRequest(
    string UserId,
    string? DisplayName,
    string Role
);

public sealed record DevTokenResponse(
    string Token,
    DateTime ExpiresAt,
    string Issuer,
    string Audience,
    IReadOnlyList<ClaimInfo> Claims
);

public sealed record ClaimInfo(string Type, string Value);
