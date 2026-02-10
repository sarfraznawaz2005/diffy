using System;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Diffy.Core.Interfaces;

namespace Diffy.App.Services;

public class SingleInstanceService : ISingleInstanceService
{
    private const string PipeName = "DiffySingleInstancePipe";
    private const string SocketName = "Diffy-single-instance.sock";
    private readonly CancellationTokenSource _cts = new();
    private bool _isListening = false;
    public event Action<string>? ArgumentsReceived;

    public void StartListening()
    {
        if (_isListening)
            return;

        _isListening = true;

#if WINDOWS
        StartNamedPipeServer();
#else
        StartUnixDomainSocketServer();
#endif
    }

    public void StopListening()
    {
        _cts.Cancel();
        _isListening = false;
    }

    public async Task SendArgsAsync(string args)
    {
#if WINDOWS
        await SendToNamedPipeAsync(args);
#else
        await SendToUnixDomainSocketAsync(args);
#endif
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

#if WINDOWS
    #region Windows Named Pipes Implementation
    
    private void StartNamedPipeServer()
    {
        Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                    var connectionTask = server.WaitForConnectionAsync(_cts.Token);

                    await connectionTask;

                    using var reader = new StreamReader(server);
                    var args = await reader.ReadToEndAsync();

                    if (!string.IsNullOrEmpty(args))
                    {
                        ArgumentsReceived?.Invoke(args);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Log or handle pipe errors
                    if (!_cts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(1000, _cts.Token);
                    }
                }
            }
        });
    }

    private async Task SendToNamedPipeAsync(string args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            await client.ConnectAsync(1000);

            using var writer = new StreamWriter(client);
            await writer.WriteAsync(args);
            await writer.FlushAsync();
        }
        catch (Exception)
        {
            // First instance might not be listening yet or is busy
        }
    }

    #endregion
#else
    #region Unix Domain Sockets Implementation (macOS/Linux)

    private void StartUnixDomainSocketServer()
    {
        Task.Run(async () =>
        {
            var socketPath = GetSocketPath();

            // Remove existing socket file if it exists (cleanup from previous run)
            if (File.Exists(socketPath))
            {
                try
                {
                    File.Delete(socketPath);
                }
                catch
                {
                    // Ignore if we can't delete it (might be in use)
                }
            }

            Socket? listener = null;

            try
            {
                listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                var endpoint = new UnixDomainSocketEndPoint(socketPath);
                listener.Bind(endpoint);
                listener.Listen(5); // Allow up to 5 pending connections

                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var client = await Task.Factory.FromAsync(
                            listener.BeginAccept,
                            listener.EndAccept,
                            null).ConfigureAwait(false);

                        // Handle connection in background to not block accepting new connections
                        _ = Task.Run(() => HandleSocketConnection(client, _cts.Token), _cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        // Log or handle socket errors
                        if (!_cts.Token.IsCancellationRequested)
                        {
                            await Task.Delay(1000, _cts.Token);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                // Log socket server error
                System.Diagnostics.Debug.WriteLine($"Socket server error: {ex.Message}");
            }
            finally
            {
                listener?.Dispose();

                // Clean up socket file on shutdown
                try
                {
                    if (File.Exists(socketPath))
                    {
                        File.Delete(socketPath);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }, _cts.Token);
    }

    private async Task HandleSocketConnection(Socket client, CancellationToken ct)
    {
        using (client)
        using (var networkStream = new NetworkStream(client, true))
        using (var reader = new StreamReader(networkStream, Encoding.UTF8))
        {
            try
            {
                var args = await reader.ReadToEndAsync(ct).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(args))
                {
                    ArgumentsReceived?.Invoke(args);
                }
            }
            catch (Exception ex)
            {
                // Log or handle connection error
                System.Diagnostics.Debug.WriteLine($"Socket connection error: {ex.Message}");
            }
        }
    }

    private async Task SendToUnixDomainSocketAsync(string args)
    {
        var socketPath = GetSocketPath();

        if (!File.Exists(socketPath))
        {
            // Socket file doesn't exist, no instance running
            return;
        }

        try
        {
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
            var endpoint = new UnixDomainSocketEndPoint(socketPath);

            await client.ConnectAsync(endpoint);

            using var networkStream = new NetworkStream(client, true);
            using var writer = new StreamWriter(networkStream, Encoding.UTF8);

            await writer.WriteAsync(args);
            await writer.FlushAsync();
        }
        catch (Exception)
        {
            // First instance might not be listening yet or is busy
        }
    }

    private static string GetSocketPath()
    {
        // Use app data directory for socket file
        var appDataPath = GetAppDataPath();
        if (string.IsNullOrEmpty(appDataPath))
            return SocketName; // Fallback

        return Path.Combine(appDataPath, SocketName);
    }

    private static string GetAppDataPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // On macOS, use ~/Library/Application Support/
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "Diffy");
        }
        else
        {
            // On Linux, use XDG_DATA_HOME or ~/.local/share/
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (string.IsNullOrEmpty(xdgDataHome))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var localShare = home != null ? Path.Combine(home, ".local", "share") : "./share";
                xdgDataHome = localShare;
            }
            return xdgDataHome != null ? Path.Combine(xdgDataHome, "diffy") : "./diffy";
        }
    }

    #endregion
#endif
}
