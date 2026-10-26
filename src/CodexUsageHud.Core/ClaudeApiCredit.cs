using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CodexUsageHud.Core;

public static class ClaudeApiCreditLimits
{
    public const string SourceUrl = "https://platform.claude.com/settings/billing";
    public const string NativeHostName = "com.codexusagehud.claude_api_credit";
    public const string HostFlag = "--claude-api-credit-host";
    public const string CurrencyUsd = "USD";
    public const int SchemaVersion = 1;
    public const int MaxMessageBytes = 8192;
    public const int MaxCacheBytes = 4096;
    public const decimal MaxAmount = 1_000_000m;
    public static readonly TimeSpan FutureSkew = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan LiveFor = TimeSpan.FromMinutes(15);
    public static readonly DateTimeOffset EarliestObservation = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
}

public static class ClaudeApiCreditState
{
    public const string Ok = "ok";
    public const string Stale = "stale";
    public const string Unavailable = "unavailable";
}

public static class ClaudeApiCreditReason
{
    public const string AccountChanged = "account_changed";
    public const string LoginChanged = "login_changed";
    public const string SourceLost = "source_lost";
    public const string Malformed = "malformed";
    public const string Expired = "expired";
    public const string Stopped = "stopped";
    public const string BindingMissing = "binding_missing";
}

public sealed record ClaudeApiCreditObservation(
    string? Currency,
    decimal? Remaining,
    decimal? GrantAmount,
    DateOnly? GrantedOnUtc,
    DateOnly? ExpiresOnUtc,
    DateTimeOffset ObservedAtUtc,
    string State,
    string OrganizationBinding)
{
    public bool HasAmounts =>
        Currency == ClaudeApiCreditLimits.CurrencyUsd &&
        Remaining is not null &&
        GrantAmount is not null;
}

public readonly record struct ClaudeApiCreditReadResult(
    ClaudeApiCreditObservation? Observation,
    bool KeepPrevious)
{
    public static ClaudeApiCreditReadResult None { get; } = new(null, false);
    public static ClaudeApiCreditReadResult Unreadable { get; } = new(null, true);
}

public static class ClaudeApiCreditLaunch
{
    public static bool IsHostMode(IReadOnlyList<string> arguments) =>
        arguments.Any(argument => argument.Equals(ClaudeApiCreditLimits.HostFlag, StringComparison.Ordinal));

    public static string ResolveDataDirectory(IReadOnlyList<string> arguments)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (!arguments[index].Equals("--data-dir", StringComparison.Ordinal)) continue;
            var value = arguments[index + 1];
            if (string.IsNullOrWhiteSpace(value) || value.Contains('\0')) continue;
            return Path.GetFullPath(value);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexUsageHUD");
    }
}

public static class ClaudeApiCreditFrame
{
    public static bool TryRead(Stream input, out byte[]? body, out string error)
    {
        body = null;
        var header = new byte[4];
        if (!ReadExact(input, header, 4))
        {
            error = "truncated";
            return false;
        }

        var length = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (length == 0 || length > ClaudeApiCreditLimits.MaxMessageBytes)
        {
            error = "oversized";
            return false;
        }

        var message = new byte[length];
        if (!ReadExact(input, message, message.Length))
        {
            error = "truncated";
            return false;
        }

        body = message;
        error = "";
        return true;
    }

    public static void WriteJson(Stream output, string json)
    {
        var body = Encoding.UTF8.GetBytes(json);
        if (body.Length == 0 || body.Length > ClaudeApiCreditLimits.MaxMessageBytes)
            throw new InvalidOperationException("frame_bounds");
        var header = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)body.Length);
        output.Write(header);
        output.Write(body);
        output.Flush();
    }

    private static bool ReadExact(Stream input, byte[] buffer, int count)
    {
        var offset = 0;
        while (offset < count)
        {
            int read;
            try
            {
                read = input.Read(buffer, offset, count - offset);
            }
            catch (IOException)
            {
                return false;
            }

            if (read <= 0) return false;
            offset += read;
        }

        return true;
    }
}

public static class ClaudeApiCreditNativeHost
{
    public static int Run(Stream input, Stream output, string directory, DateTimeOffset nowUtc)
    {
        try
        {
            if (!ClaudeApiCreditFrame.TryRead(input, out var body, out var error) || body is null)
            {
                WriteResult(output, false, string.IsNullOrEmpty(error) ? "rejected" : error);
                return 2;
            }

            var cache = new ClaudeApiCreditCache(directory);
            if (!cache.TryApplyJson(body, nowUtc, out var applyError))
            {
                WriteResult(output, false, applyError);
                return 2;
            }

            WriteResult(output, true, "");
            return 0;
        }
        catch (Exception)
        {
            try { WriteResult(output, false, "io"); }
            catch (Exception) { }
            return 3;
        }
    }

    private static void WriteResult(Stream output, bool ok, string error)
    {
        var code = SafeCode(error);
        var json = ok
            ? "{\"schema\":1,\"ok\":true}"
            : "{\"schema\":1,\"ok\":false,\"error\":\"" + code + "\"}";
        ClaudeApiCreditFrame.WriteJson(output, json);
    }

    private static string SafeCode(string error)
    {
        if (error is "truncated" or "oversized" or "rejected" or "io" or "bad_schema" or "bad_source"
            or "bad_time" or "bad_amount" or "bad_date" or "bad_binding" or "bad_kind" or "bad_state"
            or "bad_directory")
            return error;
        return "rejected";
    }
}

public sealed class ClaudeApiCreditCache
{
    public const string FileName = "claude-api-credit.json";
    public const string TempFileName = FileName + ".tmp";

    private readonly string _directory;
    private readonly object _gate = new();

    public ClaudeApiCreditCache(string directory)
    {
        _directory = directory;
    }

    public string? FinalPath => TryPaths(out var finalPath, out _) ? finalPath : null;

    public bool TryApplyJson(byte[] json, DateTimeOffset nowUtc, out string error)
    {
        if (!ClaudeApiCreditMessages.TryParse(json, nowUtc, out var message, out error) || message is null)
            return false;
        if (!TryApply(message, out error))
            return false;
        error = "";
        return true;
    }

    public bool TryApply(ClaudeApiCreditMessage message, out string error)
    {
        lock (_gate)
        {
            if (!TryPaths(out var finalPath, out var tempPath))
            {
                error = "bad_directory";
                return false;
            }

            var current = ReadStored();
            var next = ClaudeApiCreditMessages.Apply(current, message);
            var bytes = ClaudeApiCreditJson.SerializeCache(next);
            if (bytes.Length == 0 || bytes.Length > ClaudeApiCreditLimits.MaxCacheBytes)
            {
                error = "rejected";
                return false;
            }

            string? pending = tempPath;
            try
            {
                Directory.CreateDirectory(_directory);
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096,
                           FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (File.Exists(finalPath))
                    File.Replace(tempPath, finalPath, null);
                else
                    File.Move(tempPath, finalPath);
                pending = null;
            }
            catch (IOException)
            {
                error = "io";
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                error = "io";
                return false;
            }
            finally
            {
                if (pending is not null)
                    TryDelete(pending);
            }

            error = "";
            return true;
        }
    }

    public ClaudeApiCreditReadResult Read(DateTimeOffset nowUtc)
    {
        try
        {
            var stored = ReadStored();
            if (stored is null) return ClaudeApiCreditReadResult.None;
            return new ClaudeApiCreditReadResult(ClaudeApiCreditMessages.ForDisplay(stored, nowUtc), false);
        }
        catch (IOException)
        {
            return ClaudeApiCreditReadResult.Unreadable;
        }
        catch (UnauthorizedAccessException)
        {
            return ClaudeApiCreditReadResult.Unreadable;
        }
        catch (JsonException)
        {
            return ClaudeApiCreditReadResult.None;
        }
    }

    private ClaudeApiCreditObservation? ReadStored()
    {
        if (!TryPaths(out var finalPath, out _)) return null;
        if (!File.Exists(finalPath)) return null;
        var info = new FileInfo(finalPath);
        if (info.Length <= 0 || info.Length > ClaudeApiCreditLimits.MaxCacheBytes) return null;
        byte[] bytes;
        using (var stream = new FileStream(finalPath, FileMode.Open, FileAccess.Read,
                   FileShare.ReadWrite | FileShare.Delete))
        {
            if (stream.Length > ClaudeApiCreditLimits.MaxCacheBytes) return null;
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            bytes = memory.ToArray();
        }

        return ClaudeApiCreditJson.ReadCache(bytes);
    }

    private bool TryPaths(out string finalPath, out string tempPath)
    {
        finalPath = "";
        tempPath = "";
        if (string.IsNullOrWhiteSpace(_directory)) return false;
        string root;
        try
        {
            root = Path.GetFullPath(_directory);
        }
        catch (Exception)
        {
            return false;
        }

        finalPath = Path.GetFullPath(Path.Combine(root, FileName));
        tempPath = Path.GetFullPath(Path.Combine(root, TempFileName));
        return Path.GetFileName(finalPath) == FileName &&
               Path.GetFileName(tempPath) == TempFileName &&
               IsInside(root, finalPath) &&
               IsInside(root, tempPath);
    }

    private static bool IsInside(string root, string path)
    {
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                     Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception)
        {
        }
    }
}

public sealed record ClaudeApiCreditMessage(
    string Kind,
    DateTimeOffset ObservedAtUtc,
    string OrganizationBinding,
    string? Reason,
    decimal? Remaining,
    decimal? GrantAmount,
    DateOnly? GrantedOnUtc,
    DateOnly? ExpiresOnUtc,
    bool ExpiredOnArrival);

public static class ClaudeApiCreditMessages
{
    public static bool TryParse(byte[] json, DateTimeOffset nowUtc, out ClaudeApiCreditMessage? message, out string error)
    {
        message = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "bad_schema";
                return false;
            }

            var root = document.RootElement;
            if (!root.TryGetProperty("schema", out var schema) || schema.ValueKind != JsonValueKind.Number ||
                !schema.TryGetInt32(out var schemaVersion) || schemaVersion != ClaudeApiCreditLimits.SchemaVersion)
            {
                error = "bad_schema";
                return false;
            }

            if (!TryString(root, "kind", out var kind) || kind is not ("observation" or "status"))
            {
                error = "bad_kind";
                return false;
            }

            if (!TryString(root, "source", out var source) ||
                !string.Equals(source, ClaudeApiCreditLimits.SourceUrl, StringComparison.Ordinal))
            {
                error = "bad_source";
                return false;
            }

            if (!TryString(root, "observed_at_utc", out var observedText) ||
                !DateTimeOffset.TryParse(observedText, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var observed) ||
                observed.Year < 2020)
            {
                error = "bad_time";
                return false;
            }

            observed = observed.ToUniversalTime();
            if (observed < ClaudeApiCreditLimits.EarliestObservation || observed > nowUtc.ToUniversalTime() + ClaudeApiCreditLimits.FutureSkew)
            {
                error = "bad_time";
                return false;
            }

            var binding = "";
            if (root.TryGetProperty("organization_binding", out var bindingProperty) &&
                bindingProperty.ValueKind != JsonValueKind.Null)
            {
                if (bindingProperty.ValueKind != JsonValueKind.String)
                {
                    error = "bad_binding";
                    return false;
                }

                binding = bindingProperty.GetString() ?? "";
                if (!IsBinding(binding, allowEmpty: true))
                {
                    error = "bad_binding";
                    return false;
                }
            }

            if (kind == "observation")
            {
                if (!IsBinding(binding, allowEmpty: false))
                {
                    error = "bad_binding";
                    return false;
                }

                if (root.TryGetProperty("state", out var stateProperty) &&
                    (stateProperty.ValueKind != JsonValueKind.String ||
                     stateProperty.GetString() != ClaudeApiCreditState.Ok))
                {
                    error = "bad_state";
                    return false;
                }

                if (!TryMoneyFields(root, observed, out var remaining, out var grant, out var granted, out var expires,
                        out var expired, out error))
                    return false;

                message = new ClaudeApiCreditMessage("observation", observed, binding, null, remaining, grant,
                    granted, expires, expired);
                error = "";
                return true;
            }

            if (!TryString(root, "reason", out var reason) || !IsStatusReason(reason))
            {
                error = "bad_kind";
                return false;
            }

            if (root.TryGetProperty("state", out var statusState) &&
                statusState.ValueKind == JsonValueKind.String &&
                statusState.GetString() == ClaudeApiCreditState.Ok)
            {
                error = "bad_state";
                return false;
            }

            message = new ClaudeApiCreditMessage("status", observed, binding, reason, null, null, null, null, false);
            error = "";
            return true;
        }
        catch (JsonException)
        {
            error = "rejected";
            return false;
        }
    }

    public static ClaudeApiCreditObservation Apply(ClaudeApiCreditObservation? current, ClaudeApiCreditMessage message)
    {
        if (message.Kind == "observation")
        {
            var state = message.ExpiredOnArrival ? ClaudeApiCreditState.Unavailable : ClaudeApiCreditState.Ok;
            return new ClaudeApiCreditObservation(
                ClaudeApiCreditLimits.CurrencyUsd,
                message.Remaining,
                message.GrantAmount,
                message.GrantedOnUtc,
                message.ExpiresOnUtc,
                message.ObservedAtUtc,
                state,
                message.OrganizationBinding);
        }

        var reason = message.Reason ?? "";
        var sameAccount = current is not null &&
                          current.OrganizationBinding.Length == 64 &&
                          string.Equals(current.OrganizationBinding, message.OrganizationBinding, StringComparison.Ordinal);
        if (reason is ClaudeApiCreditReason.SourceLost or ClaudeApiCreditReason.Malformed)
        {
            if (sameAccount && current!.HasAmounts)
            {
                return current with { State = ClaudeApiCreditState.Stale };
            }

            return Cleared(message.ObservedAtUtc, message.OrganizationBinding);
        }

        if (reason == ClaudeApiCreditReason.Expired && sameAccount && current!.HasAmounts)
            return current with { State = ClaudeApiCreditState.Unavailable };

        if (reason == ClaudeApiCreditReason.Stopped && current is not null &&
            (message.OrganizationBinding.Length == 0 || sameAccount))
        {
            return new ClaudeApiCreditObservation(null, null, null, null, null, message.ObservedAtUtc,
                ClaudeApiCreditState.Unavailable, current.OrganizationBinding);
        }

        var binding = reason == ClaudeApiCreditReason.AccountChanged ? message.OrganizationBinding : "";
        return Cleared(message.ObservedAtUtc, binding);
    }

    public static ClaudeApiCreditObservation ForDisplay(ClaudeApiCreditObservation stored, DateTimeOffset nowUtc)
    {
        var state = stored.State;
        if (stored.ExpiresOnUtc is { } expires &&
            DateOnly.FromDateTime(nowUtc.ToUniversalTime().UtcDateTime) > expires &&
            stored.HasAmounts)
        {
            state = ClaudeApiCreditState.Unavailable;
        }
        else if (state == ClaudeApiCreditState.Ok &&
                 nowUtc.ToUniversalTime() - stored.ObservedAtUtc.ToUniversalTime() > ClaudeApiCreditLimits.LiveFor)
        {
            state = ClaudeApiCreditState.Stale;
        }

        return state == stored.State ? stored : stored with { State = state };
    }

    private static ClaudeApiCreditObservation Cleared(DateTimeOffset observedAtUtc, string binding) =>
        new(null, null, null, null, null, observedAtUtc, ClaudeApiCreditState.Unavailable, binding);

    private static bool TryMoneyFields(JsonElement root, DateTimeOffset observed, out decimal remaining,
        out decimal grant, out DateOnly grantedOn, out DateOnly expiresOn, out bool expired, out string error)
    {
        remaining = 0;
        grant = 0;
        grantedOn = default;
        expiresOn = default;
        expired = false;
        if (!TryString(root, "currency", out var currency) ||
            !string.Equals(currency, ClaudeApiCreditLimits.CurrencyUsd, StringComparison.Ordinal))
        {
            error = "bad_amount";
            return false;
        }

        if (!TryString(root, "remaining", out var remainingText) || !TryMoney(remainingText, out remaining) ||
            !TryString(root, "grant_amount", out var grantText) || !TryMoney(grantText, out grant) ||
            remaining > grant)
        {
            error = "bad_amount";
            return false;
        }

        if (!TryString(root, "granted_on_utc", out var grantedText) || !TryDate(grantedText, out grantedOn) ||
            !TryString(root, "expires_on_utc", out var expiresText) || !TryDate(expiresText, out expiresOn) ||
            grantedOn > expiresOn)
        {
            error = "bad_date";
            return false;
        }

        var observedDate = DateOnly.FromDateTime(observed.UtcDateTime);
        if (grantedOn > observedDate)
        {
            error = "bad_date";
            return false;
        }

        expired = expiresOn < observedDate;
        error = "";
        return true;
    }

    private static bool TryMoney(string text, out decimal value)
    {
        value = 0;
        if (text.Length is 0 or > 16) return false;
        foreach (var character in text)
        {
            if (character is not '.' && !char.IsAsciiDigit(character)) return false;
        }

        var dot = text.IndexOf('.');
        if (dot >= 0)
        {
            if (text.IndexOf('.', dot + 1) >= 0) return false;
            if (text.Length - dot - 1 is < 1 or > 2) return false;
        }

        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value))
            return false;
        return value >= 0 && value <= ClaudeApiCreditLimits.MaxAmount;
    }

    private static bool TryDate(string text, out DateOnly date)
    {
        date = default;
        if (text.Length != 10 || text[4] != '-' || text[7] != '-') return false;
        return DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static bool TryString(JsonElement root, string name, out string value)
    {
        value = "";
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            return false;
        var text = property.GetString();
        if (string.IsNullOrEmpty(text) || text.Length > 200) return false;
        value = text;
        return true;
    }

    private static bool IsBinding(string binding, bool allowEmpty)
    {
        if (binding.Length == 0) return allowEmpty;
        if (binding.Length != 64) return false;
        foreach (var character in binding)
        {
            if (character is not (>= '0' and <= '9' or >= 'a' and <= 'f')) return false;
        }

        return true;
    }

    private static bool IsStatusReason(string reason) =>
        reason is ClaudeApiCreditReason.AccountChanged or ClaudeApiCreditReason.LoginChanged or
            ClaudeApiCreditReason.SourceLost or ClaudeApiCreditReason.Malformed or
            ClaudeApiCreditReason.Expired or ClaudeApiCreditReason.Stopped or
            ClaudeApiCreditReason.BindingMissing;
}

public static class ClaudeApiCreditJson
{
    public static byte[] SerializeCache(ClaudeApiCreditObservation observation)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema", ClaudeApiCreditLimits.SchemaVersion);
            WriteNullableString(writer, "currency", observation.HasAmounts ? observation.Currency : null);
            WriteMoney(writer, "remaining", observation.HasAmounts ? observation.Remaining : null);
            WriteMoney(writer, "grant_amount", observation.HasAmounts ? observation.GrantAmount : null);
            WriteDate(writer, "granted_on_utc", observation.HasAmounts ? observation.GrantedOnUtc : null);
            WriteDate(writer, "expires_on_utc", observation.HasAmounts ? observation.ExpiresOnUtc : null);
            writer.WriteString("observed_at_utc", QuotaSnapshotJson.FormatUtc(observation.ObservedAtUtc));
            writer.WriteString("state", observation.State);
            writer.WriteString("organization_binding", observation.OrganizationBinding);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    public static void WritePublic(Utf8JsonWriter writer, ClaudeApiCreditObservation observation)
    {
        writer.WriteStartObject();
        WriteNullableString(writer, "currency", observation.HasAmounts ? observation.Currency : null);
        WriteMoney(writer, "remaining", observation.HasAmounts ? observation.Remaining : null);
        WriteMoney(writer, "grant_amount", observation.HasAmounts ? observation.GrantAmount : null);
        WriteDate(writer, "granted_on_utc", observation.HasAmounts ? observation.GrantedOnUtc : null);
        WriteDate(writer, "expires_on_utc", observation.HasAmounts ? observation.ExpiresOnUtc : null);
        writer.WriteString("observed_at_utc", QuotaSnapshotJson.FormatUtc(observation.ObservedAtUtc));
        writer.WriteString("state", observation.State);
        writer.WriteEndObject();
    }

    public static ClaudeApiCreditObservation? ReadCache(byte[] json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (!root.TryGetProperty("schema", out var schema) || schema.ValueKind != JsonValueKind.Number ||
            schema.GetInt32() != ClaudeApiCreditLimits.SchemaVersion)
            return null;
        if (!root.TryGetProperty("state", out var stateProperty) || stateProperty.GetString() is not (
                ClaudeApiCreditState.Ok or ClaudeApiCreditState.Stale or ClaudeApiCreditState.Unavailable))
            return null;
        if (!root.TryGetProperty("observed_at_utc", out var observedProperty) ||
            !DateTimeOffset.TryParse(observedProperty.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var observed))
            return null;
        var binding = root.TryGetProperty("organization_binding", out var bindingProperty)
            ? bindingProperty.GetString() ?? ""
            : "";
        if (binding.Length != 0 && binding.Length != 64) return null;
        decimal? remaining = null;
        decimal? grant = null;
        DateOnly? granted = null;
        DateOnly? expires = null;
        string? currency = null;
        if (root.TryGetProperty("currency", out var currencyProperty) && currencyProperty.ValueKind == JsonValueKind.String)
            currency = currencyProperty.GetString();
        if (root.TryGetProperty("remaining", out var remainingProperty) && remainingProperty.ValueKind == JsonValueKind.String &&
            decimal.TryParse(remainingProperty.GetString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var remainingValue))
            remaining = remainingValue;
        if (root.TryGetProperty("grant_amount", out var grantProperty) && grantProperty.ValueKind == JsonValueKind.String &&
            decimal.TryParse(grantProperty.GetString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var grantValue))
            grant = grantValue;
        if (root.TryGetProperty("granted_on_utc", out var grantedProperty) && grantedProperty.ValueKind == JsonValueKind.String &&
            DateOnly.TryParseExact(grantedProperty.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var grantedValue))
            granted = grantedValue;
        if (root.TryGetProperty("expires_on_utc", out var expiresProperty) && expiresProperty.ValueKind == JsonValueKind.String &&
            DateOnly.TryParseExact(expiresProperty.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiresValue))
            expires = expiresValue;
        var state = stateProperty.GetString()!;
        var hasAmounts = currency == ClaudeApiCreditLimits.CurrencyUsd && remaining is not null && grant is not null &&
                         granted is not null && expires is not null;
        if (!hasAmounts)
            return new ClaudeApiCreditObservation(null, null, null, null, null, observed.ToUniversalTime(), state, binding);
        return new ClaudeApiCreditObservation(currency, remaining, grant, granted, expires, observed.ToUniversalTime(), state, binding);
    }

    private static void WriteMoney(Utf8JsonWriter writer, string name, decimal? value)
    {
        if (value is { } amount)
            writer.WriteString(name, amount.ToString("0.00", CultureInfo.InvariantCulture));
        else
            writer.WriteNull(name);
    }

    private static void WriteDate(Utf8JsonWriter writer, string name, DateOnly? value)
    {
        if (value is { } date)
            writer.WriteString(name, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        else
            writer.WriteNull(name);
    }

    private static void WriteNullableString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null) writer.WriteNull(name);
        else writer.WriteString(name, value);
    }
}

public sealed record ClaudeApiCreditRow(
    string Title,
    string AmountText,
    string DetailText,
    string MetaText,
    string ScopeText);

public static class ClaudeApiCreditPresentation
{
    public const string Title = "Claude API赠送额度";
    public const string ScopeNote = "组织共享的 API 赠送额度，不限 Cursor。这是美元余额，不是每周额度百分比。";

    private static readonly TimeZoneInfo? Taipei = TryTaipei();

    public static ClaudeApiCreditRow BuildRow(ClaudeApiCreditObservation observation)
    {
        var amount = observation.HasAmounts
            ? "US$" + observation.Remaining!.Value.ToString("0.00", CultureInfo.InvariantCulture) +
              " / US$" + observation.GrantAmount!.Value.ToString("0.00", CultureInfo.InvariantCulture)
            : "—";
        var expired = IsExpired(observation, DateTimeOffset.UtcNow);
        var detail = "";
        if (observation.HasAmounts && observation.GrantedOnUtc is { } granted && observation.ExpiresOnUtc is { } expiryDate)
        {
            detail = "授予 " + granted.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
                     " UTC · 到期 " + expiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " UTC";
            if (expired) detail += " · 已过期";
        }
        else if (!observation.HasAmounts)
        {
            detail = "没有可用读数";
        }

        return new ClaudeApiCreditRow(Title, amount, detail,
            "观测 " + FormatObserved(observation.ObservedAtUtc) + " · " + StateCaption(observation, expired),
            ScopeNote);
    }

    private static bool IsExpired(ClaudeApiCreditObservation observation, DateTimeOffset nowUtc) =>
        observation.ExpiresOnUtc is { } expires &&
        DateOnly.FromDateTime(nowUtc.UtcDateTime) > expires;

    private static string StateCaption(ClaudeApiCreditObservation observation, bool expired)
    {
        if (!observation.HasAmounts) return "未连接";
        if (expired) return "已过期";
        return observation.State switch
        {
            ClaudeApiCreditState.Ok => "正常",
            ClaudeApiCreditState.Stale => "更新延迟",
            _ => "不可用",
        };
    }

    private static string FormatObserved(DateTimeOffset observed)
    {
        if (Taipei is null)
            return observed.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
        var local = TimeZoneInfo.ConvertTime(observed, Taipei);
        return local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " 台北";
    }

    private static TimeZoneInfo? TryTaipei()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time"); }
        catch (TimeZoneNotFoundException) { return null; }
        catch (InvalidTimeZoneException) { return null; }
    }
}
