using System.Net;
using System.Net.NetworkInformation;
using ServerPing.Shared.Models;

namespace ServerPing.Service;

public class LocalNetworkMonitor : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(10);

    private readonly object _lock = new();
    private LocalNetworkStatus _status = LocalNetworkStatus.Unknown;
    private System.Threading.Timer? _timer;
    private bool _isDetectionEnabled;
    private bool _disposed;

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

    public bool IsAvailable => Status != LocalNetworkStatus.NoNetwork;

    public void UpdateMonitoringContext(bool hasServers, bool hasEnabledServers, bool shouldDetectNetwork)
    {
        var changed = false;

        lock (_lock)
        {
            if (_disposed)
                return;

            if (!hasServers || !hasEnabledServers)
            {
                StopDetectionNoLock();
                changed = SetStatusNoLock(LocalNetworkStatus.Disabled);
            }
            else if (shouldDetectNetwork)
            {
                StartDetectionNoLock();
            }
            else
            {
                StopDetectionNoLock();
                changed = SetStatusNoLock(LocalNetworkStatus.Unknown);
            }
        }

        if (changed)
            RaiseStatusChanged();
    }

    private void StartDetectionNoLock()
    {
        if (_isDetectionEnabled)
            return;

        _isDetectionEnabled = true;
        _timer ??= new System.Threading.Timer(_ => RefreshCachedStatus(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timer.Change(TimeSpan.Zero, RefreshInterval);
    }

    private void StopDetectionNoLock()
    {
        if (!_isDetectionEnabled)
            return;

        _isDetectionEnabled = false;
        _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private void RefreshCachedStatus()
    {
        var newStatus = DetectNetworkAvailability()
            ? LocalNetworkStatus.Available
            : LocalNetworkStatus.NoNetwork;

        UpdateStatus(newStatus);
    }

    private void UpdateStatus(LocalNetworkStatus newStatus)
    {
        var changed = false;
        lock (_lock)
        {
            changed = SetStatusNoLock(newStatus);
        }

        if (changed)
            RaiseStatusChanged();
    }

    private void RaiseStatusChanged()
    {
        ThreadPool.QueueUserWorkItem(_ => StatusChanged?.Invoke(this, EventArgs.Empty));
    }

    private bool SetStatusNoLock(LocalNetworkStatus newStatus)
    {
        if (_status == newStatus)
            return false;

        _status = newStatus;
        return true;
    }

    private static bool DetectNetworkAvailability()
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

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }
}
