using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dms.Client.Api;
using Dms.Shared.Contracts.Alerts;
using Dms.Shared.Contracts.Telemetry;
using Microsoft.AspNetCore.SignalR.Client;

namespace Dms.Desktop.FleetConsole.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DmsApiClient _api;
    private readonly AuthSession _authSession;
    private readonly DrowsinessHubClient _hub;

    public MainViewModel(DmsApiClient api, AuthSession authSession, DrowsinessHubClient hub)
    {
        _api = api;
        _authSession = authSession;
        _hub = hub;

        _hub.AlertReceived += OnAlertReceived;
        _hub.StateChanged += OnHubStateChanged;
    }

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private string _email = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private string _hubStatus = "Disconnected";

    public ObservableCollection<IncidentSummary> Incidents { get; } = [];

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var response = await _api.LoginAsync(Email, Password);
            DisplayName = $"{response.DisplayName} ({response.Role})";
            IsAuthenticated = true;
            Password = "";
            await LoadIncidentsAsync();
            await StartHubAsync();
        }
        catch (DmsApiException ex)
        {
            ErrorMessage = ex.Message.Contains("401") ? "Invalid email or password." : ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await _hub.StopAsync();
        _authSession.Clear();
        Incidents.Clear();
        IsAuthenticated = false;
        HubStatus = "Disconnected";
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadIncidentsAsync();

    private async Task LoadIncidentsAsync()
    {
        try
        {
            var incidents = await _api.GetRecentIncidentsAsync(take: 100);
            Incidents.Clear();
            foreach (var incident in incidents)
            {
                Incidents.Add(incident);
            }
        }
        catch (DmsApiException ex)
        {
            ErrorMessage = $"Could not load incidents: {ex.Message}";
        }
    }

    private async Task StartHubAsync()
    {
        try
        {
            await _hub.StartAsync();
        }
        catch (Exception ex)
        {
            // Dashboard still works from the initial LoadIncidentsAsync load even
            // if the live hub connection fails (e.g. Dms.Api not reachable).
            ErrorMessage = $"Live alert feed unavailable: {ex.Message}";
        }
    }

    private void OnAlertReceived(DrowsinessAlert alert)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Incidents.Insert(0, new IncidentSummary(
                alert.IncidentId, alert.DeviceId, alert.DriverId, alert.DriverName,
                alert.Type, alert.TimestampUtc, alert.Location, null, null, null, null));
        });
    }

    private void OnHubStateChanged(HubConnectionState state)
    {
        Application.Current.Dispatcher.Invoke(() => HubStatus = state.ToString());
    }
}
