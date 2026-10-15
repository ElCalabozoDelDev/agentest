using agentest.Models;
using agentest.Services;
using agentest.Forms.Theming;
namespace agentest.Forms.Agents;
public sealed partial class ProfileEditorForm : Form
{
    private readonly AgentProfile original;
    public AgentProfile Profile { get; private set; }
    public string SessionToken => token.Text;
    public string HealthSessionToken => healthToken.Text;
    public ProfileEditorForm() : this(new AgentProfile(), "") { }
    public ProfileEditorForm(AgentProfile profile, string sessionToken, ThemePalette? palette = null, string healthSessionToken = "", IReadOnlyList<string>? groups = null)
    {
        original = profile;
        Profile = profile;
        InitializeComponent();
        name.Text = profile.Name;
        endpoint.Text = profile.Endpoint;
        method.SelectedItem = profile.Method;
        header.Text = profile.AuthenticationHeader;
        prefix.Text = profile.AuthenticationPrefix;
        token.Text = sessionToken;
        timeout.Value = Math.Clamp(profile.TimeoutSeconds, 1, 3600);
        maximum.Value = Math.Clamp(profile.MaxAttachmentMegabytes, 1, 100);
        attachments.Checked = profile.AcceptsAttachments;
        extensions.Text = profile.AllowedExtensions;
        responsePath.Text = profile.ResponseTextPath;
        conversationPath.Text = profile.ResponseConversationIdPath;
        backendCreatesConversation.Checked = profile.BackendCreatesConversation;
        template.Text = profile.RequestTemplate.ReplaceLineEndings(Environment.NewLine);
        agentSystem.Items.AddRange((groups ?? new[] { "DEFAULT", profile.AgentSystem }).Distinct(StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray());
        agentSystem.SelectedItem = agentSystem.Items.Cast<string>().FirstOrDefault(g => string.Equals(g, profile.AgentSystem, StringComparison.OrdinalIgnoreCase)) ?? "DEFAULT";
        var health = profile.Health;
        healthEndpoint.Text = health.Endpoint;
        healthMethod.SelectedItem = health.Method;
        healthInterval.Value = Math.Clamp(health.IntervalSeconds, 1, 86400);
        healthTimeout.Value = Math.Clamp(health.TimeoutSeconds, 1, 300);
        healthStatus.Value = Math.Clamp(health.ExpectedStatusCode, 100, 599);
        healthPath.Text = health.ResponseValuePath;
        healthValue.Text = health.ExpectedValue;
        healthHeader.Text = health.AuthenticationHeader;
        healthPrefix.Text = health.AuthenticationPrefix;
        healthToken.Text = healthSessionToken;
        save.Click += (_, _) => Save();
        ThemeAppearance.Apply(this, palette ?? ThemePalette.Light);
    }

    private void Save()
    {
        var candidate = new AgentProfile
        {
            Id = original.Id, Name = name.Text.Trim(), Endpoint = endpoint.Text.Trim(),
            Method = method.SelectedItem?.ToString() ?? "", AuthenticationHeader = header.Text.Trim(),
            AuthenticationPrefix = prefix.Text.Trim(), TimeoutSeconds = (int)timeout.Value,
            AcceptsAttachments = attachments.Checked, AllowedExtensions = extensions.Text.Trim(),
            MaxAttachmentMegabytes = (int)maximum.Value, ResponseTextPath = responsePath.Text.Trim(),
            ResponseConversationIdPath = conversationPath.Text.Trim(),
            BackendCreatesConversation = backendCreatesConversation.Checked,
            RequestTemplate = template.Text,
            AgentSystem = agentSystem.SelectedItem?.ToString() ?? "DEFAULT",
            Health = new HealthSettings
            {
                Endpoint = healthEndpoint.Text.Trim(), Method = healthMethod.SelectedItem?.ToString() ?? "",
                IntervalSeconds = (int)healthInterval.Value, TimeoutSeconds = (int)healthTimeout.Value,
                ExpectedStatusCode = (int)healthStatus.Value, ResponseValuePath = healthPath.Text.Trim(),
                ExpectedValue = healthValue.Text, AuthenticationHeader = healthHeader.Text.Trim(),
                AuthenticationPrefix = healthPrefix.Text.Trim()
            }
        };
        try
        {
            OrchestratorClient.ValidateProfile(candidate);
            HealthClient.Validate(candidate.Health);
            Profile = candidate;
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex) when (ex is ArgumentException or System.Text.Json.JsonException or FormatException or InvalidOperationException)
        {
            MessageBox.Show(this, ex.Message, "Revisa el perfil", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
