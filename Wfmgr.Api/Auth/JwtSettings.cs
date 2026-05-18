namespace Wfmgr.Api.Auth;

/// <summary>
/// Settings for JWT bearer token authentication.
/// <para>
/// In development, the secret key is loaded from <c>Authentication:Jwt:Secret</c>
/// in appsettings.Development.json. In production, <c>Secret</c> should be removed
/// from config and the key stored in a secure key vault or environment variable;
/// ideally authentication delegates to a real identity provider via <c>Authority</c>
/// instead of a local symmetric key.
/// </para>
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Authentication:Jwt";

    /// <summary>
    /// Symmetric signing key used to sign and validate tokens.
    /// <para>⚠ DEVELOPMENT ONLY — replace with Authority-based validation in production.</para>
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Token issuer.  Must match the <c>iss</c> claim in incoming tokens.
    /// </summary>
    public string Issuer { get; set; } = "wfmgr-dev";

    /// <summary>
    /// Token audience.  Must match the <c>aud</c> claim in incoming tokens.
    /// </summary>
    public string Audience { get; set; } = "wfmgr-api";

    /// <summary>
    /// Token lifetime in hours.
    /// </summary>
    public int TokenLifetimeHours { get; set; } = 8;
}
