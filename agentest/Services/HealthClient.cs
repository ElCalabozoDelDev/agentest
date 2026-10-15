using System.Diagnostics;
using System.Text.Json;
using agentest.Models;

namespace agentest.Services;

public sealed class HealthClient : IDisposable
{
    private readonly HttpClient client;
    private readonly SemaphoreSlim concurrency = new(4);

    public HealthClient(HttpMessageHandler? handler = null)
    {
        client = handler is null
            ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            : new HttpClient(handler);
        client.Timeout = Timeout.InfiniteTimeSpan;
    }

    public static void Validate(HealthSettings health, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(health.Endpoint))
        {
            if (required) throw new ArgumentException("Indica el endpoint de health del agente.");
            return;
        }
        if (!Uri.TryCreate(health.Endpoint, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("El endpoint de health debe ser HTTP/HTTPS y no contener credenciales.");
        if (health.Method is not ("GET" or "HEAD" or "POST"))
            throw new ArgumentException("El método de health debe ser GET, HEAD o POST (sin cuerpo).");
        if (health.Method == "HEAD" && !string.IsNullOrWhiteSpace(health.ResponseValuePath))
            throw new ArgumentException("HEAD no permite validar un campo JSON.");
        if (health.IntervalSeconds is < 1 or > 86400 || health.TimeoutSeconds is < 1 or > 300 ||
            health.ExpectedStatusCode is < 100 or > 599)
            throw new ArgumentException("Revisa el intervalo, timeout y código HTTP de health.");
        using var request = new HttpRequestMessage();
        if (!string.IsNullOrEmpty(health.AuthenticationHeader) &&
            !request.Headers.TryAddWithoutValidation(health.AuthenticationHeader, "test"))
            throw new ArgumentException("El encabezado de health no es válido.");
        if (health.AuthenticationPrefix is null || health.AuthenticationPrefix.Contains('\r') || health.AuthenticationPrefix.Contains('\n'))
            throw new ArgumentException("El prefijo de health no es válido.");
    }

    public async Task<AgentHealth> CheckAsync(HealthSettings health, string credential, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(health.Endpoint))
            return new("Sin configurar", "Configura health en Editar perfil.", DateTimeOffset.Now, 0);
        await concurrency.WaitAsync(cancellation);
        var watch = Stopwatch.StartNew();
        try
        {
            Validate(health);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(health.TimeoutSeconds));
            using var request = new HttpRequestMessage(new HttpMethod(health.Method), health.Endpoint);
            if (!string.IsNullOrEmpty(credential))
            {
                if (string.IsNullOrWhiteSpace(health.AuthenticationHeader) || credential.Contains('\r') || credential.Contains('\n'))
                    throw new ArgumentException("Revisa la autenticación de health.");
                var value = string.IsNullOrEmpty(health.AuthenticationPrefix) ? credential : health.AuthenticationPrefix + " " + credential;
                if (!request.Headers.TryAddWithoutValidation(health.AuthenticationHeader, value))
                    throw new ArgumentException("El encabezado de health no es válido.");
            }
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if ((int)response.StatusCode != health.ExpectedStatusCode)
                return Result("No saludable", $"HTTP {(int)response.StatusCode}; esperado {health.ExpectedStatusCode}.");
            if (!string.IsNullOrWhiteSpace(health.ResponseValuePath))
            {
                // Health responses are small; bound reads even when Content-Length is absent.
                await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                using var buffer = new MemoryStream();
                var bytes = new byte[4096];
                int count;
                while ((count = await stream.ReadAsync(bytes, timeout.Token)) > 0)
                {
                    if (buffer.Length + count > 65536) return Result("Error", "La respuesta de health supera 64 KB.");
                    buffer.Write(bytes, 0, count);
                }
                using var json = JsonDocument.Parse(buffer.ToArray());
                var value = json.RootElement;
                foreach (var segment in health.ResponseValuePath.Split('.'))
                {
                    if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty(segment, out var property)) value = property;
                    else if (value.ValueKind == JsonValueKind.Array && int.TryParse(segment, out var index) && index >= 0 && index < value.GetArrayLength()) value = value[index];
                    else return Result("No saludable", "No se encontró el campo JSON configurado.");
                }
                var actual = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
                if (!string.Equals(actual, health.ExpectedValue, StringComparison.Ordinal))
                    return Result("No saludable", "El valor JSON no coincide con el esperado.");
            }
            return Result("Saludable", $"HTTP {(int)response.StatusCode}");
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { return Result("Tiempo agotado", "Se superó el timeout de health."); }
        catch (HttpRequestException) { return Result("Sin conexión", "No se pudo conectar con health."); }
        catch (JsonException) { return Result("Error", "La respuesta de health no es JSON válido."); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or FormatException)
        { return Result("Error", "Revisa la configuración o la respuesta de health."); }
        finally { concurrency.Release(); }

        AgentHealth Result(string state, string detail) => new(state, detail, DateTimeOffset.Now, watch.ElapsedMilliseconds);
    }

    public void Dispose() => client.Dispose();
}
