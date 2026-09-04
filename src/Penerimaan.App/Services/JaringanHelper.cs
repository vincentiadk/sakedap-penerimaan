using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Penerimaan.App.Services;

/// <summary>
/// Menentukan alamat IP PC ini, yang jadi dasar penentuan lokasi.
/// </summary>
public static class JaringanHelper
{
    /// <summary>
    /// IPv4 dari kartu jaringan yang sedang aktif.
    ///
    /// PC bisa punya beberapa alamat sekaligus - Wi-Fi, kabel, adapter virtual
    /// dari VPN atau Docker. Kartu loopback dan tunnel dibuang, dan yang
    /// diutamakan adalah kartu berkabel karena PC pos satpam dipasang tetap.
    ///
    /// Mengembalikan string kosong bila tidak ada yang cocok.
    /// </summary>
    public static string IpLokal()
    {
        var kandidat = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .Where(n => n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .SelectMany(n => n.GetIPProperties().UnicastAddresses
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Where(a => !IPAddress.IsLoopback(a.Address))
                .Select(a => new
                {
                    Alamat = a.Address.ToString(),
                    Kabel = n.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                }))
            .ToList();

        if (kandidat.Count == 0) return "";

        return kandidat.OrderByDescending(k => k.Kabel).First().Alamat;
    }
}
