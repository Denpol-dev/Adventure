namespace Foxnet.Session;

public interface IConsoleHost
{
    ConsoleIdentity Launch();
    bool IsRunning(ConsoleIdentity identity);
    void Stop(ConsoleIdentity identity);
}

public sealed class DeviceConnectionService(LocalSession session, IConsoleHost console)
{
    private readonly object _gate = new();

    public IReadOnlyList<Device> GetDevices()
    {
        lock (_gate)
            return session.GetDevices().Select(Reconcile).ToArray();
    }

    public Device Connect(string deviceId)
    {
        lock (_gate)
        {
            var device = Reconcile(session.GetDevice(deviceId));
            if (!device.Authorized)
                throw new InvalidOperationException("Доступ не подтвержден.");
            if (device.Connection != null)
                return device;

            var identity = console.Launch();
            try
            {
                if (!session.SetConnection(deviceId, identity, null))
                    throw new InvalidOperationException("Состояние устройства изменилось.");
            }
            catch
            {
                console.Stop(identity);
                throw;
            }
            return device with { Connection = identity };
        }
    }

    public Device Disconnect(string deviceId)
    {
        lock (_gate)
            return Disconnect(session.GetDevice(deviceId));
    }

    public void RevokeAccess(string deviceId)
    {
        lock (_gate)
        {
            Disconnect(session.GetDevice(deviceId));
            session.RevokeAccess(deviceId);
        }
    }

    private Device Disconnect(Device device)
    {
        if (device.Connection is ConsoleIdentity identity)
        {
            console.Stop(identity);
            session.SetConnection(device.Id, null, identity);
        }
        return device with { Connection = null };
    }

    private Device Reconcile(Device device)
    {
        if (device.Connection is not ConsoleIdentity identity || console.IsRunning(identity))
            return device;

        session.SetConnection(device.Id, null, identity);
        return device with { Connection = null };
    }
}
