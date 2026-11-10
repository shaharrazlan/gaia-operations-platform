namespace Gaia.Api.Security;

/// <summary>
/// Config section "Management" (R13). The key is server configuration only — never sent to browsers (R13.AC6).
/// Development has a documented dev value in appsettings.Development.json; anywhere else set the environment
/// variable <c>Management__Key</c>. With no key configured, management is closed (R13.AC8).
/// </summary>
public sealed class ManagementKeyOptions
{
    public const string Section = "Management";

    public string? Key { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Key);
}
