using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dms.Shared.Contracts.Auth;
using Dms.Shared.Contracts.Devices;
using Dms.Shared.Contracts.Telemetry;

namespace Dms.Client.Api;

/// <summary>
/// Thin typed wrapper over Dms.Api's REST surface, shared by every C#
/// client (Desktop Driver HUD, Desktop Fleet Console, Web Fleet Portal) so
/// route strings and error handling live in exactly one place. Register via
/// AddDmsApiClient() — see ServiceCollectionExtensions.
/// </summary>
public class DmsApiClient(HttpClient httpClient, AuthSession authSession)
{
    private void AttachToken()
    {
        httpClient.DefaultRequestHeaders.Authorization = authSession.AccessToken is null
            ? null
            : new AuthenticationHeaderValue("Bearer", authSession.AccessToken);
    }

    public async Task<LoginResponse> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/v1/auth/login", new LoginRequest(email, password), ct);
        await EnsureSuccess(response);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: ct)
            ?? throw new DmsApiException("Login succeeded but the response body was empty.");
        authSession.SetFromLogin(result);
        return result;
    }

    public async Task<RegisterUserResponse> RegisterUserAsync(RegisterUserRequest request, CancellationToken ct = default)
    {
        AttachToken();
        var response = await httpClient.PostAsJsonAsync("api/v1/auth/register", request, ct);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<RegisterUserResponse>(cancellationToken: ct)
            ?? throw new DmsApiException("Register succeeded but the response body was empty.");
    }

    public async Task<DeviceRegistrationResponse> RegisterDeviceAsync(DeviceRegistrationRequest request, CancellationToken ct = default)
    {
        AttachToken();
        var response = await httpClient.PostAsJsonAsync("api/v1/devices/register", request, ct);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<DeviceRegistrationResponse>(cancellationToken: ct)
            ?? throw new DmsApiException("Device registration succeeded but the response body was empty.");
    }

    public async Task<List<DeviceSummary>> GetDevicesAsync(CancellationToken ct = default)
    {
        AttachToken();
        var response = await httpClient.GetAsync("api/v1/devices", ct);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<List<DeviceSummary>>(cancellationToken: ct) ?? [];
    }

    public async Task<List<UserSummary>> GetUsersAsync(UserRole? role = null, CancellationToken ct = default)
    {
        AttachToken();
        var query = role is null ? "" : $"?role={role}";
        var response = await httpClient.GetAsync($"api/v1/users{query}", ct);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<List<UserSummary>>(cancellationToken: ct) ?? [];
    }

    public async Task<IncidentIngestResponse> PostIncidentAsync(IncidentReport report, CancellationToken ct = default)
    {
        AttachToken();
        var response = await httpClient.PostAsJsonAsync("api/v1/telemetry/incidents", report, ct);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<IncidentIngestResponse>(cancellationToken: ct)
            ?? throw new DmsApiException("Incident ingest succeeded but the response body was empty.");
    }

    public async Task<List<IncidentSummary>> GetRecentIncidentsAsync(int take = 50, CancellationToken ct = default)
    {
        AttachToken();
        var response = await httpClient.GetAsync($"api/v1/telemetry/incidents?take={take}", ct);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<List<IncidentSummary>>(cancellationToken: ct) ?? [];
    }

    private static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        var body = await response.Content.ReadAsStringAsync();
        throw new DmsApiException($"{(int)response.StatusCode} {response.ReasonPhrase}: {body}");
    }
}

public class DmsApiException(string message) : Exception(message);
