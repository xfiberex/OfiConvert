using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OfiConvert.Tests;

/// <summary>
/// Servidor HTTP mínimo en localhost para ejercer la descarga y la verificación de verdad, extremo a
/// extremo, sin salir a la red.
/// </summary>
/// <remarks>
/// Sobre <see cref="TcpListener"/> y no <see cref="HttpListener"/> a propósito: en Windows,
/// <c>HttpListener</c> exige reservar la URL (netsh) o correr como ADMINISTRADOR, y eso convertiría
/// unas pruebas normales en pruebas que solo pasan en una terminal elevada. Es la trampa que
/// documenta WingetUSoft.
/// </remarks>
internal sealed class LocalHttpServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly Dictionary<string, byte[]> _routes = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _cts = new();

    public LocalHttpServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0); // puerto 0 = el sistema elige uno libre
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = AcceptLoopAsync(_cts.Token);
    }

    public int Port { get; }

    public string UrlFor(string path) => $"http://127.0.0.1:{Port}{path}";

    public string Serve(string path, byte[] content)
    {
        _routes[path] = content;
        return UrlFor(path);
    }

    public string Serve(string path, string content) => Serve(path, Encoding.UTF8.GetBytes(content));

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }

            _ = HandleAsync(client, ct);
        }
    }

    private async Task HandleAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            try
            {
                NetworkStream stream = client.GetStream();

                // Basta con la primera línea ("GET /ruta HTTP/1.1") para saber qué se pide.
                var buffer = new byte[8192];
                int read = await stream.ReadAsync(buffer, ct);
                if (read <= 0) return;

                string request = Encoding.ASCII.GetString(buffer, 0, read);
                string path = request.Split(' ').Skip(1).FirstOrDefault() ?? "/";

                if (!_routes.TryGetValue(path, out byte[]? body))
                {
                    await WriteAsync(stream, "404 Not Found", [], ct);
                    return;
                }

                await WriteAsync(stream, "200 OK", body, ct);
            }
            catch (IOException) { }
            catch (OperationCanceledException) { }
        }
    }

    private static async Task WriteAsync(NetworkStream stream, string status, byte[] body, CancellationToken ct)
    {
        // Content-Length explícito: es lo que hace que el cliente pueda reportar progreso.
        string headers =
            $"HTTP/1.1 {status}\r\n" +
            "Content-Type: application/octet-stream\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Connection: close\r\n\r\n";

        await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), ct);
        if (body.Length > 0)
            await stream.WriteAsync(body, ct);
        await stream.FlushAsync(ct);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _cts.Dispose();
    }
}
