using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhishAnalyser.Desktop.Analysis;

/// <summary>Accepts authenticated header blocks from the local Outlook add-in.</summary>
public sealed class LoopbackHeaderBridge : IDisposable
{
    public const string Url = "http://127.0.0.1:8765/headers";
    private const int MaxBodyBytes = 64 * 1024;
    private const string AllowedOrigin = "https://localhost:3000";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpListener _listener = new();
    private readonly string _token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly Func<string, Task<BridgeAnalysisResponse>> _analyse;
    private bool _disposed;

    public LoopbackHeaderBridge(Func<string, Task<BridgeAnalysisResponse>> analyse)
    {
        _analyse = analyse;
        _listener.Prefixes.Add("http://127.0.0.1:8765/");
        _listener.Start();
        _ = Task.Run(ReceiveLoopAsync);
    }

    public string Token => _token;

    private async Task ReceiveLoopAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (HttpListenerException) when (!_listener.IsListening) { break; }
            catch (ObjectDisposedException) { break; }

            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        try
        {
            var origin = request.Headers["Origin"];
            if (origin is not null && !string.Equals(origin, AllowedOrigin, StringComparison.Ordinal))
            {
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            if (origin is not null)
            {
                response.Headers["Access-Control-Allow-Origin"] = AllowedOrigin;
                response.Headers["Vary"] = "Origin";
                response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
                response.Headers["Access-Control-Allow-Headers"] = "Content-Type, X-Phish-Analyser-Token";
            }

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = (int)HttpStatusCode.NoContent;
                return;
            }

            if (request.HttpMethod == "GET" && request.Url?.AbsolutePath == "/session")
            {
                if (!string.Equals(origin, AllowedOrigin, StringComparison.Ordinal))
                {
                    response.StatusCode = (int)HttpStatusCode.Forbidden;
                    return;
                }

                await WriteJsonAsync(response, new { token = _token });
                return;
            }

            if (request.HttpMethod != "POST" || request.Url?.AbsolutePath != "/headers")
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            if (!HasValidToken(request.Headers["X-Phish-Analyser-Token"]))
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            if (request.ContentLength64 <= 0 || request.ContentLength64 > MaxBodyBytes ||
                !string.Equals(request.ContentType?.Split(';')[0], "application/json", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            var payload = JsonSerializer.Deserialize<HeaderPayload>(body, JsonOptions);
            if (string.IsNullOrWhiteSpace(payload?.Headers))
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            var analysis = await _analyse(payload.Headers);
            await WriteJsonAsync(response, analysis);
        }
        catch (JsonException exception)
        {
            response.StatusCode = (int)HttpStatusCode.BadRequest;
            response.StatusDescription = "Invalid JSON payload: " + exception.Message;
        }
        catch (HttpListenerException) { }
        catch (ObjectDisposedException) { }
        catch (Exception)
        {
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
        finally
        {
            try { response.Close(); } catch (ObjectDisposedException) { }
        }
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, object value)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions));
        response.StatusCode = (int)HttpStatusCode.OK;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
    }

    private bool HasValidToken(string? candidate)
    {
        if (candidate is null || candidate.Length != _token.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(candidate), Encoding.UTF8.GetBytes(_token));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _listener.Close();
    }

    private sealed record HeaderPayload([property: JsonPropertyName("headers")] string? Headers);
}

public sealed record BridgeAnalysisResponse(
    string State,
    int Number,
    string Headline,
    string Detail,
    IReadOnlyList<BridgeFinding> Findings);

public sealed record BridgeFinding(string Rule, string Status, string Explanation);
