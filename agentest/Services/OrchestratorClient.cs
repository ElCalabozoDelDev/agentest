using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using agentest.Models;

namespace agentest.Services;

public sealed record AgentReply(string Text, string RawResponse, string? ConversationId);

public sealed class OrchestratorClient : IDisposable
{
    private readonly HttpClient client;

    public OrchestratorClient(HttpMessageHandler? handler = null)
    {
        client = handler is null ? new HttpClient() : new HttpClient(handler);
        client.Timeout = Timeout.InfiniteTimeSpan;
    }

    public static void ValidateProfile(AgentProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name)) throw new ArgumentException("Ingresa un nombre para el perfil.");
        if (!Uri.TryCreate(profile.Endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
            throw new ArgumentException("El endpoint debe ser una URL absoluta HTTP o HTTPS.");
        if (!string.IsNullOrEmpty(uri.UserInfo)) throw new ArgumentException("Usa los campos de autenticación para las credenciales.");
        if (profile.Method is not ("POST" or "PUT" or "PATCH"))
            throw new ArgumentException("Selecciona POST, PUT o PATCH.");
        if (profile.TimeoutSeconds is < 1 or > 3600) throw new ArgumentException("El tiempo de espera debe estar entre 1 y 3600 segundos.");
        if (profile.MaxAttachmentMegabytes is < 1 or > 100) throw new ArgumentException("El límite de adjuntos debe estar entre 1 y 100 MB.");
        if (profile.AcceptsAttachments && string.IsNullOrWhiteSpace(profile.AllowedExtensions))
            throw new ArgumentException("Define extensiones permitidas o * para aceptar cualquier tipo.");
        if (JsonNode.Parse(profile.RequestTemplate) is not JsonObject)
            throw new ArgumentException("La plantilla de request debe ser un objeto JSON.");
        if (profile.AcceptsAttachments && !ContainsPlaceholder(JsonNode.Parse(profile.RequestTemplate), "{{attachments}}"))
            throw new ArgumentException("Para admitir adjuntos, agrega un valor JSON completo \"{{attachments}}\" a la plantilla.");
        if (profile.BackendCreatesConversation && string.IsNullOrWhiteSpace(profile.ResponseConversationIdPath))
            throw new ArgumentException("Define la ruta del ID de respuesta para reutilizar la conversación creada por el backend.");
        if (!string.IsNullOrWhiteSpace(profile.AuthenticationHeader))
        {
            using var request = new HttpRequestMessage();
            request.Headers.Add(profile.AuthenticationHeader, "validation");
        }
        if (profile.AuthenticationPrefix.Contains('\r') || profile.AuthenticationPrefix.Contains('\n'))
            throw new ArgumentException("El prefijo de autenticación no puede contener saltos de línea.");
    }

    private static bool ContainsPlaceholder(JsonNode? node, string placeholder) => node switch
    {
        JsonObject obj => obj.Any(p => ContainsPlaceholder(p.Value, placeholder)),
        JsonArray array => array.Any(p => ContainsPlaceholder(p, placeholder)),
        JsonValue value => value.TryGetValue<string>(out var text) && text == placeholder,
        _ => false
    };

    public static string BuildRequest(AgentProfile profile, Conversation conversation, ChatMessage message)
    {
        var history = conversation.Messages.Where(m => !ReferenceEquals(m, message) && m.DeliveryStatus == "completed")
            .Select(m => new { role = m.Role, content = m.Text });
        var attachments = message.Attachments.Select(a => new
        {
            name = a.Name, content_type = a.ContentType, size = a.Size, content_base64 = a.Base64
        });
        var values = new Dictionary<string, JsonNode?>
        {
            ["{{message}}"] = JsonValue.Create(message.Text),
            ["{{conversationId}}"] = JsonValue.Create(conversation.RemoteConversationId ??
                (profile.BackendCreatesConversation ? null : conversation.Id)),
            ["{{agentId}}"] = JsonValue.Create(profile.Id),
            ["{{history}}"] = JsonSerializer.SerializeToNode(history),
            ["{{attachments}}"] = JsonSerializer.SerializeToNode(attachments)
        };
        return Replace(JsonNode.Parse(profile.RequestTemplate), values)!.ToJsonString();
    }

    private static JsonNode? Replace(JsonNode? node, Dictionary<string, JsonNode?> values)
    {
        if (node is JsonObject obj)
            return new JsonObject(obj.Select(p => KeyValuePair.Create(p.Key, Replace(p.Value, values))));
        if (node is JsonArray array) return new JsonArray(array.Select(p => Replace(p, values)).ToArray());
        if (node is JsonValue value && value.TryGetValue<string>(out var text) && values.TryGetValue(text, out var replacement))
            return replacement?.DeepClone();
        return node?.DeepClone();
    }

    public static string ExtractResponseText(string raw, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return raw;
        using var document = JsonDocument.Parse(raw);
        var element = document.RootElement;
        foreach (var segment in path.Split('.'))
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(segment, out var property))
                element = property;
            else if (element.ValueKind == JsonValueKind.Array && int.TryParse(segment, out var index) && index >= 0 && index < element.GetArrayLength())
                element = element[index];
            else throw new InvalidDataException($"No existe la ruta '{path}' en la respuesta. Revisa el perfil del agente.");
        }
        if (element.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"La ruta '{path}' debe apuntar a un texto JSON.");
        return element.GetString() ?? "";
    }

    public async Task<AgentReply> SendAsync(AgentProfile profile, Conversation conversation,
        ChatMessage message, string token, CancellationToken cancellationToken)
    {
        ValidateProfile(profile);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(profile.TimeoutSeconds));
        using var request = new HttpRequestMessage(new HttpMethod(profile.Method), profile.Endpoint);
        request.Content = new StringContent(BuildRequest(profile, conversation, message), Encoding.UTF8, "application/json");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(token))
        {
            if (string.IsNullOrWhiteSpace(profile.AuthenticationHeader))
                throw new ArgumentException("Define el nombre del encabezado de autenticación en el perfil.");
            var prefix = profile.AuthenticationPrefix.Trim();
            request.Headers.Add(profile.AuthenticationHeader, string.IsNullOrEmpty(prefix) ? token : $"{prefix} {token}");
        }
        try
        {
            using var response = await client.SendAsync(request, timeout.Token);
            var raw = await response.Content.ReadAsStringAsync(timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                var detail = GetErrorDetail(raw);
                throw new HttpRequestException($"El orquestador respondió HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).{detail}", null, response.StatusCode);
            }
            var text = ExtractResponseText(raw, profile.ResponseTextPath.Trim());
            var conversationId = string.IsNullOrWhiteSpace(profile.ResponseConversationIdPath)
                ? null : ExtractResponseText(raw, profile.ResponseConversationIdPath.Trim());
            if (conversationId is not null && string.IsNullOrWhiteSpace(conversationId))
                throw new InvalidDataException("El identificador de conversación devuelto por el backend está vacío.");
            return new AgentReply(text, raw, conversationId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"El orquestador no respondió en {profile.TimeoutSeconds} segundos.");
        }
    }

    private static string GetErrorDetail(string raw)
    {
        var detail = raw.Trim();
        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("detail", out var value))
                detail = value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.GetRawText();
        }
        catch (JsonException) { /* Non-JSON error bodies are also useful when diagnosing the endpoint. */ }
        if (detail.Length > 2000) detail = detail[..2000] + "…";
        return string.IsNullOrWhiteSpace(detail) ? "" : "\n" + detail;
    }

    public void Dispose() => client.Dispose();
}
