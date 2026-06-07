using System.Net;
using System.Net.NetworkInformation;
using ServerPing.Shared.Models;

namespace ServerPing.Service;

public class LocalNetworkMonitor
{
    private readonly object _lock = new();
    private LocalNetworkStatus _status = LocalNetworkStatus.Unknown;

    public event EventHandler? StatusChanged;

    public LocalNetworkStatus Status
    {
        get
        {
            lock (_lock)
            {
                return _status;
            }
        }
    }

    public bool Refresh()
    {
        var newStatus = IsNetworkAvailable()
            ? LocalNetworkStatus.Available
            : LocalNetworkStatus.NoNetwork;

        var changed = false;
        lock (_lock)
        {
            if (_status != newStatus)
            {
                _status = newStatus;
                changed = true;
            }
        }

        if (changed)
            StatusChanged?.Invoke(this, EventArgs.Empty);

        return newStatus == LocalNetworkStatus.Available;
    }

    private static bool IsNetworkAvailable()
    {
        try
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
                return false;

            return NetworkInterface.GetAllNetworkInterfaces().Any(IsUsableInterface);
        }
        catch (NetworkInformationException)
        {
            return false;
        }
    }

    private static bool IsUsableInterface(NetworkInterface networkInterface)
    {
        if (networkInterface.OperationalStatus != OperationalStatus.Up)
            return false;

        if (networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            return false;

        try
        {
            return networkInterface
                .GetIPProperties()
                .UnicastAddresses
                .Any(address => IsUsableAddress(address.Address));
        }
        catch (NetworkInformationException)
        {
            return false;
        }
    }

    private static bool IsUsableAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return false;

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            return true;

        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            && !address.IsIPv6LinkLocal
            && !address.IsIPv6Multicast
            && !address.IsIPv6SiteLocal;
    }
}
