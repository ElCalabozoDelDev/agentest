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

    public ProfileEditorForm(AgentProfile profile, string sessionToken, ThemePalette? palette = null)
    {
        original = profile;
        Profile = profile;
        Text = "Perfil de agente";
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(850, 870);
        MinimumSize = new Size(700, 680);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 15 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 12; row++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
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
        layout.Controls.Add(buttons, 0, 14);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
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
            RequestTemplate = template.Text
        };
        try
        {
            OrchestratorClient.ValidateProfile(candidate);
            Profile = candidate;
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex) when (ex is ArgumentException or System.Text.Json.JsonException or FormatException or InvalidOperationException)
        {
            MessageBox.Show(this, ex.Message, "Revisa el perfil", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
