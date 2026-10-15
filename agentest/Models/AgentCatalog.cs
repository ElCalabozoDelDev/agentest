namespace agentest.Models;

public sealed class AgentCatalog
{
    public List<string> Groups { get; set; } = ["DEFAULT"];
    public List<AgentProfile> Profiles { get; set; } = [];
}
