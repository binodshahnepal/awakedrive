using System.IO;
using System.Text.Json;

namespace Dms.Desktop.DriverHud.Services;

/// <summary>
/// Generates and persists a stable device UUID for this machine (the same
/// role a mobile app's ANDROID_ID / identifierForVendor plays), so re-running
/// the HUD re-registers the *same* device instead of creating a new one each
/// launch. Stored under %AppData%\AwakeDrive\device.json.
/// </summary>
public class DeviceIdentityService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AwakeDrive", "device.json");

    public string GetOrCreateDeviceUuid()
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);

        if (File.Exists(FilePath))
        {
            var existing = JsonSerializer.Deserialize<DeviceIdentity>(File.ReadAllText(FilePath));
            if (existing is not null && !string.IsNullOrWhiteSpace(existing.DeviceUuid))
            {
                return existing.DeviceUuid;
            }
        }

        var identity = new DeviceIdentity(Guid.NewGuid().ToString());
        File.WriteAllText(FilePath, JsonSerializer.Serialize(identity));
        return identity.DeviceUuid;
    }

    private record DeviceIdentity(string DeviceUuid);
}
