using System.Text.Json;
using agentest.Models;
using agentest.Services;

namespace agentest.Persistence;

public sealed class LocalStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string RootPath { get; }
    public LocalStore(string? rootPath = null)
    {
        RootPath = rootPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Agentest");
    }
    private string ProfilesPath => Path.Combine(RootPath, "profiles.json");
    private string SettingsPath => Path.Combine(RootPath, "settings.json");
    private string ConversationsPath => Path.Combine(RootPath, "conversations");

    public async Task<AppSettings> LoadSettingsAsync()
    {
        if (!File.Exists(SettingsPath)) return new AppSettings();
        var settings = JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(SettingsPath), Options)
            ?? throw new InvalidDataException("El archivo de configuración no es válido.");
        if (!Enum.IsDefined(settings.Theme)) throw new InvalidDataException("El tema guardado no es válido.");
        return settings;
    }

    public Task SaveSettingsAsync(AppSettings settings)
    {
        if (!Enum.IsDefined(settings.Theme)) throw new ArgumentException("El tema no es válido.");
        return WriteAsync(SettingsPath, settings);
    }

    public async Task<AgentCatalog> LoadCatalogAsync()
    {
        Directory.CreateDirectory(ConversationsPath);
        if (!File.Exists(ProfilesPath)) return new AgentCatalog { Profiles = [new AgentProfile()] };
        var source = await File.ReadAllTextAsync(ProfilesPath);
        using var json = JsonDocument.Parse(source);
        var catalog = json.RootElement.ValueKind == JsonValueKind.Array
            ? new AgentCatalog { Profiles = JsonSerializer.Deserialize<List<AgentProfile>>(source, Options)! }
            : JsonSerializer.Deserialize<AgentCatalog>(source, Options);
        if (catalog is null || (json.RootElement.ValueKind == JsonValueKind.Object &&
            (!json.RootElement.TryGetProperty("Groups", out _) || !json.RootElement.TryGetProperty("Profiles", out _))))
            throw new InvalidDataException("El archivo de perfiles no contiene un catálogo válido.");
        AgentGroups.Normalize(catalog);
        return catalog;
    }

    public async Task<List<AgentProfile>> LoadProfilesAsync() => (await LoadCatalogAsync()).Profiles;

    public async Task SaveProfilesAsync(List<AgentProfile> profiles)
    {
        var catalog = await LoadCatalogAsync();
        catalog.Profiles = profiles;
        await SaveCatalogAsync(catalog);
    }

    public Task SaveCatalogAsync(AgentCatalog catalog)
    {
        var normalized = AgentGroups.Copy(catalog);
        AgentGroups.Normalize(normalized);
        return WriteAsync(ProfilesPath, normalized);
    }

    public async Task<(List<Conversation> Items, List<string> Errors)> LoadConversationsAsync()
    {
        Directory.CreateDirectory(ConversationsPath);
        List<Conversation> items = [];
        List<string> errors = [];
        foreach (var file in Directory.EnumerateFiles(ConversationsPath, "*.json"))
        {
            try
            {
                var conversation = JsonSerializer.Deserialize<Conversation>(await File.ReadAllTextAsync(file), Options);
                if (conversation is null || !Guid.TryParseExact(conversation.Id, "N", out _))
                    throw new InvalidDataException("Identificador de conversación inválido.");
                items.Add(conversation);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }
        return (items.OrderByDescending(c => c.UpdatedAt).ToList(), errors);
    }

    public Task SaveConversationAsync(Conversation conversation)
    {
        if (!Guid.TryParseExact(conversation.Id, "N", out _))
            throw new InvalidDataException("Identificador de conversación inválido.");
        return WriteAsync(Path.Combine(ConversationsPath, conversation.Id + ".json"), conversation);
    }

    private static async Task WriteAsync<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(value, Options));
        File.Move(temporaryPath, path, overwrite: true);
    }
}
