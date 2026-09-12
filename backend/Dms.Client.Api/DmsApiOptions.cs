namespace Dms.Client.Api;

/// <summary>Where the backend lives. Defaults match Dms.Api's Development
/// launch profile (http://localhost:5287) — http, not https, so WPF/Blazor
/// clients don't need the ASP.NET Core dev cert trusted locally.</summary>
public class DmsApiOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5287";
}
