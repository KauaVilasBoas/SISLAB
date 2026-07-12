using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace SISLAB.Modules.Inventory.Tests.Application.StockRead;

/// <summary>
/// A <see cref="FactAttribute"/> that skips the test (rather than failing it) when no Docker daemon is
/// reachable. The Testcontainers-backed tenant-isolation tests need a live Docker to spin up PostgreSQL;
/// on a machine/CI leg without Docker the test is reported as skipped, not red, so the suite stays green
/// (card [E4] #34 requirement: skip gracefully when Docker is unavailable).
/// </summary>
/// <remarks>
/// The probe is dependency-free (no Docker.DotNet coupling): it checks the platform's default Docker
/// endpoint — the <c>\\.\pipe\docker_engine</c> named pipe on Windows, the <c>/var/run/docker.sock</c> Unix
/// socket elsewhere — honouring a <c>DOCKER_HOST</c> override when present. The result is evaluated once and
/// cached (a static, lazily-evaluated flag) so every decorated test shares a single cheap check.
/// </remarks>
public sealed class DockerAvailableFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> DockerReachable = new(ProbeDocker);

    public DockerAvailableFactAttribute()
    {
        if (!IsDockerAvailable)
        {
            Skip = "Docker is not available on this machine; the Testcontainers PostgreSQL isolation test is skipped.";
        }
    }

    /// <summary>Whether a Docker daemon is reachable — shared with the test's lifecycle so it can no-op on skip.</summary>
    public static bool IsDockerAvailable => DockerReachable.Value;

    private static bool ProbeDocker()
    {
        // An explicit DOCKER_HOST (e.g. a TCP endpoint) means someone configured Docker access — trust it.
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST")))
        {
            return true;
        }

        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? WindowsPipeReachable()
            : UnixSocketReachable();
    }

    private static bool WindowsPipeReachable()
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", "docker_engine", PipeDirection.InOut);
            pipe.Connect(timeout: 500);
            return pipe.IsConnected;
        }
        catch
        {
            return false;
        }
    }

    private static bool UnixSocketReachable()
    {
        const string socketPath = "/var/run/docker.sock";
        if (!File.Exists(socketPath))
        {
            return false;
        }

        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.Connect(new UnixDomainSocketEndPoint(socketPath));
            return socket.Connected;
        }
        catch
        {
            return false;
        }
    }
}
