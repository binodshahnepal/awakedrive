using System.IO;
using System.Windows;
using Dms.Client.Api;
using Dms.Desktop.DriverHud.Services;
using Dms.Desktop.DriverHud.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dms.Desktop.DriverHud;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: true);
            })
            .ConfigureServices((context, services) =>
            {
                // AuthSession is Singleton: this is a single-user desktop app.
                services.AddDmsApiClient(
                    options => context.Configuration.GetSection("Api").Bind(options),
                    authSessionLifetime: ServiceLifetime.Singleton);

                var modelPath = context.Configuration["Perception:ModelPath"] ?? "models/face_landmarks.onnx";
                var resolvedModelPath = Path.IsPathRooted(modelPath) ? modelPath : Path.Combine(AppContext.BaseDirectory, modelPath);

                services.AddSingleton<CameraService>();
                services.AddSingleton<DeviceIdentityService>();
                services.AddSingleton<IncidentQueueService>();
                services.AddSingleton<IFaceLandmarkEngine>(provider =>
                    new OnnxFaceLandmarkEngine(provider.GetRequiredService<ILogger<OnnxFaceLandmarkEngine>>(), resolvedModelPath));

                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
