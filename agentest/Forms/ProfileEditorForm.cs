using agentest.Models;
using agentest.Services;
using agentest.Forms.Theming;

namespace agentest.Forms;

public sealed class ProfileEditorForm : Form
{
    private readonly AgentProfile original;
    private readonly TextBox name = new(), endpoint = new(), header = new(), prefix = new(), token = new(), extensions = new(), responsePath = new(), conversationPath = new();
    private readonly ComboBox method = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown timeout = new() { Minimum = 1, Maximum = 3600 };
    private readonly NumericUpDown maximum = new() { Minimum = 1, Maximum = 100 };
    private readonly CheckBox attachments = new() { Text = "Permitir adjuntos", AutoSize = true };
    private readonly CheckBox backendCreatesConversation = new() { Text = "El backend crea el ID (enviar null al inicio)", AutoSize = true };
    private readonly TextBox template = new() { Multiline = true, AcceptsReturn = true, AcceptsTab = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 10) };
    public AgentProfile Profile { get; private set; }
    public string SessionToken => token.Text;
    private readonly ComboBox agentSystem = new() { DropDownStyle = ComboBoxStyle.DropDownList, Name = "AgentGroup" };
    private readonly TextBox healthEndpoint = new() { Name = "HealthEndpoint" }, healthPath = new() { Name = "HealthPath" }, healthValue = new() { Name = "HealthValue" }, healthHeader = new(), healthPrefix = new(), healthToken = new();
    private readonly ComboBox healthMethod = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown healthInterval = new() { Minimum = 1, Maximum = 86400 }, healthTimeout = new() { Minimum = 1, Maximum = 300 }, healthStatus = new() { Minimum = 100, Maximum = 599 };
    public string HealthSessionToken => healthToken.Text;

    public ProfileEditorForm(AgentProfile profile, string sessionToken, ThemePalette? palette = null, string healthSessionToken = "", IReadOnlyList<string>? groups = null)
    {
        original = profile;
        Profile = profile;
        Text = "Perfil de agente";
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(850, 790);
        MinimumSize = new Size(700, 680);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 14 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 12; row++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
        AddRow(layout, 0, "Nombre", name);
        AddRow(layout, 1, "Endpoint", endpoint);
        method.Items.AddRange(["POST", "PUT", "PATCH"]);
        AddRow(layout, 2, "Método HTTP", method);
        AddRow(layout, 3, "Encabezado de acceso", header);
        AddRow(layout, 4, "Prefijo (p. ej. Bearer)", prefix);
        token.UseSystemPasswordChar = true;
        AddRow(layout, 5, "Credencial (solo sesión)", token);
        AddRow(layout, 6, "Espera máxima (seg.)", timeout);
        var attachmentRow = new FlowLayoutPanel { Dock = DockStyle.Fill };
        attachmentRow.Controls.Add(attachments);
        attachmentRow.Controls.Add(new Label { Text = "MB por archivo:", AutoSize = true, Margin = new Padding(15, 4, 0, 0) });
        maximum.Width = 65;
        attachmentRow.Controls.Add(maximum);
        AddRow(layout, 7, "Capacidades", attachmentRow);
        AddRow(layout, 8, "Extensiones (coma o *)", extensions);
        AddRow(layout, 9, "Ruta del texto de respuesta", responsePath);
        AddRow(layout, 10, "Ruta de ID de conversación", conversationPath);
        AddRow(layout, 11, "Inicio de conversación", backendCreatesConversation);
        var help = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Request: usa valores completos \"{{message}}\", \"{{history}}\", \"{{conversationId}}\", \"{{agentId}}\" y \"{{attachments}}\". Puedes agregar parámetros JSON fijos.\nRespuesta: ruta como data.answer o results.0.text. El ID del backend se reutiliza en los siguientes mensajes; deja su ruta vacía si no existe. Ambas rutas vacías muestran el cuerpo completo.\nAdjuntos: objetos JSON con name, content_type, size y content_base64."
        };
        layout.Controls.Add(help, 0, 12);
        layout.SetColumnSpan(help, 2);
        AddRow(layout, 13, "Plantilla JSON del request", template);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var save = new ThemeButton { Text = "Guardar", AutoSize = true };
        var cancel = new ThemeButton { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        var content = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var sections = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 3 };
        sections.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Dock = DockStyle.Top;
        layout.AutoSize = true;
        var healthLayout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(16), ColumnCount = 2, RowCount = 12 };
        healthLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        healthLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 11; row++) healthLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        healthLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        var identity = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(16, 16, 16, 0), ColumnCount = 2, RowCount = 2 };
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        identity.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        identity.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        AddRow(identity, 0, "Grupo / sistema agéntico", agentSystem);
        var chatTitle = new Label { Dock = DockStyle.Fill, Text = "CHAT · Conexión y contrato", TextAlign = ContentAlignment.MiddleLeft };
        identity.Controls.Add(chatTitle, 0, 1);
        identity.SetColumnSpan(chatTitle, 2);
        var healthTitle = new Label { Dock = DockStyle.Fill, Text = "HEALTH · Monitoreo de disponibilidad", TextAlign = ContentAlignment.MiddleLeft };
        healthLayout.Controls.Add(healthTitle, 0, 0);
        healthLayout.SetColumnSpan(healthTitle, 2);
        AddRow(healthLayout, 1, "Endpoint de health", healthEndpoint);
        healthMethod.Items.AddRange(["GET", "HEAD", "POST"]);
        AddRow(healthLayout, 2, "Método (sin cuerpo)", healthMethod);
        AddRow(healthLayout, 3, "Intervalo (seg.)", healthInterval);
        AddRow(healthLayout, 4, "Timeout (seg.)", healthTimeout);
        AddRow(healthLayout, 5, "Código HTTP esperado", healthStatus);
        AddRow(healthLayout, 6, "Ruta JSON (opcional)", healthPath);
        AddRow(healthLayout, 7, "Valor esperado", healthValue);
        AddRow(healthLayout, 8, "Encabezado de acceso", healthHeader);
        AddRow(healthLayout, 9, "Prefijo de acceso", healthPrefix);
        healthToken.UseSystemPasswordChar = true;
        AddRow(healthLayout, 10, "Credencial (solo sesión)", healthToken);
        var healthHelp = new Label { Dock = DockStyle.Fill, Text = "Sin sistema se usa DEFAULT. Health es obligatorio al guardar el perfil.\nLa ruta JSON admite puntos e índices (p. ej. checks.0.status); el valor se compara exactamente, distinguiendo mayúsculas. Sin ruta se valida solo HTTP.\nLa autenticación de health es independiente del chat. No ingreses secretos en la URL. El monitoreo continúa desde ambas pestañas." };
        healthLayout.Controls.Add(healthHelp, 0, 11);
        healthLayout.SetColumnSpan(healthHelp, 2);
        sections.Controls.Add(identity, 0, 0);
        sections.Controls.Add(layout, 0, 1);
        sections.Controls.Add(healthLayout, 0, 2);
        content.Controls.Add(sections);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.Controls.Add(content, 0, 0);
        root.Controls.Add(buttons, 0, 1);
        Controls.Add(root);
        CancelButton = cancel;
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

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(control, 1, row);
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
