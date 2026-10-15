using System.Text.Json;
using agentest.Models;

namespace agentest.Services;

public static class AgentGroups
{
    public static AgentCatalog Copy(AgentCatalog catalog) => JsonSerializer.Deserialize<AgentCatalog>(JsonSerializer.Serialize(catalog))!;

    public static void Normalize(AgentCatalog catalog)
    {
        if (catalog.Groups is null || catalog.Profiles is null || catalog.Groups.Any(g => string.IsNullOrWhiteSpace(g)))
            throw new InvalidDataException("El catálogo de grupos no es válido.");
        catalog.Groups = catalog.Groups.Select(g => g.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!catalog.Groups.Contains("DEFAULT", StringComparer.OrdinalIgnoreCase)) catalog.Groups.Insert(0, "DEFAULT");
        var defaultIndex = catalog.Groups.FindIndex(g => IsDefault(g));
        catalog.Groups[defaultIndex] = "DEFAULT";
        foreach (var profile in catalog.Profiles)
        {
            if (profile is null) throw new InvalidDataException("El catálogo contiene un perfil nulo.");
            var group = string.IsNullOrWhiteSpace(profile.AgentSystem) ? "DEFAULT" : profile.AgentSystem.Trim();
            if (!catalog.Groups.Contains(group, StringComparer.OrdinalIgnoreCase)) catalog.Groups.Add(group);
            profile.AgentSystem = catalog.Groups.First(g => string.Equals(g, group, StringComparison.OrdinalIgnoreCase));
            profile.Health ??= new HealthSettings();
        }
    }

    public static bool IsDefault(string name) => string.Equals(name, "DEFAULT", StringComparison.OrdinalIgnoreCase);

    public static void Add(AgentCatalog catalog, string name)
    {
        name = ValidateName(catalog, name);
        catalog.Groups.Add(name);
    }

    public static void Rename(AgentCatalog catalog, string oldName, string newName)
    {
        RequireEditable(catalog, oldName);
        newName = ValidateName(catalog, newName, oldName);
        var index = catalog.Groups.FindIndex(g => string.Equals(g, oldName, StringComparison.OrdinalIgnoreCase));
        catalog.Groups[index] = newName;
        foreach (var profile in catalog.Profiles.Where(p => string.Equals(p.AgentSystem, oldName, StringComparison.OrdinalIgnoreCase)))
            profile.AgentSystem = newName;
    }

    public static void Delete(AgentCatalog catalog, string name)
    {
        RequireEditable(catalog, name);
        foreach (var profile in catalog.Profiles.Where(p => string.Equals(p.AgentSystem, name, StringComparison.OrdinalIgnoreCase)))
            profile.AgentSystem = "DEFAULT";
        catalog.Groups.RemoveAll(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase));
    }

    private static string ValidateName(AgentCatalog catalog, string name, string? except = null)
    {
        name = name.Trim();
        if (name.Length is 0 or > 100 || name.Any(char.IsControl))
            throw new ArgumentException("Indica un nombre de grupo de 1 a 100 caracteres, sin saltos de línea.");
        if (catalog.Groups.Any(g => !string.Equals(g, except, StringComparison.OrdinalIgnoreCase) && string.Equals(g, name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Ya existe un grupo con ese nombre.");
        return name;
    }

    private static void RequireEditable(AgentCatalog catalog, string name)
    {
        if (IsDefault(name)) throw new ArgumentException("DEFAULT es el grupo de respaldo y no se puede renombrar ni eliminar.");
        if (!catalog.Groups.Contains(name, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("El grupo ya no existe.");
    }
}
