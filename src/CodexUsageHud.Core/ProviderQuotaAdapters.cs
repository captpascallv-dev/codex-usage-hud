using System.Net.Http;

namespace CodexUsageHud.Core;

public class CodexQuotaProfile
{
    private readonly AppServerClient _client;
    private readonly CodexExecutableDiscovery _discovery;
    private readonly string? _executableOverride;

    public CodexQuotaProfile(AppServerClient? client = null, CodexExecutableDiscovery? discovery = null,
        string? executableOverride = null)
    {
        _client = client ?? new AppServerClient();
        _discovery = discovery ?? new CodexExecutableDiscovery();
        _executableOverride = executableOverride;
    }

    public virtual async Task<ProviderSlotSnapshot> ReadAsync(ProviderSlotSettings settings, string? isolatedHome,
        bool suppliesLocalAnalysis, CancellationToken cancellationToken, string? primaryHome = null)
    {
        _ = primaryHome;
        if (!settings.Enabled)
        {
            return ProviderQuotaPresentation.Placeholder(settings, suppliesLocalAnalysis,
                QuotaSlotStatus.Disabled, "未启用", "用户关闭了此槽", "slot_disabled");
        }

        if (!string.IsNullOrWhiteSpace(isolatedHome) && !Directory.Exists(isolatedHome))
        {
            return ProviderQuotaPresentation.Placeholder(settings, suppliesLocalAnalysis,
                QuotaSlotStatus.SetupRequired, SecondaryCodexHome.InvalidStatusText,
                SecondaryCodexHome.InvalidDetail, SecondaryCodexHome.InvalidCode);
        }

        var executable = _discovery.Find(_executableOverride);
        if (string.IsNullOrWhiteSpace(executable))
        {
            return ProviderQuotaPresentation.Placeholder(settings, suppliesLocalAnalysis,
                QuotaSlotStatus.Unavailable, "未找到 Codex 可执行文件", "codex.exe 不可用",
                "codex_executable_missing");
        }

        IReadOnlyDictionary<string, string>? environment = null;
        if (!string.IsNullOrWhiteSpace(isolatedHome))
        {
            environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CODEX_HOME"] = Path.GetFullPath(isolatedHome),
            };
        }

        var combined = await _client.ReadQuotaAndIdentityAsync(executable, cancellationToken, environment)
            .ConfigureAwait(false);
        var result = combined.Quota;
        var identity = combined.Identity?.Hash;
        var flags = combined.IdentityShape?.Format() ?? combined.Identity?.PresenceFlags;
        var now = DateTimeOffset.UtcNow;
        var windows = ProviderQuotaPresentation.FromCodex(result.Observation);
        if (windows.Count == 0)
        {
            var code = result.ErrorCode ?? result.Observation.ErrorCode ?? "quota_unavailable";
            var status = code is "codex_executable_missing" or "app_server_launch" or "app_server_initialize_exit"
                or "app_server_rate_limits_exit"
                ? QuotaSlotStatus.NotConnected
                : QuotaSlotStatus.Unavailable;
            var text = status == QuotaSlotStatus.NotConnected
                ? "未连接：App Server 无法读取该主目录登录态"
                : "官方额度不可用";
            return new ProviderSlotSnapshot(settings.SlotId, ProviderIds.Codex, settings.Label, true,
                suppliesLocalAnalysis, status, text, now, "Codex App Server account/rateLimits/read",
                Array.Empty<QuotaWindowObservation>(), identity, code, FieldPresenceFlags: flags);
        }

        var expired = windows.All(window => window.ResetsAtUtc.HasValue && window.ResetsAtUtc.Value <= now);
        var statusLive = expired || result.Observation.IsStale
            ? QuotaSlotStatus.Stale
            : ProviderQuotaPresentation.CodexStatus(result.Observation, now);
        var statusText = statusLive == QuotaSlotStatus.Stale ? "陈旧观测，不作为实时额度" : "官方 App Server";
        return new ProviderSlotSnapshot(settings.SlotId, ProviderIds.Codex, settings.Label, true,
            suppliesLocalAnalysis, statusLive, statusText, result.Observation.ObservedAtUtc,
            "Codex App Server account/rateLimits/read · 子进程隔离 CODEX_HOME",
            windows, identity, result.ErrorCode, identity, FieldPresenceFlags: flags);
    }

    public async Task<string?> ReadIdentityAsync(string? isolatedHome, CancellationToken cancellationToken)
    {
        try
        {
            var executable = _discovery.Find(_executableOverride);
            if (string.IsNullOrWhiteSpace(executable)) return null;
            IReadOnlyDictionary<string, string>? environment = null;
            if (!string.IsNullOrWhiteSpace(isolatedHome))
            {
                environment = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["CODEX_HOME"] = Path.GetFullPath(isolatedHome),
                };
            }

            return await _client.ReadAccountIdentityAsync(executable, cancellationToken, environment)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }
}

public sealed class CursorQuotaAdapter
{
    private readonly IAllowlistedHttpSender _http;
    private readonly ICursorTokenSource _tokens;

    public CursorQuotaAdapter(IAllowlistedHttpSender http, ICursorTokenSource tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    public async Task<ProviderSlotSnapshot> ReadAsync(ProviderSlotSettings settings,
        CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Disabled, "未启用",
                "用户关闭了 Cursor 槽", "slot_disabled");

        string? token;
        try { token = _tokens.ReadAccessToken(); }
        catch (Exception)
        {
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                "无法读取本机 Cursor 登录", "仅读取 cursorAuth/accessToken", "cursor_token_unavailable");
        }

        if (string.IsNullOrWhiteSpace(token) ||
            !CursorSessionCookie.TryBuild(token, out var cookie, out var identity) ||
            identity is null)
        {
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                "未连接：本机没有可用的 Cursor 登录", "需要编辑器已登录", "cursor_sign_in_required");
        }
        try
        {
            var response = await _http.SendAsync(new AllowlistedHttpRequest("GET",
                ProviderHttpAllowlist.CursorUsageSummary,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Cookie"] = "WorkosCursorSessionToken=" + cookie,
                    ["Accept"] = "application/json",
                }), cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is 401 or 403)
            {
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                    "未连接：Cursor 登录已失效", "usage-summary 拒绝", "cursor_unauthorized");
            }

            if (response.StatusCode < 200 || response.StatusCode >= 300)
            {
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                    "Cursor 额度接口不可用", "usage-summary HTTP " + response.StatusCode,
                    "cursor_http_" + response.StatusCode);
            }

            var windows = CursorQuotaParser.Parse(response.Body, DateTimeOffset.UtcNow, out var error, out var shape);
            if (windows.Count == 0)
            {
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                    "Cursor 未返回可用的模型池", "两个池不能合并成一个限额", error ?? "cursor_pools_missing") with
                {
                    FieldPresenceFlags = shape.Format(),
                };
            }

            var now = DateTimeOffset.UtcNow;
            var stale = windows.Any(window => window.ResetsAtUtc.HasValue && window.ResetsAtUtc.Value <= now);
            return new ProviderSlotSnapshot(settings.SlotId, ProviderIds.Cursor, settings.Label, true, false,
                stale ? QuotaSlotStatus.Stale : QuotaSlotStatus.Live,
                stale ? "陈旧观测" : "Cursor usage-summary", now,
                "https://cursor.com/api/usage-summary", windows, identity, error, identity,
                FieldPresenceFlags: shape.Format());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                "Cursor 请求超时", "独立超时", "cursor_timeout");
        }
        catch (InvalidOperationException exception)
        {
            var code = ProviderHttpErrors.Sanitize(exception);
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                "Cursor 请求被拒绝", code, code);
        }
        catch (HttpRequestException)
        {
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                "Cursor 网络不可用", "独立失败", "cursor_network");
        }
    }
}

public sealed class GrokQuotaAdapter
{
    private readonly IAllowlistedHttpSender _http;
    private readonly IGrokTokenSource _tokens;

    public GrokQuotaAdapter(IAllowlistedHttpSender http, IGrokTokenSource tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    public async Task<ProviderSlotSnapshot> ReadAsync(ProviderSlotSettings settings,
        CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Disabled, "未启用",
                "用户关闭了 Grok 槽", "slot_disabled");

        GrokLoginInspection inspection;
        try { inspection = _tokens.InspectLogin(); }
        catch (Exception)
        {
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                "无法读取本机 Grok 登录", "仅检查登录文件形状，不输出密钥", "grok_token_unavailable");
        }

        if (!inspection.Usable)
        {
            if (!inspection.FilePresent)
            {
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                    "未连接：本机没有可用的 Grok 登录", "需要 Grok CLI 已登录", "grok_sign_in_required") with
                {
                    FieldPresenceFlags = inspection.Format(),
                };
            }

            if (!inspection.RecognizedFormat)
            {
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                    "本机 Grok 登录格式当前解析器无法识别，不提示重新登录",
                    "公开 CLI 形态：issuer 条目的 key 与到期字段，或根令牌字段",
                    "grok_login_unsupported") with
                {
                    FieldPresenceFlags = inspection.Format(),
                };
            }

            if (inspection.HasRefreshToken)
            {
                GrokRenewalOutcome renewal;
                try { renewal = await _tokens.EnsureFreshAsync(false, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return RenewalPlaceholder(settings, inspection, new GrokRenewalOutcome(GrokRenewalKind.Timeout, true));
                }

                inspection = _tokens.InspectLogin();
                if (!inspection.Usable)
                    return RenewalPlaceholder(settings, inspection, renewal);
            }
            else if (inspection.Expired)
            {
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                    "未连接：Grok 访问令牌已过期，且没有可续期凭据", "本地 expires_at 已过期", "grok_login_expired") with
                {
                    FieldPresenceFlags = inspection.Format(),
                };
            }
            else
            {
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                    "未连接：本机没有可用的 Grok 登录", "需要 Grok CLI 已登录", "grok_sign_in_required") with
                {
                    FieldPresenceFlags = inspection.Format(),
                };
            }
        }

        return await ReadBillingAsync(settings, inspection, retried: false, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProviderSlotSnapshot> ReadBillingAsync(ProviderSlotSettings settings,
        GrokLoginInspection inspection, bool retried, CancellationToken cancellationToken)
    {
        string? token;
        try { token = _tokens.ReadAccessToken(); }
        catch (Exception)
        {
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                "无法读取本机 Grok 登录", "仅读取已识别的 token 字段", "grok_token_unavailable") with
            {
                FieldPresenceFlags = inspection.Format(),
            };
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                "未连接：本机没有可用的 Grok 登录", "需要 Grok CLI 已登录", "grok_sign_in_required") with
            {
                FieldPresenceFlags = inspection.Format(),
            };
        }

        var identity = StableSubjectIdentity.Hash("grok|", token);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Authorization"] = "Bearer " + token,
            ["x-xai-token-auth"] = "xai-grok-cli",
            ["Accept"] = "application/json",
        };
        try
        {
            var response = await _http.SendAsync(new AllowlistedHttpRequest("GET",
                ProviderHttpAllowlist.GrokBilling, headers), cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is 401 or 403)
            {
                if (!retried && inspection.HasRefreshToken)
                {
                    GrokRenewalOutcome renewal;
                    try { renewal = await _tokens.EnsureFreshAsync(true, cancellationToken).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        return RenewalPlaceholder(settings, inspection,
                            new GrokRenewalOutcome(GrokRenewalKind.Timeout, true));
                    }

                    var after = _tokens.InspectLogin();
                    if (after.Usable)
                        return await ReadBillingAsync(settings, after, true, cancellationToken).ConfigureAwait(false);
                    return RenewalPlaceholder(settings, after, renewal);
                }

                var revoked = inspection.HasRefreshToken || retried;
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                    revoked ? "未连接：Grok 登录续期后仍被额度接口拒绝" : "未连接：Grok 访问令牌已过期，且没有可续期凭据",
                    "billing 拒绝",
                    revoked ? "grok_login_revoked" : "grok_login_expired") with
                {
                    FieldPresenceFlags = inspection.Format(),
                };
            }

            if (response.StatusCode < 200 || response.StatusCode >= 300)
            {
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                    "Grok 额度接口不可用", "billing HTTP " + response.StatusCode,
                    "grok_http_" + response.StatusCode);
            }

            var now = DateTimeOffset.UtcNow;
            var windows = GrokQuotaParser.Parse(response.Body, now, out var error);
            if (windows.Count == 0)
            {
                var elapsed = string.Equals(error, "grok_period_elapsed", StringComparison.Ordinal);
                return ProviderQuotaPresentation.Placeholder(settings, false,
                    elapsed ? QuotaSlotStatus.Stale : QuotaSlotStatus.Unavailable,
                    elapsed ? "已结束的周期，不显示为当前额度" : "Grok 未返回可用周额度",
                    "个人账单形态，缺少字段且不在进行中的周期时不按 0 处理", error);
            }

            try
            {
                var settingsResponse = await _http.SendAsync(new AllowlistedHttpRequest("GET",
                    ProviderHttpAllowlist.GrokSettings, headers), cancellationToken).ConfigureAwait(false);
                if (settingsResponse.StatusCode is >= 200 and < 300)
                    _ = GrokQuotaParser.ParsePlanName(settingsResponse.Body);
            }
            catch (Exception)
            {
                // Plan name is optional; usage already succeeded.
            }

            var stale = windows.Any(window => window.ResetsAtUtc.HasValue && window.ResetsAtUtc.Value <= now);
            return new ProviderSlotSnapshot(settings.SlotId, ProviderIds.Grok, settings.Label, true, false,
                stale ? QuotaSlotStatus.Stale : QuotaSlotStatus.Live,
                stale ? "陈旧观测" : "Grok 个人周额度", now,
                "https://cli-chat-proxy.grok.com/v1/billing?format=credits", windows, identity, error,
                identity, FieldPresenceFlags: inspection.Format());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                "Grok 请求超时", "独立超时", "grok_timeout");
        }
        catch (InvalidOperationException exception)
        {
            var code = ProviderHttpErrors.Sanitize(exception);
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                "Grok 请求被拒绝", code, code);
        }
        catch (HttpRequestException)
        {
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                "Grok 网络不可用", "独立失败", "grok_network");
        }
    }

    private static ProviderSlotSnapshot RenewalPlaceholder(ProviderSlotSettings settings,
        GrokLoginInspection inspection, GrokRenewalOutcome outcome)
    {
        switch (outcome.Kind)
        {
            case GrokRenewalKind.Revoked:
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                    "未连接：Grok 登录续期被拒绝，需要重新登录", "refresh grant 已失效",
                    "grok_login_revoked") with
                {
                    FieldPresenceFlags = inspection.Format(),
                };
            case GrokRenewalKind.NotRenewable:
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                    "未连接：Grok 访问令牌已过期，且没有可续期凭据", "本地 expires_at 已过期",
                    "grok_login_expired") with
                {
                    FieldPresenceFlags = inspection.Format(),
                };
            case GrokRenewalKind.Timeout:
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                    "Grok 登录续期超时，稍后再试", "官方 grok models 超时",
                    "grok_renewal_unavailable") with
                {
                    FieldPresenceFlags = inspection.Format(),
                };
            case GrokRenewalKind.BinaryMissing:
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                    "Grok 登录续期暂时不可用：本机没有 Grok CLI", "缺少 grok.exe",
                    "grok_renewal_unavailable") with
                {
                    FieldPresenceFlags = inspection.Format(),
                };
            default:
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                    "Grok 登录续期暂时不可用", "官方 grok models 未写出可用令牌",
                    "grok_renewal_unavailable") with
                {
                    FieldPresenceFlags = inspection.Format(),
                };
        }
    }
}

public sealed class GrokBotQuotaAdapter
{
    private readonly IAllowlistedHttpSender _http;
    private readonly ICursorTokenSource _tokens;

    public GrokBotQuotaAdapter(IAllowlistedHttpSender http, ICursorTokenSource tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    public async Task<ProviderSlotSnapshot> ReadAsync(ProviderSlotSettings settings,
        CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Disabled, "未启用",
                "用户关闭了 Grok Bot 槽", "slot_disabled");

        string? token;
        try { token = _tokens.ReadAccessToken(); }
        catch (Exception)
        {
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                "无法读取 Cursor 登录（Grok Bot 走 Cursor 账户）", "仅读取 cursorAuth/accessToken",
                "grok_bot_token_unavailable");
        }

        if (string.IsNullOrWhiteSpace(token) ||
            !CursorSessionCookie.TryBuild(token, out var cookie, out var identity) || identity is null)
        {
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                "未连接：Grok Bot 需要 Cursor 登录", "不使用 ~/.grok", "grok_bot_sign_in_required");
        }
        try
        {
            var response = await _http.SendAsync(new AllowlistedHttpRequest("POST",
                ProviderHttpAllowlist.CursorSandUsage,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Cookie"] = "WorkosCursorSessionToken=" + cookie,
                    ["Origin"] = "https://cursor.com",
                    ["Accept"] = "application/json",
                }, "{}"), cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is 401 or 403)
            {
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                    "未连接：Cursor 登录无法读取 Grok Bot", "get-sand-usage-status 拒绝",
                    "grok_bot_unauthorized");
            }

            if (response.StatusCode < 200 || response.StatusCode >= 300)
            {
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                    "Grok Bot 额度接口不可用", "HTTP " + response.StatusCode,
                    "grok_bot_http_" + response.StatusCode);
            }

            var now = DateTimeOffset.UtcNow;
            var windows = GrokBotQuotaParser.Parse(response.Body, now, out var error);
            if (windows.Count == 0)
            {
                var excluded = string.Equals(error, "grok_bot_not_included", StringComparison.Ordinal);
                return ProviderQuotaPresentation.Placeholder(settings, false,
                    excluded ? QuotaSlotStatus.NoAllowance : QuotaSlotStatus.Unavailable,
                    excluded ? "当前计划未包含 Grok Bot，不显示 100% 可用" : "Grok Bot 回复无法解读",
                    "Sand 额度与 Cursor 月度池、Grok 周池分离", error);
            }

            var stale = windows.Any(window => window.ResetsAtUtc.HasValue && window.ResetsAtUtc.Value <= now);
            return new ProviderSlotSnapshot(settings.SlotId, ProviderIds.GrokBot, settings.Label, true, false,
                stale ? QuotaSlotStatus.Stale : QuotaSlotStatus.Live,
                stale ? "陈旧观测" : "Cursor dashboard Sand", now,
                "https://cursor.com/api/dashboard/get-sand-usage-status", windows, identity, error, identity);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                "Grok Bot 请求超时", "独立超时", "grok_bot_timeout");
        }
        catch (InvalidOperationException exception)
        {
            var code = ProviderHttpErrors.Sanitize(exception);
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                "Grok Bot 请求被拒绝", code, code);
        }
        catch (HttpRequestException)
        {
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                "Grok Bot 网络不可用", "独立失败", "grok_bot_network");
        }
    }
}

public class PiCodexQuotaAdapter
{
    private readonly IAllowlistedHttpSender _http;
    private readonly IPiCodexTokenSource _tokens;

    public PiCodexQuotaAdapter(IAllowlistedHttpSender http, IPiCodexTokenSource tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    public virtual async Task<ProviderSlotSnapshot> ReadAsync(ProviderSlotSettings settings,
        CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Disabled, "未启用",
                "用户关闭了第二 Codex 槽", "slot_disabled");

        PiCodexLoginInspection inspection;
        try { inspection = _tokens.InspectLogin(); }
        catch (Exception)
        {
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                "无法读取本机 PI openai-codex 登录", "仅检查 openai-codex 条目形状，不输出密钥",
                "pi_codex_token_unavailable");
        }

        if (!inspection.Usable)
        {
            if (!inspection.FilePresent)
            {
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                    PiCodexSubscription.MissingStatusText, PiCodexSubscription.MissingDetail,
                    PiCodexSubscription.MissingCode) with
                {
                    FieldPresenceFlags = inspection.Format(),
                };
            }

            if (!inspection.HasOpenAiCodexKey)
            {
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                    PiCodexSubscription.MissingStatusText, "PI 登录文件存在但没有 openai-codex 条目",
                    PiCodexSubscription.MissingCode) with
                {
                    FieldPresenceFlags = inspection.Format(),
                };
            }

            if (!inspection.RecognizedFormat)
            {
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                    PiCodexSubscription.UnsupportedStatusText, PiCodexSubscription.UnsupportedDetail,
                    PiCodexSubscription.UnsupportedCode) with
                {
                    FieldPresenceFlags = inspection.Format(),
                };
            }

            if (inspection.Expired)
            {
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                    PiCodexSubscription.ExpiredStatusText, PiCodexSubscription.ExpiredDetail,
                    PiCodexSubscription.ExpiredCode) with
                {
                    FieldPresenceFlags = inspection.Format(),
                };
            }

            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                PiCodexSubscription.MissingStatusText, PiCodexSubscription.MissingDetail,
                PiCodexSubscription.MissingCode) with
            {
                FieldPresenceFlags = inspection.Format(),
            };
        }

        PiCodexCredential? credential;
        try { credential = _tokens.ReadCredential(); }
        catch (Exception)
        {
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                "无法读取本机 PI openai-codex 登录", "仅读取 openai-codex 的 access 与 accountId",
                "pi_codex_token_unavailable") with
            {
                FieldPresenceFlags = inspection.Format(),
            };
        }

        if (credential is null)
        {
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                PiCodexSubscription.MissingStatusText, PiCodexSubscription.MissingDetail,
                PiCodexSubscription.MissingCode) with
            {
                FieldPresenceFlags = inspection.Format(),
            };
        }

        var identity = BoundIdentity.HashAccount(credential.AccountId);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Authorization"] = "Bearer " + credential.AccessToken,
            ["chatgpt-account-id"] = credential.AccountId,
            ["User-Agent"] = "codex-cli",
            ["Accept"] = "application/json",
        };
        try
        {
            var response = await _http.SendAsync(new AllowlistedHttpRequest("GET",
                ProviderHttpAllowlist.PiCodexUsage, headers), cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is 401 or 403)
            {
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.NotConnected,
                    PiCodexSubscription.ExpiredStatusText, "wham/usage 拒绝",
                    PiCodexSubscription.ExpiredCode) with
                {
                    OpaqueIdentityHash = identity,
                    AccountKey = identity,
                    FieldPresenceFlags = inspection.Format(),
                };
            }

            if (response.StatusCode < 200 || response.StatusCode >= 300)
            {
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                    "PI Codex 额度接口不可用", "wham/usage HTTP " + response.StatusCode,
                    "pi_codex_http_" + response.StatusCode) with
                {
                    FieldPresenceFlags = inspection.Format(),
                };
            }

            var now = DateTimeOffset.UtcNow;
            var windows = PiCodexQuotaParser.Parse(response.Body, now, out var error, out var shape);
            if (windows.Count == 0)
            {
                return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                    "PI 未返回可用的用量窗口", "不把缺失窗口显示成 100% 可用",
                    error ?? "pi_codex_windows_missing") with
                {
                    OpaqueIdentityHash = identity,
                    AccountKey = identity,
                    FieldPresenceFlags = shape,
                };
            }

            var stale = windows.Any(window => window.ResetsAtUtc.HasValue && window.ResetsAtUtc.Value <= now);
            return new ProviderSlotSnapshot(settings.SlotId, ProviderIds.Codex, settings.Label, true, false,
                stale ? QuotaSlotStatus.Stale : QuotaSlotStatus.Live,
                stale ? "陈旧观测" : "PI ChatGPT/Codex 订阅", now, PiCodexSubscription.SourceDescription, windows,
                identity, error, identity, FieldPresenceFlags: shape);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                "PI Codex 请求超时", "独立超时", "pi_codex_timeout");
        }
        catch (InvalidOperationException exception)
        {
            var code = ProviderHttpErrors.Sanitize(exception);
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                "PI Codex 请求被拒绝", code, code);
        }
        catch (HttpRequestException)
        {
            return ProviderQuotaPresentation.Placeholder(settings, false, QuotaSlotStatus.Unavailable,
                "PI Codex 网络不可用", "独立失败", "pi_codex_network");
        }
    }
}
