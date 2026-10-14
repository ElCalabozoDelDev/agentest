using agentest.Models;

namespace agentest.Services;

public static class AttachmentService
{
    public static async Task<ChatAttachment> ReadAsync(string path, AgentProfile profile)
    {
        if (!profile.AcceptsAttachments) throw new InvalidOperationException("Este agente no admite adjuntos.");
        var allowed = profile.AllowedExtensions.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e == "*" || e.StartsWith('.') ? e : "." + e).ToArray();
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (!allowed.Contains("*") && !allowed.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"El agente no admite archivos '{extension}'.");
        var maximum = profile.MaxAttachmentMegabytes * 1024L * 1024;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        if (stream.Length > maximum)
            throw new InvalidOperationException($"'{Path.GetFileName(path)}' supera el límite de {profile.MaxAttachmentMegabytes} MB.");
        // Read at most the configured limit, even if the file changes during reading.
        using var buffer = new MemoryStream();
        var bytes = new byte[81920];
        int count;
        while ((count = await stream.ReadAsync(bytes)) != 0)
        {
            if (buffer.Length + count > maximum) throw new InvalidOperationException("El adjunto supera el tamaño permitido.");
            await buffer.WriteAsync(bytes.AsMemory(0, count));
        }
        return new ChatAttachment
        {
            Name = Path.GetFileName(path), Size = buffer.Length,
            ContentType = extension switch
            {
                ".pdf" => "application/pdf", ".txt" or ".md" or ".csv" => "text/plain",
                ".json" => "application/json", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg",
                _ => "application/octet-stream"
            },
            Base64 = Convert.ToBase64String(buffer.ToArray())
        };
    }
}
