namespace Academy.Agent.Cloud;

public interface IDeviceIdentityProvider
{
    Task<DeviceIdentity> GetOrCreateIdentityAsync(CancellationToken cancellationToken = default);
}