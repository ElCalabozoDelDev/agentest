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
            Check(send.Enabled, "Non-empty composer must allow send with a profile.");
            input.Clear();
            if (!string.IsNullOrEmpty(screenshotDirectory))
            {
                Directory.CreateDirectory(screenshotDirectory);
                Capture(form, Path.Combine(screenshotDirectory, "chat.png"));
            }
            form.Close();
        }
        using (var editor = new ProfileEditorForm(new AgentProfile(), "session-secret"))
        {
            editor.Show();
            Application.DoEvents();
            Check(editor.SessionToken == "session-secret", "Profile token was lost.");
            if (!string.IsNullOrEmpty(screenshotDirectory)) Capture(editor, Path.Combine(screenshotDirectory, "profile.png"));
            editor.Close();
        }
        Console.WriteLine($"PASS: {assertions} checks (JSON contracts, HTTP, cancellation, timeout, persistence, attachments, WinForms).");
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

    private sealed class FakeBackend(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(request, cancellationToken);
    }
}
'@
    Set-Content -LiteralPath (Join-Path $verificationDirectory 'Program.cs') -Value $source -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $verificationDirectory 'NuGet.Config') -Value '<configuration><packageSources><clear /></packageSources></configuration>' -Encoding UTF8
    & dotnet restore (Join-Path $verificationDirectory 'Verify.csproj') --configfile (Join-Path $verificationDirectory 'NuGet.Config')
    if ($LASTEXITCODE -ne 0) { throw "Verification restore failed with exit code $LASTEXITCODE" }
    $runArguments = @('run', '--no-restore', '-p:UseSharedCompilation=false', '--project', (Join-Path $verificationDirectory 'Verify.csproj'))
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
