param([string]$ScreenshotDirectory)
$ErrorActionPreference = 'Stop'
$projectPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../agentest/agentest.csproj'))
$temporaryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../agentest/bin')) + [IO.Path]::DirectorySeparatorChar
$verificationDirectory = Join-Path $temporaryRoot ('agentest-verify-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $verificationDirectory | Out-Null
try {
    $escapedProjectPath = [System.Security.SecurityElement]::Escape($projectPath)
    $project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup><ProjectReference Include="$escapedProjectPath" /></ItemGroup>
</Project>
"@
    Set-Content -LiteralPath (Join-Path $verificationDirectory 'Verify.csproj') -Value $project -Encoding UTF8
    $source = @'
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using agentest.Models;
using agentest.Persistence;
using agentest.Services;
using agentest.Forms;
using agentest.Forms.Theming;

internal static class Verification
{
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        assertions++;
    }
    private static void Reject(Action action, string message)
    {
        try { action(); } catch { assertions++; return; }
        throw new Exception(message);
    }
    [STAThread]
    private static void Main(string[] args)
    {
        var data = Path.Combine(AppContext.BaseDirectory, "data");
        RunAsync(data).GetAwaiter().GetResult();
        ApplicationConfiguration.Initialize();
        var screenshotDirectory = args.FirstOrDefault();
        using (var form = new ChatForm(new LocalStore(data)))
        {
            form.Show();
            for (int i = 0; i < 150; i++) { Application.DoEvents(); Thread.Sleep(10); }
            Check(form.Visible && form.Controls.Count > 0, "Chat form failed to initialize.");
            var send = Descendants(form).OfType<Button>().Single(b => b.Text == "Enviar (Ctrl+Enter)");
            var input = Descendants(form).OfType<TextBox>().Single(t => t.Multiline);
            Check(!send.Enabled, "Empty composer must not send.");
            input.Text = "UI check";
            Application.DoEvents();
            var tabs = Descendants(form).OfType<TabControl>().Single();
            Check(tabs.TabPages.Count == 2 && tabs.TabPages[0].Text == "Chat" && tabs.TabPages[1].Text == "Agentes", "Main tabs are missing.");
            tabs.SelectedIndex = 1;
            Application.DoEvents();
            Check(input.Text == "UI check", "Changing tabs lost the draft.");
            tabs.SelectedIndex = 0;
            Check(send.Enabled, "Non-empty composer must allow send with a profile.");
            var themeButton = Descendants(form).OfType<Button>().Single(b => b.Text.StartsWith("Tema:"));
            var menu = themeButton.ContextMenuStrip!;
            menu.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == "Interfaz oscura").PerformClick();
            WaitForUi(() => themeButton.Enabled && input.Enabled && send.Enabled && themeButton.Text.StartsWith("Tema: Oscuro"));
            Check(form.BackColor == ThemePalette.Dark.Window && input.BackColor == ThemePalette.Dark.Surface,
                "Dark theme did not cover the window and composer.");
            Check(input.Text == "UI check", "Theme change lost the draft.");
            Check(input.Enabled && send.Enabled, "Theme change left the composer disabled.");
            Check(new LocalStore(data).LoadSettingsAsync().GetAwaiter().GetResult().Theme == ThemeMode.Dark,
                "Dark theme selection was not saved.");
            if (!string.IsNullOrEmpty(screenshotDirectory))
            {
                Directory.CreateDirectory(screenshotDirectory);
                Capture(form, Path.Combine(screenshotDirectory, "chat-dark.png"));
                CaptureOverview(form, tabs, screenshotDirectory, "agents-dark.png");
            }
            menu.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == "Interfaz clara").PerformClick();
            WaitForUi(() => themeButton.Enabled && themeButton.Text.StartsWith("Tema: Claro"));
            Check(input.BackColor == ThemePalette.Light.Surface, "Switching back to light failed.");
            if (!string.IsNullOrEmpty(screenshotDirectory)) CaptureOverview(form, tabs, screenshotDirectory, "agents-light.png");
            menu.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == "Usar tema del sistema").PerformClick();
            WaitForUi(() => themeButton.Enabled && themeButton.Text.StartsWith("Tema: Sistema"));
            Check(new LocalStore(data).LoadSettingsAsync().GetAwaiter().GetResult().Theme == ThemeMode.System,
                "System theme selection was not saved.");
            input.Clear();
            if (!string.IsNullOrEmpty(screenshotDirectory))
            {
                Directory.CreateDirectory(screenshotDirectory);
                Capture(form, Path.Combine(screenshotDirectory, "chat.png"));
            }
            form.Close();
        }
        new LocalStore(data).SaveSettingsAsync(new AppSettings { Theme = ThemeMode.Dark }).GetAwaiter().GetResult();
        using (var restoredForm = new ChatForm(new LocalStore(data)))
        {
            restoredForm.Show();
            WaitForUi(() => Descendants(restoredForm).OfType<Button>().Any(b => b.Enabled && b.Text.StartsWith("Tema: Oscuro")));
            Check(restoredForm.BackColor == ThemePalette.Dark.Window, "Saved dark theme was not restored on startup.");
            restoredForm.Close();
        }
        VerifyMonitorUi(data);
        VerifyGroupManagerIntegration(data);
        VerifyChatSelectors(data, screenshotDirectory);
        VerifyConversationHistory(data, screenshotDirectory);
        Application.SetColorMode(SystemColorMode.Classic);
        using (var editor = new ProfileEditorForm(new AgentProfile(), "session-secret"))
        {
            editor.Show();
            Application.DoEvents();
            Check(editor.SessionToken == "session-secret", "Profile token was lost.");
            if (!string.IsNullOrEmpty(screenshotDirectory)) Capture(editor, Path.Combine(screenshotDirectory, "profile.png"));
            Check(!Descendants(editor).OfType<TabControl>().Any(), "Profile editor should keep chat and health together.");
            ShowHealth(editor);
            Application.DoEvents();
            if (!string.IsNullOrEmpty(screenshotDirectory)) Capture(editor, Path.Combine(screenshotDirectory, "profile-health.png"));
            if (!string.IsNullOrEmpty(screenshotDirectory))
            {
                editor.Size = editor.MinimumSize;
                Application.DoEvents();
                Capture(editor, Path.Combine(screenshotDirectory, "profile-health-minimum.png"));
                Descendants(editor).OfType<Panel>().Single(p => p.AutoScroll).AutoScrollPosition = System.Drawing.Point.Empty;
                Application.DoEvents();
                Capture(editor, Path.Combine(screenshotDirectory, "profile-chat-minimum.png"));
            }
            editor.Close();
        }
        Application.SetColorMode(SystemColorMode.Dark);
        using (var editor = new ProfileEditorForm(new AgentProfile(), "", ThemePalette.Dark))
        {
            editor.Show();
            Application.DoEvents();
            Check(editor.BackColor == ThemePalette.Dark.Window &&
                Descendants(editor).OfType<TextBox>().All(t => t.BackColor == ThemePalette.Dark.Surface),
                "Dark theme did not cover profile fields.");
            if (!string.IsNullOrEmpty(screenshotDirectory)) Capture(editor, Path.Combine(screenshotDirectory, "profile-dark.png"));
            ShowHealth(editor);
            Application.DoEvents();
            if (!string.IsNullOrEmpty(screenshotDirectory)) Capture(editor, Path.Combine(screenshotDirectory, "profile-health-dark.png"));
            editor.Close();
        }
        using (var editor = new ProfileEditorForm(new AgentProfile(), "chat-secret", ThemePalette.Light, "health-secret", ["DEFAULT", "Sistema guardado"]))
        {
            editor.Show();
            Application.DoEvents();
            Descendants(editor).OfType<ComboBox>().Single(c => c.Name == "AgentGroup").SelectedItem = "Sistema guardado";
            Descendants(editor).OfType<TextBox>().Single(c => c.Name == "HealthEndpoint").Text = "https://example.invalid/health";
            Descendants(editor).OfType<TextBox>().Single(c => c.Name == "HealthPath").Text = "status";
            Descendants(editor).OfType<TextBox>().Single(c => c.Name == "HealthValue").Text = "ready";
            Descendants(editor).OfType<Button>().Single(b => b.Text == "Guardar").PerformClick();
            Check(editor.DialogResult == DialogResult.OK && editor.Profile.AgentSystem == "Sistema guardado" &&
                editor.Profile.Health.ResponseValuePath == "status" && editor.Profile.Health.ExpectedValue == "ready", "Editor lost health or system settings.");
            Check(editor.SessionToken == "chat-secret" && editor.HealthSessionToken == "health-secret", "Health credentials were mixed with chat credentials.");
            var serialized = System.Text.Json.JsonSerializer.Serialize(editor.Profile);
            Check(!serialized.Contains("chat-secret") && !serialized.Contains("health-secret"), "Editor credentials entered the profile model.");
        }
        foreach (var palette in new[] { ThemePalette.Light, ThemePalette.Dark })
        {
            Application.SetColorMode(palette.IsDark ? SystemColorMode.Dark : SystemColorMode.Classic);
            var original = new AgentCatalog { Groups = ["DEFAULT", "Sistema de pruebas"], Profiles = [new AgentProfile { AgentSystem = "Sistema de pruebas" }] };
            using var manager = new GroupManagerForm(original, palette);
            manager.Show();
            Application.DoEvents();
            var name = Descendants(manager).OfType<TextBox>().Single();
            name.Text = "Grupo vacío";
            Descendants(manager).OfType<Button>().Single(b => b.Text == "Crear grupo").PerformClick();
            Check(manager.Catalog.Groups.Contains("Grupo vacío") && !original.Groups.Contains("Grupo vacío"), "Group editor must stage edits without modifying live groups.");
            name.Text = "Grupo renombrado";
            Descendants(manager).OfType<Button>().Single(b => b.Text == "Renombrar").PerformClick();
            Check(manager.Catalog.Groups.Contains("Grupo renombrado"), "Group editor rename failed.");
            Descendants(manager).OfType<ListBox>().Single().SelectedItem = "DEFAULT";
            Check(!Descendants(manager).OfType<Button>().Single(b => b.Text == "Eliminar").Enabled && !Descendants(manager).OfType<Button>().Single(b => b.Text == "Renombrar").Enabled, "DEFAULT must be protected in the UI.");
            if (!string.IsNullOrEmpty(screenshotDirectory))
            {
                Capture(manager, Path.Combine(screenshotDirectory, palette.IsDark ? "groups-dark.png" : "groups-light.png"));
                manager.Size = manager.MinimumSize;
                Application.DoEvents();
                Capture(manager, Path.Combine(screenshotDirectory, palette.IsDark ? "groups-dark-minimum.png" : "groups-light-minimum.png"));
                manager.Scale(new System.Drawing.SizeF(1.25f, 1.25f));
                Application.DoEvents();
                Capture(manager, Path.Combine(screenshotDirectory, palette.IsDark ? "groups-dark-scaled.png" : "groups-light-scaled.png"));
            }
            manager.Close();
            Check(original.Groups.Count == 2, "Cancelling groups changed the live catalog.");
        }
        Console.WriteLine($"PASS: {assertions} checks (JSON contracts, HTTP, cancellation, timeout, persistence, attachments, WinForms, themes).");
    }

    private static void WaitForUi(Func<bool> completed)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!completed())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("UI operation did not finish.");
            Application.DoEvents();
            Thread.Sleep(10);
        }
        Application.DoEvents();
    }

    private static void ShowHealth(Form editor)
    {
        var scroller = Descendants(editor).OfType<Panel>().Single(p => p.AutoScroll);
        scroller.AutoScrollPosition = new System.Drawing.Point(0, int.MaxValue);
        Application.DoEvents();
    }

    private static void CaptureOverview(Form form, TabControl tabs, string directory, string file)
    {
        var overview = Descendants(form).OfType<AgentsOverview>().Single();
        var demo = new List<AgentProfile>
        {
            new() { Id = "demo1", Name = "Coordinador", AgentSystem = "Sistema de pruebas" },
            new() { Id = "demo2", Name = "Investigador", AgentSystem = "Sistema de pruebas" },
            new() { Id = "demo3", Name = "Evaluador", AgentSystem = "Sistema de pruebas" },
            new() { Id = "demo4", Name = "Agente existente", AgentSystem = "DEFAULT" }
        };
        overview.Render(demo, new Dictionary<string, AgentHealth>
        {
            ["demo1"] = new("Saludable", "HTTP 200", DateTimeOffset.Now, 24),
            ["demo2"] = new("No saludable", "El valor JSON no coincide con el esperado.", DateTimeOffset.Now, 80),
            ["demo3"] = new("Tiempo agotado", "Se superó el timeout de health.", DateTimeOffset.Now, 5000)
        });
        tabs.SelectedIndex = 1;
        Application.DoEvents();
        Capture(form, Path.Combine(directory, file));
        if (file == "agents-light.png")
        {
            var size = form.Size;
            form.Size = form.MinimumSize;
            Application.DoEvents();
            var canvas = Descendants(overview).OfType<FlowLayoutPanel>().Single();
            Check(!canvas.HorizontalScroll.Visible, $"Overview should fit horizontally: client={canvas.ClientSize}, display={canvas.DisplayRectangle}, children={string.Join(";", canvas.Controls.Cast<Control>().Select(c => c.Bounds))}.");
            Capture(form, Path.Combine(directory, "agents-minimum.png"));
            form.Size = size;
            form.Scale(new System.Drawing.SizeF(1.25f, 1.25f));
            Application.DoEvents();
            Capture(form, Path.Combine(directory, "agents-scaled.png"));
            form.Scale(new System.Drawing.SizeF(0.8f, 0.8f));
            form.Size = size;
        }
        tabs.SelectedIndex = 0;
    }

    private static void VerifyMonitorUi(string data)
    {
        var root = Path.Combine(data, "monitor-ui");
        var store = new LocalStore(root);
        store.SaveProfilesAsync([new AgentProfile { Name = "Agente simulado", Health = new HealthSettings { Endpoint = "https://example.invalid/health", IntervalSeconds = 1 } }]).GetAwaiter().GetResult();
        var calls = 0;
        var block = false;
        var cancelled = false;
        using var client = new HealthClient(new FakeBackend(async (_, cancellation) =>
        {
            Interlocked.Increment(ref calls);
            if (block)
            {
                using var registration = cancellation.Register(() => cancelled = true);
                await Task.Delay(Timeout.Infinite, cancellation);
            }
            await Task.Delay(100, cancellation);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        using var form = new ChatForm(store, client);
        form.Show();
        WaitForUi(() => calls > 0);
        Check(Descendants(form).OfType<TextBox>().Single(t => t.Multiline).Enabled, "Health polling blocked the composer.");
        var refresh = Descendants(form).OfType<Button>().Single(b => b.Text == "Actualizar ahora");
        WaitForUi(() => refresh.Enabled && Descendants(form).OfType<Label>().Any(l => l.Text == "Saludable"));
        var tabs = Descendants(form).OfType<TabControl>().Single();
        Check(tabs.SelectedIndex == 0, "Polling should work in the Chat tab.");
        WaitForUi(() => calls >= 2 && refresh.Enabled);
        Check(calls >= 2, "Periodic health polling did not repeat.");
        tabs.SelectedIndex = 1;
        block = true;
        var previous = calls;
        refresh.PerformClick();
        WaitForUi(() => calls > previous);
        Check(!refresh.Enabled, "Manual health rounds must not overlap.");
        form.Close();
        WaitForUi(() => cancelled);
        Check(cancelled && form.IsDisposed, "Closing did not cancel an active health request.");
    }

    private static void VerifyGroupManagerIntegration(string data)
    {
        var store = new LocalStore(Path.Combine(data, "group-ui"));
        Task.Run(() => store.SaveCatalogAsync(new AgentCatalog { Groups = ["DEFAULT", "Original"], Profiles = [new AgentProfile { Id = "kept", AgentSystem = "Original" }] })).GetAwaiter().GetResult();
        using var form = new ChatForm(store);
        form.Show();
        var manage = Descendants(form).OfType<Button>().First(b => b.Text == "Administrar grupos");
        WaitForUi(() => manage.Enabled);
        var input = Descendants(form).OfType<TextBox>().Single(t => t.Multiline);
        input.Text = "Borrador conservado";
        form.BeginInvoke((Action)(() =>
        {
            var manager = Application.OpenForms.OfType<GroupManagerForm>().Single();
            Descendants(manager).OfType<ListBox>().Single().SelectedItem = "Original";
            Descendants(manager).OfType<TextBox>().Single().Text = "Renombrado";
            Descendants(manager).OfType<Button>().Single(b => b.Text == "Renombrar").PerformClick();
            Descendants(manager).OfType<TextBox>().Single().Text = "Vacío persistente";
            Descendants(manager).OfType<Button>().Single(b => b.Text == "Crear grupo").PerformClick();
            Descendants(manager).OfType<Button>().Single(b => b.Text == "Guardar cambios").PerformClick();
        }));
        manage.PerformClick();
        WaitForUi(() => manage.Enabled && input.Enabled && Descendants(form).OfType<Label>().Any(l => l.Text.StartsWith("Grupos guardados.")));
        var saved = Task.Run(() => store.LoadCatalogAsync()).GetAwaiter().GetResult();
        Check(saved.Profiles.Single().Id == "kept" && saved.Profiles.Single().AgentSystem == "Renombrado", "Group manager did not persist the profile rename.");
        Check(saved.Groups.Contains("Vacío persistente"), "Group manager did not persist an empty group.");
        Check(input.Text == "Borrador conservado" && input.Enabled, $"Group management draft: text={input.Text}, enabled={input.Enabled}, manage={manage.Enabled}.");
        Check(Descendants(form).OfType<Label>().Any(l => l.Text.StartsWith("Sin agentes.")), "Overview did not display the empty group.");
        input.Clear();
        form.Close();
    }

    private static void VerifyConversationHistory(string data, string? screenshots)
    {
        var store = new LocalStore(Path.Combine(data, "history-ui"));
        var a = new AgentProfile { Name = "Agente de pruebas", AgentSystem = "Sistema A" };
        var sibling = new AgentProfile { Name = a.Name, AgentSystem = "Sistema A" };
        var b = new AgentProfile { Name = "Evaluador", AgentSystem = "Sistema B" };
        Conversation Make(AgentProfile profile, string title, int minutes) => new()
        {
            AgentProfileId = profile.Id, Title = title, UpdatedAt = DateTimeOffset.Now.AddMinutes(minutes),
            RemoteConversationId = "remote-" + profile.Id, Messages = [new ChatMessage { Text = title }]
        };
        var newest = Make(a, "Revisión de consultas", 0);
        var older = Make(a, "Prueba anterior", -10);
        var other = Make(sibling, "Prueba independiente", -5);
        var foreign = Make(b, "Evaluación del sistema B", -3);
        Task.Run(async () =>
        {
            await store.SaveCatalogAsync(new AgentCatalog { Groups = ["DEFAULT", "Sistema A", "Sistema B"], Profiles = [a, sibling, b] });
            foreach (var conversation in new[] { newest, older, other, foreign }) await store.SaveConversationAsync(conversation);
        }).GetAwaiter().GetResult();
        using var form = new ChatForm(store);
        form.Show();
        var input = Descendants(form).OfType<TextBox>().Single(t => t.Multiline);
        WaitForUi(() => input.Enabled);
        var systems = Descendants(form).OfType<ComboBox>().Single(c => c.Name == "ChatSystem");
        var agents = Descendants(form).OfType<ComboBox>().Single(c => c.Name == "ChatAgent");
        var list = Descendants(form).OfType<ListBox>().Single(l => l.Name == "AgentConversations");
        var delete = Descendants(form).OfType<Button>().Single(button => button.Text == "Eliminar conversación");
        Check(list.Items.Count == 2 && list.Items.Cast<Conversation>().All(c => c.AgentProfileId == a.Id), "History is not isolated by selected profile ID.");
        Check(((Conversation)list.Items[0]).Id == newest.Id && !delete.Enabled, "History ordering or initial delete state is incorrect.");
        input.Text = "Borrador conservado";
        agents.SelectedItem = agents.Items.Cast<AgentProfile>().Single(p => p.Id == sibling.Id);
        Check(list.Items.Count == 1 && ((Conversation)list.Items[0]).Id == other.Id && input.Text == "Borrador conservado", "Agents with the same name shared history or lost the draft.");
        input.Clear();
        list.SelectedIndex = 0;
        agents.SelectedItem = agents.Items.Cast<AgentProfile>().Single(p => p.Id == a.Id);
        Check(list.Items.Count == 2 && list.SelectedIndex == -1 && Descendants(form).OfType<RichTextBox>().Single().Text.StartsWith("Inicia una conversación"), "Switching from an existing conversation exposed the previous agent's transcript.");
        list.SelectedIndex = 0;
        input.Focus();
        Application.DoEvents();
        Check(list.DrawMode == DrawMode.OwnerDrawFixed && list.SelectedItem is Conversation selected && selected.Id == newest.Id && delete.Enabled, "Active conversation selection was lost on focus change.");
        input.Text = "Borrador que requiere confirmación";
        form.BeginInvoke((Action)(() =>
        {
            var confirmation = Application.OpenForms.OfType<DeleteConversationForm>().Single();
            Check(Descendants(confirmation).OfType<Label>().Any(l => l.Text.Contains("borrador")), "Delete confirmation must disclose draft loss.");
            Descendants(confirmation).OfType<Button>().Single(button => button.Text == "Cancelar").PerformClick();
        }));
        delete.PerformClick();
        Check(list.Items.Count == 2 && input.Text == "Borrador que requiere confirmación" && File.Exists(Path.Combine(store.RootPath, "conversations", newest.Id + ".json")), "Cancelling deletion lost history or draft.");
        ConfirmDelete();
        WaitForUi(() => input.Enabled && list.Items.Count == 1);
        Check(input.Text == "" && ((Conversation)list.SelectedItem!).Id == older.Id && !File.Exists(Path.Combine(store.RootPath, "conversations", newest.Id + ".json")), "Confirmed deletion did not remove the local file and select the next history.");
        ConfirmDelete();
        WaitForUi(() => input.Enabled && list.Items.Count == 0);
        Check(!delete.Enabled && agents.SelectedItem is AgentProfile selectedAgent && selectedAgent.Id == a.Id, "Deleting the last history did not leave an empty chat for the same agent.");
        var persisted = Task.Run(() => store.LoadConversationsAsync()).GetAwaiter().GetResult();
        Check(persisted.Items.Count == 2 && persisted.Items.Any(c => c.Id == other.Id && c.AgentProfileId == sibling.Id) && persisted.Items.Any(c => c.Id == foreign.Id && c.AgentProfileId == b.Id), "Deletion changed another agent's history.");
        agents.SelectedItem = agents.Items.Cast<AgentProfile>().Single(p => p.Id == sibling.Id);
        Check(list.Items.Count == 1 && ((Conversation)list.Items[0]).Id == other.Id, "Remaining history was not restored after agent selection.");
        list.SelectedIndex = 0;
        systems.SelectedItem = "Sistema B";
        Check(list.Items.Count == 1 && ((Conversation)list.Items[0]).Id == foreign.Id, "System navigation did not refresh the selected agent's histories.");
        list.SelectedIndex = 0;
        if (!string.IsNullOrEmpty(screenshots))
        {
            foreach (var mode in new[] { "clara", "oscura" })
            {
                var theme = Descendants(form).OfType<Button>().Single(button => button.Text.StartsWith("Tema:"));
                theme.ContextMenuStrip!.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "Interfaz " + mode).PerformClick();
                WaitForUi(() => theme.Enabled && input.Enabled);
                input.Focus();
                form.Size = form.MinimumSize;
                Application.DoEvents();
                Capture(form, Path.Combine(screenshots, "history-" + mode + "-minimum.png"));
                form.BeginInvoke((Action)(() =>
                {
                    var confirmation = Application.OpenForms.OfType<DeleteConversationForm>().Single();
                    Capture(confirmation, Path.Combine(screenshots, "delete-history-" + mode + ".png"));
                    Descendants(confirmation).OfType<Button>().Single(button => button.Text == "Cancelar").PerformClick();
                }));
                delete.PerformClick();
            }
            form.Scale(new System.Drawing.SizeF(1.25f, 1.25f));
            form.PerformLayout();
            foreach (var layout in Descendants(form).OfType<TableLayoutPanel>()) layout.PerformLayout();
            Application.DoEvents();
            Capture(form, Path.Combine(screenshots, "history-scaled.png"));
        }
        form.Close();
        using var reopened = new ChatForm(store);
        reopened.Show();
        WaitForUi(() => Descendants(reopened).OfType<TextBox>().Single(t => t.Multiline).Enabled);
        Check(Descendants(reopened).OfType<ListBox>().Single(l => l.Name == "AgentConversations").Items.Count == 0, "Deleted conversations reappeared after restart.");
        reopened.Close();

        void ConfirmDelete()
        {
            form.BeginInvoke((Action)(() =>
            {
                var confirmation = Application.OpenForms.OfType<DeleteConversationForm>().Single();
                Descendants(confirmation).OfType<Button>().Single(button => button.Text == "Eliminar").PerformClick();
            }));
            delete.PerformClick();
        }
    }

    private static void VerifyChatSelectors(string data, string? screenshots)
    {
        var store = new LocalStore(Path.Combine(data, "selectors-ui"));
        var a1 = new AgentProfile { Name = "Coordinador", AgentSystem = "Sistema A" };
        var a2 = new AgentProfile { Name = "Investigador", AgentSystem = "Sistema A" };
        var b = new AgentProfile { Name = "Evaluador", AgentSystem = "Sistema B" };
        var history = new Conversation { AgentProfileId = b.Id, Messages = [new ChatMessage { Text = "Mensaje previo" }] };
        Task.Run(async () =>
        {
            await store.SaveCatalogAsync(new AgentCatalog { Groups = ["DEFAULT", "Sistema A", "Sistema B", "Vacío"], Profiles = [a1, a2, b] });
            await store.SaveConversationAsync(history);
        }).GetAwaiter().GetResult();
        using var client = new HealthClient(new FakeBackend((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        using var form = new ChatForm(store, client);
        form.Show();
        var manage = Descendants(form).OfType<Button>().Single(b => b.Text == "Administrar agentes");
        var input = Descendants(form).OfType<TextBox>().Single(t => t.Multiline);
        WaitForUi(() => manage.Enabled && input.Enabled);
        var systems = Descendants(form).OfType<ComboBox>().Single(c => c.Name == "ChatSystem");
        var agents = Descendants(form).OfType<ComboBox>().Single(c => c.Name == "ChatAgent");
        var send = Descendants(form).OfType<Button>().Single(b => b.Text == "Enviar (Ctrl+Enter)");
        Check(systems.Items.Count == 4 && systems.Text == "Sistema A" && agents.Items.Count == 2, "Initial system filter is incorrect.");
        Check(!Descendants(form).OfType<Button>().Any(b => b.Text is "Nuevo perfil" or "Editar perfil" or "Eliminar perfil"), "Old agent management buttons remain in Chat.");
        var menu = manage.ContextMenuStrip!;
        Check(menu.Items.Count == 3 && menu.Items[0].Text == "Nuevo agente", "Agent management actions are missing.");
        manage.PerformClick();
        Check(menu.Visible, "Manage agents did not open its menu.");
        menu.Close();
        input.Text = "Borrador conservado";
        systems.SelectedItem = "Sistema B";
        Check(agents.Items.Count == 1 && ((AgentProfile)agents.SelectedItem!).Id == b.Id && input.Text == "Borrador conservado", "System filter lost the draft or selected a foreign agent.");
        systems.SelectedItem = "Vacío";
        Check(agents.Items.Count == 0 && agents.SelectedItem is null && !send.Enabled && systems.Enabled, "Empty systems must allow selection but prevent sending.");
        Check(menu.Items[0].Enabled && !menu.Items[1].Enabled && !menu.Items[2].Enabled, "Empty systems must allow creation only.");
        systems.SelectedItem = "Sistema A";
        agents.SelectedItem = agents.Items.Cast<AgentProfile>().Single(p => p.Id == a2.Id);
        Check(input.Text == "Borrador conservado" && send.Enabled, "Agent selection lost the text draft.");
        form.BeginInvoke((Action)(() =>
        {
            var editor = Application.OpenForms.OfType<ProfileEditorForm>().Single();
            Check(Descendants(editor).OfType<ComboBox>().Single(c => c.Name == "AgentGroup").Text == "Sistema A", "New agent did not inherit the selected system.");
            Descendants(editor).OfType<TextBox>().Single(c => c.Name == "HealthEndpoint").Text = "https://example.invalid/health";
            Descendants(editor).OfType<Button>().Single(b => b.Text == "Guardar").PerformClick();
        }));
        ((ToolStripMenuItem)menu.Items[0]).PerformClick();
        WaitForUi(() => input.Enabled && agents.Items.Count == 3);
        Check(systems.Text == "Sistema A" && agents.Text == "Nuevo agente" && input.Text == "Borrador conservado", "Creation did not update the filtered selectors or preserve the draft.");
        input.Clear();
        systems.SelectedItem = "Sistema B";
        var conversations = Descendants(form).OfType<ListBox>().Single(l => l.Name == "AgentConversations");
        conversations.SelectedItem = conversations.Items.Cast<Conversation>().Single(c => c.Id == history.Id);
        Check(systems.Text == "Sistema B" && agents.Text == "Evaluador" && systems.Enabled && agents.Enabled, "Loading history must restore selectors without blocking navigation to another agent.");
        Descendants(form).OfType<Button>().Single(b => b.Text == "Nueva conversación").PerformClick();
        Check(systems.Enabled && agents.Enabled && systems.Text == "Sistema B", "New chat did not unlock the selectors.");
        if (!string.IsNullOrEmpty(screenshots))
        {
            foreach (var mode in new[] { "clara", "oscura" })
            {
                var theme = Descendants(form).OfType<Button>().Single(b => b.Text.StartsWith("Tema:"));
                theme.ContextMenuStrip!.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == "Interfaz " + mode).PerformClick();
                WaitForUi(() => theme.Enabled && input.Enabled);
                form.Size = form.MinimumSize;
                Application.DoEvents();
                Check(agents.Right <= agents.Parent!.ClientSize.Width && systems.Width > 100 && agents.Width > 100, "Selectors overflow the minimum chat width.");
                Capture(form, Path.Combine(screenshots, "chat-selectors-" + mode + "-minimum.png"));
            }
            form.Scale(new System.Drawing.SizeF(1.25f, 1.25f));
            form.PerformLayout();
            foreach (var layout in Descendants(form).OfType<TableLayoutPanel>()) layout.PerformLayout();
            Application.DoEvents();
            Check(agents.Right <= agents.Parent!.ClientSize.Width, "Scaled selectors overflow their row.");
            Check(input.Right <= input.Parent!.ClientSize.Width, "Scaled input overflows the composer.");
            Check(form.RectangleToClient(send.RectangleToScreen(send.ClientRectangle)).Bottom <= form.ClientSize.Height, "Scaled composer actions extend below the window.");
            Capture(form, Path.Combine(screenshots, "chat-selectors-scaled.png"));
        }
        form.Close();
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static void Capture(Form form, string path)
    {
        using var bitmap = new System.Drawing.Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
        bitmap.Save(path);
    }

    private static async Task RunAsync(string data)
    {
        Directory.CreateDirectory(data);
        await VerifyHealthAsync(data);
        await VerifyGroupsAsync(data);
        var profile = new AgentProfile
        {
            AcceptsAttachments = true,
            RequestTemplate = """
                {"input":"{{message}}","session":"{{conversationId}}","agent":"{{agentId}}",
                 "context":"{{history}}","files":"{{attachments}}","fixed":{"temperature":0.2}}
                """
        };
        OrchestratorClient.ValidateProfile(profile);
        var conversation = new Conversation { AgentProfileId = profile.Id };
        conversation.Messages.Add(new ChatMessage { Text = "old" });
        conversation.Messages.Add(new ChatMessage { Role = "assistant", Text = "previous answer" });
        conversation.Messages.Add(new ChatMessage { Text = "failed", DeliveryStatus = "failed" });
        conversation.Messages.Add(new ChatMessage { Role = "system", Text = "error", DeliveryStatus = "failed" });
        var message = new ChatMessage
        {
            Text = "Quotes: \"hello\"\nUnicode: á漢字", DeliveryStatus = "pending",
            Attachments = [new ChatAttachment { Name = "test.txt", Size = 3, ContentType = "text/plain", Base64 = "YWJj" }]
        };
        conversation.Messages.Add(message);
        var body = JsonNode.Parse(OrchestratorClient.BuildRequest(profile, conversation, message))!;
        Check(body["input"]!.GetValue<string>() == message.Text, "Message escaping corrupted content.");
        Check(body["context"]!.AsArray().Count == 2, "History included pending/failed/current messages.");
        Check(body["files"]![0]!["content_base64"]!.GetValue<string>() == "YWJj", "Attachment mapping failed.");
        Check(body["session"]!.GetValue<string>() == conversation.Id, "Conversation id mapping failed.");
        Check(body["fixed"]!["temperature"]!.GetValue<double>() == 0.2, "Fixed parameters changed.");
        Check(OrchestratorClient.ExtractResponseText("{\"data\":{\"results\":[{\"text\":\"ok\"}]}}", "data.results.0.text") == "ok", "Nested response failed.");
        Check(OrchestratorClient.ExtractResponseText("plain", "") == "plain", "Raw response failed.");
        Reject(() => OrchestratorClient.ExtractResponseText("{}", "missing"), "Missing path must fail.");
        Reject(() => OrchestratorClient.ExtractResponseText("{\"reply\":42}", "reply"), "Non-string response must fail.");
        Reject(() => OrchestratorClient.ValidateProfile(new AgentProfile { Endpoint = "file:///test" }), "Invalid endpoint accepted.");
        Reject(() => OrchestratorClient.ValidateProfile(new AgentProfile { AcceptsAttachments = true }), "Missing attachment placeholder accepted.");
        var file = Path.Combine(data, "attachment.txt");
        await File.WriteAllTextAsync(file, "abc", new UTF8Encoding(false));
        var attachment = await AttachmentService.ReadAsync(file, profile);
        Check(attachment.Base64 == "YWJj" && attachment.Size == 3, "File preparation failed.");
        try { await AttachmentService.ReadAsync(file, new AgentProfile { AcceptsAttachments = true, AllowedExtensions = ".pdf" }); throw new Exception("Extension accepted."); }
        catch (InvalidOperationException) { assertions++; }
        var largeFile = Path.Combine(data, "large.txt");
        await File.WriteAllBytesAsync(largeFile, new byte[1024 * 1024 + 1]);
        try { await AttachmentService.ReadAsync(largeFile, new AgentProfile { AcceptsAttachments = true, MaxAttachmentMegabytes = 1 }); throw new Exception("Oversized file accepted."); }
        catch (InvalidOperationException) { assertions++; }
        var store = new LocalStore(data);
        Check((await store.LoadSettingsAsync()).Theme == ThemeMode.System, "Default must follow the system.");
        foreach (var mode in Enum.GetValues<ThemeMode>())
        {
            await store.SaveSettingsAsync(new AppSettings { Theme = mode });
            Check((await store.LoadSettingsAsync()).Theme == mode, "Theme preference roundtrip failed.");
        }
        Check(ThemeAppearance.Resolve(ThemeMode.System, true).IsDark &&
            !ThemeAppearance.Resolve(ThemeMode.System, false).IsDark, "System theme resolution failed.");
        Check(!ThemeAppearance.Resolve(ThemeMode.Light, true).IsDark &&
            ThemeAppearance.Resolve(ThemeMode.Dark, false).IsDark, "Explicit preference must override system theme.");
        await store.SaveSettingsAsync(new AppSettings { Theme = ThemeMode.Light });
        await store.SaveProfilesAsync([profile]);
        await store.SaveConversationAsync(conversation);
        Check((await store.LoadProfilesAsync()).Single().Id == profile.Id, "Profile roundtrip failed.");
        var restored = (await store.LoadConversationsAsync()).Items.Single();
        Check(restored.Messages.Last().Text == message.Text, "History roundtrip failed.");
        Check(restored.Messages.Last().Attachments.Single().Base64 == "", "Attachment binary must not persist.");
        Check(!File.ReadAllText(Path.Combine(data, "profiles.json")).Contains("session-secret"), "Session credential persisted.");
        await File.WriteAllTextAsync(Path.Combine(data, "conversations", "broken.json"), "invalid json");
        var loaded = await store.LoadConversationsAsync();
        Check(loaded.Items.Count == 1 && loaded.Errors.Count == 1, "Corrupt history was not isolated.");
        File.Delete(Path.Combine(data, "conversations", "broken.json"));
        conversation.Id = "../escape";
        Reject(() => store.SaveConversationAsync(conversation), "Path traversal accepted.");
        conversation.Id = restored.Id;
        Reject(() => store.DeleteConversationAsync("../escape"), "Deleting history accepted path traversal.");
        Reject(() => store.DeleteConversationAsync(Guid.NewGuid().ToString()), "Deleting history must use a local compact GUID, not a remote ID.");
        var blocked = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.Combine(data, "conversations", blocked + ".json"));
        try { await store.DeleteConversationAsync(blocked); throw new Exception("Deleting a directory as history was accepted."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Check(File.Exists(Path.Combine(data, "conversations", conversation.Id + ".json")), "A failed deletion changed another conversation."); }
        var tesitoProfile = new AgentProfile
        {
            Endpoint = "http://127.0.0.1:8000/api/v1/conversations/runs",
            BackendCreatesConversation = true,
            ResponseTextPath = "response",
            RequestTemplate = """{"message":"{{message}}","conversation_id":"{{conversationId}}"}"""
        };
        var tesitoConversation = new Conversation { AgentProfileId = tesitoProfile.Id };
        var tesitoMessage = new ChatMessage { Text = "Hola" };
        var tesitoId = Guid.NewGuid().ToString();
        var requests = 0;
        using (var client = new OrchestratorClient(new FakeBackend(async (request, cancellation) =>
        {
            var payload = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellation))!.AsObject();
            Check(request.RequestUri!.AbsolutePath == "/api/v1/conversations/runs" && request.Method == HttpMethod.Post,
                "Tesito endpoint or method differs from OpenAPI.");
            Check(payload.Count == 2 && payload["message"]!.GetValue<string>() == "Hola", "Tesito payload differs from RunCreate.");
            if (requests++ == 0) Check(payload.ContainsKey("conversation_id") && payload["conversation_id"] is null,
                "New backend conversation must send JSON null, not a local UUID or the string null.");
            else Check(payload["conversation_id"]!.GetValue<string>() == tesitoId, "Follow-up must use the backend UUID.");
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(new JsonObject
                {
                    ["id"] = Guid.NewGuid().ToString(), ["conversation_id"] = tesitoId,
                    ["status"] = "completed", ["response"] = "Hola desde Tesito"
                }.ToJsonString(), Encoding.UTF8, "application/json")
            };
        })))
        {
            var reply = await client.SendAsync(tesitoProfile, tesitoConversation, tesitoMessage, "", CancellationToken.None);
            Check(reply.Text == "Hola desde Tesito", "Tesito response field was not mapped.");
            tesitoConversation.RemoteConversationId = reply.ConversationId;
            await client.SendAsync(tesitoProfile, tesitoConversation, tesitoMessage, "", CancellationToken.None);
        }
        await store.SaveProfilesAsync([tesitoProfile]);
        Check((await store.LoadProfilesAsync()).Single().BackendCreatesConversation, "Backend conversation setting was not persisted.");
        using (var client = new OrchestratorClient(new FakeBackend((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
               { Content = new StringContent("{\"detail\":\"Conversation not found\"}") }))))
        {
            try { await client.SendAsync(tesitoProfile, tesitoConversation, tesitoMessage, "", CancellationToken.None); throw new Exception("404 accepted."); }
            catch (HttpRequestException ex)
            {
                Check(ex.StatusCode == HttpStatusCode.NotFound && ex.Message.Contains("Conversation not found"), "404 diagnosis lost backend detail.");
            }
        }
        using (var client = new OrchestratorClient(new FakeBackend(async (request, cancellation) =>
        {
            Check(request.Headers.Authorization?.ToString() == "Bearer secret", "Authentication header failed.");
            Check(request.Method == HttpMethod.Post, "HTTP method failed.");
            Check(JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellation))!["input"]!.GetValue<string>() == message.Text, "HTTP request differs.");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"request_id\":\"request-1\",\"conversation_id\":\"server-session\",\"status\":\"completed\",\"answer\":\"backend answer\"}", Encoding.UTF8, "application/json") };
        })))
        {
            var reply = await client.SendAsync(profile, conversation, message, "secret", CancellationToken.None);
            Check(reply.Text == "backend answer" && reply.RawResponse.Contains("request_id"), "HTTP response mapping failed.");
            Check(reply.ConversationId == "server-session", "Backend conversation id was lost.");
            conversation.RemoteConversationId = reply.ConversationId;
            Check(JsonNode.Parse(OrchestratorClient.BuildRequest(profile, conversation, message))!["session"]!.GetValue<string>() == "server-session", "Backend conversation id was not reused.");
            await store.SaveConversationAsync(conversation);
            Check((await store.LoadConversationsAsync()).Items.Single().RemoteConversationId == "server-session", "Backend conversation id was not persisted.");
        }
        using (var client = new OrchestratorClient(new FakeBackend((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("error") }))))
        {
            try { await client.SendAsync(profile, conversation, message, "", CancellationToken.None); throw new Exception("HTTP 500 accepted."); }
            catch (HttpRequestException ex) { Check(ex.StatusCode == HttpStatusCode.InternalServerError, "HTTP error status lost."); }
        }
        using (var client = new OrchestratorClient(new FakeBackend(async (_, cancellation) =>
        {
            await Task.Delay(Timeout.Infinite, cancellation);
            return new HttpResponseMessage(HttpStatusCode.OK);
        })))
        {
            using var cancellation = new CancellationTokenSource(50);
            var requestTask = client.SendAsync(profile, conversation, message, "", cancellation.Token);
            try { await requestTask; throw new Exception("Cancellation ignored."); }
            catch (OperationCanceledException) { assertions++; }
            profile.TimeoutSeconds = 1;
            try { await client.SendAsync(profile, conversation, message, "", CancellationToken.None); throw new Exception("Timeout ignored."); }
            catch (TimeoutException) { assertions++; }
        }
        profile.Endpoint = "http://localhost:8000/chat";
        await store.SaveProfilesAsync([profile]);
    }

    private static async Task VerifyGroupsAsync(string data)
    {
        var catalog = new AgentCatalog { Profiles = [new AgentProfile { Id = "preserved", AgentSystem = "Original" }] };
        AgentGroups.Normalize(catalog);
        Check(catalog.Groups.Contains("DEFAULT") && catalog.Groups.Contains("Original"), "Legacy group names were not imported.");
        AgentGroups.Add(catalog, "  Vacío  ");
        Reject(() => AgentGroups.Add(catalog, "vacío"), "Duplicate group accepted ignoring case.");
        Reject(() => AgentGroups.Add(catalog, " "), "Empty group name accepted.");
        Reject(() => AgentGroups.Rename(catalog, "DEFAULT", "Cambio"), "DEFAULT rename accepted.");
        Reject(() => AgentGroups.Delete(catalog, "default"), "DEFAULT deletion accepted.");
        AgentGroups.Rename(catalog, "Original", "Nuevo");
        Check(catalog.Profiles.Single().AgentSystem == "Nuevo" && catalog.Profiles.Single().Id == "preserved", "Rename broke profile membership or identity.");
        var store = new LocalStore(Path.Combine(data, "group-catalog"));
        await store.SaveCatalogAsync(catalog);
        var restored = await store.LoadCatalogAsync();
        Check(restored.Groups.Contains("Vacío") && restored.Groups.Contains("Nuevo"), "Empty groups did not persist.");
        await store.SaveProfilesAsync(restored.Profiles);
        Check((await store.LoadCatalogAsync()).Groups.Contains("Vacío"), "Saving a profile dropped empty groups.");
        AgentGroups.Delete(restored, "Nuevo");
        await store.SaveCatalogAsync(restored);
        var deleted = await store.LoadCatalogAsync();
        Check(!deleted.Groups.Contains("Nuevo") && deleted.Profiles.Single().AgentSystem == "DEFAULT", "Delete did not reassign agents atomically.");
        var path = Path.Combine(store.RootPath, "profiles.json");
        await File.WriteAllTextAsync(path, "{}");
        try { await store.LoadCatalogAsync(); throw new Exception("Incomplete catalog accepted."); }
        catch (InvalidDataException) { Check(await File.ReadAllTextAsync(path) == "{}", "Invalid catalog was overwritten."); }
    }

    private static async Task VerifyHealthAsync(string data)
    {
        var legacyRoot = Path.Combine(data, "legacy");
        Directory.CreateDirectory(legacyRoot);
        var legacyPath = Path.Combine(legacyRoot, "profiles.json");
        await File.WriteAllTextAsync(legacyPath, """[{"Id":"legacy","Name":"Anterior"},{"Id":"empty","AgentSystem":"  ","Health":null}]""");
        var legacyStore = new LocalStore(legacyRoot);
        var profiles = await legacyStore.LoadProfilesAsync();
        Check(profiles.All(p => p.AgentSystem == "DEFAULT" && p.Health.Endpoint == ""), "Legacy profiles must migrate to DEFAULT without inventing health URLs.");
        profiles[0].AgentSystem = "Sistema A";
        profiles[0].Health = new HealthSettings { Endpoint = "https://example.invalid/health", ResponseValuePath = "status", ExpectedValue = "ready", IntervalSeconds = 12 };
        await legacyStore.SaveProfilesAsync(profiles);
        var restored = (await legacyStore.LoadProfilesAsync())[0];
        Check(restored.AgentSystem == "Sistema A" && restored.Health.IntervalSeconds == 12 && restored.Health.ExpectedValue == "ready", "System and health settings did not persist.");
        await File.WriteAllTextAsync(legacyPath, "invalid");
        try { await legacyStore.LoadProfilesAsync(); throw new Exception("Invalid profiles accepted."); }
        catch (System.Text.Json.JsonException) { Check(await File.ReadAllTextAsync(legacyPath) == "invalid", "Invalid profiles were overwritten."); }
        Reject(() => HealthClient.Validate(new()), "New profiles must require health.");
        Reject(() => HealthClient.Validate(new() { Endpoint = "file:///health" }), "Health accepted a file URL.");
        Reject(() => HealthClient.Validate(new() { Endpoint = "https://user:password@example.invalid" }), "Health accepted credentials in URL.");
        Reject(() => HealthClient.Validate(new() { Endpoint = "https://example.invalid", Method = "DELETE" }), "Health accepted unsupported method.");
        Reject(() => HealthClient.Validate(new() { Endpoint = "https://example.invalid", Method = "HEAD", ResponseValuePath = "status" }), "HEAD accepted JSON validation.");
        Reject(() => HealthClient.Validate(new() { Endpoint = "https://example.invalid", IntervalSeconds = 0 }), "Invalid interval accepted.");
        var health = restored.Health;
        health.ResponseValuePath = "checks.0.ready";
        health.ExpectedValue = "true";
        health.AuthenticationHeader = "X-Health-Key";
        health.AuthenticationPrefix = "";
        using (var client = new HealthClient(new FakeBackend((request, _) =>
        {
            Check(request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == "/health", "Health method or URL changed.");
            Check(request.Headers.GetValues("X-Health-Key").Single() == "session-secret", "Health authentication failed.");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"checks":[{"ready":true}]}""") });
        })))
        {
            Check((await client.CheckAsync(health, "session-secret", CancellationToken.None)).State == "Saludable", "Nested JSON health check failed.");
            health.ExpectedValue = "false";
            Check((await client.CheckAsync(health, "session-secret", CancellationToken.None)).State == "No saludable", "Mismatched JSON accepted.");
            health.ResponseValuePath = "missing";
            Check((await client.CheckAsync(health, "session-secret", CancellationToken.None)).State == "No saludable", "Missing JSON path accepted.");
        }
        health.ResponseValuePath = "";
        using (var client = new HealthClient(new FakeBackend((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))))
        {
            Check((await client.CheckAsync(health, "", CancellationToken.None)).State == "No saludable", "HTTP 503 accepted.");
            health.ExpectedStatusCode = 503;
            Check((await client.CheckAsync(health, "", CancellationToken.None)).State == "Saludable", "Configured HTTP status ignored.");
        }
        health.ExpectedStatusCode = 200;
        health.ResponseValuePath = "status";
        using (var client = new HealthClient(new FakeBackend((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("invalid json") }))))
            Check((await client.CheckAsync(health, "", CancellationToken.None)).State == "Error", "Invalid health JSON accepted.");
        using (var client = new HealthClient(new FakeBackend((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 65537)) }))))
            Check((await client.CheckAsync(health, "", CancellationToken.None)).Detail.Contains("64 KB"), "Oversized health body accepted.");
        using (var client = new HealthClient(new FakeBackend((_, _) => throw new HttpRequestException("secret should not be shown"))))
        {
            var result = await client.CheckAsync(health, "", CancellationToken.None);
            Check(result.State == "Sin conexión" && !result.Detail.Contains("secret"), $"Network failure mapping failed: {result.State}: {result.Detail}");
        }
        health.TimeoutSeconds = 1;
        using (var client = new HealthClient(new FakeBackend(async (_, cancellation) =>
        {
            await Task.Delay(Timeout.Infinite, cancellation);
            return new HttpResponseMessage(HttpStatusCode.OK);
        })))
        {
            Check((await client.CheckAsync(health, "", CancellationToken.None)).State == "Tiempo agotado", "Health timeout ignored.");
            using var cancellation = new CancellationTokenSource(30);
            try { await client.CheckAsync(health, "", cancellation.Token); throw new Exception("Health cancellation ignored."); }
            catch (OperationCanceledException) { assertions++; }
        }
        var active = 0;
        var maximum = 0;
        using (var client = new HealthClient(new FakeBackend(async (_, cancellation) =>
        {
            var current = Interlocked.Increment(ref active);
            lock (health) maximum = Math.Max(maximum, current);
            await Task.Delay(40, cancellation);
            Interlocked.Decrement(ref active);
            return new HttpResponseMessage(HttpStatusCode.OK);
        })))
        {
            health.ResponseValuePath = "";
            var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => client.CheckAsync(health, "", CancellationToken.None)));
            Check(maximum == 4 && results.All(r => r.State == "Saludable"), "Health concurrency limit failed.");
            Check((await client.CheckAsync(new(), "", CancellationToken.None)).State == "Sin configurar", "Missing health must not make a request.");
        }
    }

    private sealed class FakeBackend(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(request, cancellationToken);
    }
}
'@
    Set-Content -LiteralPath (Join-Path $verificationDirectory 'Program.cs') -Value $source -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $verificationDirectory 'NuGet.Config') -Value '<configuration><packageSources><clear /></packageSources></configuration>' -Encoding UTF8
    $artifactsProperty = '-p:ArtifactsPath=' + (Join-Path $verificationDirectory 'artifacts')
    & dotnet restore (Join-Path $verificationDirectory 'Verify.csproj') --configfile (Join-Path $verificationDirectory 'NuGet.Config') $artifactsProperty
    if ($LASTEXITCODE -ne 0) { throw "Verification restore failed with exit code $LASTEXITCODE" }
    $runArguments = @('run', '--no-restore', '-p:UseSharedCompilation=false', $artifactsProperty, '--project', (Join-Path $verificationDirectory 'Verify.csproj'))
    if ($ScreenshotDirectory) {
        $runArguments += @('--', [IO.Path]::GetFullPath($ScreenshotDirectory))
    }
    & dotnet @runArguments
    if ($LASTEXITCODE -ne 0) { throw "Verification failed with exit code $LASTEXITCODE" }
}
finally {
    $resolvedDirectory = [IO.Path]::GetFullPath($verificationDirectory)
    if ($resolvedDirectory.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolvedDirectory).StartsWith('agentest-verify-')) {
        Remove-Item -LiteralPath $resolvedDirectory -Recurse -Force
    }
}
