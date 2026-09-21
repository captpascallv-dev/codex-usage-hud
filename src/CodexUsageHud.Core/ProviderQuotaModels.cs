using System.Security.Cryptography;
using System.Text;

namespace CodexUsageHud.Core;

public static class ProviderSlotIds
{
    public const string CodexPrimary = "codex-primary";
    public const string CodexSecondary = "codex-secondary";
    public const string Cursor = "cursor";
    public const string Grok = "grok";
    public const string GrokBot = "grok-bot";

    public static readonly IReadOnlyList<string> All = new[]
    {
        CodexPrimary, CodexSecondary, Cursor, Grok, GrokBot,
    };
}

public static class ProviderIds
{
    public const string Codex = "codex";
    public const string Cursor = "cursor";
    public const string Grok = "grok";
    public const string GrokBot = "grok-bot";
}

public enum QuotaSlotStatus
{
    Live,
    Stale,
    Unavailable,
    NotConnected,
    Disabled,
    SetupRequired,
    NoAllowance,
    IdentityCollision,
    UnverifiedHistory,
}

public sealed record QuotaWindowObservation(
    string WindowId,
    string DisplayName,
    double? UsedPercent,
    DateTimeOffset? ResetsAtUtc,
    int? WindowDurationMinutes,
    bool HasAllowance,
    string? MissingResetText = null)
{
    public double? RemainingPercent => UsedPercent.HasValue && HasAllowance
        ? Math.Clamp(100d - UsedPercent.Value, 0d, 100d)
        : null;

    public bool HasUsablePercent => HasAllowance && UsedPercent.HasValue;
}

public sealed record ProviderSlotSnapshot(
    string SlotId,
    string ProviderId,
    string Label,
    bool Enabled,
    bool SuppliesLocalAnalysis,
    QuotaSlotStatus Status,
    string StatusText,
    DateTimeOffset ObservedAtUtc,
    string SourceDescription,
    IReadOnlyList<QuotaWindowObservation> Windows,
    string? OpaqueIdentityHash = null,
    string? ErrorCode = null,
    string? AccountKey = null,
    int UnverifiedHistoryCount = 0,
    int ForeignHistoryCount = 0,
    string? ConfigFingerprint = null,
    string? FieldPresenceFlags = null)
{
    public TimeSpan ObservationAge(DateTimeOffset nowUtc) =>
        nowUtc - ObservedAtUtc < TimeSpan.Zero ? TimeSpan.Zero : nowUtc - ObservedAtUtc;

    public string ShortAlias => SlotId switch
    {
        ProviderSlotIds.CodexPrimary => "Codex主",
        ProviderSlotIds.CodexSecondary => "Codex备",
        ProviderSlotIds.Cursor => "Cursor",
        ProviderSlotIds.Grok => "Grok",
        ProviderSlotIds.GrokBot => "Bot",
        _ => Label,
    };

    public double? GlanceRemainingPercent
    {
        get
        {
            if (Status is QuotaSlotStatus.Disabled or QuotaSlotStatus.NotConnected or
                QuotaSlotStatus.SetupRequired or QuotaSlotStatus.NoAllowance or
                QuotaSlotStatus.IdentityCollision)
            {
                return null;
            }

            var usable = Windows.Where(window => window.HasUsablePercent).Select(window => window.RemainingPercent!.Value)
                .ToArray();
            if (usable.Length == 0) return null;
            return usable.Min();
        }
    }

    public QuotaWindowObservation? GlanceWindow
    {
        get
        {
            var usable = Windows.Where(window => window.HasUsablePercent).ToArray();
            if (usable.Length == 0) return null;
            return usable.OrderBy(window => window.RemainingPercent)
                .ThenBy(window => window.DisplayName, StringComparer.Ordinal)
                .First();
        }
    }

    public string GlancePercentText
    {
        get
        {
            if (Status == QuotaSlotStatus.SetupRequired) return "未配置";
            if (Status == QuotaSlotStatus.NotConnected) return "未连接";
            return GlanceRemainingPercent is { } remaining
                ? remaining.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%"
                : "—";
        }
    }

    public string GlanceWindowCaption => GlanceWindow?.DisplayName ?? string.Empty;

    public string GlanceText
    {
        get
        {
            if (Status == QuotaSlotStatus.Disabled) return "未启用";
            if (Status == QuotaSlotStatus.NotConnected) return "未连接";
            if (Status == QuotaSlotStatus.SetupRequired) return "未配置";
            if (Status == QuotaSlotStatus.NoAllowance) return "无额度";
            if (Status == QuotaSlotStatus.IdentityCollision) return "同一账户";
            if (Status == QuotaSlotStatus.Unavailable) return "不可用";
            if (GlanceWindow is { } window && window.RemainingPercent is { } remaining)
            {
                var named = $"{window.DisplayName} {remaining:0}%";
                return Status == QuotaSlotStatus.Stale ? named + " · 陈旧" : named;
            }

            if (Status == QuotaSlotStatus.Stale) return "陈旧";
            return StatusText;
        }
    }
}

public sealed record ProviderQuotaBoard(
    IReadOnlyList<ProviderSlotSnapshot> Slots,
    DateTimeOffset GeneratedAtUtc)
{
    public ProviderSlotSnapshot? Find(string slotId) =>
        Slots.FirstOrDefault(slot => string.Equals(slot.SlotId, slotId, StringComparison.Ordinal));

    public ProviderSlotSnapshot? PrimaryCodex => Find(ProviderSlotIds.CodexPrimary);
}

public sealed record ProviderSlotSettings(
    string SlotId,
    string Label,
    bool Enabled,
    string? CodexHome = null);

public sealed record ProviderAccessSettings(
    IReadOnlyList<ProviderSlotSettings> Slots,
    string CompactLayout,
    bool AccessNoticeAcknowledged)
{
    public ProviderSlotSettings Slot(string slotId) =>
        Slots.First(item => string.Equals(item.SlotId, slotId, StringComparison.Ordinal));

    public static ProviderAccessSettings Default() => new(
        new[]
        {
            new ProviderSlotSettings(ProviderSlotIds.CodexPrimary, "Codex 当前", true),
            new ProviderSlotSettings(ProviderSlotIds.CodexSecondary, "Codex 第二账户", true),
            new ProviderSlotSettings(ProviderSlotIds.Cursor, "Cursor", false),
            new ProviderSlotSettings(ProviderSlotIds.Grok, "Grok", false),
            new ProviderSlotSettings(ProviderSlotIds.GrokBot, "Grok Bot", false),
        },
        CompactLayoutModes.Rail,
        false);
}

public enum SecondaryHomeStatus
{
    Ready,
    Missing,
    MissingPath,
    SameAsPrimary,
}

public static class PiCodexSubscription
{
    public const string SourceDescription = "PI openai-codex · chatgpt.com/backend-api/wham/usage";
    public const string MissingStatusText = "未连接：本机 PI 没有 ChatGPT/Codex 订阅登录";
    public const string MissingDetail = "需要 PI 已登录 openai-codex；不要求第二套 CODEX_HOME";
    public const string MissingCode = "pi_codex_sign_in_required";
    public const string ExpiredStatusText = "未连接：PI ChatGPT 订阅登录已过期";
    public const string ExpiredDetail = "不刷新或改写 PI 凭据";
    public const string ExpiredCode = "pi_codex_login_expired";
    public const string UnsupportedStatusText = "PI openai-codex 登录格式当前无法识别";
    public const string UnsupportedDetail = "不尝试刷新、重新登录或改写凭据";
    public const string UnsupportedCode = "pi_codex_login_unsupported";
}

public static class SecondaryCodexHome
{
    public const string MissingStatusText = "未配置 · 选择第二个 Codex 目录";
    public const string MissingDetail = "在设置中选择第二个 Codex 主目录。不会猜测路径，也不会扫描登录文件。";
    public const string MissingCode = "codex_home_missing";
    public const string InvalidStatusText = "指定的 Codex 主目录不存在";
    public const string InvalidDetail = "路径不存在";
    public const string InvalidCode = "codex_home_missing";
    public const string SameStatusText = "与当前 Codex 主目录相同，不能作为第二账户";
    public const string SameDetail = "请选择另一个 Codex 目录";
    public const string SameCode = "codex_home_same_as_primary";

    public static SecondaryHomeStatus Classify(string? home, string? primaryHome)
    {
        if (string.IsNullOrWhiteSpace(home)) return SecondaryHomeStatus.Missing;
        string full;
        try
        {
            full = Path.GetFullPath(home.Trim());
        }
        catch (Exception)
        {
            return SecondaryHomeStatus.MissingPath;
        }

        if (!Directory.Exists(full)) return SecondaryHomeStatus.MissingPath;
        if (!string.IsNullOrWhiteSpace(primaryHome) && SamePath(full, primaryHome))
            return SecondaryHomeStatus.SameAsPrimary;
        return SecondaryHomeStatus.Ready;
    }

    public static bool SamePath(string left, string right)
    {
        try
        {
            var a = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var b = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }
}

public static class CompactLayoutModes
{
    public const string Rail = "rail";
    public const string Card = "card";
}

public static class OpaqueIdentity
{
    public static string Hash(string material) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();

    public static string Short(string hash) => hash.Length <= 12 ? hash : hash[..12];
}

public static class ProviderQuotaPresentation
{
    public static string FormatAge(TimeSpan age)
    {
        if (age.TotalSeconds < 5) return "刚刚";
        if (age.TotalMinutes < 1) return $"{Math.Max(1, (int)age.TotalSeconds)} 秒前";
        if (age.TotalHours < 1) return $"{Math.Max(1, (int)age.TotalMinutes)} 分钟前";
        if (age.TotalDays < 1) return $"{Math.Max(1, (int)age.TotalHours)} 小时前";
        return $"{Math.Max(1, (int)age.TotalDays)} 天前";
    }

    public static string FormatReset(QuotaWindowObservation window, DateTimeOffset nowUtc)
    {
        if (!window.ResetsAtUtc.HasValue)
            return window.MissingResetText ?? "重置时间不可用";
        if (window.ResetsAtUtc.Value <= nowUtc) return "已到期（陈旧）";
        return $"重置 {window.ResetsAtUtc.Value.ToLocalTime():MM月dd日 HH:mm} · " +
               HudPresentation.FormatCountdown(window.ResetsAtUtc.Value - nowUtc);
    }

    public static string FormatWindowLine(QuotaWindowObservation window, DateTimeOffset nowUtc)
    {
        if (window.ResetsAtUtc.HasValue && window.ResetsAtUtc.Value <= nowUtc)
            return $"{window.DisplayName}：已到期，不作为当前额度";
        if (!window.HasAllowance)
            return $"{window.DisplayName}：无可用额度，不显示剩余百分比";
        if (!window.UsedPercent.HasValue)
            return $"{window.DisplayName}：缺少用量字段，不显示 100% 可用";
        return $"{window.DisplayName}：剩余 {window.RemainingPercent:0}% · 已用 {window.UsedPercent:0}% · " +
               FormatReset(window, nowUtc);
    }

    public static string StatusCaption(QuotaSlotStatus status) => status switch
    {
        QuotaSlotStatus.Live => "实时",
        QuotaSlotStatus.Stale => "陈旧观测",
        QuotaSlotStatus.Unavailable => "不可用",
        QuotaSlotStatus.NotConnected => "未连接",
        QuotaSlotStatus.Disabled => "未启用",
        QuotaSlotStatus.SetupRequired => "未配置",
        QuotaSlotStatus.NoAllowance => "当前计划未包含",
        QuotaSlotStatus.IdentityCollision => "与另一槽为同一账户",
        QuotaSlotStatus.UnverifiedHistory => "历史归属未验证",
        _ => "不可用",
    };

    public static ProviderSlotSnapshot Placeholder(ProviderSlotSettings settings, bool suppliesAnalysis,
        QuotaSlotStatus status, string statusText, string source, string? errorCode = null) =>
        new(settings.SlotId, ProviderFor(settings.SlotId), settings.Label, settings.Enabled,
            suppliesAnalysis, status, statusText, DateTimeOffset.UtcNow, source,
            Array.Empty<QuotaWindowObservation>(), ErrorCode: errorCode);

    public static string ProviderFor(string slotId) => slotId switch
    {
        ProviderSlotIds.CodexPrimary or ProviderSlotIds.CodexSecondary => ProviderIds.Codex,
        ProviderSlotIds.Cursor => ProviderIds.Cursor,
        ProviderSlotIds.Grok => ProviderIds.Grok,
        ProviderSlotIds.GrokBot => ProviderIds.GrokBot,
        _ => slotId,
    };

    public static IReadOnlyList<QuotaWindowObservation> FromCodex(QuotaObservation observation)
    {
        var windows = new List<QuotaWindowObservation>();
        if (observation.Primary is { HasValidWindow: true } primary)
            windows.Add(FromBucket(primary));
        windows.AddRange(observation.Additional.Where(bucket => bucket.HasValidWindow).Select(FromBucket));
        return windows;
    }

    public static QuotaWindowObservation FromBucket(QuotaBucket bucket) =>
        new(bucket.Id, string.IsNullOrWhiteSpace(bucket.Name) ? bucket.Id : bucket.Name,
            bucket.UsedPercent, bucket.ResetsAtUtc, bucket.WindowDurationMinutes, true);

    public static QuotaSlotStatus CodexStatus(QuotaObservation observation, DateTimeOffset nowUtc)
    {
        if (observation.Primary is not { HasValidWindow: true } primary)
            return QuotaSlotStatus.Unavailable;
        if (primary.ResetsAtUtc <= nowUtc) return QuotaSlotStatus.Stale;
        return observation.IsStale ? QuotaSlotStatus.Stale : QuotaSlotStatus.Live;
    }
}

public static class ProviderSettingKeys
{
    public const string CompactLayout = "compact_layout";
    public const string AccessNotice = "provider_access_notice_ack";
    public const string OwnerConfirmedPrimaryStamp = "primary_owner_identity_stamp";

    public static string Label(string slotId) => "slot_" + slotId.Replace('-', '_') + "_label";
    public static string Enabled(string slotId) => "slot_" + slotId.Replace('-', '_') + "_enabled";
    public static string CodexHome(string slotId) => "slot_" + slotId.Replace('-', '_') + "_home";

    public static ProviderAccessSettings FromStored(IReadOnlyDictionary<string, string?> values)
    {
        var defaults = ProviderAccessSettings.Default();
        var slots = defaults.Slots.Select(slot =>
        {
            var label = values.GetValueOrDefault(Label(slot.SlotId));
            var enabledText = values.GetValueOrDefault(Enabled(slot.SlotId));
            var home = values.GetValueOrDefault(CodexHome(slot.SlotId));
            var enabled = string.IsNullOrWhiteSpace(enabledText)
                ? slot.Enabled
                : enabledText == "1";
            return slot with
            {
                Label = string.IsNullOrWhiteSpace(label) ? slot.Label : label.Trim(),
                Enabled = enabled,
                CodexHome = string.IsNullOrWhiteSpace(home) ? slot.CodexHome : home.Trim(),
            };
        }).ToArray();
        var layout = values.GetValueOrDefault(CompactLayout);
        if (!string.Equals(layout, CompactLayoutModes.Card, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(layout, CompactLayoutModes.Rail, StringComparison.OrdinalIgnoreCase))
        {
            layout = CompactLayoutModes.Rail;
        }

        return new ProviderAccessSettings(slots, layout!,
            values.GetValueOrDefault(AccessNotice) == "1");
    }

    public static Dictionary<string, string> ToStored(ProviderAccessSettings settings)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompactLayout] = settings.CompactLayout,
            [AccessNotice] = settings.AccessNoticeAcknowledged ? "1" : "0",
        };
        foreach (var slot in settings.Slots)
        {
            values[Label(slot.SlotId)] = slot.Label;
            values[Enabled(slot.SlotId)] = slot.Enabled ? "1" : "0";
            if (!string.IsNullOrWhiteSpace(slot.CodexHome))
                values[CodexHome(slot.SlotId)] = slot.CodexHome;
        }

        return values;
    }
}

public sealed record AdapterDiagnostic(
    string SlotId,
    string Status,
    DateTimeOffset ObservedAtUtc,
    bool IdentityMatched,
    string? OpaqueIdentityShort,
    string? ErrorCode);
