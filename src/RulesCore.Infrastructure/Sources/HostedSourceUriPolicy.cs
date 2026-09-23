using System.Net;
using System.Net.Sockets;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Central security policy for remotely acquired hosted/current-user source URIs.
/// Validation is deliberately separate from HTTP resolution so every remote-source
/// workflow enforces the same scheme, credential, and public-address requirements.
/// </summary>
internal static class HostedSourceUriPolicy
{
    internal static void ValidateShape(Uri uri) =>
        ValidateShape(uri, "Hosted source");

    internal static void ValidateShape(Uri uri, string sourceLabel)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"{sourceLabel} URIs must use HTTPS.");
        }
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException($"{sourceLabel} URIs can not contain embedded credentials.");
        }
        if (string.Equals(uri.DnsSafeHost, "localhost", StringComparison.OrdinalIgnoreCase)
            || uri.AbsoluteUri.Length > 2000)
        {
            throw new ArgumentException($"{sourceLabel} URI is not allowed.");
        }
    }

    internal static Task EnsureRemoteSafeAsync(
        Uri uri,
        CancellationToken cancellationToken) =>
        EnsureRemoteSafeAsync(uri, "Hosted source", cancellationToken);

    internal static async Task EnsureRemoteSafeAsync(
        Uri uri,
        string sourceLabel,
        CancellationToken cancellationToken)
    {
        ValidateShape(uri, sourceLabel);
        if (IPAddress.TryParse(uri.DnsSafeHost, out var literal))
        {
            if (!IsPublicAddress(literal))
            {
                throw new InvalidOperationException(
                    $"{sourceLabel} address '{uri.DnsSafeHost}' is not a public network address.");
            }
            return;
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(
                uri.DnsSafeHost,
                cancellationToken);
        }
        catch (SocketException exception)
        {
            throw new HttpRequestException(
                $"{sourceLabel} host '{uri.DnsSafeHost}' could not be resolved.",
                exception);
        }

        if (addresses.Length == 0
            || addresses.Any(address => !IsPublicAddress(address)))
        {
            throw new InvalidOperationException(
                $"{sourceLabel} host '{uri.DnsSafeHost}' resolves to a non-public network address.");
        }
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }
        if (IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.None)
            || address.Equals(IPAddress.IPv6None))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return !(bytes[0] == 0
                || bytes[0] == 10
                || bytes[0] == 127
                || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
                || (bytes[0] == 169 && bytes[1] == 254)
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || bytes[0] >= 224);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !address.IsIPv6LinkLocal
                && !address.IsIPv6Multicast
                && !(bytes[0] is 0xfc or 0xfd);
        }

        return false;
    }
}