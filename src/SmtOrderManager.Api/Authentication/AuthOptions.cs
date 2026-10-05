using System.ComponentModel.DataAnnotations;

namespace SmtOrderManager.Api.Authentication;

/// <summary>
/// The one configured user who can log in, and where the cookie encryption keys are kept.
/// </summary>
internal sealed class AuthOptions
{
    /// <summary>
    /// The configuration section the options are bound from.
    /// </summary>
    public const string SectionName = "Auth";

    public const string DefaultKeysDirectory = "data/keys";

    [Required(AllowEmptyStrings = false)]
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// The password hash in the format of ASP.NET Core Identity's password hasher. Create one
    /// with <c>dotnet run --project src/SmtOrderManager.Api -- hash-password &lt;password&gt;</c>.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string PasswordHash { get; init; } = string.Empty;

    /// <summary>
    /// The directory of the Data Protection keys that encrypt the login cookie. It must survive
    /// restarts, otherwise every restart logs all users out.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string KeysDirectory { get; init; } = DefaultKeysDirectory;
}
