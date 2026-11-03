namespace EadaptorNextPing.Models;

public enum AuthMode
{
    OAuth,
    Basic,
    None
}

public enum CertSource
{
    PemPair,
    Pfx
}

public sealed class AppSettings
{
    public const string DefaultTokenEndpoint =
        "https://login.microsoftonline.com/{clientid}/oauth2/v2.0/token";

    public const string DefaultUrl =
        "https://{enterprisecode}{servercode}services.wisegrid.net/eAdaptorNext";

    public const string DefaultScope = "{clientid}/.default";

    public string Url { get; set; } = DefaultUrl;
    public bool UseAsync { get; set; }
    public string EnterpriseCode { get; set; } = string.Empty;
    public string ServerCode { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 250;
    public AuthMode AuthMode { get; set; } = AuthMode.OAuth;
    public string BasicUser { get; set; } = string.Empty;
    public string BasicPassword { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = DefaultTokenEndpoint;
    public bool AutoFixTokenUrl { get; set; } = true;
    public string ClientId { get; set; } = string.Empty;
    public string Scope { get; set; } = DefaultScope;
    public CertSource CertSource { get; set; } = CertSource.PemPair;
    public string CertificatePath { get; set; } = string.Empty;
    public string PrivateKeyPath { get; set; } = string.Empty;
    public string KeyPassword { get; set; } = string.Empty;
    public string PfxPath { get; set; } = string.Empty;
    public string PfxPassword { get; set; } = string.Empty;
    public string RequestBody { get; set; } = string.Empty;
}
