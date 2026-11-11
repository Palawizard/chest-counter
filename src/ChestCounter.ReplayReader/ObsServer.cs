using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ChestCounter;

// The page for an OBS browser source. It only listens on the loopback address, only answers GET,
// and refuses any Host other than its own, so a web page cannot reach it through DNS rebinding.
public sealed class ObsServer : IDisposable
{
    public const int FirstPort = 28470;
    private const int Ports = 10;
    private const int MaxRequestBytes = 8192;
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);

    private static readonly Dictionary<string, (string File, string Type)> Files = new()
    {
        ["/"] = ("obs.html", "text/html; charset=utf-8"),
        ["/obs.css"] = ("obs.css", "text/css; charset=utf-8"),
        ["/obs.js"] = ("obs.js", "text/javascript; charset=utf-8"),
        ["/fonts/LilitaOne.ttf"] = ("fonts/LilitaOne.ttf", "font/ttf"),
    };

    private readonly TcpListener _listener;
    private readonly string _webRoot;
    private readonly Func<string> _state;
    private readonly CancellationTokenSource _stop = new();

    private ObsServer(TcpListener listener, string webRoot, Func<string> state)
    {
        _listener = listener;
        _webRoot = webRoot;
        _state = state;
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _ = AcceptAsync();
    }

    public int Port { get; }

    public string Address => $"http://127.0.0.1:{Port}/";

    // Takes the first free port from FirstPort, so another app on that port does not stop the page.
    public static ObsServer Start(string webRoot, Func<string> state, int firstPort = FirstPort)
    {
        for (var port = firstPort; port < firstPort + Ports; port++)
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            try
            {
                listener.Start();
                return new ObsServer(listener, webRoot, state);
            }
            catch (SocketException)
            {
                listener.Dispose();
            }
        }

        throw new IOException($"no free port from {firstPort} to {firstPort + Ports - 1}");
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }

            _ = AnswerAsync(client);
        }
    }

    private async Task AnswerAsync(TcpClient client)
    {
        using var _ = client;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        timeout.CancelAfter(ReadTimeout);
        try
        {
            var stream = client.GetStream();
            var head = await ReadHeadAsync(stream, timeout.Token);
            var (status, type, body) = head is null ? (400, "text/plain", "bad request"u8.ToArray()) : Answer(head);
            var header = $"HTTP/1.1 {status} {Reason(status)}\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(header), timeout.Token);
            await stream.WriteAsync(body, timeout.Token);
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException or SocketException)
        {
            // A source that disconnects or never finishes its request is simply dropped.
        }
    }

    internal (int Status, string Type, byte[] Body) Answer(string head)
    {
        var lines = head.Split("\r\n");
        var request = lines[0].Split(' ');
        if (request.Length != 3)
        {
            return (400, "text/plain", "bad request"u8.ToArray());
        }

        var host = lines.Skip(1).FirstOrDefault(line => line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))?[5..].Trim();
        if (host != $"127.0.0.1:{Port}" && host != $"localhost:{Port}")
        {
            return (403, "text/plain", "forbidden"u8.ToArray());
        }

        if (request[0] != "GET")
        {
            return (405, "text/plain", "method not allowed"u8.ToArray());
        }

        var path = request[1].Split('?')[0];
        if (path == "/state")
        {
            return (200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(_state()));
        }

        return Files.TryGetValue(path, out var file)
            ? (200, file.Type, File.ReadAllBytes(Path.Combine(_webRoot, file.File)))
            : (404, "text/plain", "not found"u8.ToArray());
    }

    private static async Task<string?> ReadHeadAsync(NetworkStream stream, CancellationToken cancellation)
    {
        var buffer = new byte[MaxRequestBytes];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), cancellation);
            if (read == 0)
            {
                return null;
            }

            length += read;
            var end = buffer.AsSpan(0, length).IndexOf("\r\n\r\n"u8);
            if (end >= 0)
            {
                return Encoding.ASCII.GetString(buffer, 0, end);
            }
        }

        return null;
    }

    private static string Reason(int status) => status switch
    {
        200 => "OK",
        403 => "Forbidden",
        404 => "Not Found",
        405 => "Method Not Allowed",
        _ => "Bad Request",
    };
}
