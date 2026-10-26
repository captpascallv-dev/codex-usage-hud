using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CodexUsageHud.App;
using CodexUsageHud.Core;

namespace CodexUsageHud.Tests;

internal static class ClaudeApiCreditTests
{
    private const string Label = "OrgSecretLabel";
    private const string Html = "SECRET_HTML_TABLE";

    public static int Run(string[] args)
    {
        if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.Error.WriteLine("usage: --claude-api-credit <output-directory>");
            return 2;
        }

        var output = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(output);
        var log = new StringBuilder();
        var root = Path.Combine(Path.GetTempPath(), "hud-claude-api-credit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            FramingAndValidation(root, log);
            AccountFreshnessAndBounds(root, log);
            ExportGlanceAndPrivacy(root, log);
            StaticPackage(log);
            HostProcess(root, log);
            RenderMoneyRow(root, output, log);
            log.AppendLine("result=pass");
            File.WriteAllText(Path.Combine(output, "claude-api-credit-checks.txt"), log.ToString(), Encoding.UTF8);
            Console.WriteLine("claude-api-credit pass");
            return 0;
        }
        catch (Exception ex)
        {
            log.AppendLine("result=fail");
            log.AppendLine(ex.ToString());
            File.WriteAllText(Path.Combine(output, "claude-api-credit-checks.txt"), log.ToString(), Encoding.UTF8);
            Console.Error.WriteLine("claude-api-credit fail");
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        finally
        {
            try { Directory.Delete(root, true); }
            catch (Exception ex) { log.AppendLine("cleanup=" + ex.Message); }
        }
    }

    private static void FramingAndValidation(string root, StringBuilder log)
    {
        var directory = Path.Combine(root, "frames");
        Directory.CreateDirectory(directory);
        var now = new DateTimeOffset(2026, 10, 27, 2, 0, 0, TimeSpan.Zero);
        var binding = Binding(Label);
        var observed = "2026-10-27T01:59:00.0000000Z";
        var reply = Run(directory, Observation(binding, observed), now, out var code);
        Expect(code == 0 && reply.Contains("\"ok\":true", StringComparison.Ordinal), "frame-ok");
        var cache = File.ReadAllText(Path.Combine(directory, ClaudeApiCreditCache.FileName), Encoding.UTF8);
        Expect(cache.Contains("\"remaining\":\"99.20\"", StringComparison.Ordinal), "cache-remaining");
        Expect(cache.Contains("\"grant_amount\":\"100.00\"", StringComparison.Ordinal), "cache-grant");
        Expect(cache.Contains(binding, StringComparison.Ordinal), "cache-binding");
        Expect(!cache.Contains(Label, StringComparison.Ordinal), "cache-no-label");
        Expect(!cache.Contains(Html, StringComparison.Ordinal), "cache-no-html");
        Expect(!File.Exists(Path.Combine(directory, ClaudeApiCreditCache.TempFileName)), "temp-gone");
        var names = Directory.EnumerateFiles(directory).Select(Path.GetFileName).ToArray();
        Expect(names.Length == 1 && names[0] == ClaudeApiCreditCache.FileName, "one-cache");

        var before = File.ReadAllBytes(Path.Combine(directory, ClaudeApiCreditCache.FileName));
        reply = Run(directory, Observation(binding, "2026-10-27T02:04:00.0000000Z"), now, out code);
        Expect(code == 2 && reply.Contains("bad_time", StringComparison.Ordinal), "future");
        Expect(File.ReadAllBytes(Path.Combine(directory, ClaudeApiCreditCache.FileName)).AsSpan().SequenceEqual(before), "future-keeps");

        reply = Run(directory, Encoding.UTF8.GetBytes(ObservationText(binding, observed).Replace(
            ClaudeApiCreditLimits.SourceUrl, "https://example.invalid/settings/billing", StringComparison.Ordinal)), now, out code);
        Expect(code == 2 && reply.Contains("bad_source", StringComparison.Ordinal), "bad-source");
        reply = Run(directory, Encoding.UTF8.GetBytes("{\"schema\":2,\"kind\":\"observation\"}"), now, out code);
        Expect(reply.Contains("bad_schema", StringComparison.Ordinal), "bad-schema");
        reply = Run(directory, Encoding.UTF8.GetBytes(ObservationText(binding, observed, remaining: "100.01")), now, out code);
        Expect(reply.Contains("bad_amount", StringComparison.Ordinal), "remaining-over-grant");
        reply = Run(directory, Encoding.UTF8.GetBytes(ObservationText(binding, observed).Replace("\"USD\"", "\"EUR\"", StringComparison.Ordinal)), now, out code);
        Expect(reply.Contains("bad_amount", StringComparison.Ordinal), "bad-currency");
        reply = Run(directory, Encoding.UTF8.GetBytes(ObservationText(binding, observed).Replace("\"99.20\"", "99.20", StringComparison.Ordinal)), now, out code);
        Expect(reply.Contains("bad_amount", StringComparison.Ordinal), "numeric-amount");
        reply = Run(directory, Encoding.UTF8.GetBytes(ObservationText(binding, observed, expiresOn: "2026-11-12T00:00:00Z")), now, out code);
        Expect(reply.Contains("bad_date", StringComparison.Ordinal), "date-time");
        reply = Run(directory, Encoding.UTF8.GetBytes(ObservationText(binding, observed, grantedOn: "2026-10-28")), now, out code);
        Expect(reply.Contains("bad_date", StringComparison.Ordinal), "grant-after-observed");
        reply = Run(directory, Encoding.UTF8.GetBytes(ObservationText(binding.ToUpperInvariant(), observed)), now, out code);
        Expect(reply.Contains("bad_binding", StringComparison.Ordinal), "upper-binding");
        reply = Run(directory, Encoding.UTF8.GetBytes("{"), now, out code);
        Expect(reply.Contains("rejected", StringComparison.Ordinal), "bad-json");
        Expect(File.ReadAllBytes(Path.Combine(directory, ClaudeApiCreditCache.FileName)).AsSpan().SequenceEqual(before), "rejects-keep");

        reply = RunRaw(directory, new byte[] { 1, 0 }, now, out code);
        Expect(code == 2 && reply.Contains("truncated", StringComparison.Ordinal), "truncated-header");
        var shortBody = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(shortBody, 20);
        reply = RunRaw(directory, shortBody, now, out code);
        Expect(reply.Contains("truncated", StringComparison.Ordinal), "truncated-body");
        var oversized = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(oversized, 9000);
        reply = RunRaw(directory, oversized, now, out code);
        Expect(reply.Contains("oversized", StringComparison.Ordinal), "oversized");
        Expect(File.ReadAllBytes(Path.Combine(directory, ClaudeApiCreditCache.FileName)).AsSpan().SequenceEqual(before), "malformed-frames-keep");

        var empty = new ClaudeApiCreditCache("");
        Expect(!empty.TryApplyJson(Observation(binding, observed), now, out var error) && error == "bad_directory", "empty-directory");
        reply = Run(directory, Observation(binding, observed, remaining: "0.00", grant: "80.00"), now, out code);
        Expect(code == 0, "zero-and-changed-total");
        var zero = new ClaudeApiCreditCache(directory).Read(now);
        Expect(zero.Observation?.Remaining == 0.00m && zero.Observation.GrantAmount == 80.00m, "zero-stored");
        reply = Run(directory, Observation(binding, observed, remaining: "12.50", grant: "250.00"), now, out code);
        Expect(code == 0, "changed-total");
        var changed = new ClaudeApiCreditCache(directory).Read(now);
        Expect(changed.Observation?.Remaining == 12.50m && changed.Observation.GrantAmount == 250.00m, "changed-stored");
        log.AppendLine("framing_validation=pass");
    }

    private static void AccountFreshnessAndBounds(string root, StringBuilder log)
    {
        var directory = Path.Combine(root, "account");
        Directory.CreateDirectory(directory);
        var now = new DateTimeOffset(2026, 10, 27, 2, 0, 0, TimeSpan.Zero);
        var binding = Binding("alpha");
        var other = Binding("beta");
        var observed = "2026-10-27T01:59:00.0000000Z";
        Run(directory, Observation(binding, observed), now, out var code);
        Expect(code == 0, "seed");
        var original = new ClaudeApiCreditCache(directory).Read(now).Observation;
        Expect(original is not null && original.State == ClaudeApiCreditState.Ok, "seed-ok");

        Run(directory, Status("source_lost", "2026-10-27T01:59:30.0000000Z", binding), now, out code);
        var lost = new ClaudeApiCreditCache(directory).Read(now).Observation;
        Expect(lost is not null && lost.State == ClaudeApiCreditState.Stale && lost.Remaining == 99.20m, "source-lost-keeps");
        Expect(lost!.ObservedAtUtc == original!.ObservedAtUtc, "source-lost-keeps-time");

        Run(directory, Observation(binding, observed), now, out code);
        Run(directory, Status("malformed", "2026-10-27T01:59:40.0000000Z", binding), now, out code);
        var malformed = new ClaudeApiCreditCache(directory).Read(now).Observation;
        Expect(malformed?.State == ClaudeApiCreditState.Stale && malformed.Remaining == 99.20m, "malformed-keeps");

        Run(directory, Status("source_lost", "2026-10-27T01:59:50.0000000Z", other), now, out code);
        var mismatched = new ClaudeApiCreditCache(directory).Read(now).Observation;
        Expect(mismatched is not null && !mismatched.HasAmounts, "mismatched-clears");

        Run(directory, Observation(binding, observed), now, out code);
        Run(directory, Status("account_changed", "2026-10-27T01:59:55.0000000Z", other), now, out code);
        var switched = new ClaudeApiCreditCache(directory).Read(now).Observation;
        Expect(switched is not null && !switched.HasAmounts && switched.OrganizationBinding == other, "account-switch");
        var switchedFile = File.ReadAllText(Path.Combine(directory, ClaudeApiCreditCache.FileName), Encoding.UTF8);
        Expect(!switchedFile.Contains("99.20", StringComparison.Ordinal), "account-switch-drops-amount");

        Run(directory, Observation(binding, observed), now, out code);
        Run(directory, Status("stopped", "2026-10-27T01:59:56.0000000Z", ""), now, out code);
        var stopped = new ClaudeApiCreditCache(directory).Read(now).Observation;
        Expect(stopped is not null && !stopped.HasAmounts && stopped.OrganizationBinding == binding, "stopped-clears-amount");

        Run(directory, Observation(binding, observed), now, out code);
        Run(directory, Status("login_changed", "2026-10-27T01:59:57.0000000Z", binding), now, out code);
        var login = new ClaudeApiCreditCache(directory).Read(now).Observation;
        Expect(login is not null && !login.HasAmounts && login.OrganizationBinding.Length == 0, "login-clears");

        var expiryNow = new DateTimeOffset(2026, 11, 13, 0, 0, 0, TimeSpan.Zero);
        Run(directory, Observation(binding, "2026-11-13T00:00:00.0000000Z"), expiryNow, out code);
        var expired = new ClaudeApiCreditCache(directory).Read(expiryNow).Observation;
        Expect(expired?.State == ClaudeApiCreditState.Unavailable && expired.Remaining == 99.20m, "expired-keeps-amount");
        Expect(expired!.ExpiresOnUtc == new DateOnly(2026, 11, 12), "expired-date-only");

        var sameDay = new DateTimeOffset(2026, 11, 12, 23, 0, 0, TimeSpan.Zero);
        Run(directory, Observation(binding, "2026-11-12T23:00:00.0000000Z"), sameDay, out code);
        var validThrough = new ClaudeApiCreditCache(directory).Read(sameDay).Observation;
        Expect(validThrough?.State == ClaudeApiCreditState.Ok, "valid-on-expiry-date");

        Run(directory, Observation(binding, observed), now, out code);
        var cachePath = Path.Combine(directory, ClaudeApiCreditCache.FileName);
        var storedOk = File.ReadAllText(cachePath, Encoding.UTF8);
        Expect(storedOk.Contains("\"state\":\"ok\"", StringComparison.Ordinal), "file-still-ok");
        var aged = new ClaudeApiCreditCache(directory).Read(now.AddMinutes(16)).Observation;
        Expect(aged?.State == ClaudeApiCreditState.Stale && aged.ObservedAtUtc == original.ObservedAtUtc, "aged-display");
        Expect(File.ReadAllText(cachePath, Encoding.UTF8).Contains("\"state\":\"ok\"", StringComparison.Ordinal), "aged-does-not-rewrite");

        File.WriteAllText(cachePath, new string('x', 5000), Encoding.UTF8);
        var oversized = new ClaudeApiCreditCache(directory).Read(now);
        Expect(oversized.Observation is null && !oversized.KeepPrevious, "oversized-cache-absent");
        log.AppendLine("account_freshness_bounds=pass");
    }

    private static void ExportGlanceAndPrivacy(string root, StringBuilder log)
    {
        var now = new DateTimeOffset(2026, 10, 27, 2, 0, 0, TimeSpan.Zero);
        var binding = Binding(Label);
        var directory = Path.Combine(root, "export");
        Directory.CreateDirectory(directory);
        Run(directory, Observation(binding, "2026-10-27T01:59:00.0000000Z"), now, out var code);
        Expect(code == 0, "export-seed");
        var credit = new ClaudeApiCreditCache(directory).Read(now).Observation;
        Expect(credit is not null, "export-credit");
        var row = ClaudeApiCreditPresentation.BuildRow(credit!);
        Expect(row.Title == "Claude API赠送额度", "title");
        Expect(row.AmountText == "US$99.20 / US$100.00", "amount-text");
        Expect(row.DetailText.Contains("2026-10-15", StringComparison.Ordinal) &&
               row.DetailText.Contains("2026-11-12", StringComparison.Ordinal) &&
               row.DetailText.Contains("UTC", StringComparison.Ordinal), "dates");
        Expect(row.DetailText.IndexOf(':') < 0, "date-only-detail");
        Expect(!row.AmountText.Contains('%'), "money-not-percent");

        var cursor = new ProviderSlotSnapshot(ProviderSlotIds.Cursor, ProviderIds.Cursor, "Cursor", true, false,
            QuotaSlotStatus.Live, "正常", now, "Cursor",
            new[] { new QuotaWindowObservation("autoPercentUsed", "每周", 3, now.AddDays(7), 10080, true) });
        Expect(cursor.GlanceRemainingPercent == 97d, "glance-97");
        Expect(cursor.GlancePercentText == "97%", "glance-text");
        Expect(cursor.Windows.Count == 1, "one-window");

        var board = new ProviderQuotaBoard(new[]
        {
            Placeholder(ProviderSlotIds.CodexPrimary, ProviderIds.Codex),
            Placeholder(ProviderSlotIds.CodexSecondary, ProviderIds.Codex),
            cursor,
            Placeholder(ProviderSlotIds.Grok, ProviderIds.Grok),
            Placeholder(ProviderSlotIds.GrokBot, ProviderIds.GrokBot),
            Placeholder(ProviderSlotIds.Claude, ProviderIds.Claude),
        }, now);
        var writer = new QuotaSnapshotWriter(directory, "1.1.3", clock: () => now);
        writer.SetEnabled(true);
        writer.Publish(board, credit);
        writer.WhenIdleAsync().GetAwaiter().GetResult();
        var json = File.ReadAllText(writer.FinalPath, Encoding.UTF8);
        using var document = JsonDocument.Parse(json);
        Expect(document.RootElement.GetProperty("schema").GetInt32() == 1, "schema-stays-1");
        var slots = document.RootElement.GetProperty("slots").EnumerateArray().ToArray();
        Expect(slots.Length == 6, "six-slots");
        var cursorSlot = slots.Single(slot => slot.GetProperty("slot").GetString() == "cursor");
        Expect(cursorSlot.GetProperty("windows")[0].GetProperty("remaining_percent").GetDouble() == 97d, "export-glance");
        var money = document.RootElement.GetProperty("claude_api_credit");
        string[] fields = money.EnumerateObject().Select(property => property.Name).ToArray();
        Expect(fields.SequenceEqual(new[]
        {
            "currency", "remaining", "grant_amount", "granted_on_utc", "expires_on_utc", "observed_at_utc", "state",
        }), "money-fields");
        Expect(money.GetProperty("currency").GetString() == "USD", "usd");
        Expect(money.GetProperty("remaining").GetString() == "99.20", "export-remaining");
        Expect(money.GetProperty("grant_amount").GetString() == "100.00", "export-grant");
        Expect(money.GetProperty("granted_on_utc").GetString() == "2026-10-15", "export-granted");
        Expect(money.GetProperty("expires_on_utc").GetString() == "2026-11-12", "export-expires");
        Expect(money.GetProperty("state").GetString() == "ok", "export-state");
        Expect(!json.Contains(binding, StringComparison.Ordinal), "export-no-binding");
        Expect(!json.Contains(Label, StringComparison.Ordinal), "export-no-label");
        Expect(!json.Contains(Html, StringComparison.Ordinal), "export-no-html");
        Expect(!json.Contains("organization_binding", StringComparison.Ordinal), "export-no-binding-name");

        var omittedDir = Path.Combine(root, "omitted");
        var omitted = new QuotaSnapshotWriter(omittedDir, "1.1.3", clock: () => now);
        omitted.SetEnabled(true);
        omitted.Publish(board);
        omitted.WhenIdleAsync().GetAwaiter().GetResult();
        using var omittedDocument = JsonDocument.Parse(File.ReadAllText(omitted.FinalPath, Encoding.UTF8));
        Expect(!omittedDocument.RootElement.TryGetProperty("claude_api_credit", out _), "omit-without-observation");
        log.AppendLine("export_glance_privacy=pass");
    }

    private static void StaticPackage(StringBuilder log)
    {
        var repo = RepoRoot();
        var extension = Path.Combine(repo, "extension", "claude-api-credit");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(extension, "manifest.json"), Encoding.UTF8));
        var manifest = manifestDocument.RootElement;
        Expect(manifest.GetProperty("manifest_version").GetInt32() == 3, "mv3");
        var permissions = manifest.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()).ToArray();
        Expect(permissions.SequenceEqual(new[] { "nativeMessaging", "storage", "alarms", "scripting" }), "permissions");
        var hosts = manifest.GetProperty("host_permissions").EnumerateArray().Select(item => item.GetString()).ToArray();
        Expect(hosts.Length == 1 && hosts[0] == ClaudeApiCreditLimits.SourceUrl, "host-permission");
        Expect(!manifest.TryGetProperty("content_scripts", out _), "no-content-script");
        var id = ExtensionId(manifest.GetProperty("key").GetString() ?? "");
        Expect(id == "jnfblichipcdliigeooegbehgnennbfa", "extension-id");
        using var hostDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(extension, "native-host.json"), Encoding.UTF8));
        var host = hostDocument.RootElement;
        Expect(host.GetProperty("name").GetString() == ClaudeApiCreditLimits.NativeHostName, "host-name");
        Expect(host.GetProperty("type").GetString() == "stdio", "host-stdio");
        Expect(host.GetProperty("path").GetString() == "ClaudeApiCreditHost.exe", "template-path");
        Expect(host.GetProperty("allowed_origins")[0].GetString() == "chrome-extension://" + id + "/", "allowed-origin");

        var background = File.ReadAllText(Path.Combine(extension, "background.js"), Encoding.UTF8);
        var parser = File.ReadAllText(Path.Combine(extension, "parser.js"), Encoding.UTF8);
        var content = File.ReadAllText(Path.Combine(extension, "content.js"), Encoding.UTF8);
        Expect(!background.Contains("tabs.query", StringComparison.Ordinal), "no-tabs-query");
        Expect(!background.Contains("chrome.cookies", StringComparison.Ordinal) &&
               !parser.Contains("cookie", StringComparison.Ordinal) &&
               !content.Contains("cookie", StringComparison.Ordinal), "no-cookie");
        Expect(background.Contains("PERIOD_MINUTES = 5", StringComparison.Ordinal), "five-minutes");
        Expect(parser.Contains("促銷額度", StringComparison.Ordinal) &&
               parser.Contains("促销额度", StringComparison.Ordinal) &&
               parser.Contains("Promotional credits", StringComparison.Ordinal), "header-aliases");

        var install = File.ReadAllText(Path.Combine(extension, "install", "Install-ClaudeApiCreditHost.ps1"), Encoding.UTF8);
        var uninstall = File.ReadAllText(Path.Combine(extension, "install", "Uninstall-ClaudeApiCreditHost.ps1"), Encoding.UTF8);
        Expect(install.Contains("HKCU:\\Software\\Google\\Chrome\\NativeMessagingHosts\\com.codexusagehud.claude_api_credit", StringComparison.Ordinal), "hkcu");
        Expect(!install.Contains("HKLM", StringComparison.Ordinal) && !uninstall.Contains("HKLM", StringComparison.Ordinal), "no-hklm");
        Expect(!install.Contains("chrome.exe", StringComparison.OrdinalIgnoreCase), "install-does-not-launch");
        Expect(uninstall.Contains("claude-api-credit-bridge", StringComparison.Ordinal), "uninstall-bridge");
        Expect(uninstall.Contains("claude-api-credit.json", StringComparison.Ordinal), "uninstall-cache");
        Expect(!uninstall.Contains("usage.db", StringComparison.Ordinal), "uninstall-keeps-database-name");

        var engine = File.ReadAllText(Path.Combine(repo, "src", "CodexUsageHud.Core", "UsageEngine.cs"), Encoding.UTF8);
        Expect(engine.Contains("requestTimeout: TimeSpan.FromSeconds(15)", StringComparison.Ordinal), "primary-timeout");
        var glance = File.ReadAllText(Path.Combine(repo, "src", "CodexUsageHud.Core", "ProviderQuotaModels.cs"), Encoding.UTF8);
        Expect(!glance.Contains("ClaudeApiCredit", StringComparison.Ordinal), "glance-independent");
        log.AppendLine("static_package=pass extension_id=" + id);
    }

    private static void HostProcess(string root, StringBuilder log)
    {
        var exe = Path.Combine(RepoRoot(), "src", "CodexUsageHud.ClaudeApiCreditHost", "bin", "Debug", "net8.0", "ClaudeApiCreditHost.exe");
        Expect(File.Exists(exe), "host-exe");
        var directory = Path.Combine(root, "process");
        Directory.CreateDirectory(directory);
        var binding = Binding(Label);
        var observed = DateTimeOffset.UtcNow.AddSeconds(-5).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
        var frame = Frame(Observation(binding, observed));
        var reply = RunProcess(exe, directory, frame, TimeSpan.FromSeconds(15), out var code);
        Expect(code == 0 && reply.Contains("\"ok\":true", StringComparison.Ordinal), "process-ok");
        var cache = File.ReadAllText(Path.Combine(directory, ClaudeApiCreditCache.FileName), Encoding.UTF8);
        Expect(cache.Contains("\"remaining\":\"99.20\"", StringComparison.Ordinal), "process-cache");
        Expect(!cache.Contains(Label, StringComparison.Ordinal), "process-no-label");
        log.AppendLine("host_process=pass");

        var wpf = Path.Combine(RepoRoot(), "src", "CodexUsageHud.App", "bin", "Debug", "net8.0-windows", "CodexUsageHud.App.exe");
        if (!File.Exists(wpf))
        {
            log.AppendLine("wpf_host=missing");
            return;
        }

        var wpfDir = Path.Combine(root, "wpf-host");
        Directory.CreateDirectory(wpfDir);
        try
        {
            var wpfReply = RunProcess(wpf, wpfDir, frame, TimeSpan.FromSeconds(8), out var wpfCode, ClaudeApiCreditLimits.HostFlag);
            log.AppendLine("wpf_host=exit:" + wpfCode.ToString(CultureInfo.InvariantCulture) + " reply=" + wpfReply);
        }
        catch (Exception ex)
        {
            log.AppendLine("wpf_host=unreliable " + ex.Message);
        }
    }

    private static void RenderMoneyRow(string root, string output, StringBuilder log)
    {
        if (Application.Current is null)
        {
            var application = new CodexUsageHud.App.App();
            application.InitializeComponent();
        }

        var directory = Path.Combine(root, "ui");
        Directory.CreateDirectory(directory);
        var now = DateTimeOffset.UtcNow;
        var binding = Binding(Label);
        var observed = now.AddSeconds(-5);
        Run(directory, Observation(binding, observed.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture)), now, out var code);
        Expect(code == 0, "ui-seed");

        using var engine = new UsageEngine(directory, Path.Combine(directory, "usage.db"),
            Path.Combine(directory, "hud.log"), productVersion: "1.1.3");
        var read = engine.ReadClaudeApiCredit();
        Expect(read.Observation?.Remaining == 99.20m && read.Observation.GrantAmount == 100.00m, "engine-read");
        engine.SetQuotaSnapshotExportEnabled(true);
        engine.ExportCurrentQuotaSnapshot();
        engine.FlushQuotaSnapshotAsync().GetAwaiter().GetResult();
        var exported = File.ReadAllText(Path.Combine(directory, QuotaSnapshotWriter.FileName), Encoding.UTF8);
        Expect(exported.Contains("\"remaining\": \"99.20\"", StringComparison.Ordinal) ||
               exported.Contains("\"remaining\":\"99.20\"", StringComparison.Ordinal), "engine-export");
        Expect(!exported.Contains(binding, StringComparison.Ordinal), "engine-export-no-binding");

        using var window = new MainWindow(engine, () => Task.CompletedTask, false)
        {
            ShowActivated = false,
            ShowInTaskbar = false,
            Left = -12000,
            Top = -12000,
            Opacity = 1,
        };
        window.OverrideWorkAreaForTests(new Rect(-12000, -12000, 1600, 1000));
        var trayField = typeof(MainWindow).GetField("_tray", BindingFlags.Instance | BindingFlags.NonPublic);
        if (trayField?.GetValue(window) is System.Windows.Forms.NotifyIcon tray)
            tray.Visible = false;
        window.Show();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var viewModel = (MainViewModel)typeof(MainWindow).GetField("_viewModel", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(window)!;
        var cursor = new ProviderSlotSnapshot(ProviderSlotIds.Cursor, ProviderIds.Cursor, "Cursor", true, false,
            QuotaSlotStatus.Live, "正常", observed, "Cursor",
            new[] { new QuotaWindowObservation("autoPercentUsed", "每周", 3, observed.AddDays(7), 10080, true) });
        viewModel.Apply(new HudSnapshot(
            new QuotaObservation(null, Array.Empty<QuotaBucket>(), QuotaSource.Unavailable, observed, false),
            Array.Empty<SessionAggregate>(), null, observed, false, null, Array.Empty<string>(),
            Array.Empty<HudEvent>(),
            Providers: new ProviderQuotaBoard(new[]
            {
                Placeholder(ProviderSlotIds.CodexPrimary, ProviderIds.Codex),
                Placeholder(ProviderSlotIds.CodexSecondary, ProviderIds.Codex),
                cursor,
                Placeholder(ProviderSlotIds.Grok, ProviderIds.Grok),
                Placeholder(ProviderSlotIds.GrokBot, ProviderIds.GrokBot),
                Placeholder(ProviderSlotIds.Claude, ProviderIds.Claude),
            }, observed)));
        typeof(MainWindow).GetMethod("RefreshClaudeApiCredit", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, null);
        viewModel.SelectSlot(ProviderSlotIds.Cursor, true);
        Expect(viewModel.SlotDetailPercent == "97%", "ui-glance");
        Expect(viewModel.ClaudeApiCreditVisible, "ui-visible");
        Expect(viewModel.ClaudeApiCreditAmount == "US$99.20 / US$100.00", "ui-amount");
        Expect(viewModel.ClaudeApiCreditDetail.Contains("2026-10-15", StringComparison.Ordinal) &&
               viewModel.ClaudeApiCreditDetail.Contains("2026-11-12", StringComparison.Ordinal), "ui-dates");
        Expect(!string.Join('\n', viewModel.RenderedStrings()).Contains(binding, StringComparison.Ordinal), "ui-no-binding");
        Expect(!string.Join('\n', viewModel.RenderedStrings()).Contains(Label, StringComparison.Ordinal), "ui-no-label");
        var host = (FrameworkElement)(window.FindName("SlotDetailHost")
            ?? throw new InvalidOperationException("detail_missing"));
        var png = Path.Combine(output, "claude-api-credit-row.png");
        SaveElementPng(host, png);
        viewModel.SelectSlot(ProviderSlotIds.CodexPrimary, true);
        Expect(!viewModel.ClaudeApiCreditVisible, "hidden-on-codex");
        viewModel.SetClaudeApiCredit(null);
        viewModel.SelectSlot(ProviderSlotIds.Cursor, true);
        Expect(!viewModel.ClaudeApiCreditVisible, "hidden-without-observation");
        window.Hide();
        window.DisposeResources();
        log.AppendLine("ui_money_row=pass png=" + png);
    }

    private static ProviderSlotSnapshot Placeholder(string slotId, string providerId) =>
        new(slotId, providerId, slotId, false, false, QuotaSlotStatus.Disabled, "未启用",
            DateTimeOffset.UnixEpoch, "", Array.Empty<QuotaWindowObservation>());

    private static string Binding(string label)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(label.Normalize(NormalizationForm.FormC)));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static byte[] Observation(string binding, string observed, string remaining = "99.20",
        string grant = "100.00", string grantedOn = "2026-10-15", string expiresOn = "2026-11-12") =>
        Encoding.UTF8.GetBytes(ObservationText(binding, observed, remaining, grant, grantedOn, expiresOn));

    private static string ObservationText(string binding, string observed, string remaining = "99.20",
        string grant = "100.00", string grantedOn = "2026-10-15", string expiresOn = "2026-11-12") =>
        "{\"schema\":1,\"kind\":\"observation\",\"source\":\"" + ClaudeApiCreditLimits.SourceUrl +
        "\",\"observed_at_utc\":\"" + observed + "\",\"organization_binding\":\"" + binding +
        "\",\"state\":\"ok\",\"currency\":\"USD\",\"remaining\":\"" + remaining + "\",\"grant_amount\":\"" + grant +
        "\",\"granted_on_utc\":\"" + grantedOn + "\",\"expires_on_utc\":\"" + expiresOn +
        "\",\"html\":\"<table>" + Html + "</table>\",\"label\":\"" + Label + "\",\"path\":\"C:\\\\Windows\\\\System32\\\\notepad.exe\"}";

    private static byte[] Status(string reason, string observed, string binding)
    {
        var bindingJson = binding.Length == 0 ? "" : ",\"organization_binding\":\"" + binding + "\"";
        return Encoding.UTF8.GetBytes("{\"schema\":1,\"kind\":\"status\",\"source\":\"" + ClaudeApiCreditLimits.SourceUrl +
            "\",\"observed_at_utc\":\"" + observed + "\",\"state\":\"unavailable\",\"reason\":\"" + reason + "\"" +
            bindingJson + "}");
    }

    private static string Run(string directory, byte[] json, DateTimeOffset now, out int code) =>
        RunRaw(directory, Frame(json), now, out code);

    private static byte[] Frame(byte[] json)
    {
        var frame = new byte[4 + json.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(frame, (uint)json.Length);
        json.CopyTo(frame, 4);
        return frame;
    }

    private static string RunRaw(string directory, byte[] frame, DateTimeOffset now, out int code)
    {
        using var input = new MemoryStream(frame);
        using var output = new MemoryStream();
        code = ClaudeApiCreditNativeHost.Run(input, output, directory, now);
        output.Position = 0;
        if (!ClaudeApiCreditFrame.TryRead(output, out var body, out _) || body is null) return "";
        return Encoding.UTF8.GetString(body);
    }

    private static string RunProcess(string exe, string directory, byte[] frame, TimeSpan timeout, out int code, string? firstArgument = null)
    {
        var start = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        if (!string.IsNullOrEmpty(firstArgument)) start.ArgumentList.Add(firstArgument);
        start.ArgumentList.Add("--data-dir");
        start.ArgumentList.Add(directory);
        using var process = new Process { StartInfo = start };
        process.Start();
        process.StandardInput.BaseStream.Write(frame);
        process.StandardInput.BaseStream.Flush();
        process.StandardInput.Close();
        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try { process.Kill(entireProcessTree: true); }
            catch (Exception) { }
            throw new InvalidOperationException("process_timeout");
        }

        using var memory = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(memory);
        code = process.ExitCode;
        memory.Position = 0;
        if (!ClaudeApiCreditFrame.TryRead(memory, out var body, out _) || body is null)
            return Encoding.UTF8.GetString(memory.ToArray());
        return Encoding.UTF8.GetString(body);
    }

    private static string ExtensionId(string key)
    {
        var hash = SHA256.HashData(Convert.FromBase64String(key));
        return new string(hash.Take(16).SelectMany(value => new[]
        {
            (char)('a' + (value >> 4)),
            (char)('a' + (value & 15)),
        }).ToArray());
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CodexUsageHud.sln"))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("repo_root");
    }

    private static void SaveElementPng(FrameworkElement element, string outputPath)
    {
        element.Measure(new Size(320, 900));
        element.Arrange(new Rect(0, 0, 320, Math.Max(element.DesiredSize.Height, 1)));
        element.UpdateLayout();
        var width = Math.Max(element.RenderSize.Width, element.DesiredSize.Width);
        var height = Math.Max(element.RenderSize.Height, element.DesiredSize.Height);
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(width));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(height));
        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var pixels = new byte[pixelWidth * pixelHeight * 4];
        bitmap.CopyPixels(pixels, pixelWidth * 4, 0);
        var ink = 0;
        for (var index = 3; index < pixels.Length; index += 4)
        {
            if (pixels[index] > 0) ink++;
        }

        Expect(ink > 500, "png-ink");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }

    private static void Expect(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
    }
}
