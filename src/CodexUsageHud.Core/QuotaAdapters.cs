using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace CodexUsageHud.Core;

public static class AppServerProtocol
{
    public const string InitializeMethod = "initialize";
    public const string RateLimitsMethod = "account/rateLimits/read";
    public const string ModelCatalogMethod = "model/list";
    public const string ReadOnlyFlag = "-s read-only -a untrusted app-server";

    public static ProcessStartInfo CreateStartInfo(string executable)
    {
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        if (Path.GetExtension(executable).Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(executable).Equals(".bat", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            startInfo.Arguments = $"/d /s /c \"\"{executable}\" -s read-only -a untrusted app-server\"";
        }
        else
        {
            startInfo.FileName = executable;
            startInfo.ArgumentList.Add("-s");
            startInfo.ArgumentList.Add("read-only");
            startInfo.ArgumentList.Add("-a");
            startInfo.ArgumentList.Add("untrusted");
            startInfo.ArgumentList.Add("app-server");
        }
        return startInfo;
    }
}

public sealed class CodexExecutableDiscovery
{
    public string? Find(string? configuredOverride = null, string? pathValue = null,
        string? appData = null, string? localAppData = null, string? programFiles = null)
    {
        configuredOverride ??= Environment.GetEnvironmentVariable("CODEX_EXE");
        pathValue ??= Environment.GetEnvironmentVariable("PATH");
        appData ??= Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        localAppData ??= Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        programFiles ??= Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        var candidates = new List<string?>
        {
            configuredOverride,
            Environment.GetEnvironmentVariable("CODEX_DESKTOP_EXE"),
            string.IsNullOrWhiteSpace(appData) ? null : Path.Combine(appData, "npm", "codex.cmd"),
            string.IsNullOrWhiteSpace(appData) ? null : Path.Combine(appData, "npm", "codex.exe"),
            string.IsNullOrWhiteSpace(localAppData) ? null : Path.Combine(localAppData, "Programs", "Codex", "codex.exe"),
            string.IsNullOrWhiteSpace(localAppData) ? null : Path.Combine(localAppData, "Programs", "Codex", "resources", "codex.exe"),
            string.IsNullOrWhiteSpace(programFiles) ? null : Path.Combine(programFiles, "Codex", "codex.exe"),
            string.IsNullOrWhiteSpace(programFiles) ? null : Path.Combine(programFiles, "Codex", "resources", "codex.exe"),
        };

        if (!string.IsNullOrWhiteSpace(pathValue))
        {
            foreach (var directory in pathValue.Split(Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                candidates.Add(Path.Combine(directory.Trim('"'), OperatingSystem.IsWindows() ? "codex.exe" : "codex"));
                if (OperatingSystem.IsWindows())
                {
                    candidates.Add(Path.Combine(directory.Trim('"'), "codex.cmd"));
                    candidates.Add(Path.Combine(directory.Trim('"'), "codex.bat"));
                }
            }
        }

        foreach (var candidate in candidates)
        {
            if (TryResolveExecutable(candidate, out var resolved))
            {
                return resolved;
            }
        }

        return null;
    }

    private static bool TryResolveExecutable(string? candidate, out string? resolved)
    {
        resolved = null;
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        try
        {
            var full = Path.GetFullPath(candidate.Trim().Trim('"'));
            var extension = Path.GetExtension(full);
            if (!File.Exists(full) || !(extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                                       extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
                                       extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
                                       string.IsNullOrEmpty(extension)))
            {
                return false;
            }

            resolved = full;
            return true;
        }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
        catch (PathTooLongException) { return false; }
    }
}

public readonly record struct BoundedLineReadResult(string? Line, bool IsTooLong, bool IsEof);

public sealed class BoundedLineReader : IDisposable
{
    private const int ReadBufferSize = 4096;
    private readonly TextReader _reader;
    private readonly int _maxChars;
    private char[]? _readBuffer;
    private int _readOffset;
    private int _readCount;

    public BoundedLineReader(TextReader reader, int maxChars = PrivacyJsonlReader.AllowlistedRecordLimit)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (maxChars <= 0) throw new ArgumentOutOfRangeException(nameof(maxChars));
        _reader = reader;
        _maxChars = maxChars;
        _readBuffer = ArrayPool<char>.Shared.Rent(ReadBufferSize);
    }

    public int MaxRetainedCharsObserved { get; private set; }

    public async ValueTask<BoundedLineReadResult> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        if (_readBuffer is null) throw new ObjectDisposedException(nameof(BoundedLineReader));
        var lineBuffer = ArrayPool<char>.Shared.Rent(Math.Min(ReadBufferSize, _maxChars));
        var retained = 0;
        var sawData = false;
        var tooLong = false;
        try
        {
            while (true)
            {
                if (_readOffset >= _readCount)
                {
                    _readCount = await _reader.ReadAsync(_readBuffer.AsMemory(0, _readBuffer.Length), cancellationToken);
                    _readOffset = 0;
                    if (_readCount == 0)
                    {
                        if (!sawData) return new BoundedLineReadResult(null, false, true);
                        if (tooLong) return new BoundedLineReadResult(null, true, false);
                        if (retained > 0 && lineBuffer[retained - 1] == '\r') retained--;
                        return new BoundedLineReadResult(new string(lineBuffer, 0, retained), false, false);
                    }
                }

                var newline = Array.IndexOf(_readBuffer, '\n', _readOffset, _readCount - _readOffset);
                var segmentEnd = newline >= 0 ? newline : _readCount;
                var segmentLength = segmentEnd - _readOffset;
                sawData |= segmentLength > 0 || newline >= 0;

                if (!tooLong && segmentLength > 0)
                {
                    var available = _maxChars - retained;
                    var copyLength = Math.Min(segmentLength, Math.Max(0, available));
                    if (copyLength > 0)
                    {
                        lineBuffer = EnsureCapacity(lineBuffer, retained, retained + copyLength, _maxChars);
                        _readBuffer.AsSpan(_readOffset, copyLength).CopyTo(lineBuffer.AsSpan(retained));
                        retained += copyLength;
                        MaxRetainedCharsObserved = Math.Max(MaxRetainedCharsObserved, retained);
                    }
                    if (copyLength != segmentLength) tooLong = true;
                }

                _readOffset = newline >= 0 ? newline + 1 : segmentEnd;
                if (newline < 0) continue;
                if (tooLong) return new BoundedLineReadResult(null, true, false);
                if (retained > 0 && lineBuffer[retained - 1] == '\r') retained--;
                return new BoundedLineReadResult(new string(lineBuffer, 0, retained), false, false);
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(lineBuffer, clearArray: true);
        }
    }

    public void Dispose()
    {
        var buffer = Interlocked.Exchange(ref _readBuffer, null);
        if (buffer is not null) ArrayPool<char>.Shared.Return(buffer, clearArray: true);
    }

    private static char[] EnsureCapacity(char[] current, int length, int required, int maximum)
    {
        if (current.Length >= required) return current;
        var requested = Math.Min(maximum, Math.Max(required, current.Length * 2));
        var replacement = ArrayPool<char>.Shared.Rent(requested);
        current.AsSpan(0, length).CopyTo(replacement);
        ArrayPool<char>.Shared.Return(current, clearArray: true);
        return replacement;
    }
}

public sealed class AppServerClient
{
    private readonly string _hudVersion;
    private readonly TimeSpan _startupTimeout;
    private readonly TimeSpan _requestTimeout;
    private readonly TimeSpan _cleanupTimeout;

    public AppServerClient(string hudVersion = "1.0.0", TimeSpan? startupTimeout = null,
        TimeSpan? requestTimeout = null, TimeSpan? cleanupTimeout = null)
    {
        _hudVersion = hudVersion;
        _startupTimeout = PositiveOrDefault(startupTimeout, TimeSpan.FromSeconds(10));
        _requestTimeout = PositiveOrDefault(requestTimeout, TimeSpan.FromSeconds(5));
        _cleanupTimeout = PositiveOrDefault(cleanupTimeout, TimeSpan.FromSeconds(2));
    }

    public async Task<AppServerReadResult> ReadRateLimitsAsync(string executable, CancellationToken cancellationToken = default)
    {
        var observedAt = DateTimeOffset.UtcNow;
        using var process = new Process { StartInfo = AppServerProtocol.CreateStartInfo(executable), EnableRaisingEvents = true };
        BoundedLineReader? stdout = null;
        BoundedLineReader? stderr = null;
        CancellationTokenSource? drainCancellation = null;
        Task? stderrDrain = null;
        try
        {
            if (!process.Start())
            {
                return Unavailable("app_server_start");
            }

            stdout = new BoundedLineReader(process.StandardOutput);
            stderr = new BoundedLineReader(process.StandardError);
            drainCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            stderrDrain = DrainAsync(stderr, drainCancellation.Token);
            using var startupTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            startupTimeout.CancelAfter(_startupTimeout);
            await WriteRequestAsync(process, 1, AppServerProtocol.InitializeMethod,
                new { clientInfo = new { name = "codex-usage-hud", version = _hudVersion } }, startupTimeout.Token);
            var initialize = await ReadResponseAsync(stdout, 1, startupTimeout.Token);
            if (initialize is null)
            {
                return Unavailable("app_server_initialize_timeout");
            }

            using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestTimeout.CancelAfter(_requestTimeout);
            await WriteRequestAsync(process, 2, AppServerProtocol.RateLimitsMethod, new { }, requestTimeout.Token);
            var response = await ReadResponseAsync(stdout, 2, requestTimeout.Token);
            if (response is null)
            {
                return Unavailable("app_server_rate_limits_timeout");
            }

            var observation = QuotaJsonParser.Parse(response, observedAt);
            return new AppServerReadResult(observation, AppServerProtocol.ReadOnlyFlag, false, observation.ErrorCode);
        }
        catch (OperationCanceledException)
        {
            return Unavailable(cancellationToken.IsCancellationRequested ? "app_server_cancelled" : "app_server_timeout");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return Unavailable("app_server_launch");
        }
        catch (IOException)
        {
            return Unavailable("app_server_io");
        }
        finally
        {
            await CleanupProcessAsync(process, drainCancellation, stderrDrain, stdout, stderr, _cleanupTimeout);
        }
    }

    public async Task<IReadOnlyDictionary<string, string?>?> ReadModelCatalogAsync(
        string executable, CancellationToken cancellationToken = default)
    {
        using var process = new Process { StartInfo = AppServerProtocol.CreateStartInfo(executable) };
        BoundedLineReader? stdout = null;
        BoundedLineReader? stderr = null;
        CancellationTokenSource? drainCancellation = null;
        Task? stderrDrain = null;
        try
        {
            if (!process.Start())
            {
                return null;
            }

            stdout = new BoundedLineReader(process.StandardOutput);
            stderr = new BoundedLineReader(process.StandardError);
            drainCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            stderrDrain = DrainAsync(stderr, drainCancellation.Token);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_requestTimeout);
            await WriteRequestAsync(process, 1, AppServerProtocol.InitializeMethod,
                new { clientInfo = new { name = "codex-usage-hud", version = _hudVersion } }, timeout.Token);
            if (await ReadResponseAsync(stdout, 1, timeout.Token) is null)
            {
                return null;
            }

            await WriteRequestAsync(process, 2, AppServerProtocol.ModelCatalogMethod, new { }, timeout.Token);
            var response = await ReadResponseAsync(stdout, 2, timeout.Token);
            return response is null ? null : ModelCatalogParser.ParseDefaults(response);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        finally
        {
            await CleanupProcessAsync(process, drainCancellation, stderrDrain, stdout, stderr, _cleanupTimeout);
        }
    }

    private static async Task WriteRequestAsync(Process process, int id, string method, object parameters,
        CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters });
        await process.StandardInput.WriteLineAsync(request.AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync(cancellationToken);
    }

    private static async Task<string?> ReadResponseAsync(BoundedLineReader reader, int expectedId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var result = await reader.ReadLineAsync(cancellationToken);
            if (result.IsEof) return null;
            if (result.IsTooLong) continue;
            var line = result.Line!;

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number &&
                    id.TryGetInt32(out var actualId) && actualId == expectedId)
                {
                    return line;
                }
            }
            catch (JsonException)
            {
                // Notifications and malformed responses are ignored without retaining their text.
            }
        }
    }

    private static async Task DrainAsync(BoundedLineReader reader, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var result = await reader.ReadLineAsync(cancellationToken);
                if (result.IsEof) return;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
    }

    private static async Task CleanupProcessAsync(Process process, CancellationTokenSource? drainCancellation,
        Task? stderrDrain, BoundedLineReader? stdout, BoundedLineReader? stderr, TimeSpan cleanupTimeout)
    {
        drainCancellation?.Cancel();
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit((int)Math.Clamp(cleanupTimeout.TotalMilliseconds, 1, int.MaxValue));
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }

        if (stderrDrain is not null)
        {
            try
            {
                await stderrDrain.WaitAsync(cleanupTimeout);
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException)
            {
            }
            catch (IOException)
            {
            }
        }
        stdout?.Dispose();
        stderr?.Dispose();
        drainCancellation?.Dispose();
    }

    private static TimeSpan PositiveOrDefault(TimeSpan? value, TimeSpan fallback) =>
        value.HasValue && value.Value > TimeSpan.Zero ? value.Value : fallback;

    private static AppServerReadResult Unavailable(string code) => new(
        new QuotaObservation(null, Array.Empty<QuotaBucket>(), QuotaSource.Unavailable,
            DateTimeOffset.UtcNow, false, code), AppServerProtocol.ReadOnlyFlag, false, code);
}

public static class ModelCatalogParser
{
    public static IReadOnlyDictionary<string, string?>? ParseDefaults(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            if (!document.RootElement.TryGetProperty("result", out var result) ||
                result.ValueKind != JsonValueKind.Object ||
                !result.TryGetProperty("data", out var data))
            {
                return new Dictionary<string, string?>(StringComparer.Ordinal);
            }

            var defaults = new Dictionary<string, string?>(StringComparer.Ordinal);
            if (data.ValueKind == JsonValueKind.Array)
            {
                foreach (var model in data.EnumerateArray())
                {
                    AddModel(model, null, defaults);
                }
            }
            else if (data.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in data.EnumerateObject())
                {
                    AddModel(property.Value, property.Name, defaults);
                }
            }

            return defaults;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void AddModel(JsonElement element, string? key,
        IDictionary<string, string?> defaults)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var id = GetString(element, "id") ?? GetString(element, "model") ?? key;
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        if (!element.TryGetProperty("defaultServiceTier", out var tier) &&
            !element.TryGetProperty("default_service_tier", out tier))
        {
            return;
        }

        defaults[id] = tier.ValueKind == JsonValueKind.Null
            ? null
            : tier.ValueKind == JsonValueKind.String ? tier.GetString() : "unavailable";
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

public static class QuotaJsonParser
{
    public static QuotaObservation Parse(string json, DateTimeOffset observedAtUtc)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            var buckets = new List<QuotaBucket>();
            var invalidWindowDetected = false;
            CollectBuckets(document.RootElement, buckets, null, ref invalidWindowDetected);
            if (invalidWindowDetected)
            {
                return new QuotaObservation(null, Array.Empty<QuotaBucket>(), QuotaSource.Unavailable,
                    observedAtUtc, false, "quota_window_invalid");
            }
            var distinct = buckets.GroupBy(bucket => bucket.Id, StringComparer.Ordinal)
                .Select(group => group.First()).OrderBy(bucket => bucket.Id, StringComparer.Ordinal).ToArray();
            var primary = SelectPrimary(distinct, out var errorCode);
            return new QuotaObservation(primary, distinct.Where(bucket => primary is null || bucket.Id != primary.Id).ToArray(),
                primary is null ? QuotaSource.Unavailable : QuotaSource.OfficialAppServer, observedAtUtc, false, errorCode);
        }
        catch (JsonException)
        {
            return new QuotaObservation(null, Array.Empty<QuotaBucket>(), QuotaSource.Unavailable, observedAtUtc, false,
                "quota_json_invalid");
        }
    }

    private static void CollectBuckets(JsonElement element, List<QuotaBucket> buckets, string? nameHint,
        ref bool invalidWindowDetected)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (TryReadBucket(element, nameHint, out var bucket, out var invalidWindow))
            {
                buckets.Add(bucket);
            }
            invalidWindowDetected |= invalidWindow;

            foreach (var property in element.EnumerateObject())
            {
                CollectBuckets(property.Value, buckets, property.Name, ref invalidWindowDetected);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectBuckets(item, buckets, nameHint, ref invalidWindowDetected);
            }
        }
    }

    private static bool TryReadBucket(JsonElement element, string? nameHint, out QuotaBucket bucket,
        out bool invalidWindow)
    {
        bucket = default!;
        invalidWindow = false;
        var id = GetString(element, "id") ?? GetString(element, "limitId") ?? GetString(element, "limit_id") ?? nameHint;
        var name = GetString(element, "name") ?? GetString(element, "limitName") ?? id;
        var used = GetDouble(element, "usedPercent") ?? GetDouble(element, "used_percent");
        var duration = GetInt(element, "windowDurationMins") ?? GetInt(element, "window_duration_mins");
        var reset = GetDate(element, "resetsAt") ?? GetDate(element, "resets_at");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || !used.HasValue ||
            !duration.HasValue || !reset.HasValue)
        {
            return false;
        }
        if (duration.Value <= 0)
        {
            invalidWindow = true;
            return false;
        }

        var candidate = new QuotaBucket(id, name, Math.Clamp(used.Value, 0, 100), duration.Value, reset.Value);
        if (!candidate.HasValidWindow)
        {
            invalidWindow = true;
            return false;
        }
        bucket = candidate;
        return true;
    }

    private static QuotaBucket? SelectPrimary(IReadOnlyList<QuotaBucket> buckets, out string? errorCode)
    {
        errorCode = null;
        if (buckets.Count == 0)
        {
            errorCode = "quota_bucket_missing";
            return null;
        }

        var named = buckets.Where(bucket => bucket.Id.Contains("codex", StringComparison.OrdinalIgnoreCase) ||
                                             bucket.Name.Contains("codex", StringComparison.OrdinalIgnoreCase) ||
                                             bucket.Id.Contains("main", StringComparison.OrdinalIgnoreCase) ||
                                             bucket.Name.Contains("main", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (named.Length == 1)
        {
            return named[0];
        }

        if (named.Length > 1 || buckets.Count > 1)
        {
            errorCode = "quota_bucket_ambiguous";
            return null;
        }

        return buckets[0];
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? GetDouble(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        return null;
    }

    private static int? GetInt(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return null;
    }

    private static DateTimeOffset? GetDate(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var seconds))
        {
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(seconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        return value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }
}

public sealed class QuotaStateMachine
{
    private QuotaObservation? _last;
    private readonly UsageDatabase? _database;

    public QuotaStateMachine(UsageDatabase? database = null)
    {
        _database = database;
        _last = database?.LoadLatestQuota(markStale: false);
    }

    public QuotaObservation Observe(QuotaObservation current)
    {
        if (current.Primary is null)
        {
            if (string.Equals(current.ErrorCode, "quota_window_invalid", StringComparison.Ordinal)) return current;
            return _last?.Primary is not null
                ? _last with { IsStale = true, ErrorCode = current.ErrorCode ?? "quota_temporarily_unavailable" }
                : current;
        }

        if (_last?.Primary is not null && current.Primary is not null)
        {
            var increase = current.Primary.RemainingPercent - _last.Primary.RemainingPercent;
            var resetAdvance = current.Primary.ResetsAtUtc - _last.Primary.ResetsAtUtc;
            var minimumForward = TimeSpan.FromMinutes(Math.Max(30,
                Math.Min(current.Primary.WindowDurationMinutes, _last.Primary.WindowDurationMinutes) * 0.10));
            var substantialForwardWindow = resetAdvance >= minimumForward &&
                                           current.Primary.CycleStartUtc > _last.Primary.CycleStartUtc;
            if (increase >= 15 || substantialForwardWindow)
            {
                _database?.SaveResetSignal(new ResetSignal(current.ObservedAtUtc,
                    _last.Primary.RemainingPercent, current.Primary.RemainingPercent,
                    substantialForwardWindow ? "substantial_forward_window" : "remaining_increase"));
            }
        }

        _last = current;
        _database?.SaveQuota(current);
        return current;
    }

    public QuotaObservation RestoreForDisplay() => _last?.Primary is not null
        ? _last with { IsStale = true, ErrorCode = "quota_last_observation" }
        : new QuotaObservation(null, Array.Empty<QuotaBucket>(), QuotaSource.Unavailable,
            DateTimeOffset.UtcNow, false, "not_refreshed");
}

public sealed class RefreshCadence
{
    public static readonly TimeSpan UiCountdown = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan AppendScan = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan QuotaPoll = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan MetadataDiscovery = TimeSpan.FromSeconds(30);

    public DateTimeOffset? LastQuotaAttemptUtc { get; private set; }
    public DateTimeOffset? LastMetadataRefreshUtc { get; private set; }

    public bool IsQuotaDue(DateTimeOffset nowUtc, bool manual) => manual ||
        !LastQuotaAttemptUtc.HasValue || nowUtc - LastQuotaAttemptUtc.Value >= QuotaPoll;

    public bool IsMetadataDue(DateTimeOffset nowUtc) => !LastMetadataRefreshUtc.HasValue ||
        nowUtc - LastMetadataRefreshUtc.Value >= MetadataDiscovery;

    public void MarkQuotaAttempt(DateTimeOffset nowUtc) => LastQuotaAttemptUtc = nowUtc;
    public void MarkMetadataRefresh(DateTimeOffset nowUtc) => LastMetadataRefreshUtc = nowUtc;
}
