using CodexUsageHud.Core;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace CodexUsageHud.App;

public partial class MainWindow : Window, IDisposable
{
    private enum EdgeDock
    {
        None,
        Left,
        Right,
        Top,
    }

    private readonly UsageEngine _engine;
    private readonly Func<Task> _disposeRuntimeAsync;
    private readonly MainViewModel _viewModel = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly Forms.NotifyIcon _tray;
    private readonly Forms.ToolStripMenuItem _startupMenuItem;
    private readonly DispatcherTimer _countdownTimer;
    private readonly DispatcherTimer _hideTimer;
    private readonly DispatcherTimer _revealTimer;
    private Drawing.Icon? _ownedTrayIcon;
    private Task _backgroundLoopTask = Task.CompletedTask;
    private Task? _exitTask;
    private int _refreshPending;
    private int _manualRefreshPending;
    private int _driftWritePending;
    private DispatcherOperation? _viewChangeOperation;
    private string? _pendingFilter;
    private string? _pendingSort;
    private bool _exiting;
    private bool _disposed;
    private bool _settingsReady;
    private bool _isEdgeHidden;
    private bool _autoHide = true;
    private bool _restoreHiddenOnNextCompact;
    private bool _topmostPointerInvocation;
    private bool _isExpandedFullscreen;
    private EdgeDock _dockSide = EdgeDock.Right;
    private double _compactAxis = double.NaN;
    private Rect _expandedRestoreBounds = Rect.Empty;

    private const double CompactWidth = 112;
    private const double CompactHeight = 330;
    private const double CompactTopWidth = 520;
    private const double CompactTopHeight = 96;
    private const double ExpandedWidth = 1180;
    private const double ExpandedHeight = 720;
    private const double EdgeHandle = 9;
    private const double MinimumVisible = 48;
    private const double DockSnapDistance = 30;
    private const double PointerDockSnapDistance = 64;
    private const double WorkAreaMargin = 12;

    public MainWindow(UsageEngine engine, Func<Task> disposeRuntimeAsync,
        bool startRuntimeOnLoad = true)
    {
        InitializeComponent();
        _engine = engine;
        _disposeRuntimeAsync = disposeRuntimeAsync;
        DataContext = _viewModel;
        var tray = CreateTrayIcon();
        _tray = tray.Icon;
        _startupMenuItem = tray.StartupItem;

        _countdownTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = RefreshCadence.UiCountdown,
        };
        _countdownTimer.Tick += (_, _) => _viewModel.Tick(DateTimeOffset.UtcNow);
        _hideTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(520),
        };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            if (!IsMouseOver)
            {
                SetEdgeHidden(true);
                return;
            }

            // DragMove can swallow the first MouseLeave after a side snap. Keep a
            // low-frequency guard alive until the pointer really leaves the card.
            if (_autoHide && !_viewModel.IsExpanded && _dockSide != EdgeDock.None && IsVisible)
                _hideTimer.Start();
        };
        _revealTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(110),
        };
        _revealTimer.Tick += (_, _) =>
        {
            _revealTimer.Stop();
            SetEdgeHidden(false);
        };
        MouseEnter += OnWindowMouseEnter;
        MouseLeave += OnWindowMouseLeave;

        ApplyExpansionState();
        UpdateTopmostState();
        UpdateFullscreenState();
        if (startRuntimeOnLoad) Loaded += OnLoaded;
        Closing += OnClosing;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RestoreWindowStateAsync();
        ApplyExpansionState();
        EnsureVisibleOnCurrentScreens();
        _countdownTimer.Start();
        _backgroundLoopTask = BackgroundLoopAsync(_shutdown.Token);
        await RefreshAsync(true);
        UpdateTrayIconFromDial();
        ScheduleEdgeHide();
    }

    private async Task BackgroundLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(RefreshCadence.AppendScan);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await RefreshAsync(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            await WriteLogSafelyAsync("background_loop_failed");
        }
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) => await RefreshAsync(true);
    private void OnCloseToTray(object sender, RoutedEventArgs e) => Close();

    private async Task RefreshAsync(bool manualQuota)
    {
        if (_disposed || _exiting) return;
        Interlocked.Exchange(ref _refreshPending, 1);
        if (manualQuota) Interlocked.Exchange(ref _manualRefreshPending, 1);
        if (!await _refreshGate.WaitAsync(0)) return;
        try
        {
            while (Interlocked.Exchange(ref _refreshPending, 0) != 0 && !_disposed && !_exiting)
            {
                var manual = Interlocked.Exchange(ref _manualRefreshPending, 0) != 0;
                await RefreshCoreAsync(manual);
            }
        }
        finally
        {
            _refreshGate.Release();
            if (Volatile.Read(ref _refreshPending) != 0 && !_disposed && !_exiting)
                _ = Dispatcher.BeginInvoke(DispatcherPriority.Background,
                    new Action(() => _ = RefreshAsync(false)));
        }
    }

    private async Task RefreshCoreAsync(bool manualQuota)
    {
        string? selectedThreadId = null;
        await Dispatcher.InvokeAsync(() =>
        {
            selectedThreadId = (SessionGrid.SelectedItem as SessionDisplayRow)?.ThreadId;
            _viewModel.IsRefreshing = true;
            RefreshButton.IsEnabled = false;
            RefreshButton.ToolTip = "刷新中…";
        }, DispatcherPriority.Input);
        try
        {
            var frame = await _engine.RefreshAsync(manualQuota, _shutdown.Token);
            await Dispatcher.InvokeAsync(() =>
            {
                _viewModel.Apply(frame);
                if (selectedThreadId is not null)
                {
                    var selected = _viewModel.Rows.FirstOrDefault(row =>
                        string.Equals(row.ThreadId, selectedThreadId, StringComparison.Ordinal));
                    if (selected is not null) SessionGrid.SelectedItem = selected;
                }
                UpdateTextBlocks();
            }, DispatcherPriority.DataBind);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            if (!_disposed)
            {
                await WriteLogSafelyAsync("refresh_failed");
                await ApplyUnavailableAsync();
            }
        }
        finally
        {
            if (!_disposed)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    _viewModel.IsRefreshing = false;
                    RefreshButton.IsEnabled = true;
                    RefreshButton.ToolTip = "刷新额度与本地索引";
                }, DispatcherPriority.Input);
            }
        }
    }

    private async Task ApplyUnavailableAsync()
    {
        var snapshot = new HudSnapshot(
            new QuotaObservation(null, Array.Empty<QuotaBucket>(), QuotaSource.Unavailable,
                DateTimeOffset.UtcNow, false, "refresh_failed"),
            Array.Empty<SessionAggregate>(), null, DateTimeOffset.UtcNow, false,
            "来源暂时不可用", new[] { "refresh_failed" }, Array.Empty<HudEvent>());
        var frame = await Task.Run(() => HudPresentation.BuildFrame(snapshot));
        await Dispatcher.InvokeAsync(() =>
        {
            _viewModel.Apply(frame);
            UpdateTextBlocks();
        });
    }

    private void UpdateTextBlocks()
    {
        var threshold = HudPresentation.GetThreshold(_viewModel.Snapshot?.Quota ??
            new QuotaObservation(null, Array.Empty<QuotaBucket>(), QuotaSource.Unavailable,
                DateTimeOffset.UtcNow, false));
        var accent = threshold.Code switch
        {
            "critical" => System.Windows.Media.Color.FromRgb(255, 93, 103),
            "warning" => System.Windows.Media.Color.FromRgb(255, 200, 87),
            "normal" => System.Windows.Media.Color.FromRgb(121, 226, 178),
            _ => System.Windows.Media.Color.FromRgb(126, 138, 145),
        };
        var accentBrush = new SolidColorBrush(accent);
        if (accentBrush.CanFreeze) accentBrush.Freeze();
        var borderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(
            threshold.Code is "critical" or "warning" ? (byte)220 : (byte)150,
            accent.R, accent.G, accent.B));
        if (borderBrush.CanFreeze) borderBrush.Freeze();
        QuotaPercentText.Foreground = accentBrush;
        CompactQuotaStateText.Foreground = accentBrush;
        CompactTopRemainingText.Foreground = accentBrush;
        ExpandedRemainingText.Foreground = accentBrush;
        ThresholdStateText.Foreground = accentBrush;
        CompactVerticalShell.BorderBrush = borderBrush;
        CompactTopShell.BorderBrush = borderBrush;
        ExpandedShell.BorderBrush = borderBrush;
        if (SessionGrid.SelectedItem is null && SessionGrid.Items.Count > 0)
            SessionGrid.SelectedIndex = 0;
    }

    private void OnToggleExpand(object sender, RoutedEventArgs e)
    {
        _hideTimer.Stop();
        _revealTimer.Stop();
        if (_viewModel.IsExpanded)
        {
            ExitFullscreen(restoreBounds: false);
            _restoreHiddenOnNextCompact = _autoHide && _dockSide != EdgeDock.None;
            _viewModel.IsExpanded = false;
        }
        else
        {
            _restoreHiddenOnNextCompact = false;
            _isEdgeHidden = false;
            _viewModel.IsExpanded = true;
        }
        ApplyExpansionState(true);
        if (!_viewModel.IsExpanded) ScheduleEdgeHide();
    }

    private void OnTopmostPointerDown(object sender, MouseButtonEventArgs e) =>
        _topmostPointerInvocation = true;

    private async void OnToggleTopmost(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        UpdateTopmostState();
        if (_topmostPointerInvocation)
        {
            _topmostPointerInvocation = false;
            Keyboard.ClearFocus();
        }
        await SaveSettingsSafelyAsync(CaptureWindowSettings());
    }

    private async void OnMinimizeToTray(object sender, RoutedEventArgs e) =>
        await HideToTrayAsync(true);

    private void UpdateTopmostState()
    {
        if (PanelTopmostButton is null) return;
        ApplyTopmostButtonState(PanelTopmostButton);
        ApplyTopmostButtonState(CompactTopmostButton);
        ApplyTopmostButtonState(CompactTopTopmostButton);
        if (TopmostCheckBox is not null) TopmostCheckBox.IsChecked = Topmost;
    }

    private void ApplyTopmostButtonState(System.Windows.Controls.Button button)
    {
        button.Foreground = (System.Windows.Media.Brush)FindResource(Topmost ? "HudMint" : "HudMuted");
        button.Background = Topmost
            ? (System.Windows.Media.Brush)FindResource("HudSelected")
            : System.Windows.Media.Brushes.Transparent;
        button.BorderBrush = Topmost
            ? (System.Windows.Media.Brush)FindResource("HudMint")
            : System.Windows.Media.Brushes.Transparent;
        button.ToolTip = Topmost ? "取消窗口始终置顶" : "窗口始终置顶";
    }

    private void OnToggleFullscreen(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.IsExpanded) return;
        if (_isExpandedFullscreen)
        {
            ExitFullscreen(restoreBounds: true);
            return;
        }

        _expandedRestoreBounds = new Rect(Left, Top, Width, Height);
        _isExpandedFullscreen = true;
        ApplyFullscreenBounds();
        UpdateFullscreenState();
    }

    private void ApplyFullscreenBounds()
    {
        var work = GetWorkAreaLogical();
        WindowState = WindowState.Normal;
        Left = work.Left;
        Top = work.Top;
        Width = work.Width;
        Height = work.Height;
        ApplyExpandedResponsiveLayout(fullscreen: true, work.Width);
    }

    private void ExitFullscreen(bool restoreBounds)
    {
        if (!_isExpandedFullscreen) return;
        _isExpandedFullscreen = false;
        ApplyExpandedResponsiveLayout(fullscreen: false, ExpandedWidth);
        if (restoreBounds && !_expandedRestoreBounds.IsEmpty)
        {
            var work = GetWorkAreaLogical();
            Width = Math.Min(_expandedRestoreBounds.Width, work.Width - WorkAreaMargin * 2);
            Height = Math.Min(_expandedRestoreBounds.Height, work.Height - WorkAreaMargin * 2);
            Left = Clamp(_expandedRestoreBounds.Left, work.Left + WorkAreaMargin,
                work.Right - Width - WorkAreaMargin);
            Top = Clamp(_expandedRestoreBounds.Top, work.Top + WorkAreaMargin,
                work.Bottom - Height - WorkAreaMargin);
        }
        _expandedRestoreBounds = Rect.Empty;
        UpdateFullscreenState();
    }

    private void ApplyExpandedResponsiveLayout(bool fullscreen, double workWidth)
    {
        DetailColumn.Width = new GridLength(fullscreen
            ? Math.Min(420, Math.Max(360, workWidth * 0.28))
            : 330);
        DetailPanel.Padding = fullscreen ? new Thickness(17) : new Thickness(14);
        DetailContentGrid.LayoutTransform = fullscreen
            ? new ScaleTransform(1.08, 1.08)
            : Transform.Identity;
    }

    private void UpdateFullscreenState()
    {
        if (FullscreenButton is null) return;
        FullscreenButton.Content = _isExpandedFullscreen ? "\uE73F" : "\uE740";
        FullscreenButton.ToolTip = _isExpandedFullscreen ? "恢复窗口大小" : "全屏显示";
        FullscreenButton.Foreground = (System.Windows.Media.Brush)FindResource(
            _isExpandedFullscreen ? "HudMint" : "HudMuted");
        FullscreenButton.Background = _isExpandedFullscreen
            ? (System.Windows.Media.Brush)FindResource("HudSelected")
            : System.Windows.Media.Brushes.Transparent;
        FullscreenButton.BorderBrush = _isExpandedFullscreen
            ? (System.Windows.Media.Brush)FindResource("HudMint")
            : System.Windows.Media.Brushes.Transparent;
    }

    private void CollapseToCompact()
    {
        if (!_viewModel.IsExpanded) return;
        ExitFullscreen(restoreBounds: false);
        _viewModel.IsExpanded = false;
        ApplyExpansionState(true);
    }

    private void ShowCompact()
    {
        CollapseToCompact();
        _isEdgeHidden = false;
        Show();
        WindowState = WindowState.Normal;
        ApplyExpansionState();
        EnsureVisibleOnCurrentScreens();
        var preserveTopmost = Topmost;
        if (!preserveTopmost) Topmost = true;
        Activate();
        Focus();
        if (!preserveTopmost) Topmost = false;
        ScheduleEdgeHide();
    }

    internal void RestoreFromExternalActivation()
    {
        if (_disposed || _exiting) return;
        ShowCompact();
        _ = WriteLogSafelyAsync("instance_activation_received");
    }

    private async Task HideToTrayAsync(bool showBalloon)
    {
        CollapseToCompact();
        Hide();
        if (showBalloon)
        {
            _tray.ShowBalloonTip(1500, "Codex Usage HUD",
                "已最小化到系统托盘；双击托盘图标或再次启动程序可唤回。",
                Forms.ToolTipIcon.Info);
        }
        await SaveSettingsSafelyAsync(CaptureWindowSettings());
    }

    private void ApplyExpansionState(bool preserveCompactPosition = false)
    {
        if (!IsLoaded && PresentationSource.FromVisual(this) is null)
        {
            Width = _viewModel.IsExpanded ? ExpandedWidth : CompactWidth;
            Height = _viewModel.IsExpanded ? ExpandedHeight : CompactHeight;
            ExpandedShell.Visibility = _viewModel.IsExpanded ? Visibility.Visible : Visibility.Collapsed;
            CompactVerticalShell.Visibility = _viewModel.IsExpanded ? Visibility.Collapsed : Visibility.Visible;
            CompactTopShell.Visibility = Visibility.Collapsed;
            return;
        }

        var work = GetWorkAreaLogical();
        if (_viewModel.IsExpanded)
        {
            if (!preserveCompactPosition || !double.IsFinite(_compactAxis))
                _compactAxis = _dockSide == EdgeDock.Top ? Left : Top;
            _isEdgeHidden = false;
            ExpandedShell.Visibility = Visibility.Visible;
            CompactVerticalShell.Visibility = Visibility.Collapsed;
            CompactTopShell.Visibility = Visibility.Collapsed;
            Width = Math.Min(ExpandedWidth, Math.Max(720, work.Width - WorkAreaMargin * 2));
            Height = Math.Min(ExpandedHeight, Math.Max(520, work.Height - WorkAreaMargin * 2));
            ApplyExpandedResponsiveLayout(fullscreen: false, work.Width);
            switch (_dockSide)
            {
                case EdgeDock.Left:
                    Left = work.Left + WorkAreaMargin;
                    Top = Clamp(_compactAxis, work.Top + WorkAreaMargin, work.Bottom - Height - WorkAreaMargin);
                    break;
                case EdgeDock.Right:
                    Left = work.Right - Width - WorkAreaMargin;
                    Top = Clamp(_compactAxis, work.Top + WorkAreaMargin, work.Bottom - Height - WorkAreaMargin);
                    break;
                case EdgeDock.Top:
                    Left = Clamp(_compactAxis, work.Left + WorkAreaMargin, work.Right - Width - WorkAreaMargin);
                    Top = work.Top + WorkAreaMargin;
                    break;
                default:
                    Left = Clamp(Left, work.Left + WorkAreaMargin, work.Right - Width - WorkAreaMargin);
                    Top = Clamp(Top, work.Top + WorkAreaMargin, work.Bottom - Height - WorkAreaMargin);
                    break;
            }
            BeginShellFade(ExpandedShell);
            if (SessionGrid.SelectedItem is null && SessionGrid.Items.Count > 0)
                SessionGrid.SelectedIndex = 0;
        }
        else
        {
            SettingsPanel.Visibility = Visibility.Collapsed;
            DiagnosticsPanel.Visibility = Visibility.Collapsed;
            ExpandedShell.Visibility = Visibility.Collapsed;
            var topDock = _dockSide == EdgeDock.Top;
            CompactTopShell.Visibility = topDock ? Visibility.Visible : Visibility.Collapsed;
            CompactVerticalShell.Visibility = topDock ? Visibility.Collapsed : Visibility.Visible;
            Width = topDock ? CompactTopWidth : CompactWidth;
            Height = topDock ? CompactTopHeight : CompactHeight;
            var restoreHidden = _restoreHiddenOnNextCompact && _autoHide && _dockSide != EdgeDock.None;
            _restoreHiddenOnNextCompact = false;
            _isEdgeHidden = restoreHidden;
            PlaceCompact(work, hidden: restoreHidden);
            if (!restoreHidden)
                BeginShellFade(topDock ? CompactTopShell : CompactVerticalShell);
        }
    }

    private static void BeginShellFade(UIElement shell)
    {
        shell.BeginAnimation(OpacityProperty, null);
        shell.Opacity = 0.86;
        shell.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        }, HandoffBehavior.SnapshotAndReplace);
    }

    private void PlaceCompact(Rect work, bool hidden)
    {
        var axis = double.IsFinite(_compactAxis)
            ? _compactAxis
            : _dockSide == EdgeDock.Top ? work.Left + (work.Width - Width) / 2 : work.Top + 80;
        switch (_dockSide)
        {
            case EdgeDock.Left:
                Left = hidden ? work.Left - Width + EdgeHandle : work.Left;
                Top = Clamp(axis, work.Top + 6, work.Bottom - Height - 6);
                _compactAxis = Top;
                break;
            case EdgeDock.Right:
                Left = hidden ? work.Right - EdgeHandle : work.Right - Width;
                Top = Clamp(axis, work.Top + 6, work.Bottom - Height - 6);
                _compactAxis = Top;
                break;
            case EdgeDock.Top:
                Left = Clamp(axis, work.Left + 6, work.Right - Width - 6);
                Top = hidden ? work.Top - Height + EdgeHandle : work.Top;
                _compactAxis = Left;
                break;
            default:
                Left = Clamp(Left, work.Left, work.Right - Width);
                Top = Clamp(Top, work.Top, work.Bottom - Height);
                break;
        }
    }

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.IsExpanded)
        {
            _viewModel.IsExpanded = true;
            ApplyExpansionState(true);
        }
        DiagnosticsPanel.Visibility = Visibility.Collapsed;
        SyncSettingsControls();
        SettingsPanel.Visibility = Visibility.Visible;
    }

    private void OnCloseSettings(object sender, RoutedEventArgs e) =>
        SettingsPanel.Visibility = Visibility.Collapsed;

    private void SyncSettingsControls()
    {
        _settingsReady = false;
        StartupCheckBox.IsChecked = IsStartupEnabledSafely();
        TopmostCheckBox.IsChecked = Topmost;
        AutoHideCheckBox.IsChecked = _autoHide;
        DockSideComboBox.SelectedIndex = _dockSide switch
        {
            EdgeDock.Right => 0,
            EdgeDock.Left => 1,
            EdgeDock.Top => 2,
            _ => 3,
        };
        SettingsStatusText.Text = string.Empty;
        _settingsReady = true;
    }

    private async void OnStartupSettingClick(object sender, RoutedEventArgs e)
    {
        if (!_settingsReady) return;
        var enable = StartupCheckBox.IsChecked == true;
        StartupCheckBox.IsEnabled = false;
        try
        {
            await Task.Run(() => StartupRegistration.SetEnabled(enable));
            _startupMenuItem.Checked = enable;
            SettingsStatusText.Text = enable ? "已启用开机启动" : "已关闭开机启动";
        }
        catch
        {
            StartupCheckBox.IsChecked = IsStartupEnabledSafely();
            SettingsStatusText.Text = "无法修改开机启动设置";
        }
        finally
        {
            StartupCheckBox.IsEnabled = true;
        }
    }

    private async void OnTopmostSettingClick(object sender, RoutedEventArgs e)
    {
        if (!_settingsReady) return;
        Topmost = TopmostCheckBox.IsChecked == true;
        UpdateTopmostState();
        await SaveSettingsSafelyAsync(CaptureWindowSettings());
    }

    private async void OnAutoHideSettingClick(object sender, RoutedEventArgs e)
    {
        if (!_settingsReady) return;
        _autoHide = AutoHideCheckBox.IsChecked == true;
        if (!_autoHide) SetEdgeHidden(false);
        await SaveSettingsSafelyAsync(CaptureWindowSettings());
        if (_autoHide) ScheduleEdgeHide();
    }

    private async void OnDockSideSettingChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_settingsReady || DockSideComboBox.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        _dockSide = ParseDock(tag);
        _isEdgeHidden = false;
        ApplyExpansionState(true);
        await SaveSettingsSafelyAsync(CaptureWindowSettings());
    }

    private async void OnCreateShortcut(object sender, RoutedEventArgs e)
    {
        SettingsStatusText.Text = "正在创建桌面快捷方式…";
        try
        {
            var path = await Task.Run(DesktopShortcutRegistration.CreateOrRepair);
            SettingsStatusText.Text = $"快捷方式已就绪：{Path.GetFileName(path)}";
        }
        catch
        {
            SettingsStatusText.Text = "创建快捷方式失败";
        }
    }

    private async void OnRemoveShortcut(object sender, RoutedEventArgs e)
    {
        try
        {
            var removed = await Task.Run(DesktopShortcutRegistration.Remove);
            SettingsStatusText.Text = removed ? "已移除桌面快捷方式" : "桌面快捷方式不存在";
        }
        catch
        {
            SettingsStatusText.Text = "移除快捷方式失败";
        }
    }

    private void OnFilterTabChecked(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton { Tag: string filter })
            QueueViewChange(filter, null);
    }

    private void OnSortChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.ComboBox { SelectedItem: ComboBoxItem { Tag: string sort } })
            QueueViewChange(null, sort);
    }

    private void QueueViewChange(string? filter, string? sort)
    {
        if (filter is not null) _pendingFilter = filter;
        if (sort is not null) _pendingSort = sort;
        if (_viewChangeOperation is { Status: DispatcherOperationStatus.Pending }) return;

        _viewChangeOperation = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            var pendingFilter = _pendingFilter;
            var pendingSort = _pendingSort;
            _pendingFilter = null;
            _pendingSort = null;
            _viewChangeOperation = null;
            _viewModel.SetView(pendingFilter, pendingSort);
            if (SessionGrid is not null)
                SessionGrid.SelectedIndex = SessionGrid.Items.Count > 0 ? 0 : -1;
        }));
    }

    private void OnToggleDiagnostics(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = Visibility.Collapsed;
        DiagnosticsPanel.Visibility = Visibility.Visible;
    }

    private void OnCloseDiagnostics(object sender, RoutedEventArgs e) =>
        DiagnosticsPanel.Visibility = Visibility.Collapsed;

    private void OnToggleSessionChildren(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { DataContext: SessionDisplayRow row }) return;
        _viewModel.ToggleChildren(row.ThreadId);
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            var selected = _viewModel.Rows.FirstOrDefault(item =>
                string.Equals(item.ThreadId, row.ThreadId, StringComparison.Ordinal));
            if (selected is not null) SessionGrid.SelectedItem = selected;
        }));
        e.Handled = true;
    }

    private async void OnTogglePin(object sender, RoutedEventArgs e)
    {
        if (SessionGrid.SelectedItem is not SessionDisplayRow row) return;
        try
        {
            await _engine.SetPinnedThreadAsync(row.IsPinned ? null : row.ThreadId);
        }
        catch (Exception)
        {
            await WriteLogSafelyAsync("pin_write_failed");
            return;
        }
        await RefreshAsync(false);
    }

    private async void OnSetDriftAssessment(object sender, RoutedEventArgs e)
    {
        if (SessionGrid.SelectedItem is not SessionDisplayRow row ||
            sender is not System.Windows.Controls.Button { Tag: string tag } ||
            Interlocked.Exchange(ref _driftWritePending, 1) != 0) return;
        var level = tag switch
        {
            "occasional" => DriftAssessmentLevel.Occasional,
            "repeated" => DriftAssessmentLevel.Repeated,
            _ => DriftAssessmentLevel.Unassessed,
        };
        SetDriftButtonsEnabled(false);
        try
        {
            await _engine.SetSessionDriftAssessmentAsync(row.ThreadId, level);
            await RefreshAsync(false);
        }
        catch (Exception)
        {
            await WriteLogSafelyAsync("drift_assessment_write_failed");
        }
        finally
        {
            Interlocked.Exchange(ref _driftWritePending, 0);
            if (!_disposed) SetDriftButtonsEnabled(true);
        }
    }

    private void SetDriftButtonsEnabled(bool enabled)
    {
        DriftUnassessedButton.IsEnabled = enabled;
        DriftOccasionalButton.IsEnabled = enabled;
        DriftRepeatedButton.IsEnabled = enabled;
    }

    private async void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || IsInteractiveSource(e.OriginalSource as DependencyObject))
            return;
        if (_isExpandedFullscreen) return;
        _hideTimer.Stop();
        _revealTimer.Stop();
        if (_isEdgeHidden) SetEdgeHidden(false);
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        var work = GetWorkAreaLogical();
        if (!_viewModel.IsExpanded)
        {
            _dockSide = ResolveDockSide(work, new Rect(Left, Top, Width, Height), GetCursorLogical());
            _compactAxis = _dockSide == EdgeDock.Top ? Left : Top;
            ApplyExpansionState(true);
            ScheduleEdgeHide();
        }
        await SaveSettingsSafelyAsync(CaptureWindowSettings());
    }

    private static bool IsInteractiveSource(DependencyObject? source)
    {
        for (var current = source; current is not null; current = GetParent(current))
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase or Selector or
                System.Windows.Controls.Primitives.ScrollBar or DataGridColumnHeader or DataGridCell)
                return true;
            if (current is MainWindow) break;
        }
        return false;
    }

    private static DependencyObject? GetParent(DependencyObject current) => current switch
    {
        Visual or System.Windows.Media.Media3D.Visual3D => VisualTreeHelper.GetParent(current),
        FrameworkContentElement content => content.Parent,
        _ => LogicalTreeHelper.GetParent(current),
    };

    private System.Windows.Point GetCursorLogical()
    {
        var cursor = Forms.Cursor.Position;
        if (PresentationSource.FromVisual(this)?.CompositionTarget is not { } target)
            return new System.Windows.Point(cursor.X, cursor.Y);
        return target.TransformFromDevice.Transform(new System.Windows.Point(cursor.X, cursor.Y));
    }

    private static EdgeDock ResolveDockSide(Rect work, Rect windowBounds, System.Windows.Point pointer)
    {
        var pointerDistances = new[]
        {
            (Side: EdgeDock.Left, Distance: pointer.Y >= work.Top - PointerDockSnapDistance &&
                pointer.Y <= work.Bottom + PointerDockSnapDistance
                    ? Math.Abs(pointer.X - work.Left) : double.PositiveInfinity),
            (Side: EdgeDock.Right, Distance: pointer.Y >= work.Top - PointerDockSnapDistance &&
                pointer.Y <= work.Bottom + PointerDockSnapDistance
                    ? Math.Abs(pointer.X - work.Right) : double.PositiveInfinity),
            (Side: EdgeDock.Top, Distance: pointer.X >= work.Left - PointerDockSnapDistance &&
                pointer.X <= work.Right + PointerDockSnapDistance
                    ? Math.Abs(pointer.Y - work.Top) : double.PositiveInfinity),
        };
        var pointerMatch = pointerDistances.OrderBy(item => item.Distance).First();
        if (pointerMatch.Distance <= PointerDockSnapDistance)
            return pointerMatch.Side;

        var windowDistances = new[]
        {
            (Side: EdgeDock.Left, Distance: Math.Abs(windowBounds.Left - work.Left)),
            (Side: EdgeDock.Right, Distance: Math.Abs(windowBounds.Right - work.Right)),
            (Side: EdgeDock.Top, Distance: Math.Abs(windowBounds.Top - work.Top)),
        };
        var windowMatch = windowDistances.OrderBy(item => item.Distance).First();
        return windowMatch.Distance <= DockSnapDistance ? windowMatch.Side : EdgeDock.None;
    }

    private void OnWindowMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _hideTimer.Stop();
        if (_isEdgeHidden)
        {
            _revealTimer.Stop();
            _revealTimer.Start();
        }
    }

    private void OnWindowMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _revealTimer.Stop();
        ScheduleEdgeHide();
    }

    private void ScheduleEdgeHide()
    {
        _hideTimer.Stop();
        if (_autoHide && !_viewModel.IsExpanded && _dockSide != EdgeDock.None && IsVisible)
            _hideTimer.Start();
    }

    private void SetEdgeHidden(bool hidden)
    {
        if (_viewModel.IsExpanded || _dockSide == EdgeDock.None || !_autoHide)
            hidden = false;
        if (_isEdgeHidden == hidden) return;
        _isEdgeHidden = hidden;
        PlaceCompact(GetWorkAreaLogical(), hidden);
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_exiting)
        {
            e.Cancel = true;
            await HideToTrayAsync(true);
        }
    }

    private (Forms.NotifyIcon Icon, Forms.ToolStripMenuItem StartupItem) CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示悬浮卡片 / 隐藏到托盘", null, async (_, _) =>
        {
            if (IsVisible) await HideToTrayAsync(false); else ShowCompact();
        });
        menu.Items.Add("刷新", null, async (_, _) => await RefreshAsync(true));
        menu.Items.Add("设置", null, (_, _) =>
        {
            ShowCompact();
            OnSettings(this, new RoutedEventArgs());
        });
        menu.Items.Add("创建 / 修复桌面快捷方式", null, (_, _) =>
        {
            try
            {
                DesktopShortcutRegistration.CreateOrRepair();
                _tray.ShowBalloonTip(1500, "Codex Usage HUD", "桌面快捷方式已就绪", Forms.ToolTipIcon.Info);
            }
            catch
            {
                _tray.ShowBalloonTip(1500, "Codex Usage HUD", "创建桌面快捷方式失败", Forms.ToolTipIcon.Warning);
            }
        });
        var startupItem = new Forms.ToolStripMenuItem("随 Windows 登录启动")
        {
            Checked = IsStartupEnabledSafely(),
        };
        startupItem.Click += OnToggleStartup;
        menu.Items.Add(startupItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, async (_, _) => await ExitFromTrayAsync());
        var icon = new Forms.NotifyIcon
        {
            Icon = Drawing.SystemIcons.Application,
            Visible = true,
            Text = "Codex Usage HUD",
            ContextMenuStrip = menu,
        };
        menu.Opening += (_, _) => startupItem.Checked = IsStartupEnabledSafely();
        icon.DoubleClick += (_, _) => ShowCompact();
        return (icon, startupItem);
    }

    private async void OnToggleStartup(object? sender, EventArgs e)
    {
        var enable = !_startupMenuItem.Checked;
        try
        {
            await Task.Run(() => StartupRegistration.SetEnabled(enable));
            _startupMenuItem.Checked = enable;
            _tray.ShowBalloonTip(1800, "Codex Usage HUD",
                enable ? "已设置为登录 Windows 后自动启动" : "已取消登录 Windows 后自动启动",
                Forms.ToolTipIcon.Info);
        }
        catch
        {
            _startupMenuItem.Checked = IsStartupEnabledSafely();
            _tray.ShowBalloonTip(1800, "Codex Usage HUD", "无法修改开机启动设置",
                Forms.ToolTipIcon.Warning);
        }
    }

    private static bool IsStartupEnabledSafely()
    {
        try { return StartupRegistration.IsEnabled(); }
        catch { return false; }
    }

    private void UpdateTrayIconFromDial()
    {
        try
        {
            const int renderSize = 64;
            const int iconSize = 32;
            CompactVerticalShell.UpdateLayout();
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                var brush = new VisualBrush(CompactVerticalShell)
                {
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                    Stretch = Stretch.Uniform,
                };
                context.DrawRectangle(brush, null, new Rect(0, 0, renderSize, renderSize));
            }
            var rendered = new RenderTargetBitmap(renderSize, renderSize, 96, 96, PixelFormats.Pbgra32);
            rendered.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rendered));
            using var png = new MemoryStream();
            encoder.Save(png);
            png.Position = 0;
            using var source = new Drawing.Bitmap(png);
            using var scaled = new Drawing.Bitmap(source, new Drawing.Size(iconSize, iconSize));
            var handle = scaled.GetHicon();
            try
            {
                var next = (Drawing.Icon)Drawing.Icon.FromHandle(handle).Clone();
                var previous = _ownedTrayIcon;
                _ownedTrayIcon = next;
                _tray.Icon = next;
                previous?.Dispose();
            }
            finally
            {
                _ = DestroyIcon(handle);
            }
        }
        catch
        {
            _ = WriteLogSafelyAsync("tray_icon_render_failed");
        }
    }

    private async Task ExitFromTrayAsync()
    {
        _exitTask ??= ExitCoreAsync();
        await _exitTask;
    }

    internal Task RequestQualityAssuranceExitAsync() => ExitFromTrayAsync();

    private async Task ExitCoreAsync()
    {
        _exiting = true;
        _hideTimer.Stop();
        _revealTimer.Stop();
        var finalSettings = CaptureWindowSettings();
        var failed = false;
        try { await _engine.StopAcceptingAppIoAsync(); }
        catch (Exception) { failed = true; }
        _countdownTimer.Stop();
        _shutdown.Cancel();
        try { await _backgroundLoopTask; }
        catch (OperationCanceledException) { }
        catch (Exception) { failed = true; }
        try
        {
            await _refreshGate.WaitAsync();
            _refreshGate.Release();
        }
        catch (ObjectDisposedException) { }
        try { await _engine.CompleteAppIoAsync(finalSettings); }
        catch (Exception) { failed = true; }
        try { DisposeResources(); }
        catch (Exception) { failed = true; }
        try { await _disposeRuntimeAsync(); }
        catch (Exception) { failed = true; }
        if (failed) Environment.ExitCode = 5;
        System.Windows.Application.Current.Shutdown();
    }

    private async Task RestoreWindowStateAsync()
    {
        IReadOnlyDictionary<string, string?> settings;
        try
        {
            settings = await _engine.LoadSettingsAsync("always_on_top", "window_left", "window_top",
                "dock_side", "auto_hide");
        }
        catch (Exception)
        {
            await WriteLogSafelyAsync("settings_load_failed");
            return;
        }
        _viewModel.IsExpanded = false;
        _isExpandedFullscreen = false;
        _expandedRestoreBounds = Rect.Empty;
        Topmost = settings.GetValueOrDefault("always_on_top") == "1";
        _dockSide = ParseDock(settings.GetValueOrDefault("dock_side"));
        if (!settings.ContainsKey("dock_side")) _dockSide = EdgeDock.Right;
        _autoHide = settings.GetValueOrDefault("auto_hide") != "0";
        UpdateTopmostState();
        if (double.TryParse(settings.GetValueOrDefault("window_left"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var left) &&
            double.TryParse(settings.GetValueOrDefault("window_top"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var top) && IsSafePosition(left, top))
        {
            Left = left;
            Top = top;
            _compactAxis = _dockSide == EdgeDock.Top ? left : top;
        }
        SyncSettingsControls();
    }

    private static EdgeDock ParseDock(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "left" => EdgeDock.Left,
        "top" => EdgeDock.Top,
        "none" => EdgeDock.None,
        _ => EdgeDock.Right,
    };

    private static string DockText(EdgeDock value) => value switch
    {
        EdgeDock.Left => "left",
        EdgeDock.Top => "top",
        EdgeDock.None => "none",
        _ => "right",
    };

    private static bool IsSafePosition(double left, double top) =>
        double.IsFinite(left) && double.IsFinite(top) &&
        left >= SystemParameters.VirtualScreenLeft - (CompactTopWidth - MinimumVisible) &&
        top >= SystemParameters.VirtualScreenTop - (CompactHeight - MinimumVisible) &&
        left <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - MinimumVisible &&
        top <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - MinimumVisible;

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (_disposed || _exiting) return;
        try
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                new Action(() =>
                {
                    if (_disposed || _exiting) return;
                    if (_isExpandedFullscreen) ApplyFullscreenBounds();
                    else EnsureVisibleOnCurrentScreens();
                }));
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void EnsureVisibleOnCurrentScreens()
    {
        if (!IsLoaded || PresentationSource.FromVisual(this)?.CompositionTarget is not { } target) return;
        var deviceOrigin = PointToScreen(new System.Windows.Point(0, 0));
        var toDevice = target.TransformToDevice;
        var deviceWidth = Math.Max(1, (int)Math.Ceiling(Width * Math.Abs(toDevice.M11)));
        var deviceHeight = Math.Max(1, (int)Math.Ceiling(Height * Math.Abs(toDevice.M22)));
        var minimum = _isEdgeHidden ? EdgeHandle : MinimumVisible;
        var minimumWidth = Math.Max(1, (int)Math.Ceiling(minimum * Math.Abs(toDevice.M11)));
        var minimumHeight = Math.Max(1, (int)Math.Ceiling(minimum * Math.Abs(toDevice.M22)));
        var bounds = new Drawing.Rectangle((int)Math.Floor(deviceOrigin.X), (int)Math.Floor(deviceOrigin.Y),
            deviceWidth, deviceHeight);
        var workAreas = Forms.Screen.AllScreens.Select(screen => screen.WorkingArea).ToArray();
        if (HasUsableScreenIntersection(bounds, workAreas, minimumWidth, minimumHeight)) return;
        _dockSide = EdgeDock.Right;
        _compactAxis = double.NaN;
        _isEdgeHidden = false;
        ApplyExpansionState();
    }

    private static bool HasUsableScreenIntersection(Drawing.Rectangle bounds,
        IReadOnlyList<Drawing.Rectangle> workAreas, int minimumWidth, int minimumHeight)
    {
        if (minimumWidth <= 0 || minimumHeight <= 0) return false;
        foreach (var workArea in workAreas)
        {
            var intersection = Drawing.Rectangle.Intersect(bounds, workArea);
            if (intersection.Width >= minimumWidth && intersection.Height >= minimumHeight) return true;
        }
        return false;
    }

    private Rect GetWorkAreaLogical()
    {
        var screen = new WindowInteropHelper(this).Handle is { } handle && handle != IntPtr.Zero
            ? Forms.Screen.FromHandle(handle)
            : Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens.First();
        if (PresentationSource.FromVisual(this)?.CompositionTarget is not { } target)
            return SystemParameters.WorkArea;
        var topLeft = target.TransformFromDevice.Transform(
            new System.Windows.Point(screen.WorkingArea.Left, screen.WorkingArea.Top));
        var bottomRight = target.TransformFromDevice.Transform(
            new System.Windows.Point(screen.WorkingArea.Right, screen.WorkingArea.Bottom));
        return new Rect(topLeft, bottomRight);
    }

    private Dictionary<string, string> CaptureWindowSettings()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["always_on_top"] = Topmost ? "1" : "0",
            ["dock_side"] = DockText(_dockSide),
            ["auto_hide"] = _autoHide ? "1" : "0",
        };
        var left = _dockSide == EdgeDock.Top && double.IsFinite(_compactAxis) ? _compactAxis : Left;
        var top = _dockSide is EdgeDock.Left or EdgeDock.Right && double.IsFinite(_compactAxis) ? _compactAxis : Top;
        if (IsSafePosition(left, top))
        {
            values["window_left"] = left.ToString(CultureInfo.InvariantCulture);
            values["window_top"] = top.ToString(CultureInfo.InvariantCulture);
        }
        return values;
    }

    private async Task SaveSettingsSafelyAsync(IReadOnlyDictionary<string, string> values)
    {
        try { await _engine.SaveSettingsAsync(values); }
        catch (Exception) { await WriteLogSafelyAsync("settings_write_failed"); }
    }

    private async Task WriteLogSafelyAsync(string code)
    {
        try { await _engine.WriteLogCodeAsync(code); }
        catch (Exception) { }
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        if (maximum < minimum) return minimum;
        if (!double.IsFinite(value)) return minimum;
        return Math.Max(minimum, Math.Min(maximum, value));
    }

    public void DisposeResources()
    {
        if (_disposed) return;
        _disposed = true;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _countdownTimer.Stop();
        _hideTimer.Stop();
        _revealTimer.Stop();
        _shutdown.Cancel();
        _tray.Visible = false;
        _tray.Dispose();
        _ownedTrayIcon?.Dispose();
        _ownedTrayIcon = null;
        _shutdown.Dispose();
        _refreshGate.Dispose();
    }

    public void Dispose() => DisposeResources();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
