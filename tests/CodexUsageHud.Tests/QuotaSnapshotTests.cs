using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CodexUsageHud.App;
using CodexUsageHud.Core;

namespace CodexUsageHud.Tests;

internal static class QuotaSnapshotTests
{
    private const string Secret = "SNAPSHOT_SECRET_9C3E_label@example.test";

    public static int Run(string[] args)
    {
        if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.Error.WriteLine("usage: --quota-snapshot <output-directory>");
            return 2;
        }

        var output = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(output);
        var log = new StringBuilder();
        try
        {
            var root = Path.Combine(Path.GetTempPath(), "hud-quota-snapshot-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                SchemaPrivacyAndStates(root, output, log);
                TogglePendingAtomicAndFailure(root, log);
                EngineSettingRoundTrip(root, log);
                AssemblyVersion(log);
                RenderSettings(root, output, log);
            }
            finally
            {
                try { Directory.Delete(root, true); }
                catch (Exception ex) { log.AppendLine("cleanup=" + Scrub(ex.Message)); }
            }

            log.AppendLine("result=pass");
            File.WriteAllText(Path.Combine(output, "quota-snapshot-checks.txt"), log.ToString(), Encoding.UTF8);
            Console.WriteLine("quota-snapshot pass");
            return 0;
        }
        catch (Exception ex)
        {
            log.AppendLine("result=fail");
            log.AppendLine(Scrub(ex.ToString()));
            File.WriteAllText(Path.Combine(output, "quota-snapshot-checks.txt"), log.ToString(), Encoding.UTF8);
            Console.Error.WriteLine("quota-snapshot fail");
            return 1;
        }
    }

    private static void SchemaPrivacyAndStates(string root, string output, StringBuilder log)
    {
        var observed = DateTimeOffset.Parse("2026-10-04T11:58:00.0000000Z", CultureInfo.InvariantCulture);
        var retained = DateTimeOffset.Parse("2026-10-04T09:00:00.0000000Z", CultureInfo.InvariantCulture);
        var resetPrimary = DateTimeOffset.Parse("2026-10-04T16:58:00.0000000Z", CultureInfo.InvariantCulture);
        var resetWeekly = DateTimeOffset.Parse("2026-10-11T11:58:00.0000000Z", CultureInfo.InvariantCulture);
        var written = DateTimeOffset.Parse("2026-10-04T12:00:00.0000000Z", CultureInfo.InvariantCulture);
        var board = new ProviderQuotaBoard(new[]
        {
            Slot(ProviderSlotIds.CodexPrimary, ProviderIds.Codex, QuotaSlotStatus.Live, observed, new[]
            {
                Window("primary", 20, 300, resetPrimary),
                Window("weekly", 58, 10080, resetWeekly),
                Window("free-text-plan-" + Secret, 1, 60, resetPrimary),
            }),
            Slot(ProviderSlotIds.CodexSecondary, ProviderIds.Codex, QuotaSlotStatus.Stale, retained, new[]
            {
                Window("secondary_window", 10, 300, resetPrimary),
            }),
            Slot(ProviderSlotIds.Cursor, ProviderIds.Cursor, QuotaSlotStatus.Live, observed, new[]
            {
                Window("autoPercentUsed", 12, 43200, resetWeekly),
                Window("apiPercentUsed", null, null, null),
            }),
            Slot(ProviderSlotIds.Grok, ProviderIds.Grok, QuotaSlotStatus.Unavailable, observed, new[]
            {
                Window("weekly", 5, 10080, resetWeekly),
            }),
            Slot(ProviderSlotIds.GrokBot, ProviderIds.GrokBot, QuotaSlotStatus.Disabled, observed, new[]
            {
                Window("sand", 8, 10080, resetWeekly),
            }, enabled: false),
            Slot(ProviderSlotIds.Claude, ProviderIds.Claude, QuotaSlotStatus.Live, retained, new[]
            {
                Window("five_hour", 25, 300, resetPrimary),
                Window("seven_day", 40, 10080, resetWeekly),
                Window("seven_day_sonnet", 15, 10080, resetWeekly),
            }, delayed: true),
        }, written);

        var directory = Path.Combine(root, "schema");
        var writer = new QuotaSnapshotWriter(directory, "1.1.3", clock: () => written);
        writer.SetEnabled(true);
        writer.Publish(board);
        writer.WhenIdleAsync().GetAwaiter().GetResult();
        var json = File.ReadAllText(writer.FinalPath, Encoding.UTF8);
        File.WriteAllText(Path.Combine(output, "SYNTHETIC_EXAMPLE.json"), json, new UTF8Encoding(false));
        using var document = JsonDocument.Parse(json);
        var rootElement = document.RootElement;
        Expect(rootElement.GetProperty("schema").GetInt32() == 1, "schema");
        Expect(rootElement.GetProperty("written_at_utc").GetString() == QuotaSnapshotJson.FormatUtc(written), "written");
        Expect(rootElement.GetProperty("hud_version").GetString() == "1.1.3", "version");
        var slots = rootElement.GetProperty("slots").EnumerateArray().ToArray();
        Expect(slots.Length == 6, "six");
        string[] ids = { "codex_primary", "codex_secondary", "cursor", "grok", "grok_bot", "claude" };
        string[] labels = { "Codex主", "Codex备", "Cursor", "Grok", "Bot", "Claude" };
        string[] states = { "ok", "stale", "ok", "unavailable", "disabled", "stale" };
        for (var index = 0; index < 6; index++)
        {
            Expect(slots[index].GetProperty("slot").GetString() == ids[index], "slot-id");
            Expect(slots[index].GetProperty("label").GetString() == labels[index], "label");
            Expect(slots[index].GetProperty("state").GetString() == states[index], "state-" + ids[index]);
            AssertAllowlisted(slots[index], true);
        }

        var primaryWindows = slots[0].GetProperty("windows").EnumerateArray().ToArray();
        Expect(primaryWindows.Length == 2, "codex-windows");
        Expect(primaryWindows[0].GetProperty("pool").GetString() == "codex_primary_window", "pool-primary");
        Expect(primaryWindows[0].GetProperty("window_minutes").GetInt32() == 300, "minutes");
        Expect(primaryWindows[0].GetProperty("remaining_percent").GetDouble() == 80d, "remaining");
        Expect(primaryWindows[0].GetProperty("observed_at_utc").GetString() == QuotaSnapshotJson.FormatUtc(observed), "observed");
        Expect(primaryWindows[1].GetProperty("pool").GetString() == "codex_weekly", "pool-weekly");
        Expect(primaryWindows[1].GetProperty("remaining_percent").GetDouble() == 42d, "weekly-remaining");
        Expect(slots[1].GetProperty("windows")[0].GetProperty("pool").GetString() == "codex_secondary_window", "secondary-pool");
        Expect(slots[1].GetProperty("windows")[0].GetProperty("observed_at_utc").GetString() ==
               QuotaSnapshotJson.FormatUtc(retained), "retained-observed");
        var cursorWindows = slots[2].GetProperty("windows").EnumerateArray().ToArray();
        Expect(cursorWindows.Length == 2, "cursor-windows");
        Expect(cursorWindows[0].GetProperty("pool").GetString() == "cursor_auto", "cursor-auto");
        Expect(cursorWindows[1].GetProperty("pool").GetString() == "cursor_api", "cursor-api");
        Expect(cursorWindows[1].GetProperty("window_minutes").ValueKind == JsonValueKind.Null, "null-minutes");
        Expect(cursorWindows[1].GetProperty("remaining_percent").ValueKind == JsonValueKind.Null, "null-percent");
        Expect(cursorWindows[1].GetProperty("resets_at_utc").ValueKind == JsonValueKind.Null, "null-reset");
        Expect(slots[3].GetProperty("windows").GetArrayLength() == 0, "failed-slot-empty");
        Expect(slots[4].GetProperty("windows").GetArrayLength() == 0, "disabled-empty");
        var claudeWindows = slots[5].GetProperty("windows").EnumerateArray().Select(item => item.GetProperty("pool").GetString()).ToArray();
        Expect(claudeWindows.SequenceEqual(new[] { "claude_five_hour", "claude_seven_day", "claude_seven_day_sonnet" }), "claude-pools");
        Expect(!json.Contains(Secret, StringComparison.Ordinal), "secret");
        Expect(!json.Contains("AccountKey", StringComparison.Ordinal), "account-key");
        Expect(!json.Contains("OpaqueIdentity", StringComparison.Ordinal), "hash");
        Expect(!json.Contains(directory, StringComparison.OrdinalIgnoreCase), "directory");
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(profile))
            Expect(!json.Contains(profile, StringComparison.OrdinalIgnoreCase), "profile");
        log.AppendLine("schema_privacy_states=pass");
    }

    private static void TogglePendingAtomicAndFailure(string root, StringBuilder log)
    {
        var directory = Path.Combine(root, "writer");
        var off = new QuotaSnapshotWriter(directory, "1.1.3");
        off.Publish(OneBoard(10));
        off.WhenIdleAsync().GetAwaiter().GetResult();
        Expect(!File.Exists(off.FinalPath), "default-off");

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commits = 0;
        var coalesced = new QuotaSnapshotWriter(directory, "1.1.3", () =>
        {
            var count = Interlocked.Increment(ref commits);
            if (count == 1)
            {
                entered.TrySetResult();
                release.Task.GetAwaiter().GetResult();
            }
        });
        coalesced.SetEnabled(true);
        coalesced.Publish(OneBoard(1));
        Expect(entered.Task.Wait(TimeSpan.FromSeconds(5)), "commit-entered");
        for (var index = 2; index <= 20; index++)
            coalesced.Publish(OneBoard(index));
        release.TrySetResult();
        coalesced.WhenIdleAsync().GetAwaiter().GetResult();
        Expect(commits is >= 1 and <= 3, "coalesced-commits");
        using (var saved = JsonDocument.Parse(File.ReadAllText(coalesced.FinalPath)))
        {
            var remaining = saved.RootElement.GetProperty("slots")[0].GetProperty("windows")[0]
                .GetProperty("remaining_percent").GetDouble();
            Expect(remaining == 80d, "last-board");
        }

        var blockedEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockedRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockedDir = Path.Combine(root, "blocked");
        var prior = File.ReadAllBytes(coalesced.FinalPath);
        var blocked = new QuotaSnapshotWriter(blockedDir, "1.1.3", () =>
        {
            blockedEntered.TrySetResult();
            blockedRelease.Task.GetAwaiter().GetResult();
        });
        blocked.SetEnabled(true);
        blocked.Publish(OneBoard(3));
        Expect(blockedEntered.Task.Wait(TimeSpan.FromSeconds(5)), "blocked-entered");
        blocked.SetEnabled(false);
        blocked.Publish(OneBoard(4));
        blockedRelease.TrySetResult();
        blocked.WhenIdleAsync().GetAwaiter().GetResult();
        Expect(!File.Exists(blocked.FinalPath), "off-drops-pending");
        Expect(File.ReadAllBytes(coalesced.FinalPath).AsSpan().SequenceEqual(prior), "old-file-kept");

        var readersStopped = 0;
        var readerAttempts = 0;
        var goodReads = 0;
        var badReads = 0;
        var readDirectory = Path.Combine(root, "atomic");
        var atomic = new QuotaSnapshotWriter(readDirectory, "1.1.3");
        atomic.SetEnabled(true);
        var reader = Task.Run(() =>
        {
            var path = atomic.FinalPath;
            while (Volatile.Read(ref readersStopped) == 0)
            {
                Interlocked.Increment(ref readerAttempts);
                try
                {
                    byte[] bytes;
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using var memory = new MemoryStream();
                        stream.CopyTo(memory);
                        bytes = memory.ToArray();
                    }

                    if (bytes.Length == 0)
                    {
                        Interlocked.Increment(ref badReads);
                        continue;
                    }

                    using var parsed = JsonDocument.Parse(bytes);
                    if (parsed.RootElement.GetProperty("schema").GetInt32() == 1 &&
                        parsed.RootElement.GetProperty("slots").GetArrayLength() == 6)
                        Interlocked.Increment(ref goodReads);
                    else
                        Interlocked.Increment(ref badReads);
                }
                catch (FileNotFoundException)
                {
                }
                catch (IOException)
                {
                }
                catch (JsonException)
                {
                    Interlocked.Increment(ref badReads);
                }
            }
        });
        Expect(SpinWait.SpinUntil(() => Volatile.Read(ref readerAttempts) > 0, TimeSpan.FromSeconds(2)), "reader-started");
        for (var index = 0; index < 25; index++)
            atomic.Publish(OneBoard(index));
        atomic.WhenIdleAsync().GetAwaiter().GetResult();
        Expect(SpinWait.SpinUntil(() => Volatile.Read(ref goodReads) > 0, TimeSpan.FromSeconds(2)), "atomic-saw-file");
        Volatile.Write(ref readersStopped, 1);
        reader.Wait(TimeSpan.FromSeconds(5));
        Expect(badReads == 0, "atomic-reader");
        Expect(goodReads > 0, "atomic-saw-file");

        var blocker = Path.Combine(root, "not-a-directory");
        File.WriteAllText(blocker, "blocked");
        var failing = new QuotaSnapshotWriter(blocker, "1.1.3");
        failing.SetEnabled(true);
        failing.Publish(OneBoard(9));
        failing.WhenIdleAsync().GetAwaiter().GetResult();
        Expect(!File.Exists(Path.Combine(blocker, QuotaSnapshotWriter.FileName)), "io-contained");
        log.AppendLine("toggle_atomic_failure=pass commits=" + commits.ToString(CultureInfo.InvariantCulture) +
                       " good_reads=" + goodReads.ToString(CultureInfo.InvariantCulture));
    }

    private static void EngineSettingRoundTrip(string root, StringBuilder log)
    {
        var directory = Path.Combine(root, "engine");
        Directory.CreateDirectory(Path.Combine(directory, "sessions"));
        var database = Path.Combine(directory, "usage.db");
        using (var engine = new UsageEngine(directory, database, Path.Combine(directory, "hud.log"),
                   productVersion: "1.1.3"))
        {
            var missing = engine.LoadSettingsAsync(QuotaSnapshotWriter.SettingKey).GetAwaiter().GetResult();
            Expect(!QuotaSnapshotWriter.IsEnabledValue(missing.GetValueOrDefault(QuotaSnapshotWriter.SettingKey)), "missing-off");
            engine.ExportCurrentQuotaSnapshot();
            engine.FlushQuotaSnapshotAsync().GetAwaiter().GetResult();
            var snapshot = Path.Combine(directory, QuotaSnapshotWriter.FileName);
            Expect(!File.Exists(snapshot), "engine-default-no-file");
            engine.SaveSettingsAsync(new Dictionary<string, string>
            {
                [QuotaSnapshotWriter.SettingKey] = "1",
            }).GetAwaiter().GetResult();
            engine.SetQuotaSnapshotExportEnabled(true);
            engine.ExportCurrentQuotaSnapshot();
            engine.FlushQuotaSnapshotAsync().GetAwaiter().GetResult();
            Expect(File.Exists(snapshot), "engine-writes-beside-db");
            var json = File.ReadAllText(snapshot, Encoding.UTF8);
            using var document = JsonDocument.Parse(json);
            Expect(document.RootElement.GetProperty("hud_version").GetString() == "1.1.3", "engine-version");
            Expect(document.RootElement.GetProperty("slots").GetArrayLength() == 6, "engine-six");
            Expect(!json.Contains(directory, StringComparison.OrdinalIgnoreCase), "engine-path");
            var before = File.ReadAllBytes(snapshot);
            engine.SetQuotaSnapshotExportEnabled(false);
            engine.ExportCurrentQuotaSnapshot();
            engine.FlushQuotaSnapshotAsync().GetAwaiter().GetResult();
            Expect(File.ReadAllBytes(snapshot).AsSpan().SequenceEqual(before), "engine-off-keeps");
            engine.SaveSettingsAsync(new Dictionary<string, string>
            {
                [QuotaSnapshotWriter.SettingKey] = "0",
            }).GetAwaiter().GetResult();
        }

        using (var restarted = new UsageEngine(directory, database, Path.Combine(directory, "hud.log"),
                   productVersion: "1.1.3"))
        {
            var savedOff = restarted.LoadSettingsAsync(QuotaSnapshotWriter.SettingKey).GetAwaiter().GetResult();
            Expect(savedOff[QuotaSnapshotWriter.SettingKey] == "0", "persisted-off");
            restarted.SaveSettingsAsync(new Dictionary<string, string>
            {
                [QuotaSnapshotWriter.SettingKey] = "1",
            }).GetAwaiter().GetResult();
        }

        using (var enabledAgain = new UsageEngine(directory, database, Path.Combine(directory, "hud.log"),
                   productVersion: "1.1.3"))
        {
            var savedOn = enabledAgain.LoadSettingsAsync(QuotaSnapshotWriter.SettingKey).GetAwaiter().GetResult();
            Expect(savedOn[QuotaSnapshotWriter.SettingKey] == "1", "persisted-on");
        }
        Expect(!QuotaSnapshotWriter.IsEnabledValue("true"), "only-one-enables");
        log.AppendLine("engine_setting=pass");
    }

    private static void AssemblyVersion(StringBuilder log)
    {
        var version = typeof(MainWindow).Assembly.GetName().Version;
        Expect(version is not null && version.Major == 1 && version.Minor == 1 && version.Build == 3, "assembly");
        Expect(HudReleaseVersion.FromAssembly(typeof(MainWindow).Assembly) == "1.1.3", "product-text");
        log.AppendLine("assembly_version=1.1.3");
    }

    private static void RenderSettings(string root, string output, StringBuilder log)
    {
        if (Application.Current is null)
        {
            var application = new CodexUsageHud.App.App();
            application.InitializeComponent();
        }

        var directory = Path.Combine(root, "ui");
        Directory.CreateDirectory(Path.Combine(directory, "sessions"));
        using var engine = new UsageEngine(directory, Path.Combine(directory, "usage.db"),
            Path.Combine(directory, "hud.log"), productVersion: "1.1.3");
        using var window = new MainWindow(engine, () => Task.CompletedTask, false)
        {
            ShowActivated = false,
            ShowInTaskbar = false,
            Opacity = 0,
            Left = -12000,
            Top = -12000,
        };
        window.OverrideWorkAreaForTests(new Rect(-12000, -12000, 1600, 1000));
        var trayFieldEarly = typeof(MainWindow).GetField("_tray",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (trayFieldEarly?.GetValue(window) is System.Windows.Forms.NotifyIcon earlyTray)
            earlyTray.Visible = false;
        window.Show();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Invoke(window, "OnSettings", window, new RoutedEventArgs());
        window.UpdateLayout();
        var checkbox = (System.Windows.Controls.CheckBox)(window.FindName("QuotaSnapshotCheckBox")
            ?? throw new InvalidOperationException("checkbox_missing"));
        var block = (FrameworkElement)(window.FindName("QuotaSnapshotSettingBlock")
            ?? throw new InvalidOperationException("block_missing"));
        Expect(checkbox.IsChecked != true, "ui-default-off");
        var readyField = typeof(MainWindow).GetField("_settingsReady",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Expect(readyField?.GetValue(window) is true, "settings-ready");
        checkbox.IsChecked = true;
        Invoke(window, "OnQuotaSnapshotSettingClick", checkbox, new RoutedEventArgs());
        WaitForSetting(engine, "1", TimeSpan.FromSeconds(5));
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        engine.FlushQuotaSnapshotAsync().GetAwaiter().GetResult();
        var snapshot = Path.Combine(directory, QuotaSnapshotWriter.FileName);
        Expect(File.Exists(snapshot), "ui-on-writes");
        window.UpdateLayout();
        SaveElementPng(block, Path.Combine(output, "settings-quota-snapshot.png"));
        var onBytes = File.ReadAllBytes(snapshot);
        checkbox.IsChecked = false;
        Invoke(window, "OnQuotaSnapshotSettingClick", checkbox, new RoutedEventArgs());
        WaitForSetting(engine, "0", TimeSpan.FromSeconds(5));
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        engine.ExportCurrentQuotaSnapshot();
        engine.FlushQuotaSnapshotAsync().GetAwaiter().GetResult();
        Expect(File.ReadAllBytes(snapshot).AsSpan().SequenceEqual(onBytes), "ui-off-no-new-write");
        Expect(checkbox.IsChecked != true, "ui-ends-off");
        var trayField = typeof(MainWindow).GetField("_tray",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (trayField?.GetValue(window) is System.Windows.Forms.NotifyIcon tray)
        {
            tray.Visible = false;
            tray.Dispose();
        }

        window.Hide();
        window.DisposeResources();
        log.AppendLine("settings_ui=rendered default_off=true persisted_on_then_off=true");
    }

    private static ProviderQuotaBoard OneBoard(int usedPercent) => new(new[]
    {
        Slot(ProviderSlotIds.CodexPrimary, ProviderIds.Codex, QuotaSlotStatus.Live, DateTimeOffset.Parse("2026-10-04T11:00:00Z", CultureInfo.InvariantCulture),
            new[] { Window("primary", usedPercent, 300, DateTimeOffset.Parse("2026-10-04T16:00:00Z", CultureInfo.InvariantCulture)) }),
        Slot(ProviderSlotIds.CodexSecondary, ProviderIds.Codex, QuotaSlotStatus.Disabled, DateTimeOffset.UnixEpoch, Array.Empty<QuotaWindowObservation>(), false),
        Slot(ProviderSlotIds.Cursor, ProviderIds.Cursor, QuotaSlotStatus.Disabled, DateTimeOffset.UnixEpoch, Array.Empty<QuotaWindowObservation>(), false),
        Slot(ProviderSlotIds.Grok, ProviderIds.Grok, QuotaSlotStatus.Disabled, DateTimeOffset.UnixEpoch, Array.Empty<QuotaWindowObservation>(), false),
        Slot(ProviderSlotIds.GrokBot, ProviderIds.GrokBot, QuotaSlotStatus.Disabled, DateTimeOffset.UnixEpoch, Array.Empty<QuotaWindowObservation>(), false),
        Slot(ProviderSlotIds.Claude, ProviderIds.Claude, QuotaSlotStatus.Disabled, DateTimeOffset.UnixEpoch, Array.Empty<QuotaWindowObservation>(), false),
    }, DateTimeOffset.Parse("2026-10-04T12:00:00Z", CultureInfo.InvariantCulture));

    private static ProviderSlotSnapshot Slot(string slotId, string providerId, QuotaSlotStatus status,
        DateTimeOffset observed, IReadOnlyList<QuotaWindowObservation> windows, bool enabled = true, bool delayed = false) =>
        new(slotId, providerId, Secret, enabled, false, status, Secret, observed, Secret, windows,
            OpaqueIdentityHash: Secret, ErrorCode: Secret, AccountKey: Secret, ConfigFingerprint: Secret,
            FieldPresenceFlags: Secret, UpdateDelayed: delayed, DelayText: Secret);

    private static QuotaWindowObservation Window(string id, double? used, int? minutes, DateTimeOffset? reset) =>
        new(id, Secret, used, reset, minutes, used.HasValue, Secret);

    private static void AssertAllowlisted(JsonElement slot, bool windowLevel)
    {
        foreach (var property in slot.EnumerateObject())
        {
            if (property.Name is not ("slot" or "label" or "state" or "windows"))
                throw new InvalidOperationException("unexpected_slot_field");
        }

        if (!windowLevel) return;
        foreach (var window in slot.GetProperty("windows").EnumerateArray())
        {
            foreach (var property in window.EnumerateObject())
            {
                if (property.Name is not ("pool" or "window_minutes" or "remaining_percent" or "resets_at_utc" or "observed_at_utc"))
                    throw new InvalidOperationException("unexpected_window_field");
            }
        }
    }

    private static void SaveElementPng(FrameworkElement element, string outputPath)
    {
        element.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        var width = element.ActualWidth;
        var height = element.ActualHeight;
        if (width < 1 || height < 1)
        {
            element.Measure(new Size(358, double.PositiveInfinity));
            element.Arrange(new Rect(0, 0, 358, element.DesiredSize.Height));
            element.UpdateLayout();
            width = Math.Max(element.ActualWidth, element.DesiredSize.Width);
            height = Math.Max(element.ActualHeight, element.DesiredSize.Height);
        }

        var pixelWidth = Math.Max(1, (int)Math.Ceiling(width));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(height));
        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }

    private static void WaitForSetting(UsageEngine engine, string expected, TimeSpan timeout)
    {
        Task<IReadOnlyDictionary<string, string?>>? pending = null;
        var observed = "<none>";
        PumpUntil(() =>
        {
            pending ??= engine.LoadSettingsAsync(QuotaSnapshotWriter.SettingKey);
            if (!pending.IsCompleted) return false;
            observed = pending.GetAwaiter().GetResult().GetValueOrDefault(QuotaSnapshotWriter.SettingKey) ?? "<null>";
            if (observed == expected) return true;
            pending = engine.LoadSettingsAsync(QuotaSnapshotWriter.SettingKey);
            return false;
        }, timeout, () => "observed=" + observed);
    }

    private static void PumpUntil(Func<bool> done, TimeSpan timeout, Func<string>? detail)
    {
        if (done()) return;
        var frame = new DispatcherFrame();
        var started = DateTime.UtcNow;
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(20),
        };
        timer.Tick += (_, _) =>
        {
            if (done() || DateTime.UtcNow - started > timeout)
            {
                timer.Stop();
                frame.Continue = false;
            }
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        if (!done()) throw new TimeoutException("ui_pump_timeout " + (detail?.Invoke() ?? string.Empty));
    }

    private static void Invoke(MainWindow window, string name, params object?[] arguments)
    {
        var method = typeof(MainWindow).GetMethod(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("method_missing:" + name);
        method.Invoke(window, arguments);
    }

    private static void Expect(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("assertion_failed:" + name);
    }

    private static string Scrub(string text)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(profile))
            text = text.Replace(profile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        return text;
    }
}
