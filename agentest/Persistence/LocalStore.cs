using System.Text.Json;
using agentest.Models;

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

    public async Task<List<AgentProfile>> LoadProfilesAsync()
    {
        Directory.CreateDirectory(ConversationsPath);
        if (!File.Exists(ProfilesPath)) return [new AgentProfile()];
        return JsonSerializer.Deserialize<List<AgentProfile>>(await File.ReadAllTextAsync(ProfilesPath), Options)
            ?? throw new InvalidDataException("El archivo de perfiles no contiene una lista válida.");
    }

    public Task SaveProfilesAsync(List<AgentProfile> profiles) => WriteAsync(ProfilesPath, profiles);

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
