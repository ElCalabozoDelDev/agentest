using agentest.Models;
using agentest.Forms.Theming;
namespace agentest.Forms.Agents;
public sealed partial class ProfileEditorForm
{
    private TextBox name = null!, endpoint = null!, header = null!, prefix = null!, token = null!, extensions = null!, responsePath = null!, conversationPath = null!;
    private ComboBox method = null!;
    private NumericUpDown timeout = null!;
    private NumericUpDown maximum = null!;
    private CheckBox attachments = null!;
    private CheckBox backendCreatesConversation = null!;
    private TextBox template = null!;
    private ComboBox agentSystem = null!;
    private TextBox healthEndpoint = null!, healthPath = null!, healthValue = null!, healthHeader = null!, healthPrefix = null!, healthToken = null!;
    private ComboBox healthMethod = null!;
    private NumericUpDown healthInterval = null!, healthTimeout = null!, healthStatus = null!;
    private Button save = null!;
    private void InitializeComponent()
    {
        name = new();
        endpoint = new();
        header = new();
        prefix = new();
        token = new();
        extensions = new();
        responsePath = new();
        conversationPath = new();
        method = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        timeout = new() { Minimum = 1, Maximum = 3600 };
        maximum = new() { Minimum = 1, Maximum = 100 };
        attachments = new() { Text = "Permitir adjuntos", AutoSize = true };
        backendCreatesConversation = new() { Text = "El backend crea el ID (enviar null al inicio)", AutoSize = true };
        template = new() { Multiline = true, AcceptsReturn = true, AcceptsTab = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 10) };
        agentSystem = new() { DropDownStyle = ComboBoxStyle.DropDownList, Name = "AgentGroup" };
        healthEndpoint = new() { Name = "HealthEndpoint" };
        healthPath = new() { Name = "HealthPath" };
        healthValue = new() { Name = "HealthValue" };
        healthHeader = new();
        healthPrefix = new();
        healthToken = new();
        healthMethod = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        healthInterval = new() { Minimum = 1, Maximum = 86400 };
        healthTimeout = new() { Minimum = 1, Maximum = 300 };
        healthStatus = new() { Minimum = 100, Maximum = 599 };
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
        save = new ThemeButton { Text = "Guardar", AutoSize = true };
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
    }
    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(control, 1, row);
    }

}
