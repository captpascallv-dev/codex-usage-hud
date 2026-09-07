using CodexUsageHud.Core;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace CodexUsageHud.App;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private string _collapsedText = "索引中…";
    private string _overviewText = "索引中…";
    private string _freshnessText = "索引中";
    private string _eventsText = "暂无事件";
    private string _thresholdText = "? 不可用";
    private string _remainingText = "—";
    private string _usedText = "不可用";
    private string _countdownText = "不可用";
    private string _countdownCompactText = "不可用";
    private string _resetTimeText = "官方时间不可用";
    private string _runningCountText = "0";
    private string _runningCycleTotalText = "本周期不可用";
    private string _runningCycleCompactText = "—";
    private string _cycleTotalText = "不可用";
    private string _cycleCompactText = "—";
    private string _lastUpdatedText = "尚未更新";
    private string _quotaSourceText = "官方额度不可用";
    private string _quotaStateText = "不可用";
    private string _quotaStateDetailText = "官方额度不可用";
    private string _visibleSessionCountText = "0 个会话";
    private string _primarySessionCountText = "0";
    private string _internalTaskCountText = "0";
    private string _appSessionCountText = "0";
    private string _cliSessionCountText = "0";
    private string _orphanTaskCountText = "0";
    private double _quotaRingValue;
    private bool _isExpanded;
    private bool _isRefreshing;
    private HudSnapshot? _snapshot;
    private IReadOnlyList<SessionDisplayRow> _frameRows = Array.Empty<SessionDisplayRow>();
    private IReadOnlyList<SessionDisplayRow> _rows = Array.Empty<SessionDisplayRow>();
    private readonly Dictionary<(string Filter, string Sort), IReadOnlyList<SessionDisplayRow>> _rowViews = new();
    private readonly HashSet<string> _expandedParents = new(StringComparer.Ordinal);
    private string _filter = "recent";
    private string _sort = "activity";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CollapsedText { get => _collapsedText; private set => SetField(ref _collapsedText, value); }
    public string OverviewText { get => _overviewText; private set => SetField(ref _overviewText, value); }
    public string FreshnessText { get => _freshnessText; private set => SetField(ref _freshnessText, value); }
    public string EventsText { get => _eventsText; private set => SetField(ref _eventsText, value); }
    public string ThresholdText { get => _thresholdText; private set => SetField(ref _thresholdText, value); }
    public string RemainingText { get => _remainingText; private set => SetField(ref _remainingText, value); }
    public string UsedText { get => _usedText; private set => SetField(ref _usedText, value); }
    public string CountdownText { get => _countdownText; private set => SetField(ref _countdownText, value); }
    public string CountdownCompactText { get => _countdownCompactText; private set => SetField(ref _countdownCompactText, value); }
    public string ResetTimeText { get => _resetTimeText; private set => SetField(ref _resetTimeText, value); }
    public string RunningCountText { get => _runningCountText; private set => SetField(ref _runningCountText, value); }
    public string RunningCycleTotalText { get => _runningCycleTotalText; private set => SetField(ref _runningCycleTotalText, value); }
    public string RunningCycleCompactText { get => _runningCycleCompactText; private set => SetField(ref _runningCycleCompactText, value); }
    public string CycleTotalText { get => _cycleTotalText; private set => SetField(ref _cycleTotalText, value); }
    public string CycleCompactText { get => _cycleCompactText; private set => SetField(ref _cycleCompactText, value); }
    public string LastUpdatedText { get => _lastUpdatedText; private set => SetField(ref _lastUpdatedText, value); }
    public string QuotaSourceText { get => _quotaSourceText; private set => SetField(ref _quotaSourceText, value); }
    public string QuotaStateText { get => _quotaStateText; private set => SetField(ref _quotaStateText, value); }
    public string QuotaStateDetailText { get => _quotaStateDetailText; private set => SetField(ref _quotaStateDetailText, value); }
    public string VisibleSessionCountText { get => _visibleSessionCountText; private set => SetField(ref _visibleSessionCountText, value); }
    public string PrimarySessionCountText { get => _primarySessionCountText; private set => SetField(ref _primarySessionCountText, value); }
    public string InternalTaskCountText { get => _internalTaskCountText; private set => SetField(ref _internalTaskCountText, value); }
    public string AppSessionCountText { get => _appSessionCountText; private set => SetField(ref _appSessionCountText, value); }
    public string CliSessionCountText { get => _cliSessionCountText; private set => SetField(ref _cliSessionCountText, value); }
    public string OrphanTaskCountText { get => _orphanTaskCountText; private set => SetField(ref _orphanTaskCountText, value); }
    public double QuotaRingValue { get => _quotaRingValue; private set => SetField(ref _quotaRingValue, value); }
    public bool IsRefreshing { get => _isRefreshing; set => SetField(ref _isRefreshing, value); }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public IReadOnlyList<SessionDisplayRow> Rows
    {
        get => _rows;
        private set => SetField(ref _rows, value);
    }
    public HudSnapshot? Snapshot => _snapshot;

    public void Apply(HudFrame frame)
    {
        _snapshot = frame.Snapshot;
        _frameRows = frame.Rows;
        _expandedParents.IntersectWith(_frameRows.Where(row => row.HasChildren).Select(row => row.ThreadId));
        _rowViews.Clear();
        CollapsedText = frame.CollapsedText;
        OverviewText = frame.OverviewText;
        FreshnessText = frame.FreshnessText;
        EventsText = frame.EventsText;
        ThresholdText = frame.ThresholdText;
        UpdateSummary(frame.Snapshot, frame.Snapshot.GeneratedAtUtc);
        RefreshRows();
    }

    public void Apply(HudSnapshot snapshot) => Apply(HudPresentation.BuildFrame(snapshot));

    public void Tick(DateTimeOffset nowUtc)
    {
        if (_snapshot is null) return;
        var timed = _snapshot with { GeneratedAtUtc = nowUtc };
        CollapsedText = HudPresentation.BuildCollapsedText(timed);
        var quota = HudPresentation.GetUsablePrimaryQuota(timed.Quota);
        var firstLine = quota is null
            ? "额度：不可用 · 官方重置时间：不可用 · 倒计时：不可用"
            : $"额度：已用 {quota.UsedPercent:0}% · 剩余 {quota.RemainingPercent:0}% · " +
              $"官方重置 {quota.ResetsAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss} · " +
              $"倒计时 {HudPresentation.FormatCountdown(quota.ResetsAtUtc - nowUtc)}";
        var separator = OverviewText.IndexOf('\n');
        OverviewText = separator < 0 ? firstLine : firstLine + OverviewText[separator..];
        UpdateSummary(_snapshot, nowUtc);
    }

    public void SetFilter(string filter)
    {
        SetView(filter, null);
    }

    public void SetSort(string sort)
    {
        SetView(null, sort);
    }

    public void SetView(string? filter, string? sort)
    {
        if (!string.IsNullOrWhiteSpace(filter)) _filter = filter;
        if (!string.IsNullOrWhiteSpace(sort)) _sort = sort;
        RefreshRows();
    }

    public bool ToggleChildren(string threadId)
    {
        if (!_frameRows.Any(row => row.ThreadId == threadId && row.HasChildren)) return false;
        var expanded = _expandedParents.Add(threadId);
        if (!expanded) _expandedParents.Remove(threadId);
        _rowViews.Clear();
        RefreshRows();
        return expanded;
    }

    public IReadOnlyList<string> RenderedStrings()
    {
        var values = new List<string>
        {
            CollapsedText, OverviewText, FreshnessText, EventsText, ThresholdText, RemainingText,
            UsedText, CountdownText, CountdownCompactText, ResetTimeText, RunningCountText,
            RunningCycleTotalText, RunningCycleCompactText, CycleTotalText, CycleCompactText, LastUpdatedText, QuotaSourceText,
            QuotaStateText, QuotaStateDetailText, VisibleSessionCountText,
            PrimarySessionCountText, InternalTaskCountText, AppSessionCountText,
            CliSessionCountText, OrphanTaskCountText,
        };
        foreach (var row in Rows)
        {
            values.AddRange(new[] { row.Name, row.ShortId, row.RoleNickname, row.Project, row.Model,
                row.Tier, row.Status, row.LastActivity, row.RecentUsageLabel,
                row.RecentUsageConfidence, row.CurrentLabel, row.CurrentTooltip,
                row.PostCompactionBaselineText, row.PostCompactionSourceText,
                row.BaselineTrendText, row.EffectiveRunwayText, row.HistoryVolumeText,
                row.StructuralGradeText, row.LifecycleLoadText, row.LifecycleRecentText,
                row.LifecycleRiskText, row.LifecycleRiskDetailText, row.DriftAssessmentText,
                row.DriftAssessmentDetailText,
                row.ContinuationGradeText, row.ContinuationAdviceText,
                row.ContinuationEvidenceText, row.SourceLabel, row.ParentSummaryText,
                row.ChildSummaryText, row.WorkBreakdownText, row.WorkTotalText });
        }
        return values;
    }

    private void RefreshRows()
    {
        if (_snapshot is null)
        {
            Rows = Array.Empty<SessionDisplayRow>();
            VisibleSessionCountText = "0 个会话";
            return;
        }
        var viewKey = (_filter, _sort);
        if (!_rowViews.TryGetValue(viewKey, out var visibleRows))
        {
            IEnumerable<SessionDisplayRow> roots = _filter switch
            {
                "running" => _frameRows.Where(row =>
                    (!row.IsInternalTask || row.IsOrphanInternalTask) && row.EffectiveIsRunning),
                "recent" => _frameRows.Where(row => !row.IsInternalTask),
                "internal" => _frameRows.Where(row => row.IsOrphanInternalTask),
                _ => _frameRows.Where(row => !row.IsInternalTask),
            };
            roots = SortRows(roots);
            if (_filter == "recent") roots = roots.Take(30);
            var children = _frameRows.Where(row => row.ParentThreadId is not null)
                .GroupBy(row => row.ParentThreadId!, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => SortRows(group).ToArray(), StringComparer.Ordinal);
            var flattened = new List<SessionDisplayRow>();
            foreach (var root in roots) AppendRow(root, children, flattened, new HashSet<string>(StringComparer.Ordinal));
            visibleRows = flattened;
            _rowViews[viewKey] = visibleRows;
        }
        Rows = visibleRows;
        var topLevelCount = Rows.Count(row => !row.IsInternalTask || row.IsOrphanInternalTask);
        var shownChildren = Rows.Count - topLevelCount;
        VisibleSessionCountText = shownChildren > 0
            ? $"{topLevelCount} 个会话 · 展开 {shownChildren} 个子任务"
            : $"{topLevelCount} 个会话";
        PrimarySessionCountText = _frameRows.Count(row => !row.IsInternalTask).ToString(CultureInfo.InvariantCulture);
        InternalTaskCountText = _frameRows.Count(row => row.IsInternalTask).ToString(CultureInfo.InvariantCulture);
        AppSessionCountText = _frameRows.Count(row => row.Surface == SessionSurface.App).ToString(CultureInfo.InvariantCulture);
        CliSessionCountText = _frameRows.Count(row => row.Surface == SessionSurface.Cli).ToString(CultureInfo.InvariantCulture);
        OrphanTaskCountText = _frameRows.Count(row => row.IsOrphanInternalTask).ToString(CultureInfo.InvariantCulture);
    }

    private IEnumerable<SessionDisplayRow> SortRows(IEnumerable<SessionDisplayRow> rows) => _sort == "activity"
        ? rows.OrderByDescending(row => row.IsPinned)
            .ThenByDescending(row => row.LastActivityUtc ?? DateTimeOffset.MinValue)
        : rows.OrderByDescending(row => row.IsPinned)
            .ThenByDescending(row => row.WorkTotal.Total);

    private void AppendRow(SessionDisplayRow row,
        IReadOnlyDictionary<string, SessionDisplayRow[]> children,
        ICollection<SessionDisplayRow> output, ISet<string> path)
    {
        if (!path.Add(row.ThreadId)) return;
        var expanded = row.HasChildren && _expandedParents.Contains(row.ThreadId);
        output.Add(row with { IsExpanded = expanded });
        if (expanded && children.TryGetValue(row.ThreadId, out var childRows))
        {
            foreach (var child in childRows) AppendRow(child, children, output, path);
        }
        path.Remove(row.ThreadId);
    }

    private void UpdateSummary(HudSnapshot snapshot, DateTimeOffset nowUtc)
    {
        var quota = HudPresentation.GetUsablePrimaryQuota(snapshot.Quota);
        var threshold = HudPresentation.GetThreshold(snapshot.Quota);
        if (quota is null)
        {
            RemainingText = "—";
            UsedText = "不可用";
            CountdownText = "不可用";
            CountdownCompactText = "不可用";
            ResetTimeText = "官方时间不可用";
            QuotaRingValue = 0;
        }
        else
        {
            var remaining = quota.ResetsAtUtc - nowUtc;
            RemainingText = $"{quota.RemainingPercent:0}%";
            UsedText = $"{quota.UsedPercent:0}%";
            CountdownText = HudPresentation.FormatCountdown(remaining);
            CountdownCompactText = FormatCompactCountdown(remaining);
            ResetTimeText = quota.ResetsAtUtc.ToLocalTime().ToString("MM月dd日 HH:mm");
            QuotaRingValue = quota.RemainingPercent;
        }

        RunningCountText = snapshot.Sessions.Count(HudPresentation.IsRunning).ToString();
        RunningCycleTotalText = snapshot.RunningCycleTotal.HasValue
            ? $"本周期 {TokenFormat.Compact(snapshot.RunningCycleTotal.Value.Total)} raw tokens"
            : "本周期不可用";
        RunningCycleCompactText = snapshot.RunningCycleTotal.HasValue
            ? FormatDialTokens(snapshot.RunningCycleTotal.Value.Total)
            : "—";
        CycleCompactText = snapshot.CycleTotal.HasValue
            ? FormatDialTokens(snapshot.CycleTotal.Value.Total)
            : "—";
        CycleTotalText = snapshot.CycleTotal.HasValue
            ? $"{CycleCompactText} raw tokens"
            : "不可用";
        LastUpdatedText = snapshot.GeneratedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        QuotaSourceText = quota is null
            ? "官方额度不可用"
            : snapshot.Quota.Source switch
            {
                QuotaSource.OfficialAppServer => snapshot.Quota.IsStale ? "官方额度 · 观测已陈旧" : "官方额度",
                QuotaSource.RolloutFallback => "本机最后观测",
                _ => "官方额度不可用",
            };
        QuotaStateText = threshold.Code switch
        {
            "critical" => "额度紧急",
            "warning" => "额度偏低",
            "normal" => "正常",
            "stale" => "数据陈旧",
            _ => "不可用",
        };
        QuotaStateDetailText = threshold.Text;
    }

    private static string FormatCompactCountdown(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero) return "已到期";
        if (remaining.Days > 0) return $"{remaining.Days}天{remaining.Hours}时";
        if (remaining.Hours > 0) return $"{remaining.Hours}时{remaining.Minutes}分";
        return $"{Math.Max(remaining.Minutes, 1)}分";
    }

    private static string FormatDialTokens(long value)
    {
        if (value >= 1_000_000_000)
            return $"{(value / 1_000_000_000d).ToString("0.00", CultureInfo.InvariantCulture)}B";
        if (value >= 100_000_000)
            return $"{(value / 1_000_000d).ToString("0", CultureInfo.InvariantCulture)}M";
        return TokenFormat.Compact(value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

// Display formatting only: precise counts remain available in the inspector
// and tooltips. All arithmetic continues to use the original token values.
public sealed class CompactTokenConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is long count ? TokenFormat.Compact(count) : "—";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}
