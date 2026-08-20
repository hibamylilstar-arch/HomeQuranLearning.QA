using System.Text.Json;

namespace Academy.Agent.Cloud;

public sealed class FileDeviceIdentityProvider : IDeviceIdentityProvider
{
    private readonly string _identityFilePath;
    private readonly string _fallbackDeviceName;

    public FileDeviceIdentityProvider(
        string identityFilePath,
        string fallbackDeviceName)
    {
        _identityFilePath = identityFilePath;
        _fallbackDeviceName = fallbackDeviceName;
    }

    public async Task<DeviceIdentity> GetOrCreateIdentityAsync(
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_identityFilePath)!);

        if (File.Exists(_identityFilePath))
        {
            string json = await File.ReadAllTextAsync(_identityFilePath, cancellationToken);
            var identity = JsonSerializer.Deserialize<DeviceIdentity>(json);

            if (identity is not null && !string.IsNullOrWhiteSpace(identity.DeviceId))
            {
                return identity;
            }
        }

        var newIdentity = new DeviceIdentity
        {
            DeviceId = Guid.NewGuid().ToString("D"),
            DeviceName = _fallbackDeviceName
        };

        string newJson = JsonSerializer.Serialize(newIdentity, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(_identityFilePath, newJson, cancellationToken);

        return newIdentity;
    }
}