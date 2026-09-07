using CodexUsageHud.App;
using CodexUsageHud.Core;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CodexUsageHud.Tests;

internal static class Program
{
    private const string Sentinel = "PRIVACY_SENTINEL_7F2A9C4E1D8B6A30";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("--real-check", StringComparison.Ordinal))
            return RunRealCheck(args.Skip(1).ToArray());
        if (args.Length > 0 && args[0].Equals("--quota-check", StringComparison.Ordinal))
            return RunQuotaCheck().GetAwaiter().GetResult();
        if (args.Length > 0 && args[0].Equals("--lineage-cycle-check", StringComparison.Ordinal))
            return RunLineageCycleCheck(args.Skip(1).ToArray());
        if (args.Length > 0 && args[0].Equals("--refresh-diagnostic", StringComparison.Ordinal))
            return RunRefreshDiagnostic(args.Skip(1).ToArray());
        if (args.Length > 0 && args[0].Equals("--gate-helper", StringComparison.Ordinal))
            return RunGateHelper(args.Skip(1).ToArray());
        if (args.Length > 0 && args[0].Equals("--correction-03-check", StringComparison.Ordinal))
            return RunCorrection03Check();
        if (args.Length > 0 && args[0].Equals("--correction-04-check", StringComparison.Ordinal))
            return RunCorrection04Check();
        if (args.Length > 0 && args[0].Equals("--migration-crash-helper", StringComparison.Ordinal))
            return RunMigrationCrashHelper(args.Skip(1).ToArray());
        if (args.Length > 0 && args[0].Equals("--app-server-helper", StringComparison.Ordinal))
            return RunAppServerHelper(args.Skip(1).ToArray());
        if (args.Length > 0 && args[0].Equals("--ui-capture", StringComparison.Ordinal))
            return RunUiCapture(args.Skip(1).ToArray());
        if (args.Length > 0 && args[0].Equals("--database-restart-check", StringComparison.Ordinal))
            return RunDatabaseRestartCheck(args.Skip(1).ToArray());
        if (args.Length > 0 && args[0].Equals("--database-readonly-status", StringComparison.Ordinal))
            return RunDatabaseReadonlyStatus(args.Skip(1).ToArray());
        if (args.Length > 0 && args[0].Equals("--context-readonly-diagnostic", StringComparison.Ordinal))
            return RunContextReadonlyDiagnostic(args.Skip(1).ToArray());
        if (args.Length > 0 && args[0].Equals("--continuation-risk-check", StringComparison.Ordinal))
            return RunContinuationRiskCheck(args.Skip(1).ToArray());
        if (args.Length > 0 && args[0].Equals("--set-drift-assessment", StringComparison.Ordinal))
            return RunSetDriftAssessment(args.Skip(1).ToArray());
        if (args.Length > 0 && args[0].Equals("--source-hierarchy-check", StringComparison.Ordinal))
            return RunSourceHierarchyCheck(args.Skip(1).ToArray());

        var runRoot = NewRunRoot("test-run");
        var tests = new (string Name, Action Run)[]
        {
            ("token_math_and_overflow", TokenMathAndOverflow),
            ("database_saturating_aggregates_before_after_restart", () => DatabaseSaturatingAggregates(runRoot)),
            ("fixture_a_cumulative_equality", () => FixtureACumulativeEquality(runRoot)),
            ("fixture_b_exact_duplicate_and_stale_replay", () => FixtureBReplay(runRoot)),
            ("concurrent_interleaving_tuple_oracle", () => ConcurrentInterleaving(runRoot)),
            ("old_frontier_migration_recovers_unique", () => OldFrontierMigration(runRoot)),
            ("rotation_partial_truncate_restart", () => RotationPartialRestart(runRoot)),
            ("windows_file_id_128_survives_move", () => WindowsFileIdentity(runRoot)),
            ("same_length_rewrite_is_rescanned", () => SameLengthRewrite(runRoot)),
            ("malformed_batch_continues_to_tail", () => MalformedBatchContinues(runRoot)),
            ("privacy_early_drain_and_all_sinks", () => PrivacyEarlyDrain(runRoot)),
            ("session_index_stream_and_malformed_continuation", () => SessionIndexStreaming(runRoot)),
            ("metadata_unix_time_and_row_continuation", () => MetadataUnixAndContinuation(runRoot)),
            ("metadata_query_allowlist", MetadataQueryAllowlist),
            ("session_source_classification_and_persistence", () => SessionSourceClassification(runRoot)),
            ("session_hierarchy_rollup_expansion_and_orphans", SessionHierarchyRollupAndExpansion),
            ("rollout_path_normalization", () => RolloutPathNormalization(runRoot)),
            ("implicit_turns_persist_across_restart", () => ImplicitTurnsPersist(runRoot)),
            ("implicit_turn_copy_replay_is_idempotent", () => ImplicitTurnReplay(runRoot)),
            ("structural_replay_order_and_generation_reuse", () => StructuralReplayOrdering(runRoot)),
            ("nested_tier_context_and_model_catalog", () => TierContextAndCatalog(runRoot)),
            ("unchanged_metadata_is_not_rewritten", () => MetadataNoOp(runRoot)),
            ("unchanged_zero_read_write_and_append_only", () => RefreshCounters(runRoot)),
            ("watcher_prioritizes_dormant_reactivated_log", () => WatcherPrioritizesReactivatedLog(runRoot)),
            ("bounded_scan_resume", () => BoundedScanResume(runRoot)),
            ("quota_durability_stale_reset_and_ambiguity", () => QuotaDurability(runRoot)),
            ("running_cycle_scope_and_invalid_quota_window", () => RunningCycleScopeAndInvalidQuota(runRoot)),
            ("context_continuation_metrics_without_count", () => ContextContinuationMetrics(runRoot)),
            ("continuation_grade_hierarchy", ContinuationGradeHierarchy),
            ("continuation_lifecycle_and_manual_drift", () => ContinuationLifecycleAndManualDrift(runRoot)),
            ("manual_drift_persists_and_clears", () => ManualDriftPersistence(runRoot)),
            ("context_window_migration_rescan_no_double_count", () => ContextWindowMigration(runRoot)),
            ("expired_quota_is_unavailable_and_does_not_define_cycle", () => ExpiredQuotaCycle(runRoot)),
            ("refresh_cadence_60_30_8_1", RefreshCadenceTest),
            ("app_server_contract_and_executable_discovery", () => AppServerContract(runRoot)),
            ("sqlite_restart_privacy_and_schema", () => SqlitePrivacySchema(runRoot)),
            ("hud_filters_totals_thresholds_and_unknown_cycle", HudPresentationTest),
            ("view_model_required_fields_and_privacy", ViewModelFields),
            ("hud_v2_edge_beacon_visual_contract", HudV2EdgeBeaconVisualContract),
            ("pin_unpin_persistence", () => PinPersistence(runRoot)),
            ("tray_exit_shutdown_coordinator", ShutdownCoordinatorTest),
            ("single_instance_exclusive_lock", () => SingleInstanceGateTest(runRoot)),
            ("s2_1_duplicate_token_replay_state_immutable", () => DuplicateTokenStateImmutable(runRoot)),
            ("s2_1_timestampless_and_order_ties", () => TimestamplessAndOrderTies(runRoot)),
            ("s2_2_failed_commit_immediate_equals_restart", () => FailedCommitEqualsRestart(runRoot)),
            ("s2_2_busy_and_revision_conflict_publish_nothing", () => BusyAndRevisionConflict(runRoot)),
            ("s2_3_complete_envelope_duplicate_rejection", () => CompleteEnvelopeDuplicates(runRoot)),
            ("s2_4_fallback_digest_and_symbolic_settings", () => FallbackDigestAndSymbolicSettings(runRoot)),
            ("s2_4_legacy_absolute_migration_idempotent", () => LegacyAbsoluteMigration(runRoot)),
            ("s2_5_aggregate_rebuild_resumes", () => AggregateRebuildResumes(runRoot)),
            ("s2_5_incremental_snapshot_cycle_and_frontier", () => IncrementalSnapshotCycleAndFrontier(runRoot)),
            ("s2_5_frame_built_off_dispatcher", () => FrameBuiltOffDispatcher(runRoot)),
            ("s2_5_performance_100k_250_sessions", () => PrintPerformance(Correction03Performance(runRoot))),
            ("s2_6_cross_process_contention_and_kill_recovery", () => CrossProcessLockRecovery(runRoot)),
            ("s2_6_gate_error_opens_no_database_or_log", () => GateErrorNoWriters(runRoot)),
            ("s2_7_recent_degraded_reliable_and_restart", () => RecentUsageSemantics(runRoot)),
            ("s2_8_huge_unknown_drain_bounded_resume", () => UnknownDrainBoundedResume(runRoot)),
            ("s2_8_drain_truncate_rewrite_rotation_privacy", () => UnknownDrainRewriteRotation(runRoot)),
            ("correction_03_static_privacy_and_history_scan", () => Correction03StaticPrivacy(runRoot)),
            ("r3_01_semantic_alias_duplicates", () => SemanticAliasDuplicates(runRoot)),
            ("sr4_01_02_source_repair", () => SourceRepair05(runRoot)),
            ("sr5_01_context_window_schema_admission", () => SourceRepair06ContextWindowAdmission(runRoot)),
            ("sr5_02_bounded_app_server_streams", () => AppServerBoundedStreams(runRoot).GetAwaiter().GetResult()),
            ("r3_02_private_scrub_hard_death_matrix", () => PrivateScrubHardDeathMatrix(runRoot)),
            ("r3_02_private_scrub_marker_state_matrix", () => PrivateScrubMarkerStateMatrix(runRoot)),
            ("r3_03_duplicate_metadata_enrichment_legacy", () => DuplicateMetadataEnrichment(runRoot)),
            ("r3_03_enrichment_latest_oracle_reverse", () => EnrichmentLatestOracleReverse(runRoot)),
            ("r3_04_app_io_fifo_dispatcher_shutdown", () => AppIoFifoDispatcherShutdown(runRoot)),
            ("r3_04_static_dispatcher_ownership", StaticDispatcherOwnership),
            ("r3_05_bounded_recurring_turn_queries_20k", () => BoundedRecurringTurns(runRoot)),
            ("r3_06_service_tier_provenance", () => ServiceTierProvenanceTest(runRoot)),
            ("r3_07_scoped_privacy_evidence", () => ScopedPrivacyEvidence(runRoot)),
            ("hud_wpf_construction_smoke", () => HudWpfConstructionSmoke(runRoot)),
            ("hud_wpf_runtime_interactions", () => HudWpfRuntimeInteractions(runRoot)),
            ("hud_v2_1000_row_interaction_stress", () => HudV2InteractionStress(runRoot)),
            ("hud_v2_left_right_top_dock_and_handle", () => HudV2DockGeometry(runRoot)),
            ("hud_screen_recovery_geometry", HudScreenRecoveryGeometry),
            ("package_publication_privacy_contract", PackagePublicationPrivacyContract),
            ("lineage_semantic_contract_v9_and_live_replay", () => LineageSemanticContractV9AndLiveReplay(runRoot)),
            ("lineage_parent_child_grandchild_siblings_unrelated", () => LineageFamilyAndUnrelatedRoots(runRoot)),
            ("lineage_pre_cycle_late_parent_missing_cyclic", () => LineageCycleTimingAndIsolation(runRoot)),
            ("lineage_incremental_consumers_restart_privacy", () => LineageConsumersRestartPrivacy(runRoot)),
            ("lineage_tuple_presence_reelection_and_render", () => LineageTuplePresenceReelectionAndRender(runRoot)),
            ("lineage_independent_oracle_defect_injection", () => LineageIndependentOracleDefectInjection(runRoot)),
        };

        if (args.Length == 2 && args[0] == "--filter")
        {
            var filters = args[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            tests = tests.Where(test => filters.Any(filter => test.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (tests.Length == 0)
            {
                Console.WriteLine("No tests matched the requested filter.");
                return 2;
            }
        }

        var failures = 0;
        foreach (var test in tests)
        {
            try
            {
                test.Run();
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.WriteLine($"FAIL {test.Name} ({exception.GetType().Name}: {exception.Message})");
                Console.WriteLine(exception.StackTrace);
            }
        }

        Console.WriteLine($"RESULT tests={tests.Length} failures={failures}");
        return failures == 0 ? 0 : 1;
    }

    private static int RunRealCheck(string[] args)
    {
        var home = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : CodexHomeResolver.Resolve();
        var databasePath = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
            ? args[1]
            : Path.Combine(NewRunRoot("real-session"), "usage.db");
        var sentinel = args.Length > 2 && !string.IsNullOrWhiteSpace(args[2]) ? args[2] : "__CUH_PRIVACY_SENTINEL__";
        try
        {
            var result = new RealSessionVerifier().Verify(home, databasePath, sentinel);
            using var contextDatabase = new UsageDatabase(databasePath);
            var contextMetrics = contextDatabase.LoadSessionContextMetrics();
            Console.WriteLine($"REAL status={result.StatusCode} threads={result.Threads.Count} totals_unchanged={result.RestartUnchanged} samples_unchanged={result.SampleCountUnchanged} fingerprints_unchanged={result.FingerprintCountUnchanged} offsets_unchanged={result.SourceOffsetsUnchanged} database_privacy_clean={result.DatabasePrivacyClean} database_sentinel_clean={result.DatabaseSentinelClean} database_schema_clean={result.DatabaseSchemaClean} database_relative_paths_clean={result.DatabaseRelativePathsClean} hud_log_privacy={result.HudLogPrivacyStatus} rendered_string_privacy={result.RenderedStringPrivacyStatus} code={result.ErrorCode ?? "none"}");
            foreach (var thread in result.Threads)
            {
                contextMetrics.TryGetValue(thread.ThreadId, out var context);
                Console.WriteLine($"THREAD id={thread.ThreadId} parsed={thread.ParsedTokenEvents} distinct={thread.DistinctTuples} exact_replay={thread.ExactReplayCount} interleavings={thread.CumulativeInterleavings} " +
                    $"oracle_input={thread.OracleTotal.Input} oracle_raw={thread.OracleTotal.RawInput} oracle_cached={thread.OracleTotal.CachedInput} oracle_output={thread.OracleTotal.Output} oracle_reasoning={thread.OracleTotal.Reasoning} oracle_total={thread.OracleTotal.Total} " +
                    $"db_input={thread.DatabaseTotal.Input} db_raw={thread.DatabaseTotal.RawInput} db_cached={thread.DatabaseTotal.CachedInput} db_output={thread.DatabaseTotal.Output} db_reasoning={thread.DatabaseTotal.Reasoning} db_total={thread.DatabaseTotal.Total} " +
                    $"db_samples={thread.DatabaseSampleCount} db_fingerprints={thread.DatabaseFingerprintCount} components_equal={thread.ComponentsEqual} counts_equal={thread.CountsEqual} " +
                    $"context_baseline={(context?.PostCompactionPercent is double baseline ? baseline.ToString("0.0", CultureInfo.InvariantCulture) : "unavailable")}");
            }
            return result.StatusCode == "OK" ? 0 : 2;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("REAL status=CANCELLED");
            return 2;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"REAL status=ERROR code={exception.GetType().Name}");
            return 2;
        }
    }

    private static int RunSourceHierarchyCheck(string[] args)
    {
        var home = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? CodexHomeResolver.Resolve(args[0])
            : CodexHomeResolver.Resolve();
        try
        {
            var rows = new StateMetadataReader().Read(home);
            var indexNames = new SessionIndexReader().Read(home);
            var sessions = rows.Select(row => SessionMetadataMapper.ToSession(row, indexNames)).ToArray();
            var byId = sessions.ToDictionary(session => session.ThreadId, StringComparer.Ordinal);
            var internalTasks = sessions.Where(session => session.Kind == SessionKind.InternalTask).ToArray();
            var linked = internalTasks.Where(session => session.ParentThreadId is not null &&
                                                        byId.ContainsKey(session.ParentThreadId)).ToArray();
            var nested = linked.Count(session => byId[session.ParentThreadId!].Kind == SessionKind.InternalTask);
            var recentCutoff = DateTimeOffset.UtcNow.AddDays(-7);
            var recent = internalTasks.Where(session => session.LastActivityUtc >= recentCutoff).ToArray();
            var recentLinked = recent.Count(session => session.ParentThreadId is not null &&
                                                       byId.ContainsKey(session.ParentThreadId));
            Console.WriteLine("SOURCE_HIERARCHY status=" + (sessions.Length > 0 ? "OK" : "UNAVAILABLE") +
                              $" metadata={sessions.Length} app={sessions.Count(session => session.Surface == SessionSurface.App)}" +
                              $" cli={sessions.Count(session => session.Surface == SessionSurface.Cli)}" +
                              $" internal={internalTasks.Length} linked={linked.Length} nested={nested}" +
                              $" orphan={internalTasks.Length - linked.Length} recent_7d={recent.Length}" +
                              $" recent_linked={recentLinked}");
            return sessions.Length > 0 ? 0 : 2;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"SOURCE_HIERARCHY status=ERROR code={exception.GetType().Name}");
            return 2;
        }
    }

    private static int RunUiCapture(string[] args)
    {
        var outputDirectory = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.Combine(ProjectRoot(), ".artifacts", "ui-capture");
        Directory.CreateDirectory(outputDirectory);
        EnsureWpfTestApplication();

        var dataDirectory = Path.Combine(outputDirectory, "data");
        var codexHome = Path.Combine(dataDirectory, "codex-home");
        Directory.CreateDirectory(Path.Combine(codexHome, "sessions"));
        using var engine = new UsageEngine(codexHome, Path.Combine(dataDirectory, "usage.db"),
            Path.Combine(dataDirectory, "hud.log"));
        using var window = new MainWindow(engine, () => Task.CompletedTask, false);
        System.Windows.Application.Current.MainWindow = window;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.Topmost = true;
        window.Left = System.Windows.SystemParameters.VirtualScreenLeft - 4000;
        window.Top = System.Windows.SystemParameters.VirtualScreenTop - 4000;
        var viewModelField = typeof(MainWindow).GetField("_viewModel",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("view_model_field_missing");
        var viewModel = (MainViewModel)(viewModelField.GetValue(window)
            ?? throw new InvalidOperationException("view_model_missing"));
        var captureSnapshot = args.Contains("--polish", StringComparer.Ordinal)
            ? CreatePolishCaptureSnapshot() : CreateDesignCaptureSnapshot();
        viewModel.Apply(captureSnapshot);
        viewModel.ToggleChildren("019fcac6-82e");
        viewModel.IsExpanded = true;
        InvokeWindowMethod(window, "ApplyExpansionState", false);
        window.Show();
        InvokeWindowMethod(window, "UpdateTextBlocks");
        RenderWindow(window, Path.Combine(outputDirectory, "hud-expanded.png"));
        RenderWindow(window, Path.Combine(outputDirectory, "hud-expanded-125.png"), 120);
        RenderWindow(window, Path.Combine(outputDirectory, "hud-expanded-150.png"), 144);
        InvokeWindowMethod(window, "OnToggleFullscreen", window, new System.Windows.RoutedEventArgs());
        window.UpdateLayout();
        RenderWindow(window, Path.Combine(outputDirectory, "hud-fullscreen.png"));
        InvokeWindowMethod(window, "OnToggleFullscreen", window, new System.Windows.RoutedEventArgs());
        var expandedShell = (System.Windows.FrameworkElement)(window.FindName("ExpandedShell")
            ?? throw new InvalidOperationException("expanded_shell_missing"));
        var compact = (System.Windows.FrameworkElement)(window.FindName("CompactVerticalShell")
            ?? throw new InvalidOperationException("compact_shell_missing"));
        Console.WriteLine($"UI_CAPTURE_STATE expanded={viewModel.IsExpanded} width={window.Width:0} " +
                          $"height={window.Height:0} shell={expandedShell.Visibility} " +
                          $"compact={compact.Visibility}");
        viewModel.IsExpanded = false;
        InvokeWindowMethod(window, "ApplyExpansionState", false);
        InvokeWindowMethod(window, "UpdateTextBlocks");
        RenderWindow(window, Path.Combine(outputDirectory, "hud-collapsed.png"));
        RenderWindow(window, Path.Combine(outputDirectory, "hud-collapsed-125.png"), 120);
        RenderWindow(window, Path.Combine(outputDirectory, "hud-collapsed-150.png"), 144);
        var dockField = typeof(MainWindow).GetField("_dockSide",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("dock_field_missing");
        dockField.SetValue(window, Enum.Parse(dockField.FieldType, "Top"));
        InvokeWindowMethod(window, "ApplyExpansionState", false);
        InvokeWindowMethod(window, "UpdateTextBlocks");
        RenderWindow(window, Path.Combine(outputDirectory, "hud-top.png"));
        if (args.Contains("--states", StringComparer.Ordinal))
        {
            dockField.SetValue(window, Enum.Parse(dockField.FieldType, "Right"));
            foreach (var used in new[] { 87d, 94d })
            {
                viewModel.Apply(captureSnapshot with { Quota = captureSnapshot.Quota with
                {
                    Primary = captureSnapshot.Quota.Primary! with { UsedPercent = used },
                } });
                InvokeWindowMethod(window, "ApplyExpansionState", false);
                InvokeWindowMethod(window, "UpdateTextBlocks");
                RenderWindow(window, Path.Combine(outputDirectory, used == 87 ? "hud-warning.png" : "hud-critical.png"));
            }
            var unavailable = captureSnapshot with
            {
                Quota = new QuotaObservation(null, Array.Empty<QuotaBucket>(), QuotaSource.Unavailable,
                    captureSnapshot.GeneratedAtUtc, true),
                Sessions = Array.Empty<SessionAggregate>(), CycleTotal = null, RunningCycleTotal = null,
            };
            viewModel.Apply(unavailable);
            InvokeWindowMethod(window, "UpdateTextBlocks");
            RenderWindow(window, Path.Combine(outputDirectory, "hud-unavailable.png"));
            viewModel.IsExpanded = true;
            InvokeWindowMethod(window, "ApplyExpansionState", false);
            RenderWindow(window, Path.Combine(outputDirectory, "hud-empty.png"));
            var longRow = UiSession("polish-long-id-0123456789012345678901234567890",
                "需要完整显示的中文会话名称：跨模块界面验证、长数字与续接说明在正常缩放下仍然可读",
                "reviewer", "", "project-with-a-deliberately-long-name-for-layout-verification",
                "gpt-6-astra", "Standard（默认）", SessionStatus.Idle, captureSnapshot.GeneratedAtUtc,
                UiUsage(long.MaxValue - 1, 1_000_000, 1, 0), Usage(1000));
            viewModel.Apply(captureSnapshot with { Sessions = new[] { longRow } });
            InvokeWindowMethod(window, "UpdateTextBlocks");
            RenderWindow(window, Path.Combine(outputDirectory, "hud-long-content.png"));
            viewModel.Apply(captureSnapshot);
            InvokeWindowMethod(window, "UpdateTextBlocks");
        }
        if (args.Contains("--interactive", StringComparer.Ordinal))
        {
            // A real native window using only synthetic data, for pointer and
            // keyboard acceptance. No timer reads the user's Codex sessions.
            window.Title = "Codex Usage HUD · 视觉验收（示例数据）";
            window.Topmost = false;
            window.ShowInTaskbar = true;
            var settingsReady = typeof(MainWindow).GetField("_settingsReady",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            settingsReady.SetValue(window, true);
            dockField.SetValue(window, Enum.Parse(dockField.FieldType, "None"));
            viewModel.IsExpanded = true;
            InvokeWindowMethod(window, "ApplyExpansionState", false);
            window.Left = System.Windows.SystemParameters.WorkArea.Left + 35;
            window.Top = System.Windows.SystemParameters.WorkArea.Top + 15;
            window.Activate();
            Console.WriteLine("UI_PREVIEW_READY synthetic=true");
            System.Windows.Application.Current.Run();
            return 0;
        }
        window.Hide();
        Console.WriteLine($"UI_CAPTURE collapsed={Path.Combine(outputDirectory, "hud-collapsed.png")}");
        Console.WriteLine($"UI_CAPTURE top={Path.Combine(outputDirectory, "hud-top.png")}");
        Console.WriteLine($"UI_CAPTURE expanded={Path.Combine(outputDirectory, "hud-expanded.png")}");
        Console.WriteLine($"UI_CAPTURE fullscreen={Path.Combine(outputDirectory, "hud-fullscreen.png")}");
        return 0;
    }

    private static int RunDatabaseRestartCheck(string[] args)
    {
        if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.WriteLine("DATABASE_RESTART status=ERROR code=database_path_required");
            return 2;
        }

        try
        {
            var path = Path.GetFullPath(args[0]);
            var first = CaptureDatabaseRestartState(path);
            var second = CaptureDatabaseRestartState(path);
            Assert.Equal(first, second);
            Console.WriteLine($"DATABASE_RESTART status=OK schema={second.SchemaVersion} " +
                              $"sessions={second.SessionCount} samples={second.SampleCount} " +
                              $"fingerprints={second.FingerprintCount} sources={second.SourceCount} " +
                              $"total={second.Total.Total} aggregate_ready={second.AggregateReady} " +
                              $"privacy_schema_clean={second.PrivacySchemaClean} " +
                              $"relative_paths_clean={second.RelativePathsClean} " +
                              $"private_identity={second.PrivateIdentityState} " +
                              $"private_scrub={second.PrivateScrubState}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"DATABASE_RESTART status=ERROR code={exception.GetType().Name}");
            return 2;
        }
    }

    private static int RunContinuationRiskCheck(string[] args)
    {
        if (args.Length != 2 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
            return 2;
        try
        {
            using var database = new UsageDatabase(Path.GetFullPath(args[0]));
            var aggregate = database.LoadSessionAggregates(DateTimeOffset.UtcNow)
                .Single(item => string.Equals(item.Metadata.ThreadId, args[1], StringComparison.Ordinal));
            var now = DateTimeOffset.UtcNow;
            var snapshot = new HudSnapshot(new QuotaObservation(null, Array.Empty<QuotaBucket>(),
                    QuotaSource.Unavailable, now, false), new[] { aggregate }, null, now, false,
                "metadata-only", Array.Empty<string>());
            var row = HudPresentation.BuildRows(snapshot).Single();
            var lifecycle = aggregate.LifecycleMetrics;
            Console.WriteLine($"CONTINUATION_RISK status=OK thread={row.ShortId} " +
                              $"structural={row.StructuralGradeText} long_risk={row.LifecycleRiskText} " +
                              $"manual={row.DriftAssessmentText} composite={row.ContinuationGradeText} " +
                              $"lifetime_total={aggregate.SessionTotal.Total} turns={lifecycle?.AggregatedTurnCount ?? 0} " +
                              $"recent_48h={lifecycle?.Recent48HourTokens ?? 0} recent_turns={lifecycle?.Recent48HourTurnCount ?? 0} " +
                              $"comparable={lifecycle?.ComparableSessionCount ?? 0} " +
                              $"token_rank={lifecycle?.LifetimeTokenRank ?? 0} turn_rank={lifecycle?.LifetimeTurnRank ?? 0}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"CONTINUATION_RISK status=ERROR code={exception.GetType().Name}");
            return 2;
        }
    }

    private static int RunSetDriftAssessment(string[] args)
    {
        if (args.Length != 3 || !Enum.TryParse<DriftAssessmentLevel>(args[2], true, out var level)) return 2;
        try
        {
            using var database = new UsageDatabase(Path.GetFullPath(args[0]));
            database.SetSessionDriftAssessment(args[1], level, DateTimeOffset.UtcNow);
            var stored = database.LoadSessionDriftAssessments().GetValueOrDefault(args[1]) ??
                         SessionDriftAssessment.Unassessed;
            Console.WriteLine($"DRIFT_ASSESSMENT status=OK thread={SessionMetadataShortId(args[1])} level={stored.Level}");
            return stored.Level == level ? 0 : 2;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"DRIFT_ASSESSMENT status=ERROR code={exception.GetType().Name}");
            return 2;
        }
    }

    private static string SessionMetadataShortId(string value) => value.Length <= 12 ? value : value[..12];

    private static int RunDatabaseReadonlyStatus(string[] args)
    {
        if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0])) return 2;
        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = Path.GetFullPath(args[0]),
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Private,
                Pooling = false,
                DefaultTimeout = 2,
            };
            using var connection = new SqliteConnection(builder.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    (SELECT value FROM schema_info WHERE key = 'version'),
                    (SELECT COUNT(*) FROM source_files),
                    (SELECT COUNT(*) FROM source_files
                     WHERE health_code IN ('context_window_rescan', 'context_boundary_rescan', 'more_data')),
                    (SELECT COUNT(*) FROM source_files
                     WHERE health_code IN ('context_window_rescan', 'context_boundary_rescan')),
                    (SELECT COUNT(*) FROM source_files WHERE health_code = 'more_data'),
                    (SELECT COALESCE(SUM(MAX(last_length - complete_offset, 0)), 0) FROM source_files
                     WHERE health_code IN ('context_window_rescan', 'context_boundary_rescan', 'more_data')),
                    (SELECT COUNT(*) FROM session_context_state),
                    (SELECT COUNT(*) FROM context_baseline_observations),
                    (SELECT COUNT(*) FROM token_samples),
                    (SELECT COUNT(*) FROM event_fingerprints),
                    (SELECT COALESCE(SUM(canonical_total_tokens), 0) FROM token_samples),
                    (SELECT COUNT(*) FROM sessions),
                    (SELECT COUNT(*) FROM sessions WHERE session_surface = 'App'),
                    (SELECT COUNT(*) FROM sessions WHERE session_surface = 'Cli'),
                    (SELECT COUNT(*) FROM sessions WHERE session_surface = 'InternalTask'),
                    (SELECT COUNT(*) FROM sessions WHERE parent_thread_id IS NOT NULL),
                    (SELECT COUNT(*) FROM sessions AS child
                     WHERE child.session_kind = 'InternalTask' AND EXISTS
                         (SELECT 1 FROM sessions AS parent WHERE parent.thread_id = child.parent_thread_id)),
                    (SELECT COUNT(*) FROM sessions AS child
                     WHERE child.session_kind = 'InternalTask' AND EXISTS
                         (SELECT 1 FROM sessions AS parent
                          WHERE parent.thread_id = child.parent_thread_id AND parent.session_kind = 'InternalTask')),
                    COALESCE((SELECT bucket_id FROM quota_observations
                              WHERE is_primary = 1 ORDER BY id DESC LIMIT 1), 'unavailable'),
                    COALESCE((SELECT used_percent FROM quota_observations
                              WHERE is_primary = 1 ORDER BY id DESC LIMIT 1), -1),
                    COALESCE((SELECT resets_at_utc FROM quota_observations
                              WHERE is_primary = 1 ORDER BY id DESC LIMIT 1), 'unavailable'),
                    COALESCE((SELECT observed_at_utc FROM quota_observations
                              WHERE is_primary = 1 ORDER BY id DESC LIMIT 1), 'unavailable'),
                    COALESCE((SELECT source FROM quota_observations
                              WHERE is_primary = 1 ORDER BY id DESC LIMIT 1), 'unavailable');
                """;
            using var reader = command.ExecuteReader();
            if (!reader.Read()) return 2;
            Console.WriteLine($"DATABASE_STATUS schema={reader.GetString(0)} sources={reader.GetInt64(1)} " +
                              $"pending={reader.GetInt64(2)} rescan={reader.GetInt64(3)} more_data={reader.GetInt64(4)} " +
                               $"remaining_bytes={reader.GetInt64(5)} context_sessions={reader.GetInt64(6)} " +
                               $"baseline_rows={reader.GetInt64(7)} samples={reader.GetInt64(8)} " +
                               $"fingerprints={reader.GetInt64(9)} token_total={reader.GetInt64(10)} " +
                               $"sessions={reader.GetInt64(11)} app={reader.GetInt64(12)} cli={reader.GetInt64(13)} " +
                               $"internal={reader.GetInt64(14)} parent_links={reader.GetInt64(15)} " +
                               $"linked={reader.GetInt64(16)} nested={reader.GetInt64(17)} " +
                               $"quota_id={reader.GetString(18)} quota_used={reader.GetDouble(19):0.##} " +
                               $"quota_reset={reader.GetString(20)} quota_observed={reader.GetString(21)} " +
                               $"quota_source={reader.GetString(22)}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"DATABASE_STATUS status=ERROR code={exception.GetType().Name}");
            return 2;
        }
    }

    private static int RunContextReadonlyDiagnostic(string[] args)
    {
        if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0])) return 2;
        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = Path.GetFullPath(args[0]),
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Private,
                Pooling = false,
                DefaultTimeout = 2,
            };
            using var connection = new SqliteConnection(builder.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT thread_id, post_input_tokens, context_window, event_time_ticks, detection_source
                FROM context_baseline_observations
                ORDER BY thread_id, event_time_ticks, post_sample_id;
                """;
            var observations = new List<(string ThreadId, long Input, long Window, long Ticks, string Source)>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read()) observations.Add((reader.GetString(0), reader.GetInt64(1),
                    reader.GetInt64(2), reader.GetInt64(3), reader.GetString(4)));
            }

            var ratios = observations.Select(item => item.Input * 100d / item.Window).Order().ToArray();
            var groups = observations.GroupBy(item => item.ThreadId).ToArray();
            var eligible = groups.Where(group => group.Count() >= 3).ToArray();
            var trends = eligible.Select(group =>
            {
                var ordered = group.OrderBy(item => item.Ticks).ToArray();
                return ordered[^1].Input * 100d / ordered[^1].Window -
                       ordered[0].Input * 100d / ordered[0].Window;
            }).Order().ToArray();
            var intervalTurns = new List<long>();
            var intervalTokens = new List<long>();
            var shrinkingTurnRunway = 0;
            var shrinkingTokenRunway = 0;
            var comparableRunways = 0;
            foreach (var group in eligible)
            {
                var ordered = group.OrderBy(item => item.Ticks).ToArray();
                var threadTurns = new List<long>();
                var threadTokens = new List<long>();
                for (var index = 1; index < ordered.Length; index++)
                {
                    using var interval = connection.CreateCommand();
                    interval.CommandText = """
                        SELECT COUNT(DISTINCT turn_key), COALESCE(SUM(canonical_total_tokens), 0)
                        FROM token_samples
                        WHERE thread_id = $thread_id AND event_time_ticks > $start_ticks
                          AND event_time_ticks <= $end_ticks AND turn_key IS NOT NULL
                          AND turn_confidence = 'reliable';
                        """;
                    interval.Parameters.AddWithValue("$thread_id", group.Key);
                    interval.Parameters.AddWithValue("$start_ticks", ordered[index - 1].Ticks);
                    interval.Parameters.AddWithValue("$end_ticks", ordered[index].Ticks);
                    using var intervalReader = interval.ExecuteReader();
                    if (!intervalReader.Read()) continue;
                    var turns = intervalReader.GetInt64(0);
                    var tokens = intervalReader.GetInt64(1);
                    intervalTurns.Add(turns);
                    intervalTokens.Add(tokens);
                    threadTurns.Add(turns);
                    threadTokens.Add(tokens);
                }
                if (threadTurns.Count >= 2)
                {
                    var priorTurns = threadTurns.Take(threadTurns.Count - 1).Order().ToArray();
                    var priorTokens = threadTokens.Take(threadTokens.Count - 1).Order().ToArray();
                    var medianPriorTurns = priorTurns[priorTurns.Length / 2];
                    var medianPriorTokens = priorTokens[priorTokens.Length / 2];
                    comparableRunways++;
                    if (medianPriorTurns > 0 && threadTurns[^1] <= medianPriorTurns * 0.7d)
                        shrinkingTurnRunway++;
                    if (medianPriorTokens > 0 && threadTokens[^1] <= medianPriorTokens * 0.7d)
                        shrinkingTokenRunway++;
                }
            }

            static double Quantile(IReadOnlyList<double> ordered, double fraction)
            {
                if (ordered.Count == 0) return double.NaN;
                var index = (int)Math.Round((ordered.Count - 1) * fraction, MidpointRounding.AwayFromZero);
                return ordered[Math.Clamp(index, 0, ordered.Count - 1)];
            }

            static long MedianLong(IReadOnlyList<long> values)
            {
                if (values.Count == 0) return -1;
                var ordered = values.Order().ToArray();
                return ordered[ordered.Length / 2];
            }

            Console.WriteLine($"CONTEXT_DIAGNOSTIC observations={observations.Count} " +
                              $"explicit={observations.Count(item => item.Source == "explicit")} " +
                              $"heuristic={observations.Count(item => item.Source == "heuristic")} threads={groups.Length} " +
                              $"eligible_threads={eligible.Length} baseline_p50={Quantile(ratios, 0.5):0.0} " +
                              $"baseline_p75={Quantile(ratios, 0.75):0.0} baseline_p90={Quantile(ratios, 0.9):0.0} " +
                              $"trend_median_pp={Quantile(trends, 0.5):0.0} rising_10pp={trends.Count(value => value >= 10d)} " +
                              $"intervals={intervalTurns.Count} turns_median={MedianLong(intervalTurns)} " +
                              $"tokens_median={MedianLong(intervalTokens)} comparable_runways={comparableRunways} " +
                              $"shrinking_turn_runway={shrinkingTurnRunway} shrinking_token_runway={shrinkingTokenRunway}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"CONTEXT_DIAGNOSTIC status=ERROR code={exception.GetType().Name}");
            return 2;
        }
    }

    private static DatabaseRestartState CaptureDatabaseRestartState(string path)
    {
        using var database = new UsageDatabase(path);
        var batches = 0;
        while (!database.IsAggregateRebuildComplete)
        {
            _ = database.RunAggregateRebuildBatch();
            if (++batches > 1_000_000) throw new InvalidOperationException("aggregate_rebuild_did_not_finish");
        }

        var totals = database.LoadSessionTotals();
        var total = totals.Values.Aggregate(default(CanonicalTokenUsage),
            (sum, value) => sum + value);
        var forbidden = new HashSet<string>(new[]
        {
            "first_user_message", "preview", "prompt", "response", "raw_json", "credential",
        }, StringComparer.OrdinalIgnoreCase);
        var schemaClean = database.ReadSchemaColumnNames().Values.SelectMany(columns => columns)
            .All(column => !forbidden.Contains(column));
        var relativePathsClean = database.LoadSourceStates()
            .All(state => !Path.IsPathRooted(state.RelativePath));
        return new DatabaseRestartState(
            database.ReadSchemaValue("version") ?? "unavailable",
            totals.Count,
            database.GetSampleCount(),
            database.GetFingerprintCount(),
            database.LoadSourceStates().Count,
            total,
            database.IsAggregateRebuildComplete,
            schemaClean,
            relativePathsClean,
            database.ReadSchemaValue("private_source_identity_v2") ?? "unavailable",
            database.ReadSchemaValue("private_source_scrub_v2") ?? "unavailable");
    }

    private static void InvokeWindowMethod(MainWindow window, string name, params object[] arguments)
    {
        var method = typeof(MainWindow).GetMethod(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"window_method_missing:{name}");
        _ = method.Invoke(window, arguments);
    }

    private static void AwaitWithDispatcher(Task task, Dispatcher dispatcher, TimeSpan timeout)
    {
        var frame = new DispatcherFrame();
        var timedOut = false;
        var timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = timeout,
        };
        timer.Tick += (_, _) =>
        {
            timedOut = true;
            timer.Stop();
            frame.Continue = false;
        };
        _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false),
            CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        if (timedOut && !task.IsCompleted)
            throw new TimeoutException("dispatcher_task_timeout");
        task.GetAwaiter().GetResult();
    }

    private static void RenderWindow(MainWindow window, string outputPath, double dpi = 96)
    {
        var width = (int)Math.Ceiling(window.Width);
        var height = (int)Math.Ceiling(window.Height);
        var pixelWidth = (int)Math.Ceiling(window.Width * dpi / 96d);
        var pixelHeight = (int)Math.Ceiling(window.Height * dpi / 96d);
        var visual = (System.Windows.FrameworkElement)window.Content;
        visual.Measure(new System.Windows.Size(width, height));
        visual.Arrange(new System.Windows.Rect(0, 0, width, height));
        visual.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }

    private static void EnsureWpfTestApplication()
    {
        if (System.Windows.Application.Current is null)
        {
            var application = new CodexUsageHud.App.App();
            application.InitializeComponent();
        }
        if (System.Windows.Application.Current is not CodexUsageHud.App.App app)
            throw new InvalidOperationException("unexpected_wpf_application");

        var startup = typeof(CodexUsageHud.App.App).GetMethod("OnStartup",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.DeclaredOnly)
            ?? throw new InvalidOperationException("app_startup_missing");
        var startupHandler = (System.Windows.StartupEventHandler)Delegate.CreateDelegate(
            typeof(System.Windows.StartupEventHandler), app, startup);
        app.Startup -= startupHandler;

        var exit = typeof(CodexUsageHud.App.App).GetMethod("OnExit",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.DeclaredOnly)
            ?? throw new InvalidOperationException("app_exit_missing");
        var exitHandler = (System.Windows.ExitEventHandler)Delegate.CreateDelegate(
            typeof(System.Windows.ExitEventHandler), app, exit);
        app.Exit -= exitHandler;
        app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
    }

    private static HudSnapshot CreateUiCaptureSnapshot()
    {
        var now = DateTimeOffset.Parse("2026-08-05T02:38:24Z", CultureInfo.InvariantCulture);
        var sessions = new[]
        {
            UiSession("019fcac6-82e", "Codex Usage HUD 主任务", "Lead", "Huhu", "small-projects",
                "gpt-5.6-sol", "Standard", SessionStatus.Running, now.AddMinutes(-3),
                UiUsage(2_372_056, 316_226, 1_212_479, 651_178), UiUsage(697_625, 21_720, 296_647, 0), true,
                new SessionContextMetrics(108_000, 258_400, 5, 11.2, -35, -24, true)),
            UiSession("019fcd6f-cf8", "月亮故事屋", "worker", "Luna", "bedtime-story",
                "gpt-5.6-sol", "Standard", SessionStatus.Running, now.AddMinutes(-4),
                UiUsage(4_950_000, 410_000, 1_880_000, 720_000), UiUsage(77_572, 8_124, 29_647, 8_320)),
            UiSession("019fcac8-e44", "资料整理与审阅", "reviewer", "Sol", "research",
                "gpt-5.6-sol", "Standard", SessionStatus.Running, now.AddMinutes(-5),
                UiUsage(3_420_000, 330_000, 1_190_000, 410_000), UiUsage(91_721, 7_810, 34_200, 12_700)),
            UiSession("019f860a-7a2", "门店经营复盘", "analyst", "Luna", "operations",
                "gpt-5.6-sol", "Standard", SessionStatus.Unknown, now.AddHours(-1),
                UiUsage(8_640_000, 1_050_000, 2_420_000, 780_000), UiUsage(248_820, 31_000, 83_900, 21_500)),
            UiSession("019f8abe-1b1", "独立质量检查", "reviewer", "Sol", "audit",
                "gpt-5.6-sol", "Standard", SessionStatus.Idle, now.AddDays(-1),
                UiUsage(5_260_000, 620_000, 1_740_000, 540_000), UiUsage(1_101_618, 110_000, 351_000, 99_000)),
        };
        var quota = new QuotaObservation(
            new QuotaBucket("codex", "Codex", 15, 10080, now.AddDays(6).AddHours(12).AddMinutes(25)),
            Array.Empty<QuotaBucket>(), QuotaSource.OfficialAppServer, now, false);
        return new HudSnapshot(quota, sessions, UiUsage(420_000_000, 62_400_000, 122_400_000, 41_000_000),
            now, false, "官方额度 · 本地索引 0 秒前", Array.Empty<string>(),
            new[] { new HudEvent(now.AddMinutes(-1), "quota", "quota_observed", "官方额度观测正常") },
            RunningCycleTotal: UiUsage(82_000_000, 12_000_000, 24_000_000, 8_000_000));
    }

    private static HudSnapshot CreateStressSnapshot(int count)
    {
        var now = DateTimeOffset.UtcNow;
        var sessions = new List<SessionAggregate>(count);
        for (var index = 0; index < count; index++)
        {
            var internalTask = index >= count * 3 / 4;
            var status = index % 20 == 0 ? SessionStatus.Running : SessionStatus.Idle;
            var activity = now.AddSeconds(-index * 3L);
            var total = UiUsage(100_000 + index * 113L, 20_000 + index * 17L,
                40_000 + index * 29L, 8_000 + index * 7L);
            var recent = UiUsage(1_000 + index, 200 + index / 4, 400 + index / 3, 80 + index / 8);
            var metadata = new SessionMetadata($"stress-thread-{index:0000}", $"压力会话 {index:0000}",
                internalTask ? "worker" : "lead", internalTask ? "Luna" : "Huhu",
                $"project-{index % 12:00}", "gpt-5.6-sol", null, "Standard（默认）", activity, null,
                index == 0, Kind: internalTask ? SessionKind.InternalTask : SessionKind.Primary);
            sessions.Add(new SessionAggregate(metadata, status, total, recent, activity,
                $"turn-{index:0000}", false, RecentUsageKind.LatestTurn, recent, "reliable-turn"));
        }
        var quota = new QuotaObservation(
            new QuotaBucket("codex", "Codex", 29, 10080, now.AddDays(6)),
            Array.Empty<QuotaBucket>(), QuotaSource.OfficialAppServer, now, false);
        return new HudSnapshot(quota, sessions,
            UiUsage(900_000_000, 140_000_000, 250_000_000, 80_000_000), now, false,
            "官方额度 · 本地索引 0 秒前", Array.Empty<string>(), Array.Empty<HudEvent>(),
            RunningCycleTotal: UiUsage(55_000_000, 8_000_000, 16_000_000, 5_000_000));
    }

    private static HudSnapshot CreateDesignCaptureSnapshot()
    {
        var seed = CreateUiCaptureSnapshot();
        var sessions = seed.Sessions.Select((item, index) => item with
        {
            Metadata = item.Metadata with { Kind = SessionKind.Primary, Surface = SessionSurface.App },
            Status = index == 0 ? SessionStatus.Running : SessionStatus.Idle,
            SessionTotal = index == 0 ? UiUsage(3_050_000_000, 2_900_000_000, 461_000_000, 120_000_000)
                : item.SessionTotal,
            LifecycleMetrics = index == 0
                ? new SessionLifecycleMetrics(624, 1_450_000_000, 262, 29, 1, 1, 1, 1,
                    60d, 69d, 103d, 131d)
                : null,
            DriftAssessment = index == 0
                ? new SessionDriftAssessment(DriftAssessmentLevel.Repeated, seed.GeneratedAtUtc)
                : SessionDriftAssessment.Unassessed,
        }).ToList();
        var now = seed.GeneratedAtUtc;
        for (var index = sessions.Count; index < 69; index++)
        {
            var metadata = new SessionMetadata($"primary-{index:000}", $"历史会话 {index:000}",
                "lead", null, $"project-{index % 8}", "gpt-5.6-sol", null, "Standard（默认）",
                now.AddHours(-index), null, Kind: SessionKind.Primary,
                Surface: index % 5 == 0 ? SessionSurface.Cli : SessionSurface.App);
            sessions.Add(new SessionAggregate(metadata, SessionStatus.Idle, Usage(50_000 + index),
                Usage(500 + index), metadata.LastActivityUtc, $"turn-{index}", false,
                RecentUsageKind.LatestTurn, Usage(500 + index), "reliable-turn"));
        }
        for (var index = 0; index < 778; index++)
        {
            var parentThreadId = index < 750 ? sessions[index % 69].Metadata.ThreadId : null;
            var metadata = new SessionMetadata($"internal-{index:000}", $"内部子任务 {index:000}",
                "worker", "Luna", "internal", "gpt-5.6-luna", null, "Standard（默认）",
                now.AddMinutes(-index - 20), null, Kind: SessionKind.InternalTask,
                Surface: SessionSurface.InternalTask, ParentThreadId: parentThreadId, AgentDepth: 1);
            sessions.Add(new SessionAggregate(metadata, SessionStatus.Idle, Usage(10_000 + index),
                Usage(100 + index), metadata.LastActivityUtc, $"internal-turn-{index}", false,
                RecentUsageKind.LatestTurn, Usage(100 + index), "reliable-turn"));
        }
        return seed with
        {
            Sessions = sessions,
            CycleTotal = UiUsage(900_000_000, 140_000_000, 250_000_000, 80_000_000),
            RunningCycleTotal = UiUsage(21_000_000, 3_000_000, 6_000_000, 2_000_000),
        };
    }

    private static HudSnapshot CreatePolishCaptureSnapshot()
    {
        var now = DateTimeOffset.UtcNow;
        var root = UiSession("019fcac6-82e", "产品开发主任务", "Lead", "", "small-projects",
            "gpt-6-astra", "Standard", SessionStatus.Running, now.AddMinutes(-1),
            UiUsage(312_000_000, 300_000_000, 8_000_000, 2_000_000),
            UiUsage(3_620_000, 3_200_000, 80_000, 20_000), true,
            new SessionContextMetrics(108_000, 258_400, 5, 0.5, -8, -12, true));
        var child = UiSession("polish-child-001", "实现与验证", "worker", "", "small-projects",
            "gpt-6-astra", "Standard", SessionStatus.Running, now.AddMinutes(-2),
            UiUsage(37_000_000, 35_000_000, 1_200_000, 200_000),
            UiUsage(100_000, 90_000, 2_000, 1_000));
        child = child with { Metadata = child.Metadata with
        {
            Kind = SessionKind.InternalTask, Surface = SessionSurface.InternalTask,
            ParentThreadId = root.Metadata.ThreadId, AgentDepth = 1,
        } };
        var sessions = new[]
        {
            root, child,
            UiSession("polish-cli-002", "界面精修", "worker", "", "small-projects", "gpt-6-astra",
                "Standard", SessionStatus.Running, now.AddMinutes(-3), UiUsage(44_000_000, 40_000_000, 2_300_000, 300_000), Usage(3200), surface: SessionSurface.Cli),
            UiSession("polish-app-003", "月亮故事屋", "lead", "", "story-house", "gpt-6-astra",
                "Standard", SessionStatus.Idle, now.AddMinutes(-15), UiUsage(120_000_000, 110_000_000, 7_600_000, 400_000), Usage(4100)),
            UiSession("polish-app-004", "数据复盘", "analyst", "", "reports", "gpt-6-astra",
                "Standard", SessionStatus.Idle, now.AddMinutes(-30), UiUsage(90_000_000, 85_000_000, 2_800_000, 300_000), Usage(5000)),
        };
        return new HudSnapshot(new QuotaObservation(new QuotaBucket("codex", "Codex", 15, 10080,
                now.AddDays(6).AddHours(12).AddMinutes(25)), Array.Empty<QuotaBucket>(),
                QuotaSource.OfficialAppServer, now, false), sessions,
            UiUsage(1_100_000_000, 1_000_000_000, 50_000_000, 10_000_000), now,
            false, "示例数据 · 非实时额度", Array.Empty<string>(), Array.Empty<HudEvent>(),
            RunningCycleTotal: UiUsage(370_000_000, 350_000_000, 6_000_000, 1_000_000));
    }

    private static SessionAggregate UiSession(string id, string name, string role, string nickname,
        string project, string model, string tier, SessionStatus status, DateTimeOffset activity,
        CanonicalTokenUsage total, CanonicalTokenUsage recent, bool pinned = false,
        SessionContextMetrics? contextMetrics = null, SessionSurface surface = SessionSurface.App)
    {
        var metadata = new SessionMetadata(id, name, role, nickname, project, model, null, tier,
            activity, null, pinned, Kind: SessionKind.Primary, Surface: surface);
        return new SessionAggregate(metadata, status, total, recent, activity, "turn-current", false,
            RecentUsageKind.LatestTurn, recent, "可靠 turn 边界", contextMetrics);
    }

    private static CanonicalTokenUsage UiUsage(long input, long cached, long output, long reasoning)
    {
        var safeCached = Math.Min(input, cached);
        var safeReasoning = Math.Min(output, reasoning);
        var total = checked(input + output);
        return new CanonicalTokenUsage(input, input - safeCached, safeCached, 0, output, safeReasoning,
            total, total);
    }

    private static int RunLineageCycleCheck(string[] args)
    {
        var databasePath = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? args[0]
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodexUsageHUD", "usage.db");
        if (!File.Exists(databasePath))
        {
            Console.WriteLine("LINEAGE_CYCLE_CHECK status=UNAVAILABLE schema=unavailable samples=0 " +
                              "canonical_stored=0 canonical_independent=0 stored_canonical_total=0 " +
                              "independent_total=0 raw_sample_total=0 roots=0 cyclic_isolated=0 mismatch=1 " +
                              "cycle_independent_total=0 db=missing");
            return 2;
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
            DefaultTimeout = 2,
        };
        using var connection = new SqliteConnection(builder.ToString());
        try
        {
            connection.Open();
        }
        catch (SqliteException)
        {
            Console.WriteLine("LINEAGE_CYCLE_CHECK status=UNAVAILABLE schema=unavailable samples=0 " +
                              "canonical_stored=0 canonical_independent=0 stored_canonical_total=0 " +
                              "independent_total=0 raw_sample_total=0 roots=0 cyclic_isolated=0 mismatch=1 " +
                              "cycle_independent_total=0 db=readonly");
            return 2;
        }

        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA query_only=ON; PRAGMA busy_timeout=2000;";
            pragma.ExecuteNonQuery();
        }

        string schema;
        using (var schemaCmd = connection.CreateCommand())
        {
            schemaCmd.CommandText = "SELECT value FROM schema_info WHERE key = 'version';";
            schema = Convert.ToString(schemaCmd.ExecuteScalar(), CultureInfo.InvariantCulture) ?? "unavailable";
        }

        var columns = new HashSet<string>(StringComparer.Ordinal);
        using (var columnCmd = connection.CreateCommand())
        {
            columnCmd.CommandText = "PRAGMA table_info(token_samples);";
            using var reader = columnCmd.ExecuteReader();
            while (reader.Read()) columns.Add(reader.GetString(1));
        }

        var hasLineage = columns.Contains("is_lineage_canonical") && columns.Contains("semantic_identity");
        var parents = new Dictionary<string, string?>(StringComparer.Ordinal);
        using (var parentCmd = connection.CreateCommand())
        {
            parentCmd.CommandText = "SELECT thread_id, parent_thread_id FROM sessions;";
            using var reader = parentCmd.ExecuteReader();
            while (reader.Read())
                parents[reader.GetString(0)] = reader.IsDBNull(1) ? null : reader.GetString(1);
        }

        var hasLegacyAlias = columns.Contains("legacy_semantic_identity");
        var sampleSql = IndependentLineage.SampleSql(hasLineage, hasLegacyAlias);
        var samples = new List<LineageCycleRow>();
        using (var sampleCmd = connection.CreateCommand())
        {
            sampleCmd.CommandText = sampleSql;
            using var reader = sampleCmd.ExecuteReader();
            while (reader.Read())
            {
                samples.Add(new LineageCycleRow(
                    reader.GetInt64(0), reader.GetString(1),
                    new CanonicalTokenUsage(reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4),
                        reader.GetInt64(5), reader.GetInt64(6), reader.GetInt64(7), reader.GetInt64(8),
                        reader.GetInt64(9)),
                    reader.GetInt64(10), reader.GetInt64(11), reader.GetInt64(12),
                    reader.IsDBNull(13) ? null : reader.GetInt64(13),
                    reader.IsDBNull(14) ? null : reader.GetInt64(14),
                    reader.IsDBNull(15) ? null : reader.GetString(15),
                    reader.GetInt32(16),
                    reader.IsDBNull(17) ? null : reader.GetInt64(17),
                    hasLineage && !reader.IsDBNull(18) ? reader.GetString(18) : "",
                    hasLineage && !reader.IsDBNull(19) ? reader.GetString(19) : "",
                    hasLineage && !reader.IsDBNull(20) ? reader.GetString(20) : "",
                    hasLineage && reader.GetInt64(21) != 0,
                    hasLineage && reader.FieldCount > 22 && !reader.IsDBNull(22) ? reader.GetString(22) : ""));
            }
        }

        var rawTotal = 0L;
        var storedCanonicalCount = 0L;
        var storedCanonicalTotal = 0L;
        foreach (var row in samples)
        {
            rawTotal += row.Last.Total;
            if (row.StoredCanonical)
            {
                storedCanonicalCount++;
                storedCanonicalTotal += row.Last.Total;
            }
        }

        var roots = new HashSet<string>(StringComparer.Ordinal);
        var cyclicIsolated = 0L;
        foreach (var row in samples)
        {
            var root = IndependentLineage.ResolveRoot(row.ThreadId, parents);
            roots.Add(root);
            if (parents.TryGetValue(row.ThreadId, out var parent) &&
                !string.IsNullOrWhiteSpace(parent) &&
                string.Equals(root, row.ThreadId, StringComparison.Ordinal) &&
                !string.Equals(parent, root, StringComparison.Ordinal))
            {
                cyclicIsolated++;
            }

            row.IndependentRoot = root;
            row.IndependentDepth = IndependentLineage.ResolvedDepth(row.ThreadId, parents);
            IndependentLineage.Describe(row);
        }

        var independentIds = IndependentLineage.ElectCanonicalIds(samples);

        var independentCount = (long)independentIds.Count;
        var independentTotal = 0L;
        var materialMismatch = 0L;
        foreach (var row in samples)
        {
            if (independentIds.Contains(row.Id)) independentTotal += row.Last.Total;
            if (!hasLineage) continue;
            var expectedCanonical = independentIds.Contains(row.Id);
            if (row.StoredCanonical != expectedCanonical) materialMismatch++;
            if (!row.IndependentValid) materialMismatch++;
            if (!string.IsNullOrWhiteSpace(row.StoredIdentity) &&
                !string.Equals(row.IndependentIdentity, row.StoredIdentity, StringComparison.Ordinal))
            {
                materialMismatch++;
            }
            if (!string.IsNullOrWhiteSpace(row.StoredMaterial) &&
                !string.Equals(row.IndependentMaterial, row.StoredMaterial, StringComparison.Ordinal))
            {
                materialMismatch++;
            }
            if (!string.IsNullOrWhiteSpace(row.StoredLegacy) &&
                !string.Equals(row.IndependentAlias, row.StoredLegacy, StringComparison.Ordinal))
            {
                materialMismatch++;
            }
            if (!string.IsNullOrWhiteSpace(row.StoredRoot) &&
                !string.Equals(row.IndependentRoot, row.StoredRoot, StringComparison.Ordinal))
            {
                materialMismatch++;
            }
        }

        var cycleIndependent = 0L;
        var hasQuotaTable = false;
        using (var tableCmd = connection.CreateCommand())
        {
            tableCmd.CommandText =
                "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'quota_observations';";
            hasQuotaTable = tableCmd.ExecuteScalar() is not null;
        }

        if (hasQuotaTable)
        using (var quotaCmd = connection.CreateCommand())
        {
            quotaCmd.CommandText = """
                SELECT window_duration_minutes, resets_at_utc
                FROM quota_observations
                WHERE is_primary = 1
                ORDER BY id DESC LIMIT 1;
                """;
            using var quotaReader = quotaCmd.ExecuteReader();
            if (quotaReader.Read())
            {
                var minutes = quotaReader.GetInt32(0);
                var resetText = quotaReader.GetString(1);
                if (minutes > 0 &&
                    DateTimeOffset.TryParse(resetText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                        out var resetAt))
                {
                    try
                    {
                        var cycleStart = resetAt.AddMinutes(-minutes).UtcTicks;
                        var cycleEnd = resetAt.UtcTicks;
                        foreach (var row in samples)
                        {
                            if (!row.EventTimeTicks.HasValue) continue;
                            var ticks = row.EventTimeTicks.Value;
                            if (ticks >= cycleStart && ticks < cycleEnd && independentIds.Contains(row.Id))
                                cycleIndependent += row.Last.Total;
                        }
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                    }
                }
            }
        }

        var mismatch = 0;
        if (hasLineage)
        {
            if (storedCanonicalCount != independentCount) mismatch = 1;
            if (storedCanonicalTotal != independentTotal) mismatch = 1;
            if (materialMismatch > 0) mismatch = 1;
        }
        else if (rawTotal != independentTotal)
        {
            mismatch = 1;
        }

        var status = mismatch == 0 ? "MATCH" : "MISMATCH";
        Console.WriteLine(
            $"LINEAGE_CYCLE_CHECK status={status} schema={schema} samples={samples.Count} " +
            $"canonical_stored={storedCanonicalCount} canonical_independent={independentCount} " +
            $"stored_canonical_total={storedCanonicalTotal} independent_total={independentTotal} " +
            $"raw_sample_total={rawTotal} roots={roots.Count} cyclic_isolated={cyclicIsolated} " +
            $"mismatch={mismatch} cycle_independent_total={cycleIndependent} db=readonly");
        return mismatch == 0 ? 0 : 1;
    }

    private sealed class LineageCycleRow
    {
        public LineageCycleRow(long id, string threadId, CanonicalTokenUsage last, long cumulativeInput,
            long cumulativeOutput, long cumulativeTotal, long? contextWindow, long? eventTimeTicks,
            string? sourceKey, int sourceGeneration, long? sourceOffset, string storedIdentity,
            string storedMaterial, string storedRoot, bool storedCanonical, string storedLegacy = "")
        {
            Id = id;
            ThreadId = threadId;
            Last = last;
            CumulativeInput = cumulativeInput;
            CumulativeOutput = cumulativeOutput;
            CumulativeTotal = cumulativeTotal;
            ContextWindow = contextWindow;
            EventTimeTicks = eventTimeTicks;
            SourceKey = sourceKey;
            SourceGeneration = sourceGeneration;
            SourceOffset = sourceOffset;
            StoredIdentity = storedIdentity;
            StoredMaterial = storedMaterial;
            StoredRoot = storedRoot;
            StoredCanonical = storedCanonical;
            StoredLegacy = storedLegacy;
        }

        public long Id { get; }
        public string ThreadId { get; }
        public CanonicalTokenUsage Last { get; }
        public long CumulativeInput { get; }
        public long CumulativeOutput { get; }
        public long CumulativeTotal { get; }
        public long? ContextWindow { get; }
        public long? EventTimeTicks { get; }
        public string? SourceKey { get; }
        public int SourceGeneration { get; }
        public long? SourceOffset { get; }
        public string StoredIdentity { get; }
        public string StoredMaterial { get; }
        public string StoredRoot { get; }
        public bool StoredCanonical { get; }
        public string StoredLegacy { get; }
        public string IndependentRoot { get; set; } = "";
        public int IndependentDepth { get; set; }
        public string IndependentMaterial { get; set; } = "";
        public string IndependentIdentity { get; set; } = "";
        public string IndependentAlias { get; set; } = "";
        public bool IndependentValid { get; set; } = true;
    }

    private static class IndependentLineage
    {
        private const string LivePrefix = "lineage-semantic-v2|";
        private const string LegacyPrefix = "lineage-semantic-v1|";

        public static string SampleSql(bool hasLineage, bool hasLegacyAlias)
        {
            if (!hasLineage)
            {
                return """
                    SELECT id, thread_id, input_tokens, raw_input_tokens, cached_input_tokens, cache_write_input_tokens,
                           output_tokens, reasoning_output_tokens, canonical_total_tokens, reported_total_tokens,
                           cumulative_input_tokens, cumulative_output_tokens, cumulative_total_tokens, context_window,
                           event_time_ticks, source_key, source_generation, source_offset
                    FROM token_samples
                    """;
            }

            if (hasLegacyAlias)
            {
                return """
                    SELECT id, thread_id, input_tokens, raw_input_tokens, cached_input_tokens, cache_write_input_tokens,
                           output_tokens, reasoning_output_tokens, canonical_total_tokens, reported_total_tokens,
                           cumulative_input_tokens, cumulative_output_tokens, cumulative_total_tokens, context_window,
                           event_time_ticks, source_key, source_generation, source_offset,
                           semantic_identity, semantic_material, lineage_root_thread_id, is_lineage_canonical,
                           legacy_semantic_identity
                    FROM token_samples
                    """;
            }

            return """
                SELECT id, thread_id, input_tokens, raw_input_tokens, cached_input_tokens, cache_write_input_tokens,
                       output_tokens, reasoning_output_tokens, canonical_total_tokens, reported_total_tokens,
                       cumulative_input_tokens, cumulative_output_tokens, cumulative_total_tokens, context_window,
                       event_time_ticks, source_key, source_generation, source_offset,
                       semantic_identity, semantic_material, lineage_root_thread_id, is_lineage_canonical,
                       ''
                FROM token_samples
                """;
        }

        public static void Describe(LineageCycleRow row)
        {
            var aliasMaterial = EncodeLegacy(row);
            var alias = Hash(aliasMaterial);
            row.IndependentAlias = alias;
            if (!row.StoredMaterial.StartsWith(LivePrefix, StringComparison.Ordinal))
            {
                row.IndependentMaterial = aliasMaterial;
                row.IndependentIdentity = alias;
                row.IndependentValid = true;
                return;
            }

            if (!TryParseLive(row.StoredMaterial, out var total, out var last, out var context))
            {
                row.IndependentMaterial = string.Empty;
                row.IndependentIdentity = string.Empty;
                row.IndependentValid = false;
                return;
            }

            var material = EncodeLive(total, last, context);
            row.IndependentMaterial = material;
            row.IndependentIdentity = Hash(material);
            row.IndependentValid = CanonicalColumnsMatch(total, last, context, row);
        }

        public static bool IsLiveRow(LineageCycleRow row) =>
            row.StoredMaterial.StartsWith(LivePrefix, StringComparison.Ordinal);

        public static bool IsLegacyRow(LineageCycleRow row) =>
            row.StoredMaterial.StartsWith(LegacyPrefix, StringComparison.Ordinal) || !IsLiveRow(row);

        public static string ResolveRoot(string threadId, IReadOnlyDictionary<string, string?> parents)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var current = threadId;
            while (true)
            {
                if (!parents.TryGetValue(current, out var parent) || string.IsNullOrWhiteSpace(parent))
                    return current;
                if (!parents.ContainsKey(parent)) return current;
                if (!seen.Add(current) || seen.Contains(parent) ||
                    string.Equals(parent, current, StringComparison.Ordinal))
                {
                    return threadId;
                }
                current = parent;
            }
        }

        public static int ResolvedDepth(string threadId, IReadOnlyDictionary<string, string?> parents)
        {
            var root = ResolveRoot(threadId, parents);
            if (string.Equals(threadId, root, StringComparison.Ordinal)) return 0;
            var depth = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal) { threadId };
            var current = threadId;
            while (true)
            {
                if (!parents.TryGetValue(current, out var parent) || string.IsNullOrWhiteSpace(parent))
                    return depth;
                if (!parents.ContainsKey(parent)) return depth;
                if (!seen.Add(parent) || string.Equals(parent, current, StringComparison.Ordinal))
                    return depth;
                depth++;
                if (string.Equals(parent, root, StringComparison.Ordinal)) return depth;
                current = parent;
            }
        }

        public static HashSet<long> ElectCanonicalIds(IReadOnlyList<LineageCycleRow> samples)
        {
            var groups = new Dictionary<string, List<LineageCycleRow>>(StringComparer.Ordinal);
            foreach (var row in samples)
            {
                var key = row.IndependentRoot + "\n" + row.IndependentIdentity;
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<LineageCycleRow>();
                    groups[key] = list;
                }
                list.Add(row);
            }

            var independentIds = new HashSet<long>();
            var liveWinners = new List<LineageCycleRow>();
            var legacyWinners = new List<LineageCycleRow>();
            foreach (var pair in groups)
            {
                var winner = Earliest(pair.Value);
                independentIds.Add(winner.Id);
                if (IsLiveRow(winner)) liveWinners.Add(winner);
                else if (IsLegacyRow(winner)) legacyWinners.Add(winner);
            }

            if (liveWinners.Count == 0 || legacyWinners.Count == 0) return independentIds;

            var livesByAlias = new Dictionary<string, List<LineageCycleRow>>(StringComparer.Ordinal);
            foreach (var live in liveWinners)
            {
                var key = live.IndependentRoot + "\n" + live.IndependentAlias;
                if (!livesByAlias.TryGetValue(key, out var list))
                {
                    list = new List<LineageCycleRow>();
                    livesByAlias[key] = list;
                }
                list.Add(live);
            }

            foreach (var list in livesByAlias.Values)
                list.Sort(CompareOrder);

            foreach (var legacy in legacyWinners)
            {
                if (!independentIds.Contains(legacy.Id)) continue;
                var key = legacy.IndependentRoot + "\n" + legacy.IndependentIdentity;
                if (!livesByAlias.TryGetValue(key, out var lives)) continue;
                LineageCycleRow? equivalent = null;
                foreach (var live in lives)
                {
                    if (!independentIds.Contains(live.Id)) continue;
                    equivalent = live;
                    break;
                }
                if (equivalent is null) continue;
                if (CompareOrder(legacy, equivalent) < 0) independentIds.Remove(equivalent.Id);
                else independentIds.Remove(legacy.Id);
            }

            return independentIds;
        }

        public static LineageCycleRow Earliest(IReadOnlyList<LineageCycleRow> rows)
        {
            var winner = rows[0];
            foreach (var row in rows)
            {
                if (CompareOrder(row, winner) < 0) winner = row;
            }
            return winner;
        }

        public static int CompareOrder(LineageCycleRow left, LineageCycleRow right)
        {
            var leftReliable = left.EventTimeTicks.HasValue;
            var rightReliable = right.EventTimeTicks.HasValue;
            if (leftReliable != rightReliable) return leftReliable ? -1 : 1;
            if (leftReliable)
            {
                var time = left.EventTimeTicks!.Value.CompareTo(right.EventTimeTicks!.Value);
                if (time != 0) return time;
                var depth = left.IndependentDepth.CompareTo(right.IndependentDepth);
                if (depth != 0) return depth;
            }

            var source = string.Compare(left.SourceKey ?? string.Empty, right.SourceKey ?? string.Empty,
                StringComparison.Ordinal);
            if ((left.SourceKey is null) != (right.SourceKey is null))
                return left.SourceKey is null ? 1 : -1;
            if (source != 0) return source;
            var generation = left.SourceGeneration.CompareTo(right.SourceGeneration);
            if (generation != 0) return generation;
            if ((left.SourceOffset is null) != (right.SourceOffset is null))
                return left.SourceOffset is null ? 1 : -1;
            if (left.SourceOffset.HasValue)
            {
                var offset = left.SourceOffset.Value.CompareTo(right.SourceOffset!.Value);
                if (offset != 0) return offset;
            }
            return left.Id.CompareTo(right.Id);
        }

        public static bool TryParseLive(string material, out long?[] total, out long?[] last, out long? context)
        {
            total = Array.Empty<long?>();
            last = Array.Empty<long?>();
            context = null;
            if (!material.StartsWith(LivePrefix, StringComparison.Ordinal)) return false;
            var parts = material.Split('|');
            if (parts.Length != 4) return false;
            if (!string.Equals(parts[0], "lineage-semantic-v2", StringComparison.Ordinal)) return false;
            if (!TryParsePresenceTuple(parts[1], out total)) return false;
            if (!TryParsePresenceTuple(parts[2], out last)) return false;
            return TryParsePresence(parts[3], out context);
        }

        public static string EncodeLive(long?[] total, long?[] last, long? context) =>
            string.Join('|', "lineage-semantic-v2", JoinPresence(total), JoinPresence(last),
                FormatPresence(context));

        public static string EncodeLegacy(LineageCycleRow row)
        {
            static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
            var last = row.Last;
            var lastPart = string.Join(',', Number(last.Input), Number(last.RawInput), Number(last.CachedInput),
                Number(last.CacheWriteInput), Number(last.Output), Number(last.Reasoning), Number(last.Total),
                Number(last.ReportedTotal));
            var totalPart = string.Join(',', Number(row.CumulativeInput), Number(row.CumulativeOutput),
                Number(row.CumulativeTotal));
            var context = row.ContextWindow is > 0 ? "v:" + Number(row.ContextWindow.Value) : "m";
            return string.Join('|', "lineage-semantic-v1", lastPart, totalPart, context);
        }

        public static string Hash(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

        private static bool TryParsePresenceTuple(string text, out long?[] values)
        {
            var fields = text.Split(',');
            values = new long?[7];
            if (fields.Length != 7) return false;
            for (var index = 0; index < 7; index++)
            {
                if (!TryParsePresence(fields[index], out values[index])) return false;
            }
            return true;
        }

        private static bool TryParsePresence(string text, out long? value)
        {
            value = null;
            if (string.Equals(text, "m", StringComparison.Ordinal)) return true;
            if (!text.StartsWith("v:", StringComparison.Ordinal)) return false;
            if (!long.TryParse(text.AsSpan(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return false;
            value = parsed;
            return true;
        }

        private static string JoinPresence(long?[] values)
        {
            var parts = new string[values.Length];
            for (var index = 0; index < values.Length; index++)
                parts[index] = FormatPresence(values[index]);
            return string.Join(',', parts);
        }

        private static string FormatPresence(long? value) => value.HasValue
            ? "v:" + value.Value.ToString(CultureInfo.InvariantCulture)
            : "m";

        private static bool CanonicalColumnsMatch(long?[] total, long?[] last, long? context,
            LineageCycleRow row)
        {
            var canonicalLast = Canonicalize(last);
            var canonicalTotal = Canonicalize(total);
            return canonicalLast.Input == row.Last.Input &&
                   canonicalLast.RawInput == row.Last.RawInput &&
                   canonicalLast.CachedInput == row.Last.CachedInput &&
                   canonicalLast.CacheWriteInput == row.Last.CacheWriteInput &&
                   canonicalLast.Output == row.Last.Output &&
                   canonicalLast.Reasoning == row.Last.Reasoning &&
                   canonicalLast.Total == row.Last.Total &&
                   canonicalLast.ReportedTotal == row.Last.ReportedTotal &&
                   canonicalTotal.Input == row.CumulativeInput &&
                   canonicalTotal.Output == row.CumulativeOutput &&
                   canonicalTotal.Total == row.CumulativeTotal &&
                   Nullable.Equals(context, row.ContextWindow);
        }

        private static CanonicalTokenUsage Canonicalize(long?[] values)
        {
            static long NonNegative(long value) => value < 0 ? 0 : value;
            static long SaturatingAdd(long left, long right)
            {
                if (right > 0 && left > long.MaxValue - right) return long.MaxValue;
                if (right < 0 && left < long.MinValue - right) return long.MinValue;
                return left + right;
            }

            var input = NonNegative(values[0] ?? 0);
            var cached = Math.Max(NonNegative(values[1] ?? 0), NonNegative(values[2] ?? 0));
            cached = Math.Min(cached, input);
            var cacheWrite = NonNegative(values[3] ?? 0);
            var output = NonNegative(values[4] ?? 0);
            var reasoning = Math.Min(NonNegative(values[5] ?? 0), output);
            return new CanonicalTokenUsage(input, Math.Max(input - cached, 0), cached, cacheWrite,
                output, reasoning, SaturatingAdd(input, output), NonNegative(values[6] ?? 0));
        }
    }

    private static async Task<int> RunQuotaCheck()
    {
        var executable = new CodexExecutableDiscovery().Find();
        if (string.IsNullOrWhiteSpace(executable))
        {
            Console.WriteLine("QUOTA status=UNAVAILABLE code=codex_executable_missing");
            return 2;
        }

        var result = await new AppServerClient().ReadRateLimitsAsync(executable);
        var primary = result.Observation.Primary;
        if (primary is null)
        {
            Console.WriteLine($"QUOTA status=UNAVAILABLE code={result.ErrorCode ?? "quota_unavailable"} args={result.RequestArguments} tier_override={result.TierOverrideRequested} method={AppServerProtocol.RateLimitsMethod}");
            foreach (var bucket in result.Observation.Additional)
            {
                Console.WriteLine($"QUOTA_BUCKET id={bucket.Id} name={bucket.Name} used={bucket.UsedPercent:0.##} " +
                                  $"duration={bucket.WindowDurationMinutes} reset={bucket.ResetsAtUtc:O}");
            }
            return 2;
        }

        Console.WriteLine($"QUOTA status=OK id={primary.Id} used={primary.UsedPercent:0.##} remaining={primary.RemainingPercent:0.##} reset={primary.ResetsAtUtc:O} source={result.Observation.Source} args={result.RequestArguments} tier_override={result.TierOverrideRequested} method={AppServerProtocol.RateLimitsMethod}");
        return 0;
    }

    private static int RunRefreshDiagnostic(string[] args)
    {
        var root = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : NewRunRoot("refresh-diagnostic");
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "diagnostic.jsonl");
        File.Copy(Fixture("cumulative-sequential.jsonl"), source, true);
        using var database = new UsageDatabase(Path.Combine(root, "usage.db"));
        var indexer = new RolloutIndexer(database);
        var initial = indexer.ScanFile(source, "sessions/diagnostic.jsonl", "thread-fixture-a");
        var unchanged = indexer.ScanFile(source, "sessions/diagnostic.jsonl", "thread-fixture-a");
        var append = TokenLine("thread-fixture-a", 1700000099, 150, 40, 50, 10) + "\n";
        var appendBytes = Encoding.UTF8.GetByteCount(append);
        File.AppendAllText(source, append, new UTF8Encoding(false));
        var appended = indexer.ScanFile(source, "sessions/diagnostic.jsonl", "thread-fixture-a");
        var okay = initial.AcceptedSamples == 2 && unchanged.BytesRead == 0 && unchanged.TokenWrites == 0 &&
                   unchanged.SourceWrites == 0 && appended.BytesRead == appendBytes && appended.AcceptedSamples == 1;
        Console.WriteLine($"DIAGNOSTIC status={(okay ? "OK" : "MISMATCH")} initial_bytes={initial.BytesRead} unchanged_bytes={unchanged.BytesRead} unchanged_token_writes={unchanged.TokenWrites} unchanged_source_writes={unchanged.SourceWrites} append_bytes={appended.BytesRead} expected_append_bytes={appendBytes} append_token_writes={appended.TokenWrites} append_source_writes={appended.SourceWrites} elapsed_ms={initial.ElapsedMilliseconds + unchanged.ElapsedMilliseconds + appended.ElapsedMilliseconds}");
        return okay ? 0 : 2;
    }

    private static int RunGateHelper(string[] args)
    {
        if (args.Length < 2 || !int.TryParse(args[1], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var holdMilliseconds))
        {
            Console.WriteLine("GATE outcome=ERROR code=gate_helper_arguments");
            return 3;
        }

        var outcome = SingleInstanceGate.TryAcquire(args[0], out var gate, out var code);
        Console.WriteLine($"GATE outcome={outcome.ToString().ToUpperInvariant()} code={code}");
        Console.Out.Flush();
        if (outcome == InstanceGateOutcome.Acquired && gate is not null)
        {
            if (holdMilliseconds > 0)
                new ManualResetEventSlim(false).Wait(TimeSpan.FromMilliseconds(holdMilliseconds));
            gate.Dispose();
        }
        return outcome == InstanceGateOutcome.Error ? 3 : 0;
    }

    private static int RunCorrection03Check()
    {
        var root = NewRunRoot("correction-03-check");
        try
        {
            Correction03StaticPrivacy(root);
            var evidence = Correction03Performance(root);
            PrintPerformance(evidence);
            Console.WriteLine("CORRECTION_03 status=OK static_privacy=True package_preparation=True");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"CORRECTION_03 status=ERROR code={exception.GetType().Name}");
            return 2;
        }
    }

    private static int RunCorrection04Check()
    {
        var root = NewRunRoot("correction-04-check");
        try
        {
            SemanticAliasDuplicates(root);
            PrivateScrubHardDeathMatrix(root);
            PrivateScrubMarkerStateMatrix(root);
            DuplicateMetadataEnrichment(root);
            EnrichmentLatestOracleReverse(root);
            AppIoFifoDispatcherShutdown(root);
            StaticDispatcherOwnership();
            BoundedRecurringTurns(root);
            ServiceTierProvenanceTest(root);
            ScopedPrivacyEvidence(root);
            var evidence = Correction03Performance(root);
            PrintPerformance(evidence);
            Console.WriteLine("CORRECTION_04 status=OK source_scope=True dispatcher_io=False " +
                              "database_privacy=True hud_log_privacy=True rendered_string_privacy=True");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"CORRECTION_04 status=ERROR code={exception.GetType().Name}");
            return 2;
        }
    }

    private static int RunMigrationCrashHelper(string[] args)
    {
        if (args.Length != 2) return 3;
        Environment.SetEnvironmentVariable("CODEX_USAGE_HUD_TEST_MIGRATION_CRASH", args[1]);
        _ = new UsageDatabase(args[0]);
        return 3;
    }

    private static int RunAppServerHelper(string[] args)
    {
        if (args.Length < 2) return 3;
        var mode = args[0];
        File.WriteAllText(args[1], Environment.ProcessId.ToString(CultureInfo.InvariantCulture), Encoding.ASCII);
        if (Console.ReadLine() is null) return 4;

        if (mode.Equals("initialize-exit", StringComparison.Ordinal)) return 7;

        if (mode.Equals("bounded-success", StringComparison.Ordinal))
        {
            Console.Error.Write(new string('e', PrivacyJsonlReader.AllowlistedRecordLimit + 4096));
            Console.Error.Flush();
            Console.Out.WriteLine(new string('x', PrivacyJsonlReader.AllowlistedRecordLimit + 4096));
            Console.Out.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{}}");
            Console.Out.Flush();
            if (Console.ReadLine() is null) return 5;
            Console.Out.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{\"rateLimits\":{\"id\":\"codex-main\",\"name\":\"Codex\",\"usedPercent\":12,\"windowDurationMins\":10080,\"resetsAt\":1900000000}}}");
            Console.Out.Flush();
            new ManualResetEventSlim(false).Wait();
            return 0;
        }

        if (mode.Equals("unterminated-stdout", StringComparison.Ordinal))
        {
            Console.Out.Write(new string('x', PrivacyJsonlReader.AllowlistedRecordLimit + 4096));
            Console.Out.Flush();
            new ManualResetEventSlim(false).Wait();
            return 0;
        }

        return 6;
    }

    private static void TokenMathAndOverflow()
    {
        var canonical = new TokenComponents(long.MaxValue, 10, 10, 8, long.MaxValue, long.MaxValue, long.MaxValue).ToCanonical();
        Assert.Equal(long.MaxValue, canonical.Input);
        Assert.Equal(long.MaxValue - 10, canonical.RawInput);
        Assert.Equal(long.MaxValue, canonical.Output);
        Assert.Equal(long.MaxValue, canonical.Total);
        Assert.Equal(long.MaxValue, (new CanonicalTokenUsage(long.MaxValue, 0, 0, 0, 1, 0, long.MaxValue, 0) +
                                     new CanonicalTokenUsage(1, 0, 0, 0, 1, 0, 1, 0)).Total);
    }

    private static void DatabaseSaturatingAggregates(string runRoot)
    {
        var directory = Path.Combine(runRoot, "database-saturation");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "usage.db");
        var eventTime = DateTimeOffset.UtcNow.AddMinutes(-1);
        var snapshot = new TokenUsageSnapshot(
            new TokenComponents(long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue,
                long.MaxValue, long.MaxValue, long.MaxValue),
            new TokenComponents(long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue,
                long.MaxValue, long.MaxValue, long.MaxValue),
            128000);
        var replayDistinct = snapshot with { ContextWindow = 128001 };
        using (var database = new UsageDatabase(databasePath))
        {
            Assert.True(database.AcceptTokenSample("thread-saturation", snapshot, eventTime,
                eventTime, "turn-saturation", null, null));
            Assert.True(database.AcceptTokenSample("thread-saturation", replayDistinct, eventTime.AddSeconds(1),
                eventTime.AddSeconds(1), "turn-saturation", null, null));
            database.UpsertSession(new SessionMetadata("thread-saturation", "Saturated", null, null, null,
                null, null, null, eventTime, null), SessionStatus.Running, "turn-saturation", 1, true);

            AssertSaturated(database.GetSessionTotal("thread-saturation"));
            AssertSaturated(database.LoadSessionTotals()["thread-saturation"]);
            AssertSaturated(database.LoadTurnTotals()[("thread-saturation", "turn-saturation")]);
            AssertSaturated(database.GetCycleTotal(eventTime.AddMinutes(-1), eventTime.AddMinutes(2)));
            AssertSaturated(database.GetCycleTotalForThreads(eventTime.AddMinutes(-1),
                eventTime.AddMinutes(2), new[] { "thread-saturation" }));
        }

        using var restarted = new UsageDatabase(databasePath);
        AssertSaturated(restarted.GetSessionTotal("thread-saturation"));
        AssertSaturated(restarted.LoadSessionTotals()["thread-saturation"]);
        AssertSaturated(restarted.LoadTurnTotals()[("thread-saturation", "turn-saturation")]);
        AssertSaturated(restarted.GetCycleTotal(eventTime.AddMinutes(-1), eventTime.AddMinutes(2)));
        AssertSaturated(restarted.GetCycleTotalForThreads(eventTime.AddMinutes(-1),
            eventTime.AddMinutes(2), new[] { "thread-saturation" }));
    }

    private static void AssertSaturated(CanonicalTokenUsage usage)
    {
        Assert.Equal(long.MaxValue, usage.Input);
        Assert.Equal(long.MaxValue, usage.CachedInput);
        Assert.Equal(long.MaxValue, usage.CacheWriteInput);
        Assert.Equal(long.MaxValue, usage.Output);
        Assert.Equal(long.MaxValue, usage.Reasoning);
        Assert.Equal(long.MaxValue, usage.Total);
        Assert.Equal(long.MaxValue, usage.ReportedTotal);
    }

    private static void FixtureACumulativeEquality(string runRoot)
    {
        using var database = NewDatabase(runRoot, "fixture-a");
        var result = new RolloutIndexer(database).ScanFile(Fixture("cumulative-sequential.jsonl"),
            "fixtures/cumulative-sequential.jsonl", "thread-fixture-a");
        Assert.Equal(2, result.AcceptedSamples);
        var total = database.GetSessionTotal("thread-fixture-a");
        Assert.Equal(new CanonicalTokenUsage(100, 85, 15, 0, 30, 8, 130, 130), total);
        using var connection = new SqliteConnection($"Data Source={database.DatabasePath};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM token_samples WHERE source_key IS NULL OR source_offset IS NULL;";
        Assert.Equal(0L, Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture));
    }

    private static void FixtureBReplay(string runRoot)
    {
        using var database = NewDatabase(runRoot, "fixture-b");
        var result = new RolloutIndexer(database).ScanFile(Fixture("duplicate-stale-replay.jsonl"),
            "fixtures/duplicate-stale-replay.jsonl", "thread-fixture-b");
        Assert.Equal(2, result.AcceptedSamples);
        Assert.Equal(2, result.DuplicateSamples);
        Assert.Equal(2L, database.GetFingerprintCount());
        Assert.Equal(2L, database.GetDuplicateObservationCount());
        Assert.Equal(170L, database.GetSessionTotal("thread-fixture-b").Total);
    }

    private static void ConcurrentInterleaving(string runRoot)
    {
        using var database = NewDatabase(runRoot, "interleaved");
        var result = new RolloutIndexer(database).ScanFile(Fixture("concurrent-interleaving.jsonl"),
            "fixtures/concurrent-interleaving.jsonl", "thread-interleaved");
        var total = database.GetSessionTotal("thread-interleaved");
        Assert.Equal(4, result.AcceptedSamples);
        Assert.Equal(250L, total.Input);
        Assert.Equal(60L, total.Output);
        Assert.Equal(310L, total.Total);
        Assert.True(total.Total > 260L);
        Assert.True(result.ErrorCodes.Contains("cumulative_interleaving_observed", StringComparer.Ordinal));
    }

    private static void OldFrontierMigration(string runRoot)
    {
        var directory = Path.Combine(runRoot, "migration");
        Directory.CreateDirectory(directory);
        var dbPath = Path.Combine(directory, "usage.db");
        var fixture = Fixture("concurrent-interleaving.jsonl");
        var reader = new PrivacyJsonlReader().Read(fixture, 0, default, long.MaxValue, TimeSpan.FromSeconds(5));
        var tokenEvents = reader.Events.Where(item => item.TokenSnapshot is not null).ToArray();
        var identity = new FileIdentityProvider().Get(fixture, "thread-interleaved");
        using (var database = new UsageDatabase(dbPath))
        {
            foreach (var index in new[] { 0, 1, 3 })
            {
                var item = tokenEvents[index];
                Assert.True(database.AcceptTokenSample("thread-interleaved", item.TokenSnapshot!, item.EventTimeUtc,
                    DateTimeOffset.UtcNow, "turn-1", null, null));
            }
        }

        using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            connection.Open();
            var missingFingerprint = tokenEvents[2].TokenSnapshot!.Fingerprint("thread-interleaved");
            var unavailableSourceFingerprint = new string('a', 64);
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR REPLACE INTO source_files(source_key, thread_id, volume_serial, file_id, fallback_digest,
                    is_degraded, relative_path, generation, complete_offset, drain_mode,
                    drain_line_start_offset, drain_offset, last_length, health_code, state_revision)
                VALUES ($key, $thread, $volume, $file, $fallback, $degraded,
                    'sessions/interleaved.jsonl', 0, $length, 0, 0, 0, $length, 'ok', 0);
                INSERT OR REPLACE INTO event_fingerprints(
                    fingerprint, thread_id, first_seen_utc, last_seen_utc, duplicate_count, disposition)
                VALUES ($missing_fingerprint, $thread, $observed, $observed, 0, 'rejected');
                INSERT OR REPLACE INTO event_fingerprints(
                    fingerprint, thread_id, first_seen_utc, last_seen_utc, duplicate_count, disposition)
                VALUES ($unavailable_fingerprint, 'thread-unavailable-source', $observed, $observed, 0, 'rejected');
                INSERT INTO schema_info(key, value) VALUES ('token_cumulative_guard', '1') ON CONFLICT(key) DO UPDATE SET value='1';
                INSERT INTO schema_info(key, value) VALUES ('token_tuple_oracle_migration', '0') ON CONFLICT(key) DO UPDATE SET value='0';
                INSERT INTO schema_info(key, value) VALUES ('deterministic_turn_keys', '0') ON CONFLICT(key) DO UPDATE SET value='0';
                """;
            command.Parameters.AddWithValue("$key", identity.StableKey);
            command.Parameters.AddWithValue("$thread", identity.ThreadId);
            command.Parameters.AddWithValue("$volume", identity.VolumeSerial);
            command.Parameters.AddWithValue("$file", identity.FileId);
            command.Parameters.AddWithValue("$fallback", identity.FallbackKey);
            command.Parameters.AddWithValue("$degraded", identity.IsDegraded ? 1 : 0);
            command.Parameters.AddWithValue("$length", new FileInfo(fixture).Length);
            command.Parameters.AddWithValue("$missing_fingerprint", missingFingerprint);
            command.Parameters.AddWithValue("$unavailable_fingerprint", unavailableSourceFingerprint);
            command.Parameters.AddWithValue("$observed", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }

        long migratedOffset;
        CanonicalTokenUsage recovered;
        using (var migrated = new UsageDatabase(dbPath))
        {
            migratedOffset = migrated.LoadSourceStates().Single().CompleteOffset;
            Assert.Equal(0L, migratedOffset);
            var indexer = new RolloutIndexer(migrated);
            var pending = indexer.ScanFile(fixture, "sessions/interleaved.jsonl", "thread-interleaved");
            Assert.Equal(0, pending.AcceptedSamples);
            Assert.True(pending.ErrorCodes.Contains("aggregate_rebuild_pending", StringComparer.Ordinal));
            while (!migrated.RunAggregateRebuildBatch()) { }
            var scan = indexer.ScanFile(fixture, "sessions/interleaved.jsonl", "thread-interleaved");
            Assert.Equal(1, scan.AcceptedSamples);
            recovered = migrated.GetSessionTotal("thread-interleaved");
            Assert.Equal(310L, recovered.Total);
            Assert.Equal(4L, migrated.GetSampleCount("thread-interleaved"));
            Assert.Equal(4L, migrated.GetFingerprintCount());
            Assert.Equal("2", migrated.ReadSchemaValue("token_tuple_oracle_migration"));
        }
        using var restarted = new UsageDatabase(dbPath);
        Assert.Equal(new FileInfo(fixture).Length, restarted.LoadSourceStates().Single().CompleteOffset);
        var secondScan = new RolloutIndexer(restarted).ScanFile(fixture, "sessions/interleaved.jsonl", "thread-interleaved");
        Assert.Equal(0, secondScan.AcceptedSamples);
        Assert.Equal(new FileInfo(fixture).Length, restarted.LoadSourceStates().Single().CompleteOffset);
        Assert.Equal(recovered, restarted.GetSessionTotal("thread-interleaved"));
    }

    private static void RotationPartialRestart(string runRoot)
    {
        using var database = NewDatabase(runRoot, "rotation");
        var indexer = new RolloutIndexer(database);
        var source = Path.Combine(runRoot, "rotation-active.jsonl");
        var archive = Path.Combine(runRoot, "rotation-archive.jsonl");
        File.Copy(Fixture(Path.Combine("rotation", "active.jsonl")), source, true);
        var first = indexer.ScanFile(source, "sessions/active.jsonl", "thread-fixture-c");
        var partial = File.ReadAllText(Fixture(Path.Combine("rotation", "partial-tail.jsonl"))).TrimEnd('\r', '\n');
        File.AppendAllText(source, partial, new UTF8Encoding(false));
        var partialResult = indexer.ScanFile(source, "sessions/active.jsonl", "thread-fixture-c");
        Assert.Equal(first.NewOffset, partialResult.NewOffset);
        File.AppendAllText(source, "\n", new UTF8Encoding(false));
        Assert.Equal(1, indexer.ScanFile(source, "sessions/active.jsonl", "thread-fixture-c").AcceptedSamples);
        File.Copy(source, archive, true);
        Assert.Equal(0, indexer.ScanFile(archive, "archived_sessions/archive.jsonl", "thread-fixture-c").AcceptedSamples);
        File.WriteAllText(source, File.ReadAllText(Fixture(Path.Combine("rotation", "active.jsonl"))), new UTF8Encoding(false));
        var truncated = indexer.ScanFile(source, "sessions/active.jsonl", "thread-fixture-c");
        Assert.True(truncated.Generation >= 1);
        var before = database.GetSessionTotal("thread-fixture-c");
        using var restarted = new UsageDatabase(database.DatabasePath);
        var restartedIndexer = new RolloutIndexer(restarted);
        restartedIndexer.ScanFile(source, "sessions/active.jsonl", "thread-fixture-c");
        restartedIndexer.ScanFile(archive, "archived_sessions/archive.jsonl", "thread-fixture-c");
        Assert.Equal(before, restarted.GetSessionTotal("thread-fixture-c"));
    }

    private static void WindowsFileIdentity(string runRoot)
    {
        if (!OperatingSystem.IsWindows()) return;
        var directory = Path.Combine(runRoot, "file-id");
        Directory.CreateDirectory(directory);
        var firstPath = Path.Combine(directory, "first.jsonl");
        var movedPath = Path.Combine(directory, "moved.jsonl");
        File.WriteAllText(firstPath, "{}\n", new UTF8Encoding(false));
        var provider = new FileIdentityProvider();
        var first = provider.Get(firstPath, "thread-file-id");
        Assert.True(!first.IsDegraded);
        Assert.Equal(32, first.FileId.Length);
        Assert.Equal(16, first.VolumeSerial.Length);
        File.Move(firstPath, movedPath);
        var moved = provider.Get(movedPath, "thread-file-id");
        Assert.Equal(first.VolumeSerial, moved.VolumeSerial);
        Assert.Equal(first.FileId, moved.FileId);
        Assert.Equal(first.StableKey, moved.StableKey);

        var databasePath = Path.Combine(directory, "migration.db");
        using (var initialized = new UsageDatabase(databasePath)) { }
        using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO source_files(source_key, thread_id, volume_serial, file_id, fallback_digest,
                    is_degraded, relative_path, generation, complete_offset, drain_mode,
                    drain_line_start_offset, drain_offset, last_length, health_code, state_revision)
                VALUES ('legacy', 'thread-file-id', '12345678', '1234567890ABCDEF', '', 0,
                    'sessions/legacy.jsonl', 0, 10, 0, 0, 0, 10, 'ok', 0);
                UPDATE schema_info SET value = '0' WHERE key = 'windows_file_id_128';
                """;
            command.ExecuteNonQuery();
        }
        using var migrated = new UsageDatabase(databasePath);
        var reset = migrated.LoadSourceStates().Single();
        Assert.Equal(0L, reset.CompleteOffset);
        Assert.Equal("file_identity_rescan", reset.HealthCode);
    }

    private static void SameLengthRewrite(string runRoot)
    {
        var directory = Path.Combine(runRoot, "same-length-rewrite");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "rollout.jsonl");
        var firstText = "{\"type\":\"session_meta\",\"payload\":{\"id\":\"thread-rewrite\"}}\n" +
                        TokenLine("thread-rewrite", 1700003000, 10, 2, 10, 2) + "\n";
        var secondText = "{\"type\":\"session_meta\",\"payload\":{\"id\":\"thread-rewrite\"}}\n" +
                         TokenLine("thread-rewrite", 1700003001, 20, 3, 20, 3) + "\n";
        Assert.Equal(Encoding.UTF8.GetByteCount(firstText), Encoding.UTF8.GetByteCount(secondText));
        File.WriteAllText(source, firstText, new UTF8Encoding(false));
        var provider = new FileIdentityProvider();
        var identity = provider.Get(source, "thread-rewrite");
        using var database = NewDatabase(runRoot, "same-length-rewrite");
        var indexer = new RolloutIndexer(database);
        Assert.Equal(1, indexer.ScanFile(source, "sessions/rewrite.jsonl", "thread-rewrite").AcceptedSamples);
        var nextWrite = File.GetLastWriteTimeUtc(source).AddSeconds(2);
        File.WriteAllText(source, secondText, new UTF8Encoding(false));
        File.SetLastWriteTimeUtc(source, nextWrite);
        var rewrittenIdentity = provider.Get(source, "thread-rewrite");
        Assert.Equal(identity.FileId, rewrittenIdentity.FileId);
        var rewritten = indexer.ScanFile(source, "sessions/rewrite.jsonl", "thread-rewrite");
        Assert.True(rewritten.Generation >= 1);
        Assert.Equal(1, rewritten.AcceptedSamples);
        Assert.Equal(35L, database.GetSessionTotal("thread-rewrite").Total);
    }

    private static void MalformedBatchContinues(string runRoot)
    {
        var directory = Path.Combine(runRoot, "malformed-continuation");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "rollout.jsonl");
        var builder = new StringBuilder("{malformed\n");
        builder.Append("{\"type\":\"session_meta\",\"payload\":{\"id\":\"thread-malformed-tail\"}}\n");
        const int expected = 400;
        for (var index = 1; index <= expected; index++)
        {
            builder.Append(TokenLine("thread-malformed-tail", 1700010000 + index, index, 1, 1, 1)).Append('\n');
        }
        File.WriteAllText(source, builder.ToString(), new UTF8Encoding(false));
        using var database = NewDatabase(runRoot, "malformed-continuation");
        var indexer = new RolloutIndexer(database);
        var first = indexer.ScanFile(source, "sessions/malformed.jsonl", "thread-malformed-tail",
            default, 16 * 1024);
        Assert.True(first.HasMoreData);
        Assert.True(first.ErrorCodes.Contains("record_structural_error", StringComparer.Ordinal));
        for (var pass = 0; pass < 100 && database.GetSampleCount("thread-malformed-tail") < expected; pass++)
        {
            var next = indexer.ScanFile(source, "sessions/malformed.jsonl", "thread-malformed-tail",
                default, 16 * 1024);
            if (!next.HasMoreData) break;
        }
        Assert.Equal((long)expected, database.GetSampleCount("thread-malformed-tail"));
        Assert.Equal(new FileInfo(source).Length, database.LoadSourceStates().Single().CompleteOffset);
    }

    private static void PrivacyEarlyDrain(string runRoot)
    {
        var directory = Path.Combine(runRoot, "privacy");
        Directory.CreateDirectory(directory);
        var shortPath = Path.Combine(directory, "short.jsonl");
        var shortUnknown = $"{{\"type\":\"message\",\"payload\":{{\"body\":\"{Sentinel}\"}}}}\n";
        File.WriteAllText(shortPath, shortUnknown, new UTF8Encoding(false));
        var shortBatch = new PrivacyJsonlReader().Read(shortPath, 0);
        Assert.True(shortBatch.PeakRetainedBytes < Encoding.UTF8.GetByteCount(shortUnknown));

        var decoyPath = Path.Combine(directory, "direct-payload-decoy.jsonl");
        var decoy = $"{{\"type\":\"event_msg\",\"metadata\":{{\"type\":\"token_count\",\"body\":\"{Sentinel}\"}},\"payload\":{{\"type\":\"user_message\",\"body\":\"{Sentinel}\"}}}}\n";
        File.WriteAllText(decoyPath, decoy, new UTF8Encoding(false));
        var decoyBatch = new PrivacyJsonlReader().Read(decoyPath, 0);
        Assert.Equal(0, decoyBatch.Events.Count);
        Assert.True(decoyBatch.PeakRetainedBytes < Encoding.UTF8.GetByteCount(decoy));

        var largePath = Path.Combine(directory, "large.jsonl");
        var largeUnknown = $"{{\"type\":\"message\",\"payload\":{{\"body\":\"{new string('x', 300_000)}{Sentinel}\"}}}}\n";
        File.WriteAllText(largePath, largeUnknown, new UTF8Encoding(false));
        var largeBatch = new PrivacyJsonlReader().Read(largePath, 0, default, long.MaxValue, TimeSpan.FromSeconds(5));
        Assert.True(largeBatch.PeakRetainedBytes < 1024);
        Assert.True(!largeBatch.ErrorCodes.Contains("record_over_limit", StringComparer.Ordinal));

        var source = Path.Combine(directory, "combined.jsonl");
        var oversizedAllowed = $"{{\"type\":\"event_msg\",\"payload\":{{\"type\":\"token_count\",\"thread_id\":\"thread-privacy\",\"unapproved\":\"{new string('q', 270_000)}{Sentinel}\"}}}}\n";
        var structural = $"{{\"type\":\"turn_context\",\"payload\":{{\"thread_id\":\"thread-privacy\",\"model\":\"gpt-safe\",\"unapproved\":\"{Sentinel}\"}}}}\n";
        var valid = TokenLine("thread-privacy", 1700002000, 5, 2, 5, 2) + "\n";
        File.WriteAllText(source, decoy + shortUnknown + oversizedAllowed + structural + valid, new UTF8Encoding(false));
        using var database = new UsageDatabase(Path.Combine(directory, "usage.db"));
        var result = new RolloutIndexer(database).ScanFile(source, "sessions/privacy.jsonl", "thread-privacy",
            default, long.MaxValue);
        Assert.Equal(1, result.AcceptedSamples);
        Assert.True(result.ErrorCodes.Contains("record_over_limit", StringComparer.Ordinal));
        Assert.True(!database.ContainsPrivacySentinel(Sentinel));
        var logPath = Path.Combine(directory, "hud.log");
        using (var log = new PrivacyLog(logPath)) log.WriteCode("source_io", "sessions/privacy.jsonl", 0);
        Assert.DoesNotContain(Sentinel, File.ReadAllText(logPath));
        var metadata = database.LoadSessions().Single();
        var aggregate = new SessionAggregate(metadata, SessionStatus.Idle, database.GetSessionTotal(metadata.ThreadId),
            database.GetSessionTotal(metadata.ThreadId), metadata.LastActivityUtc, metadata.CurrentTurnKey);
        var snapshot = new HudSnapshot(new QuotaObservation(null, Array.Empty<QuotaBucket>(), QuotaSource.Unavailable,
            DateTimeOffset.UtcNow, false), new[] { aggregate }, null, DateTimeOffset.UtcNow, false,
            "不可用", result.ErrorCodes, database.LoadRecentEvents());
        var viewModel = new MainViewModel();
        viewModel.Apply(snapshot);
        Assert.True(viewModel.RenderedStrings().All(value => !value.Contains(Sentinel, StringComparison.Ordinal)));
    }

    private static void SessionIndexStreaming(string runRoot)
    {
        var home = Path.Combine(runRoot, "session-index-home");
        Directory.CreateDirectory(home);
        var contents = "{malformed\n" +
            $"{{\"thread_id\":\"thread-good\",\"unapproved\":\"{Sentinel}\",\"thread_name\":\"Safe name\"}}\n" +
            "{\"thread_id\":\"thread-later\",\"thread_name\":\"Later name\"}\n";
        File.WriteAllText(Path.Combine(home, "session_index.jsonl"), contents, new UTF8Encoding(false));
        var values = new SessionIndexReader().Read(home);
        Assert.Equal(2, values.Count);
        Assert.Equal("Safe name", values["thread-good"]);
        Assert.True(values.Values.All(value => !value.Contains(Sentinel, StringComparison.Ordinal)));
    }

    private static void MetadataUnixAndContinuation(string runRoot)
    {
        var home = Path.Combine(runRoot, "metadata-home");
        var sessions = Path.Combine(home, "sessions");
        Directory.CreateDirectory(sessions);
        var rollout = Path.Combine(sessions, "one.jsonl");
        File.WriteAllText(rollout, string.Empty, new UTF8Encoding(false));
        var dbPath = Path.Combine(home, "state_5.sqlite");
        using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE threads(id TEXT, rollout_path TEXT, created_at, updated_at, cwd TEXT, model TEXT,
                    reasoning_effort TEXT, source TEXT, agent_nickname TEXT, agent_role TEXT, name TEXT, private_blob TEXT);
                INSERT INTO threads VALUES ('bad-row', $path, 'not-a-date', 'also-bad', NULL, NULL, NULL, NULL, NULL, NULL, 'Bad date row', 'ignored');
                INSERT INTO threads VALUES ('seconds-row', $path, 1700000000, 1700000000123, NULL, 'model-a', NULL, NULL, NULL, NULL, 'Seconds row', 'ignored');
                INSERT INTO threads VALUES ('iso-row', $path, '2024-01-02T03:04:05Z', '2024-01-02T04:05:06Z', NULL, 'model-b', NULL, NULL, NULL, NULL, 'ISO row', 'ignored');
                """;
            command.Parameters.AddWithValue("$path", rollout);
            command.ExecuteNonQuery();
        }
        var rows = new StateMetadataReader().Read(home);
        Assert.Equal(3, rows.Count);
        var seconds = rows.Single(item => item.ThreadId == "seconds-row");
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000), seconds.CreatedAtUtc);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1700000000123), seconds.UpdatedAtUtc);
        Assert.NotNull(rows.Single(item => item.ThreadId == "iso-row").UpdatedAtUtc);
        Assert.True(rows.Single(item => item.ThreadId == "bad-row").UpdatedAtUtc is null);
    }

    private static void MetadataQueryAllowlist()
    {
        var available = new HashSet<string>(MetadataQuery.AllowedColumns.Concat(new[] { "private_blob" }), StringComparer.Ordinal);
        var sql = MetadataQuery.Build(available);
        Assert.True(!sql.Contains('*'));
        Assert.True(!sql.Contains("private_blob", StringComparison.Ordinal));
        Assert.Equal("SELECT \"id\", \"cwd\", \"name\" FROM threads;",
            MetadataQuery.Build(new HashSet<string>(new[] { "id", "name", "cwd" }, StringComparer.Ordinal)));
    }

    private static void SessionSourceClassification(string runRoot)
    {
        Assert.Equal(SessionKind.Primary, SessionMetadataMapper.ClassifySource("vscode"));
        Assert.Equal(SessionKind.Primary, SessionMetadataMapper.ClassifySource("cli"));
        Assert.Equal(SessionKind.InternalTask,
            SessionMetadataMapper.ClassifySource("{\"subagent\":{\"thread_spawn\":{}}}"));
        Assert.Equal(SessionKind.InternalTask, SessionMetadataMapper.ClassifySource("subagent"));
        Assert.Equal(SessionKind.Unknown, SessionMetadataMapper.ClassifySource("{broken"));
        Assert.Equal(SessionKind.Unknown, SessionMetadataMapper.ClassifySource("future-client"));
        Assert.Equal(SessionKind.Unknown,
            SessionMetadataMapper.ClassifySource("{\"subagent\":\"" + new string('x', 17_000) + "\"}"));

        Assert.Equal(SessionSurface.App, SessionMetadataMapper.ClassifySourceDetails("vscode").Surface);
        Assert.Equal(SessionSurface.Cli, SessionMetadataMapper.ClassifySourceDetails("cli").Surface);
        Assert.Equal(SessionSurface.Cli, SessionMetadataMapper.ClassifySourceDetails("exec").Surface);
        var parentThreadId = "019ffb11-6aab-7a92-a266-c504fa52fd08";
        var rawSource = $"{{\"subagent\":{{\"thread_spawn\":{{\"parent_thread_id\":\"{parentThreadId}\",\"depth\":2,\"marker\":\"{Sentinel}\"}}}}}}";
        var mapped = SessionMetadataMapper.ToSession(new ThreadMetadataRow(
            "internal-thread", null, null, DateTimeOffset.UtcNow, null, null, null, rawSource,
            "Luna", "worker", "Bounded internal task"),
            new Dictionary<string, string>(StringComparer.Ordinal));
        Assert.Equal(SessionKind.InternalTask, mapped.Kind);
        Assert.Equal(SessionSurface.InternalTask, mapped.Surface);
        Assert.Equal(parentThreadId, mapped.ParentThreadId);
        Assert.Equal(2, mapped.AgentDepth);
        Assert.Equal("Bounded internal task", mapped.DisplayName);
        var nameless = SessionMetadataMapper.ToSession(new ThreadMetadataRow(
                "nameless-internal", null, null, DateTimeOffset.UtcNow, null, null, null, rawSource,
                "Luna", "worker", null),
            new Dictionary<string, string>(StringComparer.Ordinal));
        Assert.Equal("Luna", nameless.DisplayName);

        var path = Path.Combine(runRoot, "session-kind", "usage.db");
        using (var database = new UsageDatabase(path))
        {
            Assert.True(database.MergeSessionMetadata(mapped));
            var stored = database.LoadSessions().Single();
            Assert.Equal(SessionKind.InternalTask, stored.Kind);
            Assert.Equal(SessionSurface.InternalTask, stored.Surface);
            Assert.Equal(parentThreadId, stored.ParentThreadId);
            Assert.Equal(2, stored.AgentDepth);
            Assert.True(!database.ContainsPrivacySentinel(Sentinel));
        }
        using var restarted = new UsageDatabase(path);
        var restored = restarted.LoadSessions().Single();
        Assert.Equal(SessionKind.InternalTask, restored.Kind);
        Assert.Equal(SessionSurface.InternalTask, restored.Surface);
        Assert.Equal(parentThreadId, restored.ParentThreadId);
        Assert.Equal(2, restored.AgentDepth);
        Assert.True(!restarted.ContainsPrivacySentinel(Sentinel));
    }

    private static void SessionHierarchyRollupAndExpansion()
    {
        var now = DateTimeOffset.Parse("2026-08-14T08:00:00Z", CultureInfo.InvariantCulture);
        const string rootId = "019ffb11-6aab-7a92-a266-c504fa52fd08";
        const string childId = "01a000ac-f44f-7eb1-a680-58d185f637cd";
        const string grandchildId = "01a000d1-7680-7abc-8000-000000000001";
        const string cliId = "01a00018-2110-7abc-8000-000000000002";
        const string orphanId = "01a000ee-0000-7abc-8000-000000000003";
        const string missingParentId = "01a000ee-0000-7abc-8000-000000000004";

        SessionAggregate Aggregate(SessionMetadata metadata, SessionStatus status, long total,
            DateTimeOffset activity) => new(metadata, status, Usage(total), Usage(total / 10), activity,
            $"turn-{metadata.ShortThreadId}", false, RecentUsageKind.LatestTurn, Usage(total / 10), "reliable-turn");

        var root = Aggregate(new SessionMetadata(rootId, "App 主会话", "lead", null, "alpha", "gpt-5.6-sol",
            null, "Standard（默认）", now.AddHours(-2), null, Kind: SessionKind.Primary,
            Surface: SessionSurface.App), SessionStatus.Idle, 100, now.AddHours(-2));
        var child = Aggregate(new SessionMetadata(childId, "执行子任务", "worker", "Luna", "alpha", "gpt-5.6-sol",
            null, "Standard（默认）", now.AddMinutes(-20), null, Kind: SessionKind.InternalTask,
            Surface: SessionSurface.InternalTask, ParentThreadId: rootId, AgentDepth: 1),
            SessionStatus.Idle, 10, now.AddMinutes(-20));
        var grandchild = Aggregate(new SessionMetadata(grandchildId, "嵌套子任务", "worker", "Sol", "alpha", "gpt-5.6-sol",
            null, "Standard（默认）", now.AddMinutes(-1), null, Kind: SessionKind.InternalTask,
            Surface: SessionSurface.InternalTask, ParentThreadId: childId, AgentDepth: 2),
            SessionStatus.Running, 5, now.AddMinutes(-1));
        var cli = Aggregate(new SessionMetadata(cliId, "CLI 会话", "lead", null, "alpha", "gpt-5.6-sol",
            null, "Standard（默认）", now.AddHours(-1), null, Kind: SessionKind.Primary,
            Surface: SessionSurface.Cli), SessionStatus.Idle, 20, now.AddHours(-1));
        var orphan = Aggregate(new SessionMetadata(orphanId, "历史孤儿子任务", "worker", null, "alpha", "gpt-5.6-sol",
            null, "Standard（默认）", now.AddHours(-3), null, Kind: SessionKind.InternalTask,
            Surface: SessionSurface.InternalTask, ParentThreadId: missingParentId, AgentDepth: 1),
            SessionStatus.Idle, 7, now.AddHours(-3));
        var quota = new QuotaObservation(new QuotaBucket("codex", "Codex", 50, 10080, now.AddDays(4)),
            Array.Empty<QuotaBucket>(), QuotaSource.OfficialAppServer, now, false);
        var snapshot = new HudSnapshot(quota, new[] { root, child, grandchild, cli, orphan }, Usage(142), now,
            false, "fresh", Array.Empty<string>(), Array.Empty<HudEvent>(), RunningCycleTotal: Usage(5));

        var rows = HudPresentation.BuildRows(snapshot);
        var rootRow = rows.Single(row => row.ThreadId == rootId);
        var childRow = rows.Single(row => row.ThreadId == childId);
        var grandchildRow = rows.Single(row => row.ThreadId == grandchildId);
        var cliRow = rows.Single(row => row.ThreadId == cliId);
        var orphanRow = rows.Single(row => row.ThreadId == orphanId);
        Assert.Equal(15L, rootRow.DescendantTotal.Total);
        Assert.Equal(115L, rootRow.WorkTotal.Total);
        Assert.Equal(1, rootRow.DirectChildCount);
        Assert.Equal(2, rootRow.DescendantCount);
        Assert.Equal(1, rootRow.RunningDescendantCount);
        Assert.True(rootRow.EffectiveIsRunning);
        Assert.True(rootRow.Status.Contains("子任务", StringComparison.Ordinal));
        Assert.Equal("APP", rootRow.SourceLabel);
        Assert.Equal(grandchild.LastActivityUtc, rootRow.LastActivityUtc);
        Assert.Equal(15L, childRow.WorkTotal.Total);
        Assert.True(childRow.HasChildren);
        Assert.Equal(rootId, childRow.ParentThreadId);
        Assert.Equal(1, childRow.HierarchyDepth);
        Assert.Equal(2, grandchildRow.HierarchyDepth);
        Assert.Equal(childId, grandchildRow.ParentThreadId);
        Assert.Equal("子任务", grandchildRow.SourceLabel);
        Assert.Equal("CLI", cliRow.SourceLabel);
        Assert.Equal(20L, cliRow.WorkTotal.Total);
        Assert.True(orphanRow.IsOrphanInternalTask);
        Assert.True(orphanRow.ParentThreadId is null);
        Assert.True(orphanRow.ParentSummaryText.Contains("不可用", StringComparison.Ordinal));
        Assert.Equal(142L, HudPresentation.BuildFrame(snapshot).Snapshot.CycleTotal!.Value.Total);

        var viewModel = new MainViewModel();
        viewModel.Apply(snapshot);
        viewModel.SetFilter("all");
        Assert.Equal(2, viewModel.Rows.Count);
        Assert.Equal("1", viewModel.AppSessionCountText);
        Assert.Equal("1", viewModel.CliSessionCountText);
        Assert.Equal("3", viewModel.InternalTaskCountText);
        Assert.Equal("1", viewModel.OrphanTaskCountText);
        Assert.True(viewModel.ToggleChildren(rootId));
        Assert.Equal(3, viewModel.Rows.Count);
        Assert.True(viewModel.Rows.Any(row => row.ThreadId == childId));
        Assert.True(!viewModel.Rows.Any(row => row.ThreadId == grandchildId));
        Assert.True(viewModel.ToggleChildren(childId));
        Assert.Equal(4, viewModel.Rows.Count);
        Assert.True(viewModel.Rows.Any(row => row.ThreadId == grandchildId));
        Assert.True(viewModel.VisibleSessionCountText.Contains("展开 2", StringComparison.Ordinal));
        viewModel.SetSort("token");
        Assert.Equal(rootId, viewModel.Rows[0].ThreadId);
        viewModel.SetFilter("internal");
        Assert.Equal(orphanId, viewModel.Rows.Single().ThreadId);

        const string cycleA = "01a00100-0000-7abc-8000-000000000001";
        const string cycleB = "01a00100-0000-7abc-8000-000000000002";
        var a = Aggregate(new SessionMetadata(cycleA, "cycle-a", null, null, null, null, null, null,
            now, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
            ParentThreadId: cycleB), SessionStatus.Idle, 3, now);
        var b = Aggregate(new SessionMetadata(cycleB, "cycle-b", null, null, null, null, null, null,
            now, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
            ParentThreadId: cycleA), SessionStatus.Idle, 4, now);
        var cycleRows = HudPresentation.BuildRows(snapshot with { Sessions = new[] { a, b }, CycleTotal = Usage(7) });
        Assert.True(cycleRows.All(row => row.IsOrphanInternalTask && !row.HasChildren));
        Assert.Equal(7L, cycleRows.Sum(row => row.WorkTotal.Total));

        var parentWithoutActivity = root with
        {
            Metadata = root.Metadata with { LastActivityUtc = null },
            LastActivityUtc = null,
        };
        var duplicateSafeRows = HudPresentation.BuildRows(snapshot with
        {
            Sessions = new[] { parentWithoutActivity, parentWithoutActivity, child },
            CycleTotal = Usage(110),
        });
        Assert.Equal(2, duplicateSafeRows.Count);
        Assert.Equal(child.LastActivityUtc, duplicateSafeRows.Single(row => row.ThreadId == rootId).LastActivityUtc);
    }

    private static void RolloutPathNormalization(string runRoot)
    {
        var home = Path.Combine(runRoot, "paths-home");
        var sessions = Path.Combine(home, "sessions");
        var archive = Path.Combine(home, "archived_sessions");
        Directory.CreateDirectory(sessions);
        Directory.CreateDirectory(archive);
        var file = Path.Combine(sessions, "nested", "rollout.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, string.Empty);
        Assert.True(RolloutPathNormalizer.TryNormalize(home, "sessions/nested/rollout.jsonl", out var relative));
        Assert.Equal("sessions/nested/rollout.jsonl", relative);
        Assert.True(RolloutPathNormalizer.TryNormalize(home, file, out var absolute));
        Assert.Equal(relative, absolute);
        Assert.True(RolloutPathNormalizer.TryNormalize(home, @"\\?\" + file, out var extended));
        Assert.Equal(relative, extended);
        Assert.True(!RolloutPathNormalizer.TryNormalize(home, Path.Combine(runRoot, "outside.jsonl"), out _));
    }

    private static void ImplicitTurnsPersist(string runRoot)
    {
        var directory = Path.Combine(runRoot, "turns");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "turns.jsonl");
        var lines = new[]
        {
            "{\"type\":\"session_meta\",\"payload\":{\"id\":\"thread-turns\"}}",
            "{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_started\",\"thread_id\":\"thread-turns\",\"timestamp\":1700010000}}",
            TokenLine("thread-turns", 1700010001, 10, 2, 10, 2),
            "{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_complete\",\"thread_id\":\"thread-turns\",\"timestamp\":1700010002}}",
            "{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_started\",\"thread_id\":\"thread-turns\",\"timestamp\":1700010003}}",
            TokenLine("thread-turns", 1700010004, 30, 6, 20, 4),
            "{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_complete\",\"thread_id\":\"thread-turns\",\"timestamp\":1700010005}}",
        };
        File.WriteAllText(source, string.Join('\n', lines) + "\n", new UTF8Encoding(false));
        var dbPath = Path.Combine(directory, "usage.db");
        using (var database = new UsageDatabase(dbPath))
        {
            var indexer = new RolloutIndexer(database);
            indexer.ScanFile(source, "sessions/turns.jsonl", "thread-turns");
            var session = database.LoadSessions().Single();
            Assert.Equal(2L, session.TurnSequence);
            Assert.True(session.CurrentTurnKey?.StartsWith("implicit-", StringComparison.Ordinal) == true);
            Assert.True(!session.TurnOpen);
            Assert.Equal(24L, indexer.LoadAggregates(DateTimeOffset.UtcNow).Single().LatestTurnTotal.Total);
        }
        using var restarted = new UsageDatabase(dbPath);
        var restartedIndexer = new RolloutIndexer(restarted);
        var aggregate = restartedIndexer.LoadAggregates(DateTimeOffset.UtcNow).Single();
        Assert.True(aggregate.CurrentTurnKey?.StartsWith("implicit-", StringComparison.Ordinal) == true);
        Assert.Equal(24L, aggregate.LatestTurnTotal.Total);
        Assert.Equal(36L, aggregate.SessionTotal.Total);
    }

    private static void ImplicitTurnReplay(string runRoot)
    {
        var directory = Path.Combine(runRoot, "implicit-turn-replay");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "active.jsonl");
        var archive = Path.Combine(directory, "archive.jsonl");
        var lines = new[]
        {
            "{\"type\":\"session_meta\",\"payload\":{\"id\":\"thread-turn-replay\"}}",
            "{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_started\",\"thread_id\":\"thread-turn-replay\",\"timestamp\":1700020000}}",
            TokenLine("thread-turn-replay", 1700020001, 10, 2, 10, 2),
            "{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_complete\",\"thread_id\":\"thread-turn-replay\",\"timestamp\":1700020002}}",
            "{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_started\",\"thread_id\":\"thread-turn-replay\",\"timestamp\":1700020003}}",
            TokenLine("thread-turn-replay", 1700020004, 30, 6, 20, 4),
            "{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_complete\",\"thread_id\":\"thread-turn-replay\",\"timestamp\":1700020005}}",
        };
        File.WriteAllText(source, string.Join('\n', lines) + "\n", new UTF8Encoding(false));
        var dbDirectory = Path.Combine(runRoot, "implicit-turn-replay-db");
        Directory.CreateDirectory(dbDirectory);
        var dbPath = Path.Combine(dbDirectory, "usage.db");
        SessionMetadata before;
        CanonicalTokenUsage beforeTotal;
        using (var database = new UsageDatabase(dbPath))
        {
            var indexer = new RolloutIndexer(database);
            indexer.ScanFile(source, "sessions/active.jsonl", "thread-turn-replay");
            before = database.LoadSessions().Single();
            beforeTotal = database.GetSessionTotal("thread-turn-replay");
        }
        File.Copy(source, archive, true);
        using (var restarted = new UsageDatabase(dbPath))
        {
            var restartedIndexer = new RolloutIndexer(restarted);
            restartedIndexer.ScanFile(archive, "archived_sessions/archive.jsonl", "thread-turn-replay");
            var after = restarted.LoadSessions().Single();
            Assert.Equal(2L, before.TurnSequence);
            Assert.Equal(before.TurnSequence, after.TurnSequence);
            Assert.Equal(before.CurrentTurnKey, after.CurrentTurnKey);
            Assert.Equal(before.TurnOpen, after.TurnOpen);
            Assert.Equal(before.StoredStatus, after.StoredStatus);
            Assert.Equal(beforeTotal, restarted.GetSessionTotal("thread-turn-replay"));
            Assert.Equal(24L, restartedIndexer.LoadAggregates(DateTimeOffset.UtcNow).Single().LatestTurnTotal.Total);
        }
    }

    private static void StructuralReplayOrdering(string runRoot)
    {
        var directory = Path.Combine(runRoot, "structural-replay");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "source.jsonl");
        var replay = Path.Combine(directory, "replay.jsonl");
        var initialLines = new[]
        {
            "{\"type\":\"session_meta\",\"payload\":{\"id\":\"thread-structural\"}}",
            "{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_started\",\"thread_id\":\"thread-structural\",\"timestamp\":1700050000}}",
            TokenLine("thread-structural", 1700050001, 10, 2, 10, 2),
            "{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_complete\",\"thread_id\":\"thread-structural\",\"timestamp\":1700050001}}",
        };
        File.WriteAllText(source, string.Join('\n', initialLines) + "\n", new UTF8Encoding(false));
        using var database = NewDatabase(runRoot, "structural-replay");
        var indexer = new RolloutIndexer(database);
        indexer.ScanFile(source, "sessions/source.jsonl", "thread-structural");
        var before = database.LoadSessions().Single();
        var beforeTotal = database.GetSessionTotal("thread-structural");
        var beforeStructuralCount = database.GetStructuralEventCount();

        var missingTimeReplay = new[]
        {
            "{\"type\":\"session_meta\",\"payload\":{\"id\":\"thread-structural\"}}",
            "{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_started\",\"thread_id\":\"thread-structural\"}}",
            TokenLine("thread-structural", 1700050001, 10, 2, 10, 2),
            "{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_complete\",\"thread_id\":\"thread-structural\"}}",
        };
        File.WriteAllText(replay, string.Join('\n', missingTimeReplay) + "\n", new UTF8Encoding(false));
        File.SetLastWriteTimeUtc(replay, DateTime.UtcNow.AddHours(2));
        indexer.ScanFile(replay, "archived_sessions/replay.jsonl", "thread-structural");
        var afterMissingTime = database.LoadSessions().Single();
        Assert.Equal(before.CurrentTurnKey, afterMissingTime.CurrentTurnKey);
        Assert.Equal(before.TurnSequence, afterMissingTime.TurnSequence);
        Assert.Equal(before.TurnOpen, afterMissingTime.TurnOpen);
        Assert.Equal(before.StoredStatus, afterMissingTime.StoredStatus);
        Assert.Equal(before.LastActivityUtc, afterMissingTime.LastActivityUtc);
        Assert.Equal(beforeTotal, database.GetSessionTotal("thread-structural"));

        var equalTimeCrossSource = Path.Combine(directory, "equal-time-cross-source.jsonl");
        File.WriteAllText(equalTimeCrossSource,
            "{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_started\",\"thread_id\":\"thread-structural\",\"timestamp\":1700050001}}\n",
            new UTF8Encoding(false));
        File.SetLastWriteTimeUtc(equalTimeCrossSource, DateTime.UtcNow.AddHours(3));
        indexer.ScanFile(equalTimeCrossSource, "sessions/equal-time-cross-source.jsonl", "thread-structural");
        var afterCrossSource = database.LoadSessions().Single();
        Assert.Equal(before.CurrentTurnKey, afterCrossSource.CurrentTurnKey);
        Assert.Equal(before.TurnSequence, afterCrossSource.TurnSequence);
        Assert.Equal(before.TurnOpen, afterCrossSource.TurnOpen);
        Assert.Equal(before.LastActivityUtc, afterCrossSource.LastActivityUtc);

        var sameSourceEqualTime = Path.Combine(directory, "same-source-equal-time.jsonl");
        File.WriteAllText(sameSourceEqualTime,
            "{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_started\",\"thread_id\":\"thread-equal\",\"timestamp\":1700050100}}\n" +
            "{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_complete\",\"thread_id\":\"thread-equal\",\"timestamp\":1700050100}}\n",
            new UTF8Encoding(false));
        var equalIndexer = new RolloutIndexer(database);
        equalIndexer.ScanFile(sameSourceEqualTime, "sessions/same-source-equal-time.jsonl", "thread-equal");
        var equalSession = database.LoadSessions().Single(item => item.ThreadId == "thread-equal");
        Assert.Equal(1L, equalSession.TurnSequence);
        Assert.True(!equalSession.TurnOpen);
        Assert.True(equalSession.CurrentTurnKey?.StartsWith("implicit-", StringComparison.Ordinal) == true);

        File.WriteAllText(source, initialLines[0] + "\n", new UTF8Encoding(false));
        indexer.ScanFile(source, "sessions/source.jsonl", "thread-structural");
        File.WriteAllText(source, string.Join('\n', initialLines) + "\n", new UTF8Encoding(false));
        indexer.ScanFile(source, "sessions/source.jsonl", "thread-structural");
        var afterGenerationReuse = database.LoadSessions().Single(item => item.ThreadId == "thread-structural");
        Assert.Equal(before.CurrentTurnKey, afterGenerationReuse.CurrentTurnKey);
        Assert.Equal(before.TurnSequence, afterGenerationReuse.TurnSequence);
        Assert.Equal(before.TurnOpen, afterGenerationReuse.TurnOpen);
        Assert.Equal(before.LastActivityUtc, afterGenerationReuse.LastActivityUtc);
        Assert.Equal(beforeTotal, database.GetSessionTotal("thread-structural"));
        Assert.True(database.GetStructuralEventCount() >= beforeStructuralCount);
    }

    private static void TierContextAndCatalog(string runRoot)
    {
        var path = Path.Combine(runRoot, "tier-context.jsonl");
        var lines = "{\"type\":\"turn_context\",\"payload\":{\"thread_id\":\"tier-thread\",\"model\":\"model-a\",\"timestamp\":1700060000,\"thread_settings\":{\"service_tier\":\"priority\"},\"unapproved\":\"" + Sentinel + "\"}}\n" +
                    "{\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"thread_id\":\"tier-thread\",\"info\":{\"model_context_window\":196000,\"total_token_usage\":{\"input_tokens\":1,\"output_tokens\":1},\"last_token_usage\":{\"input_tokens\":1,\"output_tokens\":1}}}}\n";
        File.WriteAllText(path, lines, new UTF8Encoding(false));
        var batch = new PrivacyJsonlReader().Read(path, 0);
        Assert.Equal("priority", batch.Events[0].ServiceTier);
        Assert.Equal(196000L, batch.Events.Single(item => item.TokenSnapshot is not null).TokenSnapshot!.ContextWindow);
        using (var database = NewDatabase(runRoot, "tier-context"))
        {
            new RolloutIndexer(database).ScanFile(path, "sessions/tier.jsonl", "tier-thread");
            Assert.Equal("Fast", database.LoadSessions().Single().ServiceTier);
        }
        var catalog = ModelCatalogParser.ParseDefaults("{\"result\":{\"data\":{\"model-a\":{\"id\":\"model-a\",\"defaultServiceTier\":null},\"model-b\":{\"id\":\"model-b\",\"defaultServiceTier\":\"priority\"}}}}")!;
        Assert.Equal("Fast", ServiceTierResolver.Resolve("priority", "model-a", catalog));
        Assert.Equal("Standard（默认）", ServiceTierResolver.Resolve(null, "model-a", catalog));
        Assert.Equal("不可用", ServiceTierResolver.Resolve(null, "model-b", catalog));
        Assert.Equal("不可用", ServiceTierResolver.Resolve(null, "model-missing", catalog));
    }

    private static void MetadataNoOp(string runRoot)
    {
        using var database = NewDatabase(runRoot, "metadata-noop");
        var indexer = new RolloutIndexer(database);
        var metadata = new SessionMetadata("thread-metadata-noop", "Safe", "worker", "Luna", "project",
            "model-a", "high", null, DateTimeOffset.FromUnixTimeSeconds(1700000000), null);
        Assert.True(indexer.MergeMetadata(metadata));
        Assert.True(!indexer.MergeMetadata(metadata));
        Assert.True(indexer.MergeMetadata(metadata with { Model = "model-b" }));
        Assert.True(!indexer.MergeMetadata(metadata with { Model = "model-b" }));
    }

    private static void RefreshCounters(string runRoot)
    {
        var directory = Path.Combine(runRoot, "refresh-counters");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "source.jsonl");
        File.Copy(Fixture("cumulative-sequential.jsonl"), source, true);
        using var database = new UsageDatabase(Path.Combine(directory, "usage.db"));
        var indexer = new RolloutIndexer(database);
        indexer.ScanFile(source, "sessions/source.jsonl", "thread-fixture-a");
        var unchanged = indexer.ScanFile(source, "sessions/source.jsonl", "thread-fixture-a");
        Assert.True(unchanged.WasSkipped);
        Assert.Equal(0L, unchanged.BytesRead);
        Assert.Equal(0, unchanged.TokenWrites);
        Assert.Equal(0, unchanged.SourceWrites);
        var append = TokenLine("thread-fixture-a", 1700000090, 150, 40, 50, 10) + "\n";
        File.AppendAllText(source, append, new UTF8Encoding(false));
        var appended = indexer.ScanFile(source, "sessions/source.jsonl", "thread-fixture-a");
        Assert.Equal((long)Encoding.UTF8.GetByteCount(append), appended.BytesRead);
        Assert.Equal(1, appended.AcceptedSamples);
        Assert.Equal(1, appended.SourceWrites);
    }

    private static void WatcherPrioritizesReactivatedLog(string runRoot)
    {
        var directory = Path.Combine(runRoot, "watcher-priority");
        var home = Path.Combine(directory, "codex-home");
        var sessions = Path.Combine(home, "sessions");
        Directory.CreateDirectory(sessions);
        const int ordinaryFiles = 49;
        for (var index = 0; index < ordinaryFiles; index++)
        {
            var thread = $"ordinary-{index:00}";
            var path = Path.Combine(sessions, $"ordinary-{index:00}.jsonl");
            File.WriteAllText(path,
                $"{{\"type\":\"session_meta\",\"payload\":{{\"id\":\"{thread}\"}}}}\n" +
                TokenLine(thread, 1700030000 + index, 1, 1, 1, 1) + "\n", new UTF8Encoding(false));
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(index));
        }

        var targetThread = "dormant-reactivated";
        var target = Path.Combine(sessions, "zz-dormant.jsonl");
        File.WriteAllText(target,
            $"{{\"type\":\"session_meta\",\"payload\":{{\"id\":\"{targetThread}\"}}}}\n" +
            TokenLine(targetThread, 1700040000, 10, 2, 10, 2) + "\n", new UTF8Encoding(false));
        File.SetLastWriteTimeUtc(target, DateTime.UtcNow.AddDays(-10));

        var dbPath = Path.Combine(directory, "usage.db");
        using var engine = new UsageEngine(home, dbPath);
        engine.Cadence.MarkQuotaAttempt(DateTimeOffset.UtcNow);
        engine.RefreshAsync().GetAwaiter().GetResult();
        Assert.Equal(0L, engine.Database.GetSampleCount(targetThread));

        File.AppendAllText(target, TokenLine(targetThread, 1700040001, 20, 4, 10, 2) + "\n",
            new UTF8Encoding(false));
        Thread.Sleep(250);
        engine.RefreshAsync().GetAwaiter().GetResult();
        Assert.Equal(2L, engine.Database.GetSampleCount(targetThread));
        Assert.Equal(24L, engine.Database.GetSessionTotal(targetThread).Total);
    }

    private static void BoundedScanResume(string runRoot)
    {
        var directory = Path.Combine(runRoot, "bounded");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "large.jsonl");
        var builder = new StringBuilder("{\"type\":\"session_meta\",\"payload\":{\"id\":\"thread-bounded\"}}\n");
        for (var index = 1; index <= 120; index++)
            builder.Append(TokenLine("thread-bounded", 1700100000 + index, index * 10, index * 2, 10, 2)).Append('\n');
        File.WriteAllText(source, builder.ToString(), new UTF8Encoding(false));
        var dbPath = Path.Combine(directory, "usage.db");
        long firstOffset;
        using (var database = new UsageDatabase(dbPath))
        {
            var first = new RolloutIndexer(database).ScanFile(source, "sessions/large.jsonl", "thread-bounded", default, 2048);
            Assert.True(first.HasMoreData);
            Assert.True(first.NewOffset > 0 && first.NewOffset < new FileInfo(source).Length);
            firstOffset = first.NewOffset;
        }
        using (var restarted = new UsageDatabase(dbPath))
        {
            var indexer = new RolloutIndexer(restarted);
            var second = indexer.ScanFile(source, "sessions/large.jsonl", "thread-bounded", default, 2048);
            Assert.Equal(firstOffset, second.PreviousOffset);
            var guard = 0;
            while (second.HasMoreData && guard++ < 200)
                second = indexer.ScanFile(source, "sessions/large.jsonl", "thread-bounded", default, 2048);
            Assert.True(!second.HasMoreData);
            Assert.Equal(120L, restarted.GetSampleCount("thread-bounded"));
        }
    }

    private static void QuotaDurability(string runRoot)
    {
        using var database = NewDatabase(runRoot, "quota");
        var futureReset = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds();
        var first = QuotaJsonParser.Parse(QuotaJson("codex-main", "Codex", 80, futureReset), DateTimeOffset.UtcNow);
        var state = new QuotaStateMachine(database);
        state.Observe(first);
        var smallDriftAndName = QuotaJsonParser.Parse(QuotaJson("codex-renamed", "Codex Main", 80, futureReset + 60), DateTimeOffset.UtcNow.AddMinutes(1));
        state.Observe(smallDriftAndName);
        Assert.Equal(0L, database.GetResetSignalCount());
        var restored = new QuotaStateMachine(database);
        var jump = QuotaJsonParser.Parse(QuotaJson("codex-renamed", "Codex Main", 60, futureReset + 60), DateTimeOffset.UtcNow.AddMinutes(2));
        restored.Observe(jump);
        Assert.Equal(1L, database.GetResetSignalCount());
        var stale = restored.Observe(new QuotaObservation(null, Array.Empty<QuotaBucket>(), QuotaSource.Unavailable,
            DateTimeOffset.UtcNow.AddMinutes(3), false, "temporary_failure"));
        Assert.NotNull(stale.Primary);
        Assert.True(stale.IsStale);
        Assert.Equal("temporary_failure", stale.ErrorCode);
        var ambiguous = QuotaJsonParser.Parse("{\"result\":{\"data\":[{\"id\":\"codex-a\",\"name\":\"Codex A\",\"usedPercent\":1,\"windowDurationMins\":60,\"resetsAt\":1786460611},{\"id\":\"codex-b\",\"name\":\"Codex B\",\"usedPercent\":2,\"windowDurationMins\":60,\"resetsAt\":1786460611}]}}", DateTimeOffset.UtcNow);
        Assert.True(ambiguous.Primary is null);
        Assert.Equal("quota_bucket_ambiguous", ambiguous.ErrorCode);

        var currentShape = QuotaJsonParser.Parse(
            "{\"result\":{\"rateLimits\":{\"primary\":{\"usedPercent\":2,\"windowDurationMins\":10080,\"resetsAt\":1787801704},\"secondary\":{\"usedPercent\":24,\"windowDurationMins\":10080,\"resetsAt\":1787580470}}}}",
            DateTimeOffset.UtcNow);
        Assert.NotNull(currentShape.Primary);
        Assert.Equal("primary", currentShape.Primary!.Id);
        Assert.Equal(2d, currentShape.Primary.UsedPercent);
        Assert.Equal(1, currentShape.Additional.Count);
        Assert.Equal("secondary", currentShape.Additional[0].Id);

        var rolloverStart = DateTimeOffset.Parse("2026-08-20T03:33:00Z", CultureInfo.InvariantCulture);
        var rolloverState = new QuotaStateMachine();
        rolloverState.Observe(new QuotaObservation(
            new QuotaBucket("primary", "primary", 100, 10080, rolloverStart),
            Array.Empty<QuotaBucket>(), QuotaSource.OfficialAppServer, rolloverStart.AddMinutes(-1), false));
        var unavailableAfterExpiry = rolloverState.Observe(new QuotaObservation(null,
            Array.Empty<QuotaBucket>(), QuotaSource.Unavailable, rolloverStart.AddMinutes(1), false,
            "quota_bucket_ambiguous"));
        Assert.True(unavailableAfterExpiry.Primary is null);
        Assert.Equal("quota_bucket_ambiguous", unavailableAfterExpiry.ErrorCode);
        var rolledOver = rolloverState.Observe(new QuotaObservation(
            new QuotaBucket("primary", "primary", 2, 10080, rolloverStart.AddDays(7)),
            Array.Empty<QuotaBucket>(), QuotaSource.OfficialAppServer, rolloverStart.AddMinutes(2), false));
        Assert.Equal(98d, rolledOver.Primary!.RemainingPercent);

        var expiredAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        restored.Observe(new QuotaObservation(new QuotaBucket("primary", "primary", 50, 10080, expiredAt),
            Array.Empty<QuotaBucket>(), QuotaSource.OfficialAppServer, expiredAt.AddMinutes(-1), false));
        var expiredRestore = new QuotaStateMachine(database).RestoreForDisplay();
        Assert.True(expiredRestore.Primary is null);
        Assert.Equal("quota_last_observation_expired", expiredRestore.ErrorCode);
    }

    private static void RunningCycleScopeAndInvalidQuota(string runRoot)
    {
        using var database = NewDatabase(runRoot, "running-cycle-scope");
        var cycleStart = DateTimeOffset.Parse("2026-08-05T00:00:00Z", CultureInfo.InvariantCulture);
        var cycleEnd = cycleStart.AddHours(1);

        static TokenUsageSnapshot Snapshot(long cumulative, long increment, long contextWindow) => new(
            new TokenComponents(cumulative, null, null, null, 0, null, cumulative),
            new TokenComponents(increment, null, null, null, 0, null, increment), contextWindow);

        Assert.True(database.AcceptTokenSample("thread-running", Snapshot(100, 100, 1000),
            cycleStart.AddSeconds(-1), cycleStart, "turn-before", null, null));
        Assert.True(database.AcceptTokenSample("thread-running", Snapshot(110, 10, 1001),
            cycleStart.AddMinutes(10), cycleStart.AddMinutes(10), "turn-running", null, null));
        Assert.True(database.AcceptTokenSample("thread-idle", Snapshot(20, 20, 2000),
            cycleStart.AddMinutes(20), cycleStart.AddMinutes(20), "turn-idle", null, null));
        Assert.True(database.AcceptTokenSample("thread-running", Snapshot(140, 30, 1002),
            cycleEnd.AddSeconds(1), cycleEnd.AddSeconds(1), "turn-after", null, null));

        Assert.Equal(30L, database.GetCycleTotal(cycleStart, cycleEnd).Total);
        Assert.Equal(10L, database.GetCycleTotalForThreads(cycleStart, cycleEnd,
            new[] { "thread-running", "thread-running" }).Total);
        Assert.Equal(20L, database.GetCycleTotalForThreads(cycleStart, cycleEnd,
            new[] { "thread-idle" }).Total);
        Assert.Equal(0L, database.GetCycleTotalForThreads(cycleStart, cycleEnd,
            Array.Empty<string>()).Total);

        var validJson = QuotaJson("codex-main", "Codex", 25, 1786460611);
        var zeroWindow = QuotaJsonParser.Parse(validJson.Replace("\"windowDurationMins\":10080",
            "\"windowDurationMins\":0", StringComparison.Ordinal), cycleStart);
        var negativeWindow = QuotaJsonParser.Parse(validJson.Replace("\"windowDurationMins\":10080",
            "\"windowDurationMins\":-5", StringComparison.Ordinal), cycleStart);
        var oversizedWindow = QuotaJsonParser.Parse(validJson.Replace("\"windowDurationMins\":10080",
            "\"windowDurationMins\":2147483647", StringComparison.Ordinal), cycleStart);
        var mixedInvalidWindow = QuotaJsonParser.Parse(
            "{\"result\":{\"rate_limits\":[{\"id\":\"codex-main\",\"name\":\"Codex\",\"usedPercent\":25,\"windowDurationMins\":10080,\"resetsAt\":1786460611},{\"id\":\"secondary\",\"name\":\"Secondary\",\"usedPercent\":5,\"windowDurationMins\":0,\"resetsAt\":1786460611}]}}",
            cycleStart);
        Assert.True(zeroWindow.Primary is null);
        Assert.Equal("quota_window_invalid", zeroWindow.ErrorCode);
        Assert.True(negativeWindow.Primary is null);
        Assert.Equal("quota_window_invalid", negativeWindow.ErrorCode);
        Assert.True(oversizedWindow.Primary is null);
        Assert.Equal("quota_window_invalid", oversizedWindow.ErrorCode);
        Assert.True(mixedInvalidWindow.Primary is null);
        Assert.Equal("quota_window_invalid", mixedInvalidWindow.ErrorCode);

        var state = new QuotaStateMachine(database);
        Assert.NotNull(state.Observe(QuotaJsonParser.Parse(validJson, cycleStart)).Primary);
        Assert.True(state.Observe(zeroWindow).Primary is null);
        var invalidDirect = new QuotaObservation(
            new QuotaBucket("codex-main", "Codex", 25, 0, cycleEnd), Array.Empty<QuotaBucket>(),
            QuotaSource.OfficialAppServer, cycleStart, false);
        Assert.Equal("unavailable", HudPresentation.GetThreshold(invalidDirect).Code);
        var invalidViewModel = new MainViewModel();
        invalidViewModel.Apply(new HudSnapshot(invalidDirect, Array.Empty<SessionAggregate>(), null,
            cycleStart, false, "额度不可用；本周期不可用", Array.Empty<string>()));
        Assert.Equal("不可用", invalidViewModel.UsedText);
        Assert.Equal("官方额度不可用", invalidViewModel.QuotaSourceText);
        Assert.True(invalidViewModel.OverviewText.Contains("额度：不可用", StringComparison.Ordinal));
    }

    private static void ContextContinuationMetrics(string runRoot)
    {
        var root = Path.Combine(runRoot, "context-continuation");
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "thread.jsonl");
        var dbPath = Path.Combine(root, "usage.db");
        const string threadId = "thread-context-continuation";
        const long window = 258_400;
        long cumulative = 1_000_000;
        const long start = 1_786_320_000;

        static string TurnLine(string thread, long timestamp, string turn, string model) =>
            $"{{\"timestamp\":{timestamp},\"type\":\"turn_context\",\"payload\":{{\"thread_id\":\"{thread}\",\"turn_id\":\"{turn}\",\"model\":\"{model}\"}}}}";
        static string CompactLine(string thread, long timestamp) =>
            $"{{\"timestamp\":{timestamp},\"type\":\"event_msg\",\"payload\":{{\"type\":\"context_compacted\",\"thread_id\":\"{thread}\"}}}}";
        static string ContextTokenLine(string thread, long timestamp, string model, long totalInput,
            long lastInput, long contextWindow) => System.Text.Json.JsonSerializer.Serialize(new
            {
                type = "event_msg",
                payload = new
                {
                    type = "token_count", thread_id = thread, timestamp, model,
                    info = new
                    {
                        total_token_usage = new { input_tokens = totalInput, output_tokens = 0, total_tokens = totalInput },
                        last_token_usage = new { input_tokens = lastInput, output_tokens = 0, total_tokens = lastInput },
                        model_context_window = contextWindow,
                    },
                },
            });

        var lines = new List<string>
        {
            $"{{\"type\":\"session_meta\",\"payload\":{{\"id\":\"{threadId}\"}}}}",
            TurnLine(threadId, start, "turn-1", "gpt-test"),
            CompactLine(threadId, start + 1),
        };
        cumulative += 105_000;
        lines.Add(ContextTokenLine(threadId, start + 2, "gpt-test", cumulative, 105_000, window));
        lines.Add(TurnLine(threadId, start + 20, "turn-2", "gpt-test"));
        cumulative += 60_000;
        lines.Add(ContextTokenLine(threadId, start + 21, "gpt-test", cumulative, 60_000, window));
        lines.Add(TurnLine(threadId, start + 60, "turn-3", "gpt-test"));
        lines.Add(CompactLine(threadId, start + 61));
        cumulative += 120_000;
        lines.Add(ContextTokenLine(threadId, start + 62, "gpt-test", cumulative, 120_000, window));
        lines.Add(TurnLine(threadId, start + 120, "turn-4", "gpt-test"));
        lines.Add(CompactLine(threadId, start + 121));
        cumulative += 132_000;
        lines.Add(ContextTokenLine(threadId, start + 122, "gpt-test", cumulative, 132_000, window));
        File.WriteAllText(source, string.Join('\n', lines) + "\n", new UTF8Encoding(false));

        using (var database = new UsageDatabase(dbPath))
        {
            var scan = new RolloutIndexer(database).ScanFile(source, "sessions/thread.jsonl", threadId);
            Assert.Equal(4, scan.AcceptedSamples);

            var metrics = database.LoadSessionContextMetrics()[threadId];
            Assert.Equal(120_000L, metrics.PostCompactionInputTokens);
            Assert.Equal(window, metrics.PostCompactionWindowTokens);
            Assert.Equal(3, metrics.PostCompactionSampleCount);
            Assert.True(metrics.UsesExplicitCompactionBoundaries);
            Assert.Near(46.439, metrics.PostCompactionPercent!.Value, 0.01);
            Assert.Near(10.449, metrics.BaselineTrendPercentagePoints!.Value, 0.01);
            Assert.Near(-50, metrics.TurnRunwayChangePercent!.Value, 0.01);
            Assert.Near(-27.273, metrics.TokenRunwayChangePercent!.Value, 0.01);

            var row = new SessionDisplayRow(threadId, "上下文测试", "thread-conte", "lead",
                "test", "gpt-test", "Standard", "运行中", "08-10 08:00:00",
                DateTimeOffset.FromUnixTimeSeconds(start + 122),
                Usage(1), "最近一轮", "reliable-turn", Usage(2_100_000_000), true, false,
                true, SessionKind.Primary, metrics);
            Assert.Equal("B-", row.ContinuationGradeText);
            Assert.Equal("建议当前完整工作包结束后续接", row.ContinuationAdviceText);
            Assert.True(row.PostCompactionBaselineText.Contains("46%", StringComparison.Ordinal));
            Assert.True(row.PostCompactionSourceText.Contains("官方压缩边界", StringComparison.Ordinal));
            Assert.True(row.BaselineTrendText.Contains("+10.4", StringComparison.Ordinal));
            Assert.True(row.EffectiveRunwayText.Contains("回合 -50%", StringComparison.Ordinal));
            Assert.True(row.EffectiveRunwayText.Contains("间隔 token 续航 -27%", StringComparison.Ordinal));
            Assert.True(row.ContinuationEvidenceText.Contains("底座定级 B+", StringComparison.Ordinal));
            Assert.True(!row.ContinuationAdviceText.Contains("次数", StringComparison.Ordinal));

            static TokenUsageSnapshot Snapshot(long cumulativeTotal, long lastInput, long contextWindow) => new(
                new TokenComponents(cumulativeTotal, null, null, null, 0, null, cumulativeTotal),
                new TokenComponents(lastInput, null, null, null, 0, null, lastInput), contextWindow);
            const string heuristicThread = "thread-context-heuristic";
            var heuristicTime = DateTimeOffset.FromUnixTimeSeconds(start + 500);
            long heuristicCumulative = 3_000_000;
            foreach (var index in Enumerable.Range(0, 3))
            {
                var turn = $"heuristic-{index}";
                var time = heuristicTime.AddMinutes(index * 3);
                Assert.True(database.AcceptTokenSample(heuristicThread,
                    Snapshot(heuristicCumulative, 232_000, window), time, time, turn, "gpt-test", null));
                Assert.True(database.AcceptTokenSample(heuristicThread,
                    Snapshot(heuristicCumulative, 0, window), time.AddSeconds(10), time.AddSeconds(10),
                    turn, "gpt-test", null));
                heuristicCumulative += 106_000 + index * 2_000;
                Assert.True(database.AcceptTokenSample(heuristicThread,
                    Snapshot(heuristicCumulative, 106_000 + index * 2_000, window), time.AddSeconds(20),
                    time.AddSeconds(20), turn, "gpt-test", null));
            }
            var heuristic = database.LoadSessionContextMetrics()[heuristicThread];
            Assert.Equal(3, heuristic.PostCompactionSampleCount);
            Assert.True(!heuristic.UsesExplicitCompactionBoundaries);

            const string falsePositiveThread = "thread-context-false-positive";
            var falseTime = heuristicTime.AddHours(2);
            Assert.True(database.AcceptTokenSample(falsePositiveThread,
                Snapshot(2_000_000, 232_000, window), falseTime, falseTime, "turn-a", "gpt-test", null));
            Assert.True(database.AcceptTokenSample(falsePositiveThread,
                Snapshot(2_000_000, 0, window), falseTime.AddSeconds(10), falseTime.AddSeconds(10),
                "turn-b", "gpt-test", null));
            Assert.True(database.AcceptTokenSample(falsePositiveThread,
                Snapshot(2_100_000, 108_000, window), falseTime.AddSeconds(20), falseTime.AddSeconds(20),
                "turn-b", "gpt-test", null));
            Assert.Equal(0, database.LoadSessionContextMetrics()[falsePositiveThread]
                .PostCompactionSampleCount);
        }

        using (var restarted = new UsageDatabase(dbPath))
        {
            var restored = restarted.LoadSessionContextMetrics()[threadId];
            Assert.Equal(120_000L, restored.PostCompactionInputTokens);
            Assert.Equal(3, restored.PostCompactionSampleCount);

            var appended = new[]
            {
                TurnLine(threadId, start + 180, "turn-next-model", "gpt-next"),
                CompactLine(threadId, start + 181),
                ContextTokenLine(threadId, start + 182, "gpt-next", cumulative + 70_000, 70_000, window),
            };
            File.AppendAllText(source, string.Join('\n', appended) + "\n", new UTF8Encoding(false));
            var appendScan = new RolloutIndexer(restarted).ScanFile(source, "sessions/thread.jsonl", threadId);
            Assert.Equal(1, appendScan.AcceptedSamples);
            var resetSegment = restarted.LoadSessionContextMetrics()[threadId];
            Assert.Equal(1, resetSegment.PostCompactionSampleCount);
            Assert.True(!resetSegment.HasReliablePostCompactionBaseline);
        }

        AssertForkExplicitContextBoundaries(runRoot);
    }

    private static void ContinuationGradeHierarchy()
    {
        static SessionDisplayRow Row(double baselinePercent, double? trend = 0d,
            double? turnRunway = 0d, double? tokenRunway = 0d, int samples = 3)
        {
            const long window = 1_000_000;
            var input = (long)Math.Round(window * baselinePercent / 100d,
                MidpointRounding.AwayFromZero);
            var metrics = new SessionContextMetrics(input, window, samples, trend,
                turnRunway, tokenRunway, true);
            return new SessionDisplayRow("grade-thread", "等级测试", "grade-thread", "lead",
                "test", "gpt-test", "Standard", "空闲", "08-10 12:00:00",
                DateTimeOffset.FromUnixTimeSeconds(1_786_320_000), Usage(1), "最近一轮",
                "reliable-turn", Usage(1), false, false, false, SessionKind.Primary, metrics);
        }

        foreach (var (baseline, grade) in new[]
                 {
                     (24.9, "S"), (25d, "A+"), (35d, "A-"), (45d, "B+"),
                     (55d, "B-"), (65d, "C"),
                 })
            Assert.Equal(grade, Row(baseline).ContinuationGradeText);

        var split = Row(46d, 1.5d, 325d, -34d);
        Assert.Equal("B+", split.ContinuationGradeText);
        Assert.Equal("仍可继续，但建议准备续接", split.ContinuationAdviceText);
        Assert.True(split.ContinuationEvidenceText.Contains("底座趋势稳定", StringComparison.Ordinal));
        Assert.True(split.ContinuationEvidenceText.Contains("续航信号分化", StringComparison.Ordinal));
        Assert.True(split.EffectiveRunwayText.Contains("间隔 token 续航 -34%", StringComparison.Ordinal));

        var rising = Row(46d, 3.1d, 40d, 40d);
        Assert.Equal("B-", rising.ContinuationGradeText);
        Assert.True(rising.ContinuationEvidenceText.Contains("底座上升", StringComparison.Ordinal));

        var improvingRunway = Row(46d, 0d, 25d, 30d);
        Assert.Equal("A-", improvingRunway.ContinuationGradeText);
        Assert.True(improvingRunway.ContinuationEvidenceText.Contains("共同改善", StringComparison.Ordinal));

        var shrinkingRunway = Row(46d, 0d, -25d, -30d);
        Assert.Equal("B-", shrinkingRunway.ContinuationGradeText);
        Assert.True(shrinkingRunway.ContinuationEvidenceText.Contains("共同缩短", StringComparison.Ordinal));

        var oneMetricOnly = Row(46d, 0d, -80d, null);
        Assert.Equal("B+", oneMetricOnly.ContinuationGradeText);
        Assert.True(oneMetricOnly.ContinuationEvidenceText.Contains("续航样本不足", StringComparison.Ordinal));

        var insufficient = Row(46d, 0d, 0d, 0d, 2);
        Assert.Equal("—", insufficient.ContinuationGradeText);
        Assert.Equal("样本不足，暂不判断", insufficient.ContinuationAdviceText);
    }

    private static void ContinuationLifecycleAndManualDrift(string runRoot)
    {
        var directory = Path.Combine(runRoot, "continuation-lifecycle");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "usage.db");
        var now = DateTimeOffset.Parse("2026-08-12T15:30:00Z", CultureInfo.InvariantCulture);
        using var database = new UsageDatabase(databasePath);
        const string model = "gpt-5.6-sol";
        for (var session = 0; session < 10; session++)
        {
            var threadId = session == 0 ? "lead-thread" : $"control-{session}";
            database.UpsertSession(new SessionMetadata(threadId, threadId, null, null, "project", model,
                "max", "Standard（默认）", now, null, Kind: SessionKind.Primary));
            var turns = session == 0 ? 20 : 1;
            long cumulative = 0;
            for (var turn = 0; turn < turns; turn++)
            {
                var increment = session == 0 ? 10_000L : 100L;
                cumulative += increment;
                var snapshot = new TokenUsageSnapshot(
                    new TokenComponents(cumulative, 0, null, 0, 0, 0, cumulative),
                    new TokenComponents(increment, 0, null, 0, 0, 0, increment), 258_400);
                Assert.True(database.AcceptTokenSample(threadId, snapshot, now.AddMinutes(turn),
                    now.AddMinutes(turn), $"turn-{turn}", model, "Standard（默认）"));
            }
        }

        var aggregate = database.LoadSessionAggregates(now.AddHours(1))
            .Single(item => item.Metadata.ThreadId == "lead-thread");
        Assert.True(aggregate.LifecycleMetrics is not null);
        Assert.Equal(10, aggregate.LifecycleMetrics!.ComparableSessionCount);
        Assert.Equal(1, aggregate.LifecycleMetrics.LifetimeTokenRank);
        Assert.Equal(1, aggregate.LifecycleMetrics.LifetimeTurnRank);
        var snapshotForPresentation = new HudSnapshot(
            new QuotaObservation(null, Array.Empty<QuotaBucket>(), QuotaSource.Unavailable, now, false),
            new[] { aggregate }, null, now, false, "test", Array.Empty<string>());
        var row = HudPresentation.BuildRows(snapshotForPresentation).Single();
        Assert.Equal("极高", row.LifecycleRiskText);
        Assert.Equal("B-", row.ContinuationGradeText);
        Assert.True(row.LifecycleRiskDetailText.Contains("第 1/10", StringComparison.Ordinal));

        database.SetSessionDriftAssessment("lead-thread", DriftAssessmentLevel.Repeated, now);
        aggregate = database.LoadSessionAggregates(now.AddHours(1))
            .Single(item => item.Metadata.ThreadId == "lead-thread");
        row = HudPresentation.BuildRows(snapshotForPresentation with { Sessions = new[] { aggregate } }).Single();
        Assert.Equal("连续明显漂移", row.DriftAssessmentText);
        Assert.Equal("C", row.ContinuationGradeText);
        Assert.True(row.ContinuationEvidenceText.Contains("人工 连续明显漂移", StringComparison.Ordinal));
    }

    private static void ManualDriftPersistence(string runRoot)
    {
        var directory = Path.Combine(runRoot, "manual-drift");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "usage.db");
        var observed = DateTimeOffset.Parse("2026-08-12T15:45:00Z", CultureInfo.InvariantCulture);
        using (var database = new UsageDatabase(databasePath))
        {
            database.SetSessionDriftAssessment("thread-drift", DriftAssessmentLevel.Occasional, observed);
            var value = database.LoadSessionDriftAssessments()["thread-drift"];
            Assert.Equal(DriftAssessmentLevel.Occasional, value.Level);
            Assert.Equal(observed, value.ObservedAtUtc);
        }
        using (var restarted = new UsageDatabase(databasePath))
        {
            var value = restarted.LoadSessionDriftAssessments()["thread-drift"];
            Assert.Equal(DriftAssessmentLevel.Occasional, value.Level);
            restarted.SetSessionDriftAssessment("thread-drift", DriftAssessmentLevel.Unassessed,
                observed.AddMinutes(1));
        }
        using var cleared = new UsageDatabase(databasePath);
        Assert.True(!cleared.LoadSessionDriftAssessments().ContainsKey("thread-drift"));
        Assert.Equal("10", cleared.ReadSchemaValue("version"));
    }

    private static void ContextWindowMigration(string runRoot)
    {
        var root = Path.Combine(runRoot, "context-window-migration");
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "thread.jsonl");
        var dbPath = Path.Combine(root, "usage.db");
        File.WriteAllText(source, TokenLine("thread-context-migration", 1700305000,
            100, 10, 100, 10) + "\n", new UTF8Encoding(false));

        using (var database = new UsageDatabase(dbPath))
        {
            var indexer = new RolloutIndexer(database);
            var first = indexer.ScanFile(source, "sessions/thread.jsonl", "thread-context-migration");
            Assert.Equal(1, first.AcceptedSamples);
            Assert.Equal(1L, database.GetSampleCount("thread-context-migration"));
            Assert.Equal(110L, database.GetSessionTotal("thread-context-migration").Total);
        }

        using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                DELETE FROM schema_info WHERE key = 'context_capture_version';
                UPDATE token_samples SET context_window = NULL;
                DELETE FROM session_context_state;
                DELETE FROM context_baseline_observations;
                """;
            command.ExecuteNonQuery();
        }

        using var migrated = new UsageDatabase(dbPath);
        var reset = migrated.LoadSourceStates().Single();
        Assert.Equal("context_boundary_rescan", reset.HealthCode);
        Assert.Equal(0L, reset.CompleteOffset);
        var replay = new RolloutIndexer(migrated).ScanFile(source, "sessions/thread.jsonl",
            "thread-context-migration");
        Assert.Equal(0, replay.AcceptedSamples);
        Assert.Equal(1, replay.DuplicateSamples);
        Assert.Equal(1L, migrated.GetSampleCount("thread-context-migration"));
        Assert.Equal(110L, migrated.GetSessionTotal("thread-context-migration").Total);
        using var verify = new SqliteConnection($"Data Source={dbPath}");
        verify.Open();
        using var verifyCommand = verify.CreateCommand();
        verifyCommand.CommandText = "SELECT context_window FROM token_samples WHERE thread_id = $thread_id;";
        verifyCommand.Parameters.AddWithValue("$thread_id", "thread-context-migration");
        Assert.Equal(128_000L, Convert.ToInt64(verifyCommand.ExecuteScalar(), CultureInfo.InvariantCulture));
    }

    private static void ExpiredQuotaCycle(string runRoot)
    {
        var directory = Path.Combine(runRoot, "expired-quota-cycle");
        var home = Path.Combine(directory, "codex-home");
        Directory.CreateDirectory(home);
        var databasePath = Path.Combine(directory, "usage.db");
        using (var database = new UsageDatabase(databasePath))
        {
            var expired = new QuotaObservation(
                new QuotaBucket("codex-main", "Codex", 25, 10080, DateTimeOffset.UtcNow.AddMinutes(-1)),
                Array.Empty<QuotaBucket>(), QuotaSource.OfficialAppServer, DateTimeOffset.UtcNow.AddHours(-1), false);
            database.SaveQuota(expired);
        }
        using var engine = new UsageEngine(home, databasePath);
        var snapshot = engine.GetSnapshot();
        Assert.True(snapshot.Quota.Primary is null);
        Assert.True(!snapshot.Quota.IsStale);
        Assert.Equal("quota_last_observation_expired", snapshot.Quota.ErrorCode);
        Assert.True(snapshot.CycleTotal is null);
        Assert.True(HudPresentation.BuildCollapsedText(snapshot).Contains("额度不可用", StringComparison.Ordinal));
        Assert.True(HudPresentation.BuildCollapsedText(snapshot).Contains("本周期不可用", StringComparison.Ordinal));
    }

    private static void RefreshCadenceTest()
    {
        var cadence = new RefreshCadence();
        var now = DateTimeOffset.Parse("2026-08-05T00:00:00Z", CultureInfo.InvariantCulture);
        Assert.True(cadence.IsQuotaDue(now, false));
        Assert.True(cadence.IsMetadataDue(now));
        cadence.MarkQuotaAttempt(now);
        cadence.MarkMetadataRefresh(now);
        Assert.True(!cadence.IsQuotaDue(now.AddSeconds(59), false));
        Assert.True(cadence.IsQuotaDue(now.AddSeconds(60), false));
        Assert.True(cadence.IsQuotaDue(now.AddSeconds(1), true));
        Assert.True(!cadence.IsMetadataDue(now.AddSeconds(29)));
        Assert.True(cadence.IsMetadataDue(now.AddSeconds(30)));
        Assert.Equal(TimeSpan.FromSeconds(8), RefreshCadence.AppendScan);
        Assert.Equal(TimeSpan.FromSeconds(1), RefreshCadence.UiCountdown);
    }

    private static void AppServerContract(string runRoot)
    {
        var info = AppServerProtocol.CreateStartInfo("codex");
        Assert.SequenceEqual(new[] { "-s", "read-only", "-a", "never", "app-server" }, info.ArgumentList);
        Assert.Equal("-s read-only -a never app-server", AppServerProtocol.ReadOnlyFlag);
        Assert.True(!info.ArgumentList.Any(argument => argument.Contains("fast", StringComparison.OrdinalIgnoreCase) ||
                                                       argument.Contains("priority", StringComparison.OrdinalIgnoreCase) ||
                                                       argument.Contains("ultrafast", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal("account/rateLimits/read", AppServerProtocol.RateLimitsMethod);
        var directory = Path.Combine(runRoot, "fake-path");
        Directory.CreateDirectory(directory);
        var candidate = Path.Combine(directory, "codex.cmd");
        File.WriteAllText(candidate, "@exit /b 0", Encoding.ASCII);
        Assert.Equal(candidate, new CodexExecutableDiscovery().Find(null, directory, "", "", ""));
        var wrapperInfo = AppServerProtocol.CreateStartInfo(candidate);
        Assert.True(wrapperInfo.Arguments.Contains("-a never", StringComparison.Ordinal));
        Assert.True(!wrapperInfo.Arguments.Contains("untrusted", StringComparison.Ordinal));

        var appData = Path.Combine(runRoot, "fake-appdata");
        var npmWrapper = Path.Combine(appData, "npm", "codex.cmd");
        var npmNative = Path.Combine(appData, "npm", "node_modules", "@openai", "codex",
            "node_modules", "@openai", "codex-win32-x64", "vendor",
            "x86_64-pc-windows-msvc", "bin", "codex.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(npmNative)!);
        Directory.CreateDirectory(Path.GetDirectoryName(npmWrapper)!);
        File.WriteAllText(npmWrapper, "@exit /b 0", Encoding.ASCII);
        File.WriteAllText(npmNative, "native", Encoding.ASCII);
        Assert.Equal(npmNative, new CodexExecutableDiscovery().Find(null, "", appData, "", ""));
        var source = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "CodexUsageHud.Core", "QuotaAdapters.cs"));
        Assert.True(!source.Contains("HttpClient", StringComparison.Ordinal));
    }

    private static void SqlitePrivacySchema(string runRoot)
    {
        using var database = NewDatabase(runRoot, "schema");
        Assert.True(!database.ContainsPrivacySentinel(Sentinel));
        var safe = new HashSet<string>(StringComparer.Ordinal)
        {
            "key", "value", "source_key", "thread_id", "volume_serial", "file_id", "fallback_digest",
            "is_degraded", "relative_path", "generation", "complete_offset", "last_length",
            "drain_mode", "drain_line_start_offset", "drain_offset", "last_write_utc_ticks",
            "cursor_turn_key", "health_code", "state_revision",
            "fingerprint", "first_seen_utc", "last_seen_utc", "duplicate_count", "disposition", "diagnostic_code",
            "id", "input_tokens", "raw_input_tokens", "cached_input_tokens", "cache_write_input_tokens",
            "output_tokens", "reasoning_output_tokens", "cumulative_input_tokens", "cumulative_output_tokens",
            "cumulative_total_tokens", "canonical_total_tokens", "reported_total_tokens", "event_time_utc",
            "event_time_ticks", "observed_at_utc", "source_generation", "source_offset", "turn_key",
            "model", "service_tier", "confidence", "turn_confidence", "event_order_confidence",
            "service_tier_source", "session_kind", "session_surface", "parent_thread_id", "agent_depth",
            "display_name", "role",
            "nickname", "project_tag", "reasoning_effort", "last_activity_utc", "status", "current_turn_key",
            "current_turn_reliable", "turn_sequence", "turn_open", "pinned", "bucket_id", "bucket_name", "used_percent",
            "window_duration_minutes", "resets_at_utc", "source", "is_stale", "is_primary",
            "previous_remaining_percent", "current_remaining_percent", "reason_code", "offset", "error_code",
            "current_structural_event_identity", "current_structural_source_hash",
            "current_structural_generation", "current_structural_offset", "current_structural_time_utc",
            "event_identity", "base_identity", "event_kind", "association_key", "event_time_utc", "source_identity_hash",
            "source_generation", "source_offset", "occurrence", "observation_key", "canonical", "disposition",
            "input_tokens", "raw_input_tokens", "cached_input_tokens", "cache_write_input_tokens",
            "output_tokens", "reasoning_output_tokens", "canonical_total_tokens", "reported_total_tokens",
            "sample_id", "maximum_cumulative_total", "bucket_start_ticks", "singleton",
            "cycle_start_ticks", "cycle_end_ticks", "status", "cursor_sample_id", "total_samples",
            "state_event_time_ticks", "state_source_key", "state_source_generation",
            "state_source_offset", "state_event_kind",
            "context_window", "latest_event_ticks", "latest_source_key",
            "latest_source_generation", "latest_source_offset", "latest_sample_id",
            "current_input_tokens", "current_context_window", "current_model", "high_input_tokens",
            "high_context_window", "high_cumulative_total", "high_event_ticks", "high_turn_key",
            "awaiting_post", "marker_event_ticks", "marker_turn_key", "post_sample_id",
            "post_input_tokens", "explicit_marker_ticks", "explicit_marker_source_key",
            "explicit_marker_source_generation", "explicit_marker_source_offset",
            "explicit_marker_identity", "explicit_marker_turn_key", "model_key", "detection_source",
            "boundary_event_identity", "runway_turns", "runway_tokens",
            "assessment_level",
            "semantic_identity", "semantic_material", "legacy_semantic_identity",
            "lineage_root_thread_id", "is_lineage_canonical",
        };
        Assert.True(database.ReadSchemaColumnNames().Values.SelectMany(columns => columns).All(safe.Contains));
    }

    private static void HudPresentationTest()
    {
        var now = DateTimeOffset.Parse("2026-08-05T00:00:00Z", CultureInfo.InvariantCulture);
        var sessions = SampleSessions(now);
        var quota = new QuotaObservation(new QuotaBucket("codex-main", "Codex", 80, 10080, now.AddDays(7)),
            Array.Empty<QuotaBucket>(), QuotaSource.OfficialAppServer, now, false);
        var snapshot = new HudSnapshot(quota, sessions, new CanonicalTokenUsage(30, 20, 10, 0, 5, 1, 35, 35),
            now, true, "官方额度", Array.Empty<string>(), Array.Empty<HudEvent>(),
            RunningCycleTotal: Usage(10));
        var rows = HudPresentation.BuildRows(snapshot);
        Assert.Equal(3, rows.Count);
        Assert.Equal(1, rows.Count(row => row.IsRunning));
        Assert.Equal(1, rows.Count(row => row.IsCurrent));
        Assert.True(rows.Single(row => row.IsCurrent).IsInferredCurrent);
        Assert.True(HudPresentation.BuildFrame(snapshot).OverviewText.Contains(
            "运行中会话本额度周期合计：10 raw tokens", StringComparison.Ordinal));
        Assert.True(!HudPresentation.BuildFrame(snapshot).OverviewText.Contains("当前活跃会话合计", StringComparison.Ordinal));
        Assert.Equal("warning", HudPresentation.GetThreshold(quota).Code);
        Assert.Equal("critical", HudPresentation.GetThreshold(quota with
        {
            Primary = quota.Primary! with { UsedPercent = 91 }
        }).Code);
        Assert.Equal("normal", HudPresentation.GetThreshold(quota with
        {
            Primary = quota.Primary! with { UsedPercent = 79 }
        }).Code);
        Assert.Equal("stale", HudPresentation.GetThreshold(quota with { IsStale = true }).Code);
        Assert.True(HudPresentation.BuildCollapsedText(snapshot with { CycleTotal = null }).Contains("本周期不可用", StringComparison.Ordinal));
    }

    private static void ViewModelFields()
    {
        var now = DateTimeOffset.Parse("2026-08-05T00:00:00Z", CultureInfo.InvariantCulture);
        var quota = new QuotaObservation(new QuotaBucket("codex-main", "Codex", 10, 10080, now.AddDays(7)),
            Array.Empty<QuotaBucket>(), QuotaSource.OfficialAppServer, now, false);
        var snapshot = new HudSnapshot(quota, SampleSessions(now),
            new CanonicalTokenUsage(100, 80, 20, 0, 30, 5, 130, 130), now, false,
            "官方额度 · 本地索引 0 秒前", Array.Empty<string>(),
            new[] { new HudEvent(now, "quota", "quota_observed", "官方额度观测") },
            RunningCycleTotal: Usage(10));
        var viewModel = new MainViewModel();
        viewModel.Apply(snapshot);
        Assert.Equal("正常", viewModel.QuotaStateText);
        viewModel.Apply(snapshot with
        {
            Quota = quota with { Primary = quota.Primary! with { UsedPercent = 81 } }
        });
        Assert.Equal("额度偏低", viewModel.QuotaStateText);
        Assert.Equal("注意：剩余不高于 20%", viewModel.QuotaStateDetailText);
        foreach (var required in new[] { "已用", "剩余", "官方重置", "倒计时", "运行中会话", "运行中会话本额度周期合计",
                     "本额度周期全部会话合计", "阈值状态" })
            Assert.True(viewModel.OverviewText.Contains(required, StringComparison.Ordinal));
        Assert.True(viewModel.Rows.All(row => !string.IsNullOrWhiteSpace(row.Name) &&
            !string.IsNullOrWhiteSpace(row.ShortId) && !string.IsNullOrWhiteSpace(row.RoleNickname) &&
            !string.IsNullOrWhiteSpace(row.Project) && !string.IsNullOrWhiteSpace(row.Model) &&
            !string.IsNullOrWhiteSpace(row.Tier) && !string.IsNullOrWhiteSpace(row.Status)));
        viewModel.SetFilter("running");
        Assert.Equal(1, viewModel.Rows.Count);
        viewModel.SetFilter("recent");
        Assert.Equal(3, viewModel.Rows.Count);
        Assert.True(viewModel.Rows.Single(row => row.IsCurrent).CurrentLabel.Contains("推断", StringComparison.Ordinal));
        viewModel.SetFilter("all");
        Assert.Equal(3, viewModel.Rows.Count);
        viewModel.SetFilter("internal");
        Assert.Equal(0, viewModel.Rows.Count);
        Assert.True(viewModel.RenderedStrings().All(value => !value.Contains(Sentinel, StringComparison.Ordinal)));

        var pinnedSessions = SampleSessions(now).Select((session, index) => index == 0
            ? session with { Metadata = session.Metadata with { Pinned = true } }
            : session).ToArray();
        var pinnedViewModel = new MainViewModel();
        pinnedViewModel.Apply(snapshot with { Sessions = pinnedSessions });
        pinnedViewModel.SetFilter("all");
        pinnedViewModel.SetSort("token");
        Assert.Equal("thread-running-0001", pinnedViewModel.Rows[0].ThreadId);
        Assert.True(pinnedViewModel.Rows[0].IsPinned);
    }

    private static void HudV2EdgeBeaconVisualContract()
    {
        var root = ProjectRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "CodexUsageHud.App", "MainWindow.xaml"));
        var appXaml = File.ReadAllText(Path.Combine(root, "src", "CodexUsageHud.App", "App.xaml"));
        var window = File.ReadAllText(Path.Combine(root, "src", "CodexUsageHud.App", "MainWindow.xaml.cs"));
        foreach (var required in new[]
                 {
                     "CompactVerticalShell", "CompactTopShell", "ExpandedShell", "运行中", "最近会话", "全部",
                     "未归属", "SettingsPanel", "DiagnosticsPanel", "会话累计",
                     "CompactSettingsButton", "CompactTopmostButton", "CompactTopTopmostButton",
                     "CompactTopExpandButton", "CompactTopCountdownText", "CompactQuotaStateText",
                     "CompactTopRemainingText", "QuotaStateDetailText",
                     "CompactTopRunningText", "本周期摘要", "置顶会话始终优先",
                     "RunningCycleTotalText", "IsPinned", "窗口始终置顶", "FullscreenButton",
                     "续接参考", "压缩后底座", "底座趋势", "间隔续航",
                     "EffectiveRunwayText", "长会话风险", "实际漂移（人工确认）", "StructuralGradeText",
                     "DriftUnassessedButton", "DriftOccasionalButton", "DriftRepeatedButton",
                     "ContinuationGradeText",
                     "ContinuationAdviceText", "ContinuationEvidenceText", "SourceLabel",
                     "OnToggleSessionChildren", "工作合计 · raw", "父子关系和用量元数据",
                     "EnableRowVirtualization=\"True\"", "VirtualizationMode=\"Recycling\"",
                     "raw token 与平台额度不是同一单位，不能直接换算",
                 })
            Assert.True(xaml.Contains(required, StringComparison.Ordinal));
        foreach (var required in new[]
                 {
                     "CompactWidth = 224", "CompactTopWidth = 660", "ExpandedWidth = 1180", "EdgeHandle = 9",
                     "DockSnapDistance = 30", "PointerDockSnapDistance = 64", "ResolveDockSide",
                      "ApplyExpansionState(true)", "ApplyFullscreenBounds", "ExitFullscreen",
                      "_restoreHiddenOnNextCompact", "Keyboard.ClearFocus", "WaitAsync(0)",
                      "OnSetDriftAssessment", "SetSessionDriftAssessmentAsync",
                 })
            Assert.True(window.Contains(required, StringComparison.Ordinal));
        var countdownTick = window[window.IndexOf("_countdownTimer.Tick", StringComparison.Ordinal)..
            window.IndexOf("_hideTimer =", StringComparison.Ordinal)];
        Assert.True(!countdownTick.Contains("UpdateTextBlocks", StringComparison.Ordinal));
        Assert.True(!xaml.Contains("CircularProgressRing", StringComparison.Ordinal));
        Assert.True(!xaml.Contains("QuotaDialContainer", StringComparison.Ordinal));
        Assert.True(!xaml.Contains("项目分组", StringComparison.Ordinal));
        Assert.True(!xaml.Contains("压缩次数", StringComparison.Ordinal));
        Assert.True(!xaml.Contains("当前上下文（最近调用）", StringComparison.Ordinal));
        Assert.True(!xaml.Contains("•••", StringComparison.Ordinal));
        Assert.True(!window.Contains("MessageBox.Show", StringComparison.Ordinal));
        Assert.True(xaml.Contains("AllowsTransparency=\"False\"", StringComparison.Ordinal));
        Assert.True(xaml.Contains("TextOptions.TextRenderingMode=\"ClearType\"", StringComparison.Ordinal));
        Assert.True(appXaml.Contains("TextOptions.TextHintingMode\" Value=\"Fixed\"", StringComparison.Ordinal));
        Assert.True(!xaml.Contains("DropShadowEffect", StringComparison.Ordinal));
        Assert.True(!window.Contains("new ScaleTransform(1.08", StringComparison.Ordinal));
        var conversationColumn = xaml[xaml.IndexOf("Header=\"会话\"", StringComparison.Ordinal)..];
        conversationColumn = conversationColumn[..conversationColumn.IndexOf("</DataGridTemplateColumn>", StringComparison.Ordinal)];
        Assert.True(conversationColumn.IndexOf("Text=\"{Binding Name}\"", StringComparison.Ordinal) <
                    conversationColumn.IndexOf("Binding Path=\"SourceLabel\"", StringComparison.Ordinal));
        Assert.True(xaml.Contains("Topmost=\"False\"", StringComparison.Ordinal));
        foreach (var required in new[]
                 {
                     "CompactMinimizeButton", "PanelTopmostButton", "CompactTopmostButton",
                     "CompactTopTopmostButton", "AutoHideCheckBox",
                     "OnToggleTopmost", "OnMinimizeToTray", "always_on_top", "CollapseToCompact",
                     "UpdateTrayIconFromDial", "RestoreFromExternalActivation",
                 })
            Assert.True((xaml + window).Contains(required, StringComparison.Ordinal));
        var app = File.ReadAllText(Path.Combine(root, "src", "CodexUsageHud.App", "App.xaml.cs"));
        var activation = File.ReadAllText(Path.Combine(root, "src", "CodexUsageHud.App",
            "SingleInstanceActivation.cs"));
        var startup = File.ReadAllText(Path.Combine(root, "src", "CodexUsageHud.App",
            "StartupRegistration.cs"));
        var shortcut = File.ReadAllText(Path.Combine(root, "src", "CodexUsageHud.App",
            "DesktopShortcutRegistration.cs"));
        Assert.True(app.Contains("TrySignalAsync", StringComparison.Ordinal));
        Assert.True(activation.Contains("PipeOptions.CurrentUserOnly", StringComparison.Ordinal));
        Assert.True((window + startup).Contains("随 Windows 登录启动", StringComparison.Ordinal));
        Assert.True(startup.Contains("Registry.CurrentUser", StringComparison.Ordinal));
        Assert.True(shortcut.Contains("WScript.Shell", StringComparison.Ordinal));
        var executableWithSpaces = Path.Combine(root, "folder with spaces", "CodexUsageHud.App.exe");
        var startupCommand = StartupRegistration.BuildCommand(executableWithSpaces);
        Assert.Equal($"\"{Path.GetFullPath(executableWithSpaces)}\" --autostart", startupCommand);
        Assert.True(StartupRegistration.CommandTargetsExecutable(startupCommand, executableWithSpaces));
        Assert.True(!StartupRegistration.CommandTargetsExecutable(startupCommand,
            Path.Combine(root, "other", "CodexUsageHud.App.exe")));
        Assert.True(xaml.Contains("ResizeMode=\"NoResize\"", StringComparison.Ordinal));
        Assert.True(!xaml.Contains("最近统计·raw", StringComparison.Ordinal));
    }

    private static void HudWpfConstructionSmoke(string runRoot)
    {
        EnsureWpfTestApplication();
        var directory = Path.Combine(runRoot, "hud-wpf-construction");
        var codexHome = Path.Combine(directory, "codex-home");
        Directory.CreateDirectory(Path.Combine(codexHome, "sessions"));
        using var engine = new UsageEngine(codexHome, Path.Combine(directory, "usage.db"),
            Path.Combine(directory, "hud.log"));
        try
        {
            using var window = new MainWindow(engine, () => Task.CompletedTask);
            Assert.Equal(224d, window.Width);
            Assert.Equal(324d, window.Height);
        }
        catch (Exception exception)
        {
            var chain = new List<string>();
            for (var current = exception; current is not null; current = current.InnerException)
                chain.Add($"{current.GetType().Name}: {current.Message}");
            throw new InvalidOperationException(string.Join(" -> ", chain), exception);
        }
    }

    private static void HudScreenRecoveryGeometry()
    {
        var safePosition = typeof(MainWindow).GetMethod("IsSafePosition",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("safe_position_method_missing");
        bool IsSafe(double left, double top) => (bool)(safePosition.Invoke(null, new object[] { left, top })
            ?? throw new InvalidOperationException("safe_position_result_missing"));

        var virtualLeft = System.Windows.SystemParameters.VirtualScreenLeft;
        var virtualTop = System.Windows.SystemParameters.VirtualScreenTop;
        Assert.True(!IsSafe(virtualLeft - 800, virtualTop));
        Assert.True(IsSafe(virtualLeft - 100, virtualTop));
        Assert.True(IsSafe(virtualLeft, virtualTop - 100));
        Assert.True(!IsSafe(virtualLeft, virtualTop - 300));

        var intersection = typeof(MainWindow).GetMethod("HasUsableScreenIntersection",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("screen_intersection_method_missing");
        bool HasIntersection(System.Drawing.Rectangle bounds, System.Drawing.Rectangle[] screens) =>
            (bool)(intersection.Invoke(null, new object[] { bounds, screens, 48, 48 })
                ?? throw new InvalidOperationException("screen_intersection_result_missing"));
        var workAreas = new[] { new System.Drawing.Rectangle(0, 0, 1920, 1080) };
        Assert.True(!HasIntersection(new System.Drawing.Rectangle(-500, 0, 112, 330), workAreas));
        Assert.True(HasIntersection(new System.Drawing.Rectangle(-60, 0, 112, 330), workAreas));
        Assert.True(!HasIntersection(new System.Drawing.Rectangle(2200, 0, 112, 330), workAreas));
    }

    private static void PackagePublicationPrivacyContract()
    {
        var root = ProjectRoot();
        var publish = File.ReadAllText(Path.Combine(root, "scripts", "publish.ps1"));
        Assert.True(publish.Contains("docs\\PACKAGE_ACCEPTANCE.md", StringComparison.Ordinal));
        Assert.True(!publish.Contains("'docs\\ACCEPTANCE_EVIDENCE.md'", StringComparison.Ordinal));
        Assert.True(publish.Contains("-p:DebugType=None", StringComparison.Ordinal));
        Assert.True(publish.Contains("Test-PackagePrivacy", StringComparison.Ordinal));

        var publicEvidencePath = Path.Combine(root, "docs", "PACKAGE_ACCEPTANCE.md");
        Assert.True(File.Exists(publicEvidencePath));
        var publicEvidence = File.ReadAllText(publicEvidencePath);
        Assert.True(!publicEvidence.Contains(@"C:\Users\", StringComparison.OrdinalIgnoreCase));
        Assert.True(!System.Text.RegularExpressions.Regex.IsMatch(publicEvidence,
            @"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b"));

        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var spec = File.ReadAllText(Path.Combine(root, "docs", "TECHNICAL_SPEC.md"));
        Assert.True(!readme.Contains("78 checks", StringComparison.Ordinal));
        Assert.True(!readme.Contains("intentionally does not deduplicate across different thread",
            StringComparison.Ordinal));
        Assert.True(!readme.Contains("future validation item", StringComparison.Ordinal));
        Assert.True(readme.Contains("84 checks", StringComparison.Ordinal));
        Assert.True(readme.Contains("cache_read_input_tokens", StringComparison.Ordinal));
        Assert.True(!publicEvidence.Contains("FINAL_PACKAGE_VERIFIED", StringComparison.Ordinal));
        Assert.True(!publicEvidence.Contains("78 existing checks", StringComparison.Ordinal));
        Assert.True(publicEvidence.Contains("PRE_RELEASE_CANDIDATE", StringComparison.Ordinal));
        Assert.True(publicEvidence.Contains("84 automated tests", StringComparison.Ordinal));
        Assert.True(spec.Contains("cache_read_input_tokens", StringComparison.Ordinal));
        Assert.True(spec.Contains("seven independent", StringComparison.Ordinal));
        Assert.True(spec.Contains("missing is distinct from explicit zero", StringComparison.Ordinal) ||
                    spec.Contains("missing-versus-zero", StringComparison.Ordinal));
        Assert.True(readme.Contains("parent-tree", StringComparison.Ordinal) ||
                    readme.Contains("解析后的父树根", StringComparison.Ordinal));
    }

    private static void HudWpfRuntimeInteractions(string runRoot)
    {
        EnsureWpfTestApplication();

        var directory = Path.Combine(runRoot, "hud-wpf-runtime");
        var codexHome = Path.Combine(directory, "codex-home");
        Directory.CreateDirectory(Path.Combine(codexHome, "sessions"));
        using var engine = new UsageEngine(codexHome, Path.Combine(directory, "usage.db"),
            Path.Combine(directory, "hud.log"));
        using var window = new MainWindow(engine, () => Task.CompletedTask, false);
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.Left = System.Windows.SystemParameters.VirtualScreenLeft - 4000;
        window.Top = System.Windows.SystemParameters.VirtualScreenTop - 4000;
        window.Show();
        InvokeWindowMethod(window, "EnsureVisibleOnCurrentScreens");
        Assert.True(window.Left >= System.Windows.SystemParameters.VirtualScreenLeft - 312);
        Assert.True(window.Top >= System.Windows.SystemParameters.VirtualScreenTop - 282);

        var viewModelField = typeof(MainWindow).GetField("_viewModel",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("view_model_field_missing");
        var viewModel = (MainViewModel)(viewModelField.GetValue(window)
            ?? throw new InvalidOperationException("view_model_missing"));
        viewModel.Apply(CreateUiCaptureSnapshot());
        InvokeWindowMethod(window, "UpdateTextBlocks");
        InvokeWindowMethod(window, "UpdateTrayIconFromDial");
        var ownedTrayIcon = typeof(MainWindow).GetField("_ownedTrayIcon",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("owned_tray_icon_field_missing");
        Assert.NotNull(ownedTrayIcon.GetValue(window));
        var startupMenu = typeof(MainWindow).GetField("_startupMenuItem",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("startup_menu_item_field_missing");
        Assert.Equal("随 Windows 登录启动",
            ((System.Windows.Forms.ToolStripMenuItem)(startupMenu.GetValue(window)
                ?? throw new InvalidOperationException("startup_menu_item_missing"))).Text);

        Assert.True(!window.Topmost);
        Assert.True(!window.ShowInTaskbar);
        Assert.Near(224d, window.Width, 0.5d);
        Assert.Near(324d, window.Height, 0.5d);

        InvokeWindowMethod(window, "OnToggleTopmost", window, new System.Windows.RoutedEventArgs());
        Assert.True(window.Topmost);
        var panelTopmost = (System.Windows.Controls.Button)(window.FindName("PanelTopmostButton")
            ?? throw new InvalidOperationException("panel_topmost_missing"));
        var compactTopmost = (System.Windows.Controls.Button)(window.FindName("CompactTopmostButton")
            ?? throw new InvalidOperationException("compact_topmost_missing"));
        var compactTopTopmost = (System.Windows.Controls.Button)(window.FindName("CompactTopTopmostButton")
            ?? throw new InvalidOperationException("compact_top_topmost_missing"));
        Assert.True(panelTopmost.ToolTip?.ToString()?.Contains("取消", StringComparison.Ordinal) == true);
        Assert.True(compactTopmost.ToolTip?.ToString()?.Contains("取消", StringComparison.Ordinal) == true);
        Assert.True(compactTopTopmost.ToolTip?.ToString()?.Contains("取消", StringComparison.Ordinal) == true);
        var pointerInput = typeof(MainWindow).GetField("_topmostPointerInvocation",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("topmost_pointer_field_missing");
        var pointerArgs = new System.Windows.Input.MouseButtonEventArgs(
            System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount,
            System.Windows.Input.MouseButton.Left);
        InvokeWindowMethod(window, "OnTopmostPointerDown", panelTopmost, pointerArgs);
        Assert.True((bool)(pointerInput.GetValue(window) ?? false));
        InvokeWindowMethod(window, "OnToggleTopmost", window, new System.Windows.RoutedEventArgs());
        Assert.True(!window.Topmost);
        Assert.True(!(bool)(pointerInput.GetValue(window) ?? true));
        Assert.Equal("窗口始终置顶", panelTopmost.ToolTip?.ToString());
        Assert.Equal("窗口始终置顶", compactTopmost.ToolTip?.ToString());
        Assert.Equal("窗口始终置顶", compactTopTopmost.ToolTip?.ToString());
        Assert.True(panelTopmost.BorderBrush is System.Windows.Media.SolidColorBrush { Color.A: 0 });

        var grid = (System.Windows.Controls.DataGrid)(window.FindName("SessionGrid")
            ?? throw new InvalidOperationException("session_grid_missing"));
        var sort = (System.Windows.Controls.ComboBox)(window.FindName("SortComboBox")
            ?? throw new InvalidOperationException("sort_combo_missing"));
        Assert.Equal("activity", ((System.Windows.Controls.ComboBoxItem)sort.SelectedItem).Tag as string);
        Assert.Equal("019fcac6-82e", ((SessionDisplayRow)grid.SelectedItem).ThreadId);

        sort.SelectedIndex = 1;
        Assert.Equal("Codex Usage HUD 主任务", viewModel.Rows[0].Name);
        Assert.True(viewModel.Rows[0].IsPinned);
        sort.SelectedIndex = 0;
        Assert.True(viewModel.Rows[0].IsPinned);

        viewModel.SetFilter("running");
        Assert.Equal(3, viewModel.Rows.Count);
        viewModel.SetFilter("recent");
        Assert.Equal(5, viewModel.Rows.Count);
        viewModel.SetFilter("all");
        Assert.Equal(5, viewModel.Rows.Count);
        viewModel.SetFilter("internal");
        Assert.Equal(0, viewModel.Rows.Count);

        var hierarchyBase = CreateUiCaptureSnapshot();
        var hierarchyParent = hierarchyBase.Sessions[0];
        var hierarchyChildMetadata = new SessionMetadata("runtime-child", "运行时子任务", "worker", "Luna",
            "small-projects", "gpt-5.6-sol", null, "Standard（默认）", hierarchyBase.GeneratedAtUtc,
            null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
            ParentThreadId: hierarchyParent.Metadata.ThreadId, AgentDepth: 1);
        var hierarchyChild = new SessionAggregate(hierarchyChildMetadata, SessionStatus.Running, Usage(42), Usage(4),
            hierarchyChildMetadata.LastActivityUtc, "runtime-child-turn", false, RecentUsageKind.LatestTurn,
            Usage(4), "reliable-turn");
        viewModel.Apply(hierarchyBase with
        {
            Sessions = hierarchyBase.Sessions.Concat(new[] { hierarchyChild }).ToArray(),
        });
        viewModel.SetFilter("all");
        var hierarchyRootRow = viewModel.Rows.Single(row => row.ThreadId == hierarchyParent.Metadata.ThreadId);
        var hierarchyButton = new System.Windows.Controls.Button { DataContext = hierarchyRootRow };
        InvokeWindowMethod(window, "OnToggleSessionChildren", hierarchyButton,
            new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Assert.True(viewModel.Rows.Any(row => row.ThreadId == hierarchyChild.Metadata.ThreadId));
        Assert.Equal(hierarchyParent.Metadata.ThreadId,
            ((SessionDisplayRow)grid.SelectedItem).ThreadId);

        viewModel.Apply(CreateUiCaptureSnapshot());
        viewModel.SetFilter("running");
        grid.SelectedIndex = 0;

        var statusButton = (System.Windows.Controls.Button)(window.FindName("DataStatusButton")
            ?? throw new InvalidOperationException("status_button_missing"));
        var diagnostics = (System.Windows.FrameworkElement)(window.FindName("DiagnosticsPanel")
            ?? throw new InvalidOperationException("diagnostics_panel_missing"));
        statusButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Assert.Equal(System.Windows.Visibility.Visible, diagnostics.Visibility);
        InvokeWindowMethod(window, "OnCloseDiagnostics", window, new System.Windows.RoutedEventArgs());

        var settingsButton = (System.Windows.Controls.Button)(window.FindName("CompactSettingsButton")
            ?? throw new InvalidOperationException("compact_settings_button_missing"));
        settingsButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        var settingsPanel = (System.Windows.FrameworkElement)(window.FindName("SettingsPanel")
            ?? throw new InvalidOperationException("settings_panel_missing"));
        Assert.Equal(System.Windows.Visibility.Visible, settingsPanel.Visibility);
        InvokeWindowMethod(window, "OnCloseSettings", window, new System.Windows.RoutedEventArgs());
        Assert.Equal(System.Windows.Visibility.Collapsed, settingsPanel.Visibility);
        Assert.True(viewModel.IsExpanded);
        Assert.Near(1180d, window.Width, 0.5d);
        InvokeWindowMethod(window, "OnToggleExpand", window, new System.Windows.RoutedEventArgs());
        Assert.Near(224d, window.Width, 0.5d);
        InvokeWindowMethod(window, "OnToggleExpand", window, new System.Windows.RoutedEventArgs());
        Assert.Near(1180d, window.Width, 0.5d);
        Assert.Near(820d, window.Height, 0.5d);
        var workMethod = typeof(MainWindow).GetMethod("GetWorkAreaLogical",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("work_area_method_missing");
        var work = (System.Windows.Rect)(workMethod.Invoke(window, null)
            ?? throw new InvalidOperationException("work_area_missing"));
        var detailColumn = (System.Windows.Controls.ColumnDefinition)(window.FindName("DetailColumn")
            ?? throw new InvalidOperationException("detail_column_missing"));
        var detailContent = (System.Windows.FrameworkElement)(window.FindName("DetailContentGrid")
            ?? throw new InvalidOperationException("detail_content_missing"));
        InvokeWindowMethod(window, "OnToggleFullscreen", window, new System.Windows.RoutedEventArgs());
        Assert.Near(work.Width, window.Width, 1.5d);
        Assert.Near(work.Height, window.Height, 1.5d);
        Assert.True(detailColumn.Width.Value > 404d);
        Assert.True(detailContent.LayoutTransform.Value.IsIdentity);
        InvokeWindowMethod(window, "OnToggleFullscreen", window, new System.Windows.RoutedEventArgs());
        Assert.Near(1180d, window.Width, 0.5d);
        Assert.Near(820d, window.Height, 0.5d);
        Assert.Near(404d, detailColumn.Width.Value, 0.1d);

        var trayField = typeof(MainWindow).GetField("_tray",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("tray_field_missing");
        var tray = (System.Windows.Forms.NotifyIcon)(trayField.GetValue(window)
            ?? throw new InvalidOperationException("tray_missing"));
        tray.Visible = false;
        InvokeWindowMethod(window, "OnMinimizeToTray", window, new System.Windows.RoutedEventArgs());
        Assert.True(!window.IsVisible);
        Assert.True(!viewModel.IsExpanded);
        Assert.Near(224d, window.Width, 0.5d);
        Assert.Near(324d, window.Height, 0.5d);
        InvokeWindowMethod(window, "RestoreFromExternalActivation");
        Assert.True(window.IsVisible);
        Assert.True(!viewModel.IsExpanded);
        Assert.Near(224d, window.Width, 0.5d);
        Assert.Near(324d, window.Height, 0.5d);
        InvokeWindowMethod(window, "OnMinimizeToTray", window, new System.Windows.RoutedEventArgs());
        Assert.True(!window.IsVisible);
        tray.ContextMenuStrip?.Items[0].PerformClick();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Assert.True(window.IsVisible);
        Assert.Near(224d, window.Width, 0.5d);
        Assert.Near(324d, window.Height, 0.5d);

        engine.SaveSettingsAsync(new Dictionary<string, string>
        {
            ["always_on_top"] = "1",
            ["dock_side"] = "right",
            ["auto_hide"] = "0",
        }).GetAwaiter().GetResult();
        viewModel.IsExpanded = true;
        InvokeWindowMethod(window, "ApplyExpansionState", false);
        var restore = typeof(MainWindow).GetMethod("RestoreWindowStateAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("restore_window_state_missing");
        var restoreTask = (Task)(restore.Invoke(window, null)
            ?? throw new InvalidOperationException("restore_window_state_task_missing"));
        AwaitWithDispatcher(restoreTask, window.Dispatcher, TimeSpan.FromSeconds(5));
        InvokeWindowMethod(window, "ApplyExpansionState", false);
        Assert.True(!viewModel.IsExpanded);
        Assert.True(window.Topmost);
        Assert.Near(224d, window.Width, 0.5d);
        Assert.Near(324d, window.Height, 0.5d);
        window.Topmost = false;
        InvokeWindowMethod(window, "UpdateTopmostState");

        InvokeWindowMethod(window, "OnToggleExpand", window, new System.Windows.RoutedEventArgs());
        window.Close();
        Assert.True(!window.IsVisible);
        Assert.True(!viewModel.IsExpanded);

        var capture = typeof(MainWindow).GetMethod("CaptureWindowSettings",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("capture_settings_missing");
        var settings = (Dictionary<string, string>)(capture.Invoke(window, null)
            ?? throw new InvalidOperationException("settings_missing"));
        Assert.Equal("0", settings["always_on_top"]);
        Assert.True(!settings.ContainsKey("collapsed"));
        Assert.Equal("right", settings["dock_side"]);
        window.Hide();
    }

    private static void HudV2InteractionStress(string runRoot)
    {
        EnsureWpfTestApplication();
        var directory = Path.Combine(runRoot, "hud-v2-stress");
        var codexHome = Path.Combine(directory, "codex-home");
        Directory.CreateDirectory(Path.Combine(codexHome, "sessions"));
        using var engine = new UsageEngine(codexHome, Path.Combine(directory, "usage.db"),
            Path.Combine(directory, "hud.log"));
        engine.Cadence.MarkQuotaAttempt(DateTimeOffset.UtcNow);
        using var window = new MainWindow(engine, () => Task.CompletedTask, false)
        {
            ShowActivated = false,
            ShowInTaskbar = false,
            Left = System.Windows.SystemParameters.WorkArea.Left + 30,
            Top = System.Windows.SystemParameters.WorkArea.Top + 30,
        };
        window.Show();
        var viewModelField = typeof(MainWindow).GetField("_viewModel",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("view_model_field_missing");
        var viewModel = (MainViewModel)(viewModelField.GetValue(window)
            ?? throw new InvalidOperationException("view_model_missing"));
        viewModel.Apply(CreateStressSnapshot(1000));
        InvokeWindowMethod(window, "UpdateTextBlocks");
        viewModel.IsExpanded = true;
        InvokeWindowMethod(window, "ApplyExpansionState", false);
        window.UpdateLayout();

        var grid = (System.Windows.Controls.DataGrid)(window.FindName("SessionGrid")
            ?? throw new InvalidOperationException("session_grid_missing"));
        Assert.True(System.Windows.Controls.VirtualizingPanel.GetIsVirtualizing(grid));
        Assert.Equal(System.Windows.Controls.VirtualizationMode.Recycling,
            System.Windows.Controls.VirtualizingPanel.GetVirtualizationMode(grid));

        var running = (System.Windows.Controls.RadioButton)(window.FindName("RunningNav")
            ?? throw new InvalidOperationException("running_nav_missing"));
        var recent = (System.Windows.Controls.RadioButton)(window.FindName("RecentNav")
            ?? throw new InvalidOperationException("recent_nav_missing"));
        var all = (System.Windows.Controls.RadioButton)(window.FindName("AllNav")
            ?? throw new InvalidOperationException("all_nav_missing"));
        var internalNav = (System.Windows.Controls.RadioButton)(window.FindName("InternalNav")
            ?? throw new InvalidOperationException("internal_nav_missing"));
        var sort = (System.Windows.Controls.ComboBox)(window.FindName("SortComboBox")
            ?? throw new InvalidOperationException("sort_combo_missing"));
        var navigation = new[] { running, recent, all, internalNav };
        var timings = new List<long>();
        var rowNotifications = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.Rows)) rowNotifications++;
        };
        for (var index = 0; index < 80; index++)
        {
            var watch = Stopwatch.StartNew();
            navigation[index % navigation.Length].IsChecked = true;
            sort.SelectedIndex = index % 2;
            window.UpdateLayout();
            watch.Stop();
            timings.Add(watch.ElapsedMilliseconds);
        }
        var interactionP95 = Percentile95(timings);
        var coalescedApply = Stopwatch.StartNew();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
        coalescedApply.Stop();
        Console.WriteLine($"HUD_V2_NAVIGATION rows=1000 p95_ms={interactionP95} " +
                          $"coalesced_apply_ms={coalescedApply.ElapsedMilliseconds} notifications={rowNotifications}");
        Assert.True(interactionP95 <= 150);
        Assert.True(coalescedApply.Elapsed < TimeSpan.FromMilliseconds(500));
        Assert.True(rowNotifications <= 4);
        Assert.True(viewModel.Rows.Count > 0 && viewModel.Rows.All(row => row.IsInternalTask));
        Assert.True(viewModel.Rows.Count <= 750);

        var collapse = (System.Windows.Controls.Button)(window.FindName("CollapseButton")
            ?? throw new InvalidOperationException("collapse_button_missing"));
        var expand = (System.Windows.Controls.Button)(window.FindName("CompactExpandButton")
            ?? throw new InvalidOperationException("compact_expand_button_missing"));
        var toggleWatch = Stopwatch.StartNew();
        for (var index = 0; index < 30; index++)
        {
            collapse.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            expand.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        }
        window.UpdateLayout();
        toggleWatch.Stop();
        Assert.True(viewModel.IsExpanded);
        Assert.True(toggleWatch.Elapsed < TimeSpan.FromSeconds(3));

        var settings = (System.Windows.Controls.Button)(window.FindName("PanelSettingsButton")
            ?? throw new InvalidOperationException("settings_button_missing"));
        var closeSettings = (System.Windows.Controls.Button)(window.FindName("CloseSettingsButton")
            ?? throw new InvalidOperationException("close_settings_button_missing"));
        for (var index = 0; index < 30; index++)
        {
            settings.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            closeSettings.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        }
        var settingsPanel = (System.Windows.FrameworkElement)(window.FindName("SettingsPanel")
            ?? throw new InvalidOperationException("settings_panel_missing"));
        Assert.Equal(System.Windows.Visibility.Collapsed, settingsPanel.Visibility);

        var selectedThread = ((SessionDisplayRow)grid.SelectedItem).ThreadId;
        var repeatedDrift = (System.Windows.Controls.Button)(window.FindName("DriftRepeatedButton")
            ?? throw new InvalidOperationException("drift_repeated_button_missing"));
        var driftPending = typeof(MainWindow).GetField("_driftWritePending",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("drift_pending_field_missing");
        for (var index = 0; index < 20; index++)
            repeatedDrift.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        var driftSaved = Task.Run(async () =>
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                if (engine.Database.LoadSessionDriftAssessments().TryGetValue(selectedThread, out var assessment) &&
                    assessment.Level == DriftAssessmentLevel.Repeated &&
                    (int)(driftPending.GetValue(window) ?? 1) == 0) return;
                await Task.Delay(20);
            }
            throw new InvalidOperationException("drift_write_timeout");
        });
        AwaitWithDispatcher(driftSaved, window.Dispatcher, TimeSpan.FromSeconds(5));
        Assert.True(repeatedDrift.IsEnabled);

        var refresh = typeof(MainWindow).GetMethod("RefreshAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("refresh_method_missing");
        var refreshTasks = new List<Task>();
        var clickWatch = Stopwatch.StartNew();
        for (var index = 0; index < 30; index++)
            refreshTasks.Add((Task)(refresh.Invoke(window, new object[] { false })
                ?? throw new InvalidOperationException("refresh_task_missing")));
        clickWatch.Stop();
        Assert.True(clickWatch.ElapsedMilliseconds <= 150);
        AwaitWithDispatcher(Task.WhenAll(refreshTasks), window.Dispatcher, TimeSpan.FromSeconds(10));

        var heartbeat = false;
        _ = window.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => heartbeat = true));
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Assert.True(heartbeat);
        Assert.True(window.IsVisible);
        Assert.True(viewModel.IsExpanded);
        Console.WriteLine($"HUD_V2_STRESS rows=1000 navigation_p95_ms={interactionP95} " +
                          $"toggle_30_pairs_ms={toggleWatch.ElapsedMilliseconds} " +
                          $"refresh_30_submit_ms={clickWatch.ElapsedMilliseconds} row_events={rowNotifications}");
        window.Hide();
    }

    private static void HudV2DockGeometry(string runRoot)
    {
        EnsureWpfTestApplication();
        var directory = Path.Combine(runRoot, "hud-v2-dock");
        var codexHome = Path.Combine(directory, "codex-home");
        Directory.CreateDirectory(Path.Combine(codexHome, "sessions"));
        using var engine = new UsageEngine(codexHome, Path.Combine(directory, "usage.db"),
            Path.Combine(directory, "hud.log"));
        using var window = new MainWindow(engine, () => Task.CompletedTask, false)
        {
            ShowActivated = false,
            ShowInTaskbar = false,
        };
        window.Show();
        var viewModelField = typeof(MainWindow).GetField("_viewModel",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("view_model_field_missing");
        var viewModel = (MainViewModel)(viewModelField.GetValue(window)
            ?? throw new InvalidOperationException("view_model_missing"));
        viewModel.Apply(CreateUiCaptureSnapshot());
        var dockField = typeof(MainWindow).GetField("_dockSide",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("dock_field_missing");
        var autoHideField = typeof(MainWindow).GetField("_autoHide",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("auto_hide_field_missing");
        var hiddenField = typeof(MainWindow).GetField("_isEdgeHidden",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("hidden_field_missing");
        var workMethod = typeof(MainWindow).GetMethod("GetWorkAreaLogical",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("work_area_method_missing");
        var hiddenMethod = typeof(MainWindow).GetMethod("SetEdgeHidden",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("hidden_method_missing");
        var scheduleHide = typeof(MainWindow).GetMethod("ScheduleEdgeHide",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("schedule_hide_method_missing");
        var resolveDock = typeof(MainWindow).GetMethod("ResolveDockSide",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("resolve_dock_method_missing");
        var work = (System.Windows.Rect)(workMethod.Invoke(window, null)
            ?? throw new InvalidOperationException("work_area_missing"));
        autoHideField.SetValue(window, true);

        string Resolve(System.Windows.Rect windowBounds, System.Windows.Point pointer) =>
            resolveDock.Invoke(null, new object[] { work, windowBounds, pointer })?.ToString()
            ?? throw new InvalidOperationException("resolve_dock_result_missing");
        Assert.Equal("Right", Resolve(new System.Windows.Rect(work.Right - 300, work.Top + 100, 224, 324),
            new System.Windows.Point(work.Right - 8, work.Top + 220)));
        Assert.Equal("Left", Resolve(new System.Windows.Rect(work.Left + 180, work.Top + 100, 224, 324),
            new System.Windows.Point(work.Left + 8, work.Top + 220)));
        Assert.Equal("Top", Resolve(new System.Windows.Rect(work.Left + 300, work.Top + 160, 660, 80),
            new System.Windows.Point(work.Left + 500, work.Top + 8)));
        Assert.Equal("None", Resolve(new System.Windows.Rect(work.Left + 300, work.Top + 160, 224, 324),
            new System.Windows.Point(work.Left + 500, work.Top + 500)));

        void Dock(string name)
        {
            viewModel.IsExpanded = false;
            hiddenField.SetValue(window, false);
            dockField.SetValue(window, Enum.Parse(dockField.FieldType, name));
            InvokeWindowMethod(window, "ApplyExpansionState", false);
        }

        Dock("Right");
        Assert.Near(work.Right - window.Width, window.Left, 1.5);
        scheduleHide.Invoke(window, null);
        AwaitWithDispatcher(Task.Delay(700), window.Dispatcher, TimeSpan.FromSeconds(2));
        Assert.True((bool)(hiddenField.GetValue(window) ?? false));
        Assert.Near(work.Right - 9, window.Left, 1.5);
        hiddenMethod.Invoke(window, new object[] { false });
        InvokeWindowMethod(window, "OnToggleExpand", window, new System.Windows.RoutedEventArgs());
        Assert.Near(work.Right - window.Width - 12, window.Left, 1.5);
        InvokeWindowMethod(window, "OnToggleExpand", window, new System.Windows.RoutedEventArgs());
        Assert.True((bool)(hiddenField.GetValue(window) ?? false));
        Assert.Near(work.Right - 9, window.Left, 1.5);

        Dock("Left");
        Assert.Near(work.Left, window.Left, 1.5);
        scheduleHide.Invoke(window, null);
        AwaitWithDispatcher(Task.Delay(700), window.Dispatcher, TimeSpan.FromSeconds(2));
        Assert.True((bool)(hiddenField.GetValue(window) ?? false));
        Assert.Near(work.Left - window.Width + 9, window.Left, 1.5);
        hiddenMethod.Invoke(window, new object[] { false });
        InvokeWindowMethod(window, "OnToggleExpand", window, new System.Windows.RoutedEventArgs());
        Assert.Near(work.Left + 12, window.Left, 1.5);
        InvokeWindowMethod(window, "OnToggleExpand", window, new System.Windows.RoutedEventArgs());
        Assert.True((bool)(hiddenField.GetValue(window) ?? false));
        Assert.Near(work.Left - window.Width + 9, window.Left, 1.5);

        Dock("Top");
        Assert.Near(660, window.Width, 0.5);
        Assert.Near(80, window.Height, 0.5);
        var topCountdown = (System.Windows.Controls.TextBlock)(window.FindName("CompactTopCountdownText")
            ?? throw new InvalidOperationException("top_countdown_missing"));
        var topRunning = (System.Windows.Controls.TextBlock)(window.FindName("CompactTopRunningText")
            ?? throw new InvalidOperationException("top_running_missing"));
        Assert.True(!string.IsNullOrWhiteSpace(topCountdown.Text));
        Assert.Equal("3", topRunning.Text);
        Assert.Near(work.Top, window.Top, 1.5);
        hiddenMethod.Invoke(window, new object[] { true });
        Assert.Near(work.Top - window.Height + 9, window.Top, 1.5);
        var enter = new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice,
            Environment.TickCount)
        {
            RoutedEvent = System.Windows.Input.Mouse.MouseEnterEvent,
        };
        window.RaiseEvent(enter);
        AwaitWithDispatcher(Task.Delay(220), window.Dispatcher, TimeSpan.FromSeconds(2));
        Assert.True(!(bool)(hiddenField.GetValue(window) ?? true));
        Assert.Near(work.Top, window.Top, 1.5);
        InvokeWindowMethod(window, "OnToggleExpand", window, new System.Windows.RoutedEventArgs());
        Assert.Near(work.Top + 12, window.Top, 1.5);
        InvokeWindowMethod(window, "OnToggleExpand", window, new System.Windows.RoutedEventArgs());
        Assert.True((bool)(hiddenField.GetValue(window) ?? false));
        Assert.Near(work.Top - window.Height + 9, window.Top, 1.5);
        window.Hide();
    }

    private static void PinPersistence(string runRoot)
    {
        var dbPath = Path.Combine(runRoot, "pin", "usage.db");
        using (var database = new UsageDatabase(dbPath))
        {
            database.UpsertSession(new SessionMetadata("one-thread", "One", null, null, null, null, null, null, null, null));
            database.UpsertSession(new SessionMetadata("two-thread", "Two", null, null, null, null, null, null, null, null));
            database.SetPinnedThread("two-thread");
            Assert.Equal("two-thread", database.LoadSessions().Single(item => item.Pinned).ThreadId);
        }
        using var restarted = new UsageDatabase(dbPath);
        Assert.Equal("two-thread", restarted.LoadSessions().Single(item => item.Pinned).ThreadId);
        restarted.SetPinnedThread(null);
        Assert.True(restarted.LoadSessions().All(item => !item.Pinned));
    }

    private static void ShutdownCoordinatorTest()
    {
        var close = 0;
        var shutdown = 0;
        var coordinator = new ShutdownCoordinator();
        coordinator.RequestExit(() => close++, () => shutdown++);
        coordinator.RequestExit(() => close++, () => shutdown++);
        Assert.True(coordinator.ExitRequested);
        Assert.Equal(1, close);
        Assert.Equal(1, shutdown);
    }

    private static void SingleInstanceGateTest(string runRoot)
    {
        var directory = Path.Combine(runRoot, "instance-lock-basic");
        var firstOutcome = SingleInstanceGate.TryAcquire(directory, out var first, out var firstCode);
        Assert.Equal(InstanceGateOutcome.Acquired, firstOutcome);
        Assert.Equal("instance_lock_acquired", firstCode);
        Assert.NotNull(first);
        var secondOutcome = SingleInstanceGate.TryAcquire(directory, out var second, out var secondCode);
        Assert.Equal(InstanceGateOutcome.Contended, secondOutcome);
        Assert.Equal("instance_lock_contended", secondCode);
        Assert.True(second is null);

        var activationReceived = new ManualResetEventSlim(false);
        using (var activationServer = new SingleInstanceActivationServer(directory, () =>
               {
                   activationReceived.Set();
                   return Task.CompletedTask;
               }))
        {
            var pipeName = SingleInstanceActivationServer.GetPipeName(directory);
            Assert.Equal(pipeName, SingleInstanceActivationServer.GetPipeName(directory));
            Assert.True(!pipeName.Contains(directory, StringComparison.OrdinalIgnoreCase));
            Assert.True(!pipeName.Equals(
                SingleInstanceActivationServer.GetPipeName(directory + "-other"),
                StringComparison.Ordinal));
            Assert.True(SingleInstanceActivationServer.TrySignalAsync(directory,
                TimeSpan.FromSeconds(3)).GetAwaiter().GetResult());
            Assert.True(activationReceived.Wait(TimeSpan.FromSeconds(2)));
        }
        first!.Dispose();
        Assert.Equal(InstanceGateOutcome.Acquired,
            SingleInstanceGate.TryAcquire(directory, out var recovered, out _));
        recovered!.Dispose();
    }

    private static void DuplicateTokenStateImmutable(string runRoot)
    {
        var directory = Path.Combine(runRoot, "s2-1-duplicate-state");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "source.jsonl");
        var replay = Path.Combine(directory, "replay.jsonl");
        var now = DateTimeOffset.UtcNow.AddSeconds(-5).ToUnixTimeSeconds();
        var original = string.Join('\n', new[]
        {
            "{\"type\":\"session_meta\",\"payload\":{\"id\":\"thread-s2-1-duplicate\"}}",
            $"{{\"type\":\"event_msg\",\"payload\":{{\"type\":\"task_started\",\"thread_id\":\"thread-s2-1-duplicate\",\"timestamp\":{now}}}}}",
            $"{{\"type\":\"turn_context\",\"payload\":{{\"thread_id\":\"thread-s2-1-duplicate\",\"turn_id\":\"turn-live\",\"model\":\"model-a\",\"service_tier\":\"priority\",\"timestamp\":{now + 1}}}}}",
            TokenLineAdvanced("thread-s2-1-duplicate", now + 2, 10, 2, 10, 2, "model-a", "priority"),
        }) + "\n";
        File.WriteAllText(source, original, new UTF8Encoding(false));
        var databasePath = Path.Combine(directory, "usage.db");
        SessionMetadata before;
        SessionAggregate beforeAggregate;
        using (var database = new UsageDatabase(databasePath))
        {
            var indexer = new RolloutIndexer(database);
            indexer.ScanFile(source, "sessions/source.jsonl", "thread-s2-1-duplicate");
            before = database.LoadSessions().Single();
            beforeAggregate = indexer.LoadAggregates(DateTimeOffset.UtcNow).Single();

            var replayText = "{\"type\":\"session_meta\",\"payload\":{\"id\":\"thread-s2-1-duplicate\"}}\n" +
                             TokenLineAdvanced("thread-s2-1-duplicate", null, 10, 2, 10, 2,
                                 "model-b", "standard") + "\n";
            File.WriteAllText(replay, replayText, new UTF8Encoding(false));
            File.SetLastWriteTimeUtc(replay, DateTime.UtcNow.AddHours(3));
            var result = indexer.ScanFile(replay, "archived_sessions/replay.jsonl",
                "thread-s2-1-duplicate");
            Assert.Equal(0, result.AcceptedSamples);
            Assert.Equal(1, result.DuplicateSamples);
            Assert.Equal(before, database.LoadSessions().Single());
            var immediate = indexer.LoadAggregates(DateTimeOffset.UtcNow).Single();
            Assert.Equal(beforeAggregate.SessionTotal, immediate.SessionTotal);
            Assert.Equal(beforeAggregate.RecentKind, immediate.RecentKind);
            Assert.Equal(beforeAggregate.RecentUsage, immediate.RecentUsage);
            Assert.Equal(beforeAggregate.CurrentTurnKey, immediate.CurrentTurnKey);
            Assert.Equal(beforeAggregate.Status, immediate.Status);
        }

        using var restarted = new UsageDatabase(databasePath);
        Assert.Equal(before, restarted.LoadSessions().Single());
        var reopened = restarted.LoadSessionAggregates(DateTimeOffset.UtcNow).Single();
        Assert.Equal(beforeAggregate.SessionTotal, reopened.SessionTotal);
        Assert.Equal(beforeAggregate.RecentUsage, reopened.RecentUsage);
        Assert.Equal(beforeAggregate.CurrentTurnKey, reopened.CurrentTurnKey);
    }

    private static void TimestamplessAndOrderTies(string runRoot)
    {
        var directory = Path.Combine(runRoot, "s2-1-order");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "source.jsonl");
        var crossSource = Path.Combine(directory, "cross.jsonl");
        var now = DateTimeOffset.UtcNow.AddSeconds(-10).ToUnixTimeSeconds();
        var firstTokenTime = now + 2;
        var initial = string.Join('\n', new[]
        {
            "{\"type\":\"session_meta\",\"payload\":{\"id\":\"thread-s2-1-order\"}}",
            TokenLineAdvanced("thread-s2-1-order", firstTokenTime, 10, 2, 10, 2, "model-a", "standard"),
        }) + "\n";
        File.WriteAllText(source, initial, new UTF8Encoding(false));
        using var database = new UsageDatabase(Path.Combine(directory, "usage.db"));
        var indexer = new RolloutIndexer(database);
        indexer.ScanFile(source, "sessions/source.jsonl", "thread-s2-1-order");
        var before = database.LoadSessions().Single();

        File.AppendAllText(source, TokenLineAdvanced("thread-s2-1-order", null,
            20, 4, 10, 2, "model-b", "priority") + "\n", new UTF8Encoding(false));
        Assert.Equal(1, indexer.ScanFile(source, "sessions/source.jsonl", "thread-s2-1-order").AcceptedSamples);
        var afterMissing = database.LoadSessions().Single();
        Assert.Equal(before.Model, afterMissing.Model);
        Assert.Equal(before.ServiceTier, afterMissing.ServiceTier);
        Assert.Equal(before.LastActivityUtc, afterMissing.LastActivityUtc);
        Assert.Equal(before.CurrentTurnKey, afterMissing.CurrentTurnKey);
        Assert.Equal(RecentUsageKind.LatestEventDegraded,
            indexer.LoadAggregates(DateTimeOffset.UtcNow).Single().RecentKind);

        File.AppendAllText(source, TokenLineAdvanced("thread-s2-1-order", firstTokenTime,
            30, 6, 10, 2, "model-c", "priority") + "\n", new UTF8Encoding(false));
        indexer.ScanFile(source, "sessions/source.jsonl", "thread-s2-1-order");
        var sameSourceTie = database.LoadSessions().Single();
        Assert.Equal("model-c", sameSourceTie.Model);
        Assert.Equal("Fast", sameSourceTie.ServiceTier);

        File.WriteAllText(crossSource, TokenLineAdvanced("thread-s2-1-order", firstTokenTime,
            40, 8, 10, 2, "model-d", "standard") + "\n", new UTF8Encoding(false));
        indexer.ScanFile(crossSource, "archived_sessions/cross.jsonl", "thread-s2-1-order");
        var crossTie = database.LoadSessions().Single();
        Assert.Equal(sameSourceTie.Model, crossTie.Model);
        Assert.Equal(sameSourceTie.ServiceTier, crossTie.ServiceTier);
        Assert.Equal(sameSourceTie.LastActivityUtc, crossTie.LastActivityUtc);

        File.AppendAllText(crossSource, TokenLineAdvanced("thread-s2-1-order", firstTokenTime - 5,
            50, 10, 10, 2, "model-e", "standard") + "\n", new UTF8Encoding(false));
        indexer.ScanFile(crossSource, "archived_sessions/cross.jsonl", "thread-s2-1-order");
        Assert.Equal("model-c", database.LoadSessions().Single().Model);
    }

    private static void FailedCommitEqualsRestart(string runRoot)
    {
        var directory = Path.Combine(runRoot, "s2-2-failed-commit");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "source.jsonl");
        File.WriteAllText(source,
            "{\"type\":\"session_meta\",\"payload\":{\"id\":\"thread-s2-2-fail\"}}\n" +
            TokenLine("thread-s2-2-fail", 1700101000, 10, 2, 10, 2) + "\n",
            new UTF8Encoding(false));
        var databasePath = Path.Combine(directory, "usage.db");
        SessionAggregate before;
        long offsetBefore;
        using (var database = new UsageDatabase(databasePath))
        {
            var indexer = new RolloutIndexer(database);
            indexer.ScanFile(source, "sessions/source.jsonl", "thread-s2-2-fail");
            before = indexer.LoadAggregates(DateTimeOffset.Parse("2026-08-05T00:00:00Z",
                CultureInfo.InvariantCulture)).Single();
            offsetBefore = database.LoadSourceStates().Single().CompleteOffset;
            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                connection.Open();
                using var trigger = connection.CreateCommand();
                trigger.CommandText = """
                    CREATE TRIGGER fail_scan_source BEFORE UPDATE ON source_files
                    BEGIN SELECT RAISE(ABORT, 'scan_abort'); END;
                    """;
                trigger.ExecuteNonQuery();
            }
            File.AppendAllText(source, TokenLine("thread-s2-2-fail", 1700101001,
                20, 4, 10, 2) + "\n", new UTF8Encoding(false));
            var failed = false;
            try
            {
                indexer.ScanFile(source, "sessions/source.jsonl", "thread-s2-2-fail");
            }
            catch (SqliteException)
            {
                failed = true;
            }
            Assert.True(failed);
            Assert.Equal(1L, database.GetSampleCount());
            Assert.Equal(offsetBefore, database.LoadSourceStates().Single().CompleteOffset);
            Assert.Equal(before, indexer.LoadAggregates(DateTimeOffset.Parse(
                "2026-08-05T00:00:00Z", CultureInfo.InvariantCulture)).Single());
        }

        using (var cleanup = new SqliteConnection($"Data Source={databasePath}"))
        {
            cleanup.Open();
            using var command = cleanup.CreateCommand();
            command.CommandText = "DROP TRIGGER fail_scan_source;";
            command.ExecuteNonQuery();
        }
        using var restarted = new UsageDatabase(databasePath);
        Assert.Equal(1L, restarted.GetSampleCount());
        Assert.Equal(offsetBefore, restarted.LoadSourceStates().Single().CompleteOffset);
        Assert.Equal(before, restarted.LoadSessionAggregates(DateTimeOffset.Parse(
            "2026-08-05T00:00:00Z", CultureInfo.InvariantCulture)).Single());
    }

    private static void BusyAndRevisionConflict(string runRoot)
    {
        var directory = Path.Combine(runRoot, "s2-2-busy-conflict");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "source.jsonl");
        File.WriteAllText(source,
            "{\"type\":\"session_meta\",\"payload\":{\"id\":\"thread-s2-2-busy\"}}\n" +
            TokenLine("thread-s2-2-busy", 1700102000, 10, 2, 10, 2) + "\n",
            new UTF8Encoding(false));
        var databasePath = Path.Combine(directory, "usage.db");
        using var database = new UsageDatabase(databasePath);
        var indexer = new RolloutIndexer(database);
        indexer.ScanFile(source, "sessions/source.jsonl", "thread-s2-2-busy");
        var before = database.GetSessionTotal("thread-s2-2-busy");
        var state = database.LoadSourceStates().Single();
        File.AppendAllText(source, TokenLine("thread-s2-2-busy", 1700102001,
            20, 4, 10, 2) + "\n", new UTF8Encoding(false));

        using (var blocker = new SqliteConnection($"Data Source={databasePath}"))
        {
            blocker.Open();
            using var transaction = blocker.BeginTransaction(deferred: false);
            var busy = indexer.ScanFile(source, "sessions/source.jsonl", "thread-s2-2-busy");
            Assert.True(busy.ErrorCodes.Contains("database_busy", StringComparer.Ordinal));
            Assert.Equal(before, database.GetSessionTotal("thread-s2-2-busy"));
            Assert.Equal(state, database.LoadSourceStates().Single());
            transaction.Rollback();
        }

        var staleEvent = new ParsedEvent("event_msg:token_count", "thread-s2-2-busy", null,
            null, null, null, null, new TokenUsageSnapshot(
                new TokenComponents(30, null, null, null, 6, null, 36),
                new TokenComponents(10, null, null, null, 2, null, 12), 128000),
            DateTimeOffset.FromUnixTimeSeconds(1700102002), SourceOffset: state.CompleteOffset);
        var proposed = state with { StateRevision = state.StateRevision + 1 };
        var conflict = database.CommitScan(new ScanTransactionRequest(
            new SourceStateExpectation(state.Identity.StableKey, true, state.Generation,
                state.Cursor, state.StateRevision - 1), proposed, new[] { staleEvent },
            Array.Empty<ParserErrorRecord>(), DateTimeOffset.UtcNow));
        Assert.True(!conflict.Committed);
        Assert.True(conflict.DiagnosticCodes.Contains("source_revision_conflict", StringComparer.Ordinal));
        Assert.Equal(before, database.GetSessionTotal("thread-s2-2-busy"));
        Assert.Equal(1L, database.GetSampleCount("thread-s2-2-busy"));
    }

    private static void CompleteEnvelopeDuplicates(string runRoot)
    {
        var directory = Path.Combine(runRoot, "s2-3-envelope");
        Directory.CreateDirectory(directory);
        var cases = new (string Name, string Line, string Error)[]
        {
            ("root-type", "{\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"thread_id\":\"dup\",\"info\":{\"total_token_usage\":{},\"last_token_usage\":{}}},\"type\":\"message\"}", "duplicate_root_property"),
            ("root-payload", "{\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"thread_id\":\"dup\",\"info\":{\"total_token_usage\":{},\"last_token_usage\":{}}},\"payload\":{\"type\":\"user_message\"}}", "duplicate_root_property"),
            ("payload-type", "{\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"type\":\"user_message\",\"thread_id\":\"dup\",\"info\":{\"total_token_usage\":{},\"last_token_usage\":{}}}}", "duplicate_payload_property"),
            ("approved", "{\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"thread_id\":\"dup\",\"info\":{\"total_token_usage\":{\"input_tokens\":1,\"input_tokens\":2},\"last_token_usage\":{\"input_tokens\":1}}}}", "duplicate_approved_property"),
            ("payload-array", "{\"type\":\"session_meta\",\"payload\":[{\"id\":\"must-not-route\"}]}", "record_structural_error"),
        };
        foreach (var item in cases)
        {
            var path = Path.Combine(directory, item.Name + ".jsonl");
            File.WriteAllText(path, item.Line + "\n" + TokenLine("thread-envelope-valid",
                1700103000, 1, 1, 1, 1) + "\n", new UTF8Encoding(false));
            var batch = new PrivacyJsonlReader().Read(path, 0);
            Assert.Equal(1, batch.Events.Count(item => item.TokenSnapshot is not null));
            Assert.True(batch.ErrorCodes.Contains(item.Error, StringComparer.Ordinal));
        }

        var payloadFirst = Path.Combine(directory, "payload-first.jsonl");
        File.WriteAllText(payloadFirst,
            "{\"payload\":{\"type\":\"token_count\",\"thread_id\":\"thread-envelope-payload-first\",\"info\":{\"total_token_usage\":{\"input_tokens\":2,\"output_tokens\":1},\"last_token_usage\":{\"input_tokens\":2,\"output_tokens\":1}}},\"type\":\"event_msg\",\"timestamp\":1700103001}\n",
            new UTF8Encoding(false));
        var payloadFirstBatch = new PrivacyJsonlReader().Read(payloadFirst, 0);
        Assert.Equal(1, payloadFirstBatch.Events.Count(item => item.TokenSnapshot is not null));
        Assert.Equal("thread-envelope-payload-first", payloadFirstBatch.Events.Single().ThreadId);
    }

    private static void FallbackDigestAndSymbolicSettings(string runRoot)
    {
        var directory = Path.Combine(runRoot, "s2-4-fallback");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "private-name.jsonl");
        File.WriteAllText(source,
            "{\"type\":\"session_meta\",\"payload\":{\"id\":\"thread-fallback\"}}\n",
            new UTF8Encoding(false));
        var provider = new FileIdentityProvider(forceDegraded: true);
        var identity = provider.Get(source, "thread-fallback");
        Assert.True(identity.IsDegraded);
        Assert.Equal(64, identity.FallbackDigest.Length);
        Assert.True(identity.StableKey.StartsWith("fallback-v2:", StringComparison.Ordinal));
        Assert.DoesNotContain(source, identity.FallbackDigest);
        Assert.DoesNotContain(source, identity.StableKey);

        using var database = new UsageDatabase(Path.Combine(directory, "usage.db"));
        var indexer = new RolloutIndexer(database, identityProvider: provider);
        indexer.ScanFile(source, "sessions/private-name.jsonl", "thread-fallback");
        var persisted = database.LoadSourceStates().Single();
        Assert.Equal(identity.FallbackDigest, persisted.Identity.FallbackDigest);
        Assert.True(database.ReadPrivacyTextValues().All(value =>
            !value.Contains(directory, StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(@"%LOCALAPPDATA%\CodexUsageHUD\usage.db", HudPresentation.SymbolicDatabasePath);
        Assert.True(!Path.IsPathRooted(HudPresentation.SymbolicDatabasePath));
        var windowSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "CodexUsageHud.App",
            "MainWindow.xaml.cs"));
        Assert.True(!windowSource.Contains("Database.DatabasePath", StringComparison.Ordinal));
    }

    private static void LegacyAbsoluteMigration(string runRoot)
    {
        var directory = Path.Combine(runRoot, "s2-4-legacy-migration");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "usage.db");
        var privatePath = Path.Combine(directory, "private", "rollout.jsonl");
        CreateLegacyV4Database(databasePath, privatePath);
        SourceFileState firstState;
        using (var database = new UsageDatabase(databasePath))
        {
            while (!database.RunAggregateRebuildBatch()) { }
            firstState = database.LoadSourceStates().Single();
            Assert.True(firstState.Identity.IsDegraded);
            Assert.Equal(64, firstState.Identity.FallbackDigest.Length);
            Assert.True(firstState.Identity.StableKey.StartsWith("fallback-v2:", StringComparison.Ordinal));
            Assert.Equal(1L, database.GetFingerprintCount());
            Assert.Equal(1L, database.GetSampleCount());
            Assert.Equal(12L, database.GetSessionTotal("thread-legacy-private").Total);
            Assert.True(database.ReadPrivacyTextValues().All(value =>
                !value.Contains(privatePath, StringComparison.OrdinalIgnoreCase)));
            Assert.Equal("logical_complete", database.ReadSchemaValue("private_source_identity_v2"));
            Assert.Equal("complete", database.ReadSchemaValue("private_source_scrub_v2"));
        }
        Assert.DoesNotContain(privatePath, Encoding.UTF8.GetString(File.ReadAllBytes(databasePath)));

        using var restarted = new UsageDatabase(databasePath);
        Assert.Equal(firstState, restarted.LoadSourceStates().Single());
        Assert.Equal(1L, restarted.GetFingerprintCount());
        Assert.Equal(1L, restarted.GetSampleCount());
        Assert.Equal(12L, restarted.GetSessionTotal("thread-legacy-private").Total);
    }

    private static void AggregateRebuildResumes(string runRoot)
    {
        var directory = Path.Combine(runRoot, "s2-5-rebuild-resume");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "usage.db");
        SeedPerformanceDatabase(databasePath, 4_501, 1,
            FloorToMinute(DateTimeOffset.UtcNow.AddDays(-1)));
        using (var first = new UsageDatabase(databasePath))
        {
            Assert.True(!first.IsAggregateRebuildComplete);
            Assert.True(!first.AcceptTokenSample("blocked-during-rebuild",
                new TokenUsageSnapshot(new TokenComponents(1, null, null, null, 1, null, 2),
                    new TokenComponents(1, null, null, null, 1, null, 2), 128000),
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, null));
            Assert.True(!first.RunAggregateRebuildBatch());
            Assert.Equal(2000, first.PerformanceMetrics.LastRebuildBatchRows);
        }
        using (var second = new UsageDatabase(databasePath))
        {
            Assert.True(!second.IsAggregateRebuildComplete);
            Assert.True(!second.RunAggregateRebuildBatch());
            Assert.Equal(2000, second.PerformanceMetrics.LastRebuildBatchRows);
        }
        using (var third = new UsageDatabase(databasePath))
        {
            Assert.True(third.RunAggregateRebuildBatch());
            Assert.Equal(501, third.PerformanceMetrics.LastRebuildBatchRows);
            Assert.Equal(4_501L, third.GetSampleCount());
            Assert.Equal(4_501L, third.GetFingerprintCount());
            Assert.Equal(9_002L, third.GetSessionTotal("perf-thread-000").Total);
        }
        using var restarted = new UsageDatabase(databasePath);
        Assert.True(restarted.IsAggregateRebuildComplete);
        Assert.Equal(9_002L, restarted.GetSessionTotal("perf-thread-000").Total);
    }

    private static void IncrementalSnapshotCycleAndFrontier(string runRoot)
    {
        using var database = NewDatabase(runRoot, "s2-5-incremental-cycle");
        var start = FloorToMinute(DateTimeOffset.UtcNow.AddHours(-2));
        var end = start.AddHours(1);
        for (var index = 0; index < 4; index++)
        {
            var snapshot = new TokenUsageSnapshot(
                new TokenComponents((index + 1) * 10, null, null, null, (index + 1) * 2, null,
                    (index + 1) * 12),
                new TokenComponents(10, null, null, null, 2, null, 12), 128000 + index);
            Assert.True(database.AcceptTokenSample("thread-cycle", snapshot,
                start.AddMinutes(index * 20), DateTimeOffset.UtcNow, "turn-cycle", null, null));
        }
        database.UpsertSession(new SessionMetadata("thread-cycle", "Cycle", null, null, null,
            null, null, null, DateTimeOffset.UtcNow, null), SessionStatus.Idle, "turn-cycle", 1, false);
        Assert.Equal(48L, database.GetSessionTotal("thread-cycle").Total);
        Assert.Equal(48L, database.LoadSessionTotals()["thread-cycle"].Total);
        Assert.Equal(48L, database.LoadTurnTotals()[("thread-cycle", "turn-cycle")].Total);

        var firstWatch = Stopwatch.StartNew();
        var first = database.GetCycleTotal(start, end);
        firstWatch.Stop();
        Assert.Equal(36L, first.Total);
        Assert.True(database.PerformanceMetrics.CycleBucketRowsRead <= 60);
        var second = database.GetCycleTotal(start, end);
        Assert.Equal(first, second);
        Assert.Equal(1L, database.PerformanceMetrics.CycleAggregateRowsRead);
        Assert.Equal(0L, database.PerformanceMetrics.RecurringTokenHistoryRowsRead);
        Assert.True(firstWatch.ElapsedMilliseconds < 500);

        var stale = new TokenUsageSnapshot(
            new TokenComponents(5, null, null, null, 1, null, 6),
            new TokenComponents(1, null, null, null, 1, null, 2), 196000);
        Assert.True(database.AcceptTokenSample("thread-cycle", stale, start.AddMinutes(50),
            DateTimeOffset.UtcNow, "turn-cycle", null, null));
        Assert.True(database.LoadRecentEvents().All(item => !item.Summary.Contains("token", StringComparison.OrdinalIgnoreCase)));
    }

    private static void FrameBuiltOffDispatcher(string runRoot)
    {
        var directory = Path.Combine(runRoot, "s2-5-off-dispatcher");
        var home = Path.Combine(directory, "codex-home");
        Directory.CreateDirectory(Path.Combine(home, "sessions"));
        Directory.CreateDirectory(Path.Combine(home, "archived_sessions"));
        using var engine = new UsageEngine(home, Path.Combine(directory, "usage.db"));
        engine.Cadence.MarkQuotaAttempt(DateTimeOffset.UtcNow);
        var callerThread = Environment.CurrentManagedThreadId;
        var frame = engine.RefreshAsync().GetAwaiter().GetResult();
        Assert.True(frame.BuildThreadId != callerThread);
        Assert.True(frame.BuildElapsedMilliseconds <= 250);
        var source = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "CodexUsageHud.Core", "UsageEngine.cs"));
        Assert.True(source.Contains("Task.Run", StringComparison.Ordinal));
        Assert.True(!source.Contains("Dispatcher", StringComparison.Ordinal));
    }

    private static PerformanceEvidence Correction03Performance(string runRoot)
    {
        var directory = Path.Combine(runRoot, "s2-5-performance-100k");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "usage.db");
        var cycleStart = FloorToMinute(DateTimeOffset.UtcNow.AddDays(-7));
        SeedPerformanceDatabase(databasePath, 100_000, 250, cycleStart);
        var rebuildTimes = new List<long>();
        using var database = new UsageDatabase(databasePath);
        for (var guard = 0; guard < 100 && !database.IsAggregateRebuildComplete; guard++)
        {
            database.RunAggregateRebuildBatch();
            var metrics = database.PerformanceMetrics;
            if (metrics.LastRebuildBatchRows > 0)
            {
                Assert.True(metrics.LastRebuildBatchRows <= 2000);
                rebuildTimes.Add(metrics.LastRebuildBatchMilliseconds);
            }
        }
        Assert.True(database.IsAggregateRebuildComplete);
        Assert.Equal(100_000L, database.GetSampleCount());
        Assert.Equal(100_000L, database.GetFingerprintCount());
        Assert.Equal(200_000L, database.LoadSessionTotals().Values.Sum(item => item.Total));
        var rebuildP95 = Percentile95(rebuildTimes);
        Assert.True(rebuildP95 <= 500);

        var sessions = database.LoadSessionAggregates(DateTimeOffset.UtcNow);
        Assert.Equal(250, sessions.Count);
        var snapshot = new HudSnapshot(new QuotaObservation(null, Array.Empty<QuotaBucket>(),
            QuotaSource.Unavailable, DateTimeOffset.UtcNow, false), sessions, null,
            DateTimeOffset.UtcNow, false, "performance", Array.Empty<string>());
        for (var warmup = 0; warmup < 5; warmup++) HudPresentation.BuildFrame(snapshot);
        var frameTimes = new List<long>();
        HudFrame? frame = null;
        for (var sample = 0; sample < 30; sample++)
        {
            var watch = Stopwatch.StartNew();
            frame = HudPresentation.BuildFrame(snapshot);
            watch.Stop();
            frameTimes.Add(watch.ElapsedMilliseconds);
        }
        var frameP95 = Percentile95(frameTimes);
        Assert.True(frameP95 <= 250);

        var viewModel = new MainViewModel();
        var applyTimes = new List<long>();
        for (var sample = 0; sample < 30; sample++)
        {
            var watch = Stopwatch.StartNew();
            viewModel.Apply(frame!);
            watch.Stop();
            applyTimes.Add(watch.ElapsedMilliseconds);
        }
        var applyP95 = Percentile95(applyTimes);
        Assert.True(applyP95 <= 50);

        var activation = Stopwatch.StartNew();
        var cycle = database.GetCycleTotal(cycleStart, cycleStart.AddDays(7));
        activation.Stop();
        var activationMetrics = database.PerformanceMetrics;
        Assert.Equal(200_000L, cycle.Total);
        Assert.True(activationMetrics.CycleBucketRowsRead + activationMetrics.CycleBoundarySampleRowsRead <= 10082);
        Assert.True(activation.ElapsedMilliseconds <= 500);
        database.GetCycleTotal(cycleStart, cycleStart.AddDays(7));
        Assert.Equal(1L, database.PerformanceMetrics.CycleAggregateRowsRead);
        Assert.Equal(0L, database.PerformanceMetrics.RecurringTokenHistoryRowsRead);

        return new PerformanceEvidence(100_000, 250, rebuildP95, frameP95, applyP95,
            activation.ElapsedMilliseconds, activationMetrics.CycleBucketRowsRead,
            activationMetrics.CycleBoundarySampleRowsRead, database.PerformanceMetrics.CycleAggregateRowsRead);
    }

    private static void PrintPerformance(PerformanceEvidence evidence)
    {
        Console.WriteLine($"PERF samples={evidence.Samples} sessions={evidence.Sessions} " +
            $"rebuild_p95_ms={evidence.RebuildP95Milliseconds} frame_p95_ms={evidence.FrameP95Milliseconds} " +
            $"dispatcher_apply_p95_ms={evidence.DispatcherApplyP95Milliseconds} " +
            $"cycle_activation_ms={evidence.CycleActivationMilliseconds} " +
            $"cycle_bucket_rows={evidence.CycleBucketRows} boundary_rows={evidence.BoundaryRows} " +
            $"existing_cycle_rows={evidence.ExistingCycleRows}");
    }

    private static void CrossProcessLockRecovery(string runRoot)
    {
        var directory = Path.Combine(runRoot, "s2-6-cross-process");
        Directory.CreateDirectory(directory);
        using var first = StartGateHelper(directory, 30_000);
        var firstLine = ReadLineWithin(first, 2000);
        Assert.True(firstLine.Contains("outcome=ACQUIRED", StringComparison.Ordinal));

        var contentionWatch = Stopwatch.StartNew();
        using (var second = StartGateHelper(directory, 0))
        {
            var secondLine = ReadLineWithin(second, 2000);
            Assert.True(second.WaitForExit(2000));
            contentionWatch.Stop();
            Assert.True(secondLine.Contains("outcome=CONTENDED", StringComparison.Ordinal));
            Assert.Equal(0, second.ExitCode);
        }
        Assert.True(contentionWatch.Elapsed < TimeSpan.FromSeconds(2));
        Assert.True(!File.Exists(Path.Combine(directory, "usage.db")));
        Assert.True(!File.Exists(Path.Combine(directory, "hud.log")));

        first.Kill(entireProcessTree: true);
        Assert.True(first.WaitForExit(2000));
        var recoveryWatch = Stopwatch.StartNew();
        using var recovered = StartGateHelper(directory, 0);
        var recoveredLine = ReadLineWithin(recovered, 2000);
        Assert.True(recovered.WaitForExit(2000));
        recoveryWatch.Stop();
        Assert.True(recoveredLine.Contains("outcome=ACQUIRED", StringComparison.Ordinal));
        Assert.True(recoveryWatch.Elapsed < TimeSpan.FromSeconds(2));
    }

    private static void GateErrorNoWriters(string runRoot)
    {
        var directory = Path.Combine(runRoot, "s2-6-gate-error");
        Directory.CreateDirectory(directory);
        var invalid = Path.Combine(directory, "not-a-directory");
        File.WriteAllText(invalid, "safe", Encoding.ASCII);
        var outcome = SingleInstanceGate.TryAcquire(invalid, out var gate, out var code);
        Assert.Equal(InstanceGateOutcome.Error, outcome);
        Assert.True(gate is null);
        Assert.True(code.StartsWith("instance_lock_", StringComparison.Ordinal));
        Assert.True(!File.Exists(Path.Combine(invalid, "usage.db")));
        Assert.True(!File.Exists(Path.Combine(invalid, "hud.log")));
    }

    private static void RecentUsageSemantics(string runRoot)
    {
        var directory = Path.Combine(runRoot, "s2-7-recent");
        Directory.CreateDirectory(directory);
        var tokenOnly = Path.Combine(directory, "token-only.jsonl");
        var reliable = Path.Combine(directory, "reliable.jsonl");
        File.WriteAllText(tokenOnly,
            "{\"type\":\"session_meta\",\"payload\":{\"id\":\"thread-recent-degraded\"}}\n" +
            TokenLine("thread-recent-degraded", 1700107000, 10, 2, 10, 2) + "\n",
            new UTF8Encoding(false));
        File.WriteAllText(reliable,
            "{\"type\":\"session_meta\",\"payload\":{\"id\":\"thread-recent-turn\"}}\n" +
            "{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_started\",\"thread_id\":\"thread-recent-turn\",\"timestamp\":1700107100}}\n" +
            TokenLine("thread-recent-turn", 1700107101, 10, 2, 10, 2) + "\n" +
            TokenLine("thread-recent-turn", 1700107102, 20, 4, 10, 2) + "\n",
            new UTF8Encoding(false));
        var databasePath = Path.Combine(directory, "usage.db");
        using (var database = new UsageDatabase(databasePath))
        {
            var indexer = new RolloutIndexer(database);
            indexer.ScanFile(tokenOnly, "sessions/token-only.jsonl", "thread-recent-degraded");
            indexer.ScanFile(reliable, "sessions/reliable.jsonl", "thread-recent-turn");
            var rows = indexer.LoadAggregates(DateTimeOffset.UtcNow);
            var degraded = rows.Single(item => item.Metadata.ThreadId == "thread-recent-degraded");
            Assert.Equal(RecentUsageKind.LatestEventDegraded, degraded.RecentKind);
            Assert.Equal("最近事件（降级）", degraded.RecentUsageLabel);
            Assert.Equal(12L, degraded.RecentUsage.Total);
            var turn = rows.Single(item => item.Metadata.ThreadId == "thread-recent-turn");
            Assert.Equal(RecentUsageKind.LatestTurn, turn.RecentKind);
            Assert.Equal("最近一轮", turn.RecentUsageLabel);
            Assert.Equal(24L, turn.RecentUsage.Total);
            var before = turn;
            var copy = Path.Combine(directory, "copy.jsonl");
            File.Copy(reliable, copy, true);
            indexer.ScanFile(copy, "archived_sessions/copy.jsonl", "thread-recent-turn");
            Assert.Equal(before.RecentUsage,
                indexer.LoadAggregates(DateTimeOffset.UtcNow).Single(item =>
                    item.Metadata.ThreadId == "thread-recent-turn").RecentUsage);
        }
        using var restarted = new UsageDatabase(databasePath);
        var afterRestart = restarted.LoadSessionAggregates(DateTimeOffset.UtcNow);
        Assert.Equal(RecentUsageKind.LatestEventDegraded,
            afterRestart.Single(item => item.Metadata.ThreadId == "thread-recent-degraded").RecentKind);
        Assert.Equal(24L, afterRestart.Single(item =>
            item.Metadata.ThreadId == "thread-recent-turn").RecentUsage.Total);
    }

    private static void UnknownDrainBoundedResume(string runRoot)
    {
        var directory = Path.Combine(runRoot, "s2-8-drain-resume");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "huge.jsonl");
        var unknown = "{\"type\":\"message\",\"payload\":{\"body\":\"" +
                      new string('x', 256_000) + Sentinel;
        File.WriteAllText(source, unknown, new UTF8Encoding(false));
        var databasePath = Path.Combine(directory, "usage.db");
        long previousDrain = 0;
        using (var database = new UsageDatabase(databasePath))
        {
            var indexer = new RolloutIndexer(database);
            for (var pass = 0; pass < 5; pass++)
            {
                var result = indexer.ScanFile(source, "sessions/huge.jsonl", "thread-drain", default, 4096);
                Assert.True(result.BytesRead <= 4096);
                var state = database.LoadSourceStates().Single();
                Assert.True(state.Cursor.DrainMode);
                Assert.Equal(0L, state.Cursor.CompleteOffset);
                Assert.True(state.Cursor.DrainOffset > previousDrain);
                previousDrain = state.Cursor.DrainOffset;
            }
        }
        using (var restarted = new UsageDatabase(databasePath))
        {
            var state = restarted.LoadSourceStates().Single();
            Assert.Equal(previousDrain, state.Cursor.DrainOffset);
            var indexer = new RolloutIndexer(restarted);
            var next = indexer.ScanFile(source, "sessions/huge.jsonl", "thread-drain", default, 4096);
            Assert.True(next.BytesRead <= 4096);
            Assert.True(restarted.LoadSourceStates().Single().Cursor.DrainOffset > previousDrain);
        }

        File.AppendAllText(source, new string('y', 8192), new UTF8Encoding(false));
        using (var appended = new UsageDatabase(databasePath))
        {
            var indexer = new RolloutIndexer(appended);
            var guard = 0;
            while (appended.LoadSourceStates().Single().Cursor.DrainOffset < new FileInfo(source).Length && guard++ < 200)
            {
                var pass = indexer.ScanFile(source, "sessions/huge.jsonl", "thread-drain", default, 4096);
                Assert.True(pass.BytesRead <= 4096);
            }
        }

        File.AppendAllText(source, "\"}}\n" +
            "{\"type\":\"session_meta\",\"payload\":{\"id\":\"thread-drain\"}}\n" +
            TokenLine("thread-drain", 1700108000, 5, 2, 5, 2) + "\n", new UTF8Encoding(false));
        using var final = new UsageDatabase(databasePath);
        var finalIndexer = new RolloutIndexer(final);
        for (var pass = 0; pass < 200 && final.GetSampleCount("thread-drain") == 0; pass++)
        {
            var result = finalIndexer.ScanFile(source, "sessions/huge.jsonl", "thread-drain", default, 4096);
            Assert.True(result.BytesRead <= 4096);
        }
        Assert.Equal(1L, final.GetSampleCount("thread-drain"));
        Assert.Equal(new FileInfo(source).Length, final.LoadSourceStates().Single().CompleteOffset);
        Assert.True(!final.ContainsPrivacySentinel(Sentinel));
    }

    private static void UnknownDrainRewriteRotation(string runRoot)
    {
        var directory = Path.Combine(runRoot, "s2-8-drain-rotation");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "active.jsonl");
        var moved = Path.Combine(directory, "moved.jsonl");
        var copied = Path.Combine(directory, "copied.jsonl");
        var unknown = "{\"type\":\"message\",\"payload\":{\"body\":\"" +
                      new string('q', 40_000) + Sentinel;
        File.WriteAllText(source, unknown, new UTF8Encoding(false));
        using var database = NewDatabase(runRoot, "s2-8-drain-rotation-db");
        var indexer = new RolloutIndexer(database);
        indexer.ScanFile(source, "sessions/active.jsonl", "thread-drain-rotation", default, 2048);
        var beforeMove = database.LoadSourceStates().Single();
        Assert.True(beforeMove.Cursor.DrainMode);
        File.Move(source, moved);
        indexer.ScanFile(moved, "archived_sessions/moved.jsonl", "thread-drain-rotation", default, 2048);
        var afterMove = database.LoadSourceStates().Single();
        Assert.Equal(beforeMove.Identity.StableKey, afterMove.Identity.StableKey);
        Assert.True(afterMove.Cursor.DrainOffset > beforeMove.Cursor.DrainOffset);

        File.Copy(moved, copied, true);
        indexer.ScanFile(copied, "archived_sessions/copied.jsonl", "thread-drain-rotation", default, 2048);
        Assert.Equal(2, database.LoadSourceStates().Count);

        var valid = "{\"type\":\"session_meta\",\"payload\":{\"id\":\"thread-drain-rotation\"}}\n" +
                    TokenLine("thread-drain-rotation", 1700108100, 6, 2, 6, 2) + "\n";
        File.WriteAllText(moved, valid, new UTF8Encoding(false));
        File.SetLastWriteTimeUtc(moved, DateTime.UtcNow.AddSeconds(3));
        var truncated = indexer.ScanFile(moved, "archived_sessions/moved.jsonl", "thread-drain-rotation",
            default, 4096);
        Assert.True(truncated.Generation > afterMove.Generation);
        Assert.Equal(1, truncated.AcceptedSamples);
        Assert.True(!database.ContainsPrivacySentinel(Sentinel));

        var length = Encoding.UTF8.GetByteCount(valid);
        var replacement = "{\"type\":\"message\",\"payload\":{\"body\":\"" +
                          new string('z', Math.Max(0, length - 49));
        replacement = replacement.Length > length ? replacement[..length] : replacement.PadRight(length, 'z');
        File.WriteAllText(moved, replacement, new UTF8Encoding(false));
        File.SetLastWriteTimeUtc(moved, DateTime.UtcNow.AddSeconds(6));
        var rewritten = indexer.ScanFile(moved, "archived_sessions/moved.jsonl", "thread-drain-rotation",
            default, 1024);
        Assert.True(rewritten.Generation > truncated.Generation);
    }

    private static void Correction03StaticPrivacy(string runRoot)
    {
        var projectRoot = ProjectRoot();
        var privacyReader = File.ReadAllText(Path.Combine(projectRoot, "src", "CodexUsageHud.Core",
            "PrivacyJsonlReader.cs"));
        Assert.True(!privacyReader.Contains("JsonDocument", StringComparison.Ordinal));
        Assert.True(!privacyReader.Contains("GetRawText", StringComparison.Ordinal));
        Assert.True(!privacyReader.Contains("Encoding.UTF8.GetString", StringComparison.Ordinal));
        var databaseSource = File.ReadAllText(Path.Combine(projectRoot, "src", "CodexUsageHud.Core",
            "UsageDatabase.cs"));
        Assert.True(!databaseSource.Contains("SUM(", StringComparison.OrdinalIgnoreCase));
        Assert.True(!databaseSource.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        var appSource = File.ReadAllText(Path.Combine(projectRoot, "src", "CodexUsageHud.App", "App.xaml.cs"));
        Assert.True(appSource.IndexOf("TryAcquire(localData", StringComparison.Ordinal) <
                    appSource.IndexOf("new UsageEngine", StringComparison.Ordinal));
        Assert.True(!appSource.Contains("Fast", StringComparison.OrdinalIgnoreCase));
        Assert.True(!appSource.Contains("priority", StringComparison.OrdinalIgnoreCase));
        Assert.True(!appSource.Contains("ultrafast", StringComparison.OrdinalIgnoreCase));
        var publishSource = File.ReadAllText(Path.Combine(projectRoot, "scripts", "publish.ps1"));
        Assert.True(publishSource.Contains("--data-dir", StringComparison.Ordinal));
        Assert.True(publishSource.Contains("CODEX_HOME", StringComparison.Ordinal));
        Assert.True(publishSource.Contains("CodexUsageHUD-win-x64.zip.sha256", StringComparison.Ordinal));
        Assert.True(!publishSource.Contains("Add-Content -LiteralPath $manifest", StringComparison.Ordinal));
        var xaml = File.ReadAllText(Path.Combine(projectRoot, "src", "CodexUsageHud.App", "MainWindow.xaml"));
        foreach (var label in new[]
                 {
                     "原始输入 (raw)", "缓存输入 (raw)", "输出 (raw)", "推理 (raw)", "合计 (raw)",
                     "会话累计",
                 })
            Assert.True(xaml.Contains(label, StringComparison.Ordinal));
        foreach (var binding in new[]
                 {
                     "RecentUsage.RawInput", "RecentUsage.CachedInput", "RecentUsage.Output",
                     "RecentUsage.Reasoning", "RecentUsage.Total", "SessionTotal.RawInput",
                     "SessionTotal.CachedInput", "SessionTotal.Output", "SessionTotal.Reasoning",
                     "SessionTotal.Total",
                 })
            Assert.True(xaml.Contains(binding, StringComparison.Ordinal));

        var directory = Path.Combine(runRoot, "static-privacy-db");
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "source.jsonl");
        File.WriteAllText(source, $"{{\"type\":\"message\",\"payload\":{{\"body\":\"{Sentinel}\"}}}}\n",
            new UTF8Encoding(false));
        using var database = new UsageDatabase(Path.Combine(directory, "usage.db"));
        new RolloutIndexer(database).ScanFile(source, "sessions/source.jsonl", "thread-static-privacy");
        Assert.True(!database.ContainsPrivacySentinel(Sentinel));
        Assert.True(database.ReadPrivacyTextValues().All(value => !Path.IsPathRooted(value)));
        Assert.Equal(0L, database.PerformanceMetrics.RecurringTokenHistoryRowsRead);
    }

    private static void SemanticAliasDuplicates(string runRoot)
    {
        var directory = Path.Combine(runRoot, "r3-01-semantic-aliases");
        Directory.CreateDirectory(directory);
        var cases = new[]
        {
            "alias-service-tier.jsonl",
            "alias-turn-key.jsonl",
            "alias-context-window.jsonl",
        };
        foreach (var file in cases)
        {
            var path = Fixture(Path.Combine("correction-04", file));
            var batch = new PrivacyJsonlReader().Read(path, 0);
            Assert.Equal(1, batch.Events.Count(item => item.TokenSnapshot is not null));
            Assert.True(batch.ErrorCodes.Contains("duplicate_approved_property", StringComparer.Ordinal));
        }
    }

    private static void SourceRepair05(string runRoot)
    {
        SourceRepair05SemanticAliases(runRoot);
        SourceRepair05TokenSchemaAdmission(runRoot);
    }

    private static void SourceRepair05SemanticAliases(string runRoot)
    {
        var directory = Path.Combine(runRoot, "sr4-01-semantic-aliases");
        Directory.CreateDirectory(directory);
        var source = Fixture(Path.Combine("correction-05", "semantic-aliases.jsonl"));
        var batch = new PrivacyJsonlReader().Read(source, 0);

        Assert.Equal(1, batch.Events.Count);
        Assert.Equal(1, batch.Events.Count(item => item.TokenSnapshot is not null));
        Assert.Equal(6, batch.LinesSkipped);
        Assert.True(batch.ErrorCodes.Contains("duplicate_approved_property", StringComparer.Ordinal));
        Assert.True(batch.Events.All(item => item.ErrorCode is null));
        Assert.Equal(new FileInfo(source).Length, batch.Cursor.CompleteOffset);

        var databasePath = Path.Combine(directory, "usage.db");
        SessionMetadata beforeRestart;
        using (var database = new UsageDatabase(databasePath))
        {
            var result = new RolloutIndexer(database).ScanFile(source,
                "sessions/correction-05-semantic-aliases.jsonl", "alias-thread");
            Assert.Equal(1, result.AcceptedSamples);
            Assert.Equal(0, result.DuplicateSamples);
            Assert.True(result.ErrorCodes.Contains("duplicate_approved_property", StringComparer.Ordinal));
            Assert.Equal(1L, database.GetSampleCount("alias-thread"));
            Assert.Equal(1L, database.GetFingerprintCount("alias-thread"));
            Assert.Equal(0L, database.GetDuplicateObservationCount());
            Assert.Equal(0L, database.GetStructuralEventCount());
            Assert.Equal(new CanonicalTokenUsage(10, 10, 0, 0, 2, 0, 12, 12),
                database.GetSessionTotal("alias-thread"));

            var metadata = database.LoadSessions().Single();
            Assert.True(metadata.Model is null);
            Assert.True(metadata.ServiceTier is null);
            Assert.Equal(ServiceTierProvenance.Unavailable, metadata.ServiceTierSource);
            Assert.True(metadata.CurrentTurnKey is null);
            Assert.Equal(0L, metadata.TurnSequence);
            Assert.True(!metadata.TurnOpen);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700300007), metadata.LastActivityUtc);
            beforeRestart = metadata;
        }

        using var restarted = new UsageDatabase(databasePath);
        var replay = new RolloutIndexer(restarted).ScanFile(source,
            "sessions/correction-05-semantic-aliases.jsonl", "alias-thread");
        Assert.Equal(0, replay.AcceptedSamples);
        Assert.Equal(0, replay.DuplicateSamples);
        Assert.Equal(beforeRestart, restarted.LoadSessions().Single());
        Assert.Equal(1L, restarted.GetSampleCount("alias-thread"));
        Assert.Equal(1L, restarted.GetFingerprintCount("alias-thread"));
        Assert.Equal(0L, restarted.GetDuplicateObservationCount());
    }

    private static void SourceRepair05TokenSchemaAdmission(string runRoot)
    {
        var directory = Path.Combine(runRoot, "sr4-02-token-schema");
        Directory.CreateDirectory(directory);
        var source = Fixture(Path.Combine("correction-05", "token-schema-degraded.jsonl"));
        var batch = new PrivacyJsonlReader().Read(source, 0);

        Assert.Equal(1, batch.Events.Count);
        Assert.Equal(1, batch.Events.Count(item => item.TokenSnapshot is not null));
        Assert.Equal(6, batch.LinesSkipped);
        Assert.True(batch.ErrorCodes.Contains("token_schema_degraded", StringComparer.Ordinal));
        Assert.True(batch.Events.All(item => item.ErrorCode is null && item.TokenSnapshot is not null));
        Assert.Equal(new FileInfo(source).Length, batch.Cursor.CompleteOffset);

        var databasePath = Path.Combine(directory, "usage.db");
        CanonicalTokenUsage beforeTotal;
        SessionMetadata beforeRestart;
        using (var database = new UsageDatabase(databasePath))
        {
            var result = new RolloutIndexer(database).ScanFile(source,
                "sessions/correction-05-token-schema.jsonl", "snapshot-thread");
            Assert.Equal(1, result.AcceptedSamples);
            Assert.Equal(0, result.DuplicateSamples);
            Assert.True(result.ErrorCodes.Contains("token_schema_degraded", StringComparer.Ordinal));
            Assert.Equal(1L, database.GetSampleCount("snapshot-thread"));
            Assert.Equal(1L, database.GetFingerprintCount("snapshot-thread"));
            Assert.Equal(0L, database.GetDuplicateObservationCount());
            Assert.Equal(0L, database.GetStructuralEventCount());
            Assert.Equal(0, database.LoadTurnTotals().Count);

            beforeTotal = database.GetSessionTotal("snapshot-thread");
            Assert.Equal(new CanonicalTokenUsage(10, 10, 0, 0, 2, 0, 12, 12), beforeTotal);
            var metadata = database.LoadSessions().Single();
            Assert.True(metadata.Model is null);
            Assert.True(metadata.ServiceTier is null);
            Assert.Equal(ServiceTierProvenance.Unavailable, metadata.ServiceTierSource);
            Assert.True(metadata.CurrentTurnKey is null);
            Assert.Equal(0L, metadata.TurnSequence);
            Assert.True(!metadata.TurnOpen);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700300000), metadata.LastActivityUtc);
            beforeRestart = metadata;

            using var connection = new SqliteConnection($"Data Source={database.DatabasePath};Mode=ReadOnly");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT relative_path, offset, error_code, observed_at_utc FROM parser_errors WHERE error_code = 'token_schema_degraded';";
            using var errors = command.ExecuteReader();
            var errorCount = 0;
            while (errors.Read())
            {
                errorCount++;
                Assert.Equal("sessions/correction-05-token-schema.jsonl", errors.GetString(0));
                Assert.True(errors.GetInt64(1) >= 0);
                Assert.Equal("token_schema_degraded", errors.GetString(2));
                Assert.True(DateTimeOffset.TryParse(errors.GetString(3), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out _));
            }
            Assert.Equal(1, errorCount);
        }

        using var restarted = new UsageDatabase(databasePath);
        var replay = new RolloutIndexer(restarted).ScanFile(source,
            "sessions/correction-05-token-schema.jsonl", "snapshot-thread");
        Assert.Equal(0, replay.AcceptedSamples);
        Assert.Equal(0, replay.DuplicateSamples);
        Assert.Equal(beforeTotal, restarted.GetSessionTotal("snapshot-thread"));
        Assert.Equal(beforeRestart, restarted.LoadSessions().Single());
        Assert.Equal(1L, restarted.GetSampleCount("snapshot-thread"));
        Assert.Equal(1L, restarted.GetFingerprintCount("snapshot-thread"));
        Assert.Equal(0L, restarted.GetDuplicateObservationCount());
    }

    private static void SourceRepair06ContextWindowAdmission(string runRoot)
    {
        var directory = Path.Combine(runRoot, "sr5-01-context-window-schema");
        Directory.CreateDirectory(directory);
        var source = Fixture(Path.Combine("correction-06", "context-window-degraded.jsonl"));
        var batch = new PrivacyJsonlReader().Read(source, 0);

        Assert.Equal(2, batch.Events.Count);
        Assert.Equal(2, batch.Events.Count(item => item.TokenSnapshot is not null));
        Assert.Equal(14, batch.LinesSkipped);
        Assert.True(batch.ErrorCodes.Contains("token_schema_degraded", StringComparer.Ordinal));
        Assert.True(batch.Events.All(item => item.ErrorCode is null && item.TokenSnapshot is not null));
        Assert.SequenceEqual(new long?[] { 128000, 256000 },
            batch.Events.Select(item => item.TokenSnapshot!.ContextWindow));
        Assert.Equal(new FileInfo(source).Length, batch.Cursor.CompleteOffset);

        var databasePath = Path.Combine(directory, "usage.db");
        CanonicalTokenUsage beforeTotal;
        SessionMetadata beforeRestart;
        using (var database = new UsageDatabase(databasePath))
        {
            var result = new RolloutIndexer(database).ScanFile(source,
                "sessions/correction-06-context-window.jsonl", "context-thread");
            Assert.Equal(2, result.AcceptedSamples);
            Assert.Equal(0, result.DuplicateSamples);
            Assert.True(result.ErrorCodes.Contains("token_schema_degraded", StringComparer.Ordinal));
            Assert.Equal(2L, database.GetSampleCount("context-thread"));
            Assert.Equal(2L, database.GetFingerprintCount("context-thread"));
            Assert.Equal(0L, database.GetDuplicateObservationCount());
            Assert.Equal(0L, database.GetStructuralEventCount());
            Assert.Equal(0, database.LoadTurnTotals().Count);

            beforeTotal = database.GetSessionTotal("context-thread");
            Assert.Equal(new CanonicalTokenUsage(15, 15, 0, 0, 3, 0, 18, 18), beforeTotal);
            var metadata = database.LoadSessions().Single();
            Assert.True(metadata.Model is null);
            Assert.True(metadata.ServiceTier is null);
            Assert.Equal(ServiceTierProvenance.Unavailable, metadata.ServiceTierSource);
            Assert.True(metadata.CurrentTurnKey is null);
            Assert.Equal(0L, metadata.TurnSequence);
            Assert.True(!metadata.TurnOpen);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700400002), metadata.LastActivityUtc);
            beforeRestart = metadata;

            using var connection = new SqliteConnection($"Data Source={database.DatabasePath};Mode=ReadOnly");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM parser_errors WHERE error_code = 'token_schema_degraded';";
            Assert.Equal(1L, (long)(command.ExecuteScalar() ?? 0L));
        }

        using var restarted = new UsageDatabase(databasePath);
        var replay = new RolloutIndexer(restarted).ScanFile(source,
            "sessions/correction-06-context-window.jsonl", "context-thread");
        Assert.Equal(0, replay.AcceptedSamples);
        Assert.Equal(0, replay.DuplicateSamples);
        Assert.Equal(beforeTotal, restarted.GetSessionTotal("context-thread"));
        Assert.Equal(beforeRestart, restarted.LoadSessions().Single());
        Assert.Equal(2L, restarted.GetSampleCount("context-thread"));
        Assert.Equal(2L, restarted.GetFingerprintCount("context-thread"));
        Assert.Equal(0L, restarted.GetDuplicateObservationCount());
    }

    private static async Task AppServerBoundedStreams(string runRoot)
    {
        const int directLimit = 1024;
        using (var reader = new BoundedLineReader(new StringReader(
                   new string('x', directLimit + 17) + "\n{\"id\":2}\n"), directLimit))
        {
            var oversized = await reader.ReadLineAsync();
            Assert.True(oversized.IsTooLong && !oversized.IsEof && oversized.Line is null);
            Assert.True(reader.MaxRetainedCharsObserved <= directLimit);
            var valid = await reader.ReadLineAsync();
            Assert.Equal("{\"id\":2}", valid.Line);
            Assert.True(!valid.IsTooLong && !valid.IsEof);
            Assert.True((await reader.ReadLineAsync()).IsEof);
        }

        using (var reader = new BoundedLineReader(new StringReader(new string('y', directLimit + 17)), directLimit))
        {
            var unterminated = await reader.ReadLineAsync();
            Assert.True(unterminated.IsTooLong && !unterminated.IsEof && unterminated.Line is null);
            Assert.True(reader.MaxRetainedCharsObserved <= directLimit);
            Assert.True((await reader.ReadLineAsync()).IsEof);
        }

        var directory = Path.Combine(runRoot, "sr5-02-app-server-streams");
        Directory.CreateDirectory(directory);
        var successPidPath = Path.Combine(directory, "success.pid");
        var successWrapper = CreateAppServerHelperWrapper(directory, "bounded-success", successPidPath);
        var successClient = new AppServerClient("test", TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2));
        var success = await successClient.ReadRateLimitsAsync(successWrapper);
        Assert.Equal(QuotaSource.OfficialAppServer, success.Observation.Source);
        Assert.NotNull(success.Observation.Primary);
        Assert.Equal(12d, success.Observation.Primary!.UsedPercent);
        Assert.True(!success.TierOverrideRequested);
        AssertProcessExited(ReadPidWithin(successPidPath));

        var timeoutPidPath = Path.Combine(directory, "timeout.pid");
        var timeoutWrapper = CreateAppServerHelperWrapper(directory, "unterminated-stdout", timeoutPidPath);
        var timeoutClient = new AppServerClient("test", TimeSpan.FromMilliseconds(350),
            TimeSpan.FromMilliseconds(350), TimeSpan.FromSeconds(2));
        var stopwatch = Stopwatch.StartNew();
        var unavailable = await timeoutClient.ReadRateLimitsAsync(timeoutWrapper);
        stopwatch.Stop();
        Assert.Equal(QuotaSource.Unavailable, unavailable.Observation.Source);
        Assert.True(unavailable.Observation.Primary is null);
        Assert.Equal("app_server_timeout", unavailable.ErrorCode);
        Assert.True(!unavailable.TierOverrideRequested);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
        AssertProcessExited(ReadPidWithin(timeoutPidPath));

        var exitPidPath = Path.Combine(directory, "initialize-exit.pid");
        var exitWrapper = CreateAppServerHelperWrapper(directory, "initialize-exit", exitPidPath);
        var exitClient = new AppServerClient("test", TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2));
        var exited = await exitClient.ReadRateLimitsAsync(exitWrapper);
        Assert.Equal(QuotaSource.Unavailable, exited.Observation.Source);
        Assert.Equal("app_server_initialize_exit", exited.ErrorCode);
        AssertProcessExited(ReadPidWithin(exitPidPath));
    }

    private static void PrivateScrubHardDeathMatrix(string runRoot)
    {
        foreach (var point in new[]
                 {
                     "after-logical-pending-commit",
                     "after-vacuum-before-complete-marker",
                     "after-complete-marker-commit",
                 })
        {
            var directory = Path.Combine(runRoot, "r3-02-hard-death", point);
            Directory.CreateDirectory(directory);
            var databasePath = Path.Combine(directory, "usage.db");
            var privateSentinel = $@"C:\PRIVATE_PATH_SENTINEL_C04\{point}\rollout.jsonl";
            CreateLegacyV4Database(databasePath, privateSentinel);
            using (var helper = StartMigrationCrashHelper(databasePath, point))
            {
                Assert.True(helper.WaitForExit(10_000));
                Assert.True(helper.ExitCode != 0);
            }

            SourceFileState resumedState;
            using (var resumed = new UsageDatabase(databasePath))
            {
                while (!resumed.RunAggregateRebuildBatch()) { }
                Assert.Equal("logical_complete", resumed.ReadSchemaValue("private_source_identity_v2"));
                Assert.Equal("complete", resumed.ReadSchemaValue("private_source_scrub_v2"));
                Assert.Equal(1L, resumed.GetSampleCount());
                Assert.Equal(1L, resumed.GetFingerprintCount());
                Assert.Equal(12L, resumed.GetSessionTotal("thread-legacy-private").Total);
                resumedState = resumed.LoadSourceStates().Single();
            }
            using (var secondRestart = new UsageDatabase(databasePath))
            {
                Assert.Equal(resumedState, secondRestart.LoadSourceStates().Single());
                Assert.Equal(12L, secondRestart.GetSessionTotal("thread-legacy-private").Total);
                Assert.Equal("complete", secondRestart.ReadSchemaValue("private_source_scrub_v2"));
            }
            AssertFileSetDoesNotContain(databasePath, privateSentinel);
        }
    }

    private static void PrivateScrubMarkerStateMatrix(string runRoot)
    {
        var directory = Path.Combine(runRoot, "r3-02-marker-matrix");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "v5-missing-scrub.db");
        using (var initial = new UsageDatabase(databasePath))
        {
            Assert.True(initial.AcceptTokenSample("marker-thread",
                new TokenUsageSnapshot(new TokenComponents(4, null, null, null, 1, null, 5),
                    new TokenComponents(4, null, null, null, 1, null, 5), 128000),
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, null));
        }
        const string freePageSentinel = "PRIVATE_PATH_SENTINEL_V5_MISSING_SCRUB";
        using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO ui_settings(key, value) VALUES ('private-test', $sentinel);
                DELETE FROM ui_settings WHERE key = 'private-test';
                INSERT INTO schema_info(key, value) VALUES ('private_source_identity_v2', '1')
                    ON CONFLICT(key) DO UPDATE SET value = '1';
                DELETE FROM schema_info WHERE key = 'private_source_scrub_v2';
                PRAGMA wal_checkpoint(TRUNCATE);
                """;
            command.Parameters.AddWithValue("$sentinel", freePageSentinel);
            command.ExecuteNonQuery();
        }
        using (var upgraded = new UsageDatabase(databasePath))
        {
            Assert.Equal("logical_complete", upgraded.ReadSchemaValue("private_source_identity_v2"));
            Assert.Equal("complete", upgraded.ReadSchemaValue("private_source_scrub_v2"));
            Assert.Equal(5L, upgraded.GetSessionTotal("marker-thread").Total);
        }
        AssertFileSetDoesNotContain(databasePath, freePageSentinel);

        foreach (var state in new[]
                 {
                     (Identity: (string?)null, Scrub: (string?)null),
                     (Identity: (string?)"1", Scrub: (string?)"pending"),
                     (Identity: (string?)"logical_complete", Scrub: (string?)"pending"),
                 })
        {
            var path = Path.Combine(directory, $"accepted-{state.Identity ?? "missing"}-{state.Scrub ?? "missing"}.db");
            using (var created = new UsageDatabase(path)) { }
            SetPrivateMarkers(path, state.Identity, state.Scrub);
            using var reopened = new UsageDatabase(path);
            Assert.Equal("logical_complete", reopened.ReadSchemaValue("private_source_identity_v2"));
            Assert.Equal("complete", reopened.ReadSchemaValue("private_source_scrub_v2"));
        }

        foreach (var invalid in new[]
                 {
                     (Identity: (string?)"unknown", Scrub: (string?)"pending"),
                     (Identity: (string?)"logical_complete", Scrub: (string?)"unknown"),
                 })
        {
            var path = Path.Combine(directory, $"invalid-{Guid.NewGuid():N}.db");
            using (var created = new UsageDatabase(path)) { }
            SetPrivateMarkers(path, invalid.Identity, invalid.Scrub);
            try
            {
                _ = new UsageDatabase(path);
                throw new InvalidOperationException("expected_private_scrub_state_failure");
            }
            catch (InvalidOperationException exception)
            {
                Assert.Equal("PRIVATE_SOURCE_SCRUB_STATE_INVALID", exception.Message);
            }
        }
    }

    private static void DuplicateMetadataEnrichment(string runRoot)
    {
        var directory = Path.Combine(runRoot, "r3-03-legacy-enrichment");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "usage.db");
        var source = Fixture(Path.Combine("correction-04", "legacy-enrichment.jsonl"));

        using (var initial = new UsageDatabase(databasePath))
        {
            var scan = ScanFileToEnd(new RolloutIndexer(initial), source,
                "sessions/r3-enrich.jsonl", "thread-r3-enrich");
            Assert.Equal(1, scan.AcceptedSamples);
            Assert.Equal(12L, initial.GetSessionTotal("thread-r3-enrich").Total);
        }
        DegradeTurnAndSourceMarkers(databasePath);

        string invariantBefore;
        using (var migrated = new UsageDatabase(databasePath))
        {
            while (!migrated.RunAggregateRebuildBatch()) { }
            Assert.Equal(12L, migrated.GetSessionTotal("thread-r3-enrich").Total);
            Assert.Equal(0, migrated.LoadTurnTotals().Count);
            invariantBefore = NonTurnInvariantSnapshot(databasePath, "thread-r3-enrich");
            var scan = ScanFileToEnd(new RolloutIndexer(migrated), source,
                "sessions/r3-enrich.jsonl", "thread-r3-enrich");
            Assert.Equal(0, scan.AcceptedSamples);
            Assert.Equal(1, scan.DuplicateSamples);
            Assert.True(scan.ErrorCodes.Contains("duplicate_metadata_recovered", StringComparer.Ordinal));
            Assert.Equal(1L, migrated.GetSampleCount("thread-r3-enrich"));
            Assert.Equal(1L, migrated.GetFingerprintCount("thread-r3-enrich"));
            Assert.Equal(12L, migrated.GetSessionTotal("thread-r3-enrich").Total);
            Assert.Equal(12L, migrated.LoadTurnTotals()[("thread-r3-enrich", "turn-recovered")].Total);
            Assert.Equal(invariantBefore, NonTurnInvariantSnapshot(databasePath, "thread-r3-enrich"));
            var metadata = TokenMetadataSnapshot(databasePath, "thread-r3-enrich");
            Assert.True(metadata.Contains("turn-recovered", StringComparison.Ordinal));
            Assert.True(!metadata.Contains("|NULL|NULL|", StringComparison.Ordinal));
            Assert.True(LatestEventSnapshot(databasePath, "thread-r3-enrich")
                .Contains("turn-recovered", StringComparison.Ordinal));
        }

        using (var restarted = new UsageDatabase(databasePath))
        {
            var before = NonTurnInvariantSnapshot(databasePath, "thread-r3-enrich") + "|" +
                         LatestEventSnapshot(databasePath, "thread-r3-enrich") + "|" +
                         TurnAggregateSnapshot(databasePath, "thread-r3-enrich");
            var replay = ScanFileToEnd(new RolloutIndexer(restarted), source,
                "sessions/r3-enrich.jsonl", "thread-r3-enrich");
            Assert.Equal(0, replay.AcceptedSamples);
            var after = NonTurnInvariantSnapshot(databasePath, "thread-r3-enrich") + "|" +
                        LatestEventSnapshot(databasePath, "thread-r3-enrich") + "|" +
                        TurnAggregateSnapshot(databasePath, "thread-r3-enrich");
            Assert.Equal(before, after);
        }
    }

    private static void EnrichmentLatestOracleReverse(string runRoot)
    {
        var directory = Path.Combine(runRoot, "r3-03-reverse-oracle");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "usage.db");
        var sourceA = Fixture(Path.Combine("correction-04", "reverse-a.jsonl"));
        var sourceB = Fixture(Path.Combine("correction-04", "reverse-b.jsonl"));

        string cleanLatest;
        string cleanTurns;
        using (var initial = new UsageDatabase(databasePath))
        {
            var indexer = new RolloutIndexer(initial);
            ScanFileToEnd(indexer, sourceA, "sessions/a.jsonl", "thread-r3-oracle");
            ScanFileToEnd(indexer, sourceB, "sessions/b.jsonl", "thread-r3-oracle");
            cleanLatest = LatestEventSnapshot(databasePath, "thread-r3-oracle");
            cleanTurns = TurnAggregateSnapshot(databasePath, "thread-r3-oracle");
        }
        DegradeTurnAndSourceMarkers(databasePath);

        using var migrated = new UsageDatabase(databasePath);
        while (!migrated.RunAggregateRebuildBatch()) { }
        var invariantBefore = NonTurnInvariantSnapshot(databasePath, "thread-r3-oracle");
        var reverseIndexer = new RolloutIndexer(migrated);
        var first = ScanFileToEnd(reverseIndexer, sourceB, "sessions/b.jsonl", "thread-r3-oracle");
        var second = ScanFileToEnd(reverseIndexer, sourceA, "sessions/a.jsonl", "thread-r3-oracle");
        Assert.Equal(2, first.DuplicateSamples);
        Assert.Equal(1, second.DuplicateSamples);
        Assert.Equal(cleanLatest, LatestEventSnapshot(databasePath, "thread-r3-oracle"));
        Assert.Equal(cleanTurns, TurnAggregateSnapshot(databasePath, "thread-r3-oracle"));
        Assert.Equal(invariantBefore, NonTurnInvariantSnapshot(databasePath, "thread-r3-oracle"));
    }

    private static void AppIoFifoDispatcherShutdown(string runRoot)
    {
        var directory = Path.Combine(runRoot, "r3-04-app-io");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "usage.db");
        var logPath = Path.Combine(directory, "hud.log");
        using var database = new UsageDatabase(databasePath);
        using var firstEntered = new ManualResetEventSlim(false);
        using var firstRelease = new ManualResetEventSlim(false);
        using var logEntered = new ManualResetEventSlim(false);
        using var logRelease = new ManualResetEventSlim(false);
        var delayFirstSave = 1;
        var delayLog = 0;
        var operations = new List<string>();
        var operationsGate = new object();
        var queue = new AppIoQueue(database, database.SetPinnedThread, new PrivacyLog(logPath), 2,
            operation =>
            {
                lock (operationsGate) operations.Add(operation);
                if (operation == "save-settings" && Interlocked.Exchange(ref delayFirstSave, 0) == 1)
                {
                    firstEntered.Set();
                    firstRelease.Wait(TimeSpan.FromSeconds(5));
                }
                if (operation == "write-log" && Volatile.Read(ref delayLog) == 1)
                {
                    logEntered.Set();
                    logRelease.Wait(TimeSpan.FromSeconds(5));
                }
            });
        Assert.Equal(2, queue.Capacity);

        var first = queue.SaveSettingsAsync(new Dictionary<string, string> { ["fifo"] = "first" });
        Assert.True(firstEntered.Wait(TimeSpan.FromSeconds(2)));
        var second = queue.SaveSettingsAsync(new Dictionary<string, string> { ["fifo"] = "second" });
        Assert.True(!second.IsCompleted);
        firstRelease.Set();
        Task.WhenAll(first, second).GetAwaiter().GetResult();
        Assert.Equal("second", database.LoadSetting("fifo"));

        using (var lockConnection = new SqliteConnection($"Data Source={databasePath};Pooling=False;Default Timeout=2"))
        {
            lockConnection.Open();
            using var transaction = lockConnection.BeginTransaction(deferred: false);
            var blocked = queue.SaveSettingsAsync(new Dictionary<string, string> { ["locked"] = "released" });
            var dispatcherResponsive = false;
            Dispatcher.CurrentDispatcher.BeginInvoke(() => dispatcherResponsive = true,
                DispatcherPriority.Send);
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(150),
            };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
            Assert.True(dispatcherResponsive);
            Assert.True(!blocked.IsCompleted);
            transaction.Rollback();
            blocked.GetAwaiter().GetResult();
        }

        Volatile.Write(ref delayLog, 1);
        var pendingLog = queue.WriteLogCodeAsync("refresh_failed", @"C:\private\rollout.jsonl", 7);
        Assert.True(logEntered.Wait(TimeSpan.FromSeconds(2)));
        queue.StopAcceptingAsync().GetAwaiter().GetResult();
        try
        {
            queue.SaveSettingsAsync(new Dictionary<string, string> { ["late-before-final"] = "rejected" })
                .GetAwaiter().GetResult();
            throw new InvalidOperationException("expected_app_io_closed");
        }
        catch (InvalidOperationException exception)
        {
            Assert.Equal("app_io_closed", exception.Message);
        }
        var complete = queue.CompleteAsync(new Dictionary<string, string> { ["final"] = "saved" });
        Assert.True(!complete.IsCompleted);
        logRelease.Set();
        Task.WhenAll(pendingLog, complete).GetAwaiter().GetResult();
        Assert.Equal("saved", database.LoadSetting("final"));
        try
        {
            queue.SaveSettingsAsync(new Dictionary<string, string> { ["late"] = "rejected" })
                .GetAwaiter().GetResult();
            throw new InvalidOperationException("expected_app_io_closed");
        }
        catch (InvalidOperationException exception)
        {
            Assert.Equal("app_io_closed", exception.Message);
        }
        queue.DisposeAsync().AsTask().GetAwaiter().GetResult();

        var log = File.ReadAllText(logPath, Encoding.UTF8);
        Assert.DoesNotContain(@"C:\private\rollout.jsonl", log);
        Assert.True(log.Trim().Split('|').Length == 4);
        lock (operationsGate)
        {
            Assert.True(operations.IndexOf("write-log") < operations.IndexOf("final-settings"));
        }

        var disposeCount = 0;
        using var disposeEntered = new ManualResetEventSlim(false);
        using var disposeRelease = new ManualResetEventSlim(false);
        var lease = new AsyncResourceLease(() =>
        {
            disposeEntered.Set();
            disposeRelease.Wait(TimeSpan.FromSeconds(5));
            Interlocked.Increment(ref disposeCount);
        });
        var firstDispose = lease.DisposeAsync();
        Assert.True(disposeEntered.Wait(TimeSpan.FromSeconds(2)));
        var secondDispose = lease.DisposeAsync();
        Assert.True(ReferenceEquals(firstDispose, secondDispose));
        Assert.True(!secondDispose.IsCompleted);
        disposeRelease.Set();
        Task.WhenAll(firstDispose, secondDispose).GetAwaiter().GetResult();
        Assert.Equal(1, disposeCount);
    }

    private static void StaticDispatcherOwnership()
    {
        var root = ProjectRoot();
        var app = File.ReadAllText(Path.Combine(root, "src", "CodexUsageHud.App", "App.xaml.cs"));
        var window = File.ReadAllText(Path.Combine(root, "src", "CodexUsageHud.App", "MainWindow.xaml.cs"));
        var engine = File.ReadAllText(Path.Combine(root, "src", "CodexUsageHud.Core", "UsageEngine.cs"));
        Assert.True(app.Contains("await Task.Run(() => CreateRuntime", StringComparison.Ordinal));
        Assert.True(app.Contains("new UsageEngine", StringComparison.Ordinal));
        var onExit = app[app.IndexOf("private void OnExit", StringComparison.Ordinal)..
            app.IndexOf("private Task DisposeRuntimeAsync", StringComparison.Ordinal)];
        Assert.True(!onExit.Contains("Dispose(", StringComparison.Ordinal));
        foreach (var forbidden in new[]
                 {
                     ".SaveSetting(", ".LoadSetting(", ".SetPinnedThread(",
                     "new PrivacyLog", ".WriteCode(",
                 })
            Assert.True(!window.Contains(forbidden, StringComparison.Ordinal));
        foreach (var required in new[]
                 {
                     "_backgroundLoopTask", "_refreshGate", "WaitAsync(0)", "_refreshPending",
                     "await _engine.CompleteAppIoAsync",
                     "await _engine.StopAcceptingAppIoAsync",
                     "await _disposeRuntimeAsync", "await _engine.WriteLogCodeAsync",
                 })
            Assert.True(window.Contains(required, StringComparison.Ordinal));
        var exitCore = window[window.IndexOf("private async Task ExitCoreAsync", StringComparison.Ordinal)..
            window.IndexOf("private async Task RestoreWindowStateAsync", StringComparison.Ordinal)];
        Assert.True(exitCore.IndexOf("await _engine.StopAcceptingAppIoAsync", StringComparison.Ordinal) <
                    exitCore.IndexOf("_shutdown.Cancel();", StringComparison.Ordinal));
        Assert.True(engine.Contains("Channel.CreateBounded", StringComparison.Ordinal));
        Assert.True(engine.Contains("SingleReader = true", StringComparison.Ordinal));
        Assert.True(!app.Contains("Fast", StringComparison.OrdinalIgnoreCase));
        Assert.True(!app.Contains("priority", StringComparison.OrdinalIgnoreCase));
        Assert.True(!app.Contains("ultrafast", StringComparison.OrdinalIgnoreCase));
    }

    private static void BoundedRecurringTurns(string runRoot)
    {
        var directory = Path.Combine(runRoot, "r3-05-bounded-turns");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "usage.db");
        using var database = new UsageDatabase(databasePath);
        const int sessions = 4;
        const int turnsPerSession = 5_000;
        for (var session = 0; session < sessions; session++)
        {
            var thread = $"bounded-thread-{session}";
            database.UpsertSession(new SessionMetadata(thread, thread, null, null, "bounded",
                "model", null, null, DateTimeOffset.UtcNow, null), SessionStatus.Idle,
                $"turn-{session}-0000", 1, false);
        }
        using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
        {
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var turn = connection.CreateCommand();
            turn.Transaction = transaction;
            turn.CommandText = """
                INSERT INTO turn_token_aggregates(thread_id, turn_key, input_tokens, raw_input_tokens,
                    cached_input_tokens, cache_write_input_tokens, output_tokens,
                    reasoning_output_tokens, canonical_total_tokens, reported_total_tokens)
                VALUES ($thread, $turn, 1, 1, 0, 0, 1, 0, 2, 2);
                """;
            turn.Parameters.Add("$thread", SqliteType.Text);
            turn.Parameters.Add("$turn", SqliteType.Text);
            for (var session = 0; session < sessions; session++)
            {
                for (var index = 0; index < turnsPerSession; index++)
                {
                    turn.Parameters["$thread"].Value = $"bounded-thread-{session}";
                    turn.Parameters["$turn"].Value = $"turn-{session}-{index:0000}";
                    turn.ExecuteNonQuery();
                }
            }
            using var latest = connection.CreateCommand();
            latest.Transaction = transaction;
            latest.CommandText = """
                INSERT INTO latest_token_events(thread_id, sample_id, turn_key, event_time_ticks,
                    source_key, source_generation, source_offset, turn_confidence,
                    event_order_confidence, input_tokens, raw_input_tokens, cached_input_tokens,
                    cache_write_input_tokens, output_tokens, reasoning_output_tokens,
                    canonical_total_tokens, reported_total_tokens)
                VALUES ($thread, $sample, $turn, $ticks, $source, 0, $offset, 'reliable',
                    'reliable', 1, 1, 0, 0, 1, 0, 2, 2);
                """;
            foreach (var name in new[] { "$thread", "$sample", "$turn", "$ticks", "$source", "$offset" })
                latest.Parameters.Add(name, name is "$sample" or "$ticks" or "$offset"
                    ? SqliteType.Integer : SqliteType.Text);
            for (var session = 0; session < sessions; session++)
            {
                latest.Parameters["$thread"].Value = $"bounded-thread-{session}";
                latest.Parameters["$sample"].Value = session + 1;
                latest.Parameters["$turn"].Value = session % 2 == 0
                    ? $"turn-{session}-0000" : $"turn-{session}-4999";
                latest.Parameters["$ticks"].Value = DateTimeOffset.UtcNow.UtcTicks + session;
                latest.Parameters["$source"].Value = $"source-{session}";
                latest.Parameters["$offset"].Value = session;
                latest.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        var frameTimes = new List<long>();
        IReadOnlyList<SessionAggregate> aggregates = Array.Empty<SessionAggregate>();
        for (var iteration = 0; iteration < 30; iteration++)
        {
            var watch = Stopwatch.StartNew();
            aggregates = database.LoadSessionAggregates(DateTimeOffset.UtcNow);
            watch.Stop();
            frameTimes.Add(watch.ElapsedMilliseconds);
        }
        Assert.Equal(sessions, aggregates.Count);
        Assert.Equal(6L, database.PerformanceMetrics.RecurringTurnRowsRead);
        Assert.True(database.PerformanceMetrics.RecurringTurnRowsRead <= sessions * 2);
        Assert.Equal(0L, database.GetSampleCount());
        Assert.Equal(0L, database.PerformanceMetrics.RecurringTokenHistoryRowsRead);
        Assert.True(Percentile95(frameTimes) <= 250);
        var plan = database.GetRecurringTurnQueryPlan();
        Assert.True(plan.Any(item => item.Contains("SEARCH aggregates USING INDEX sqlite_autoindex_turn_token_aggregates_1",
            StringComparison.OrdinalIgnoreCase)));
        Assert.True(plan.All(item => !item.Contains("SCAN turn_token_aggregates", StringComparison.OrdinalIgnoreCase) &&
                                     !item.Contains("SCAN aggregates", StringComparison.OrdinalIgnoreCase)));
        Console.WriteLine($"TURN_BOUND rows={database.PerformanceMetrics.RecurringTurnRowsRead} " +
                          $"sessions={sessions} historical_turns={sessions * turnsPerSession} " +
                          $"p95_ms={Percentile95(frameTimes)}");
    }

    private static void ServiceTierProvenanceTest(string runRoot)
    {
        var directory = Path.Combine(runRoot, "r3-06-tier-provenance");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "usage.db");
        var source = Path.Combine(directory, "tier.jsonl");
        using (var database = new UsageDatabase(databasePath))
        {
            database.UpsertSession(new SessionMetadata("tier-catalog", "Catalog", null, null, null,
                "model-catalog", null, null, DateTimeOffset.UtcNow, null));
            Assert.True(database.ApplyCatalogTier("tier-catalog", "Standard（默认）"));
            var inferred = database.LoadSessions().Single(item => item.ThreadId == "tier-catalog");
            Assert.Equal("Standard（默认）", inferred.ServiceTier);
            Assert.Equal(ServiceTierProvenance.CatalogDefault, inferred.ServiceTierSource);
            Assert.True(!database.ApplyCatalogTier("tier-catalog", "不可用"));

            File.WriteAllText(source, TokenLineAdvanced("tier-catalog", 1700203000,
                4, 1, 4, 1, "model-catalog", "standard") + "\n", new UTF8Encoding(false));
            var indexer = new RolloutIndexer(database);
            Assert.Equal(1, ScanFileToEnd(indexer, source, "sessions/tier.jsonl", "tier-catalog").AcceptedSamples);
            var explicitStandard = database.LoadSessions().Single(item => item.ThreadId == "tier-catalog");
            Assert.Equal("Standard（默认）", explicitStandard.ServiceTier);
            Assert.Equal(ServiceTierProvenance.RolloutExplicit, explicitStandard.ServiceTierSource);
            Assert.True(!database.ApplyCatalogTier("tier-catalog", "Fast"));

            File.AppendAllText(source, TokenLineAdvanced("tier-catalog", 1700203001,
                8, 2, 4, 1, "model-catalog", "fast") + "\n", new UTF8Encoding(false));
            ScanFileToEnd(indexer, source, "sessions/tier.jsonl", "tier-catalog");
            var explicitFast = database.LoadSessions().Single(item => item.ThreadId == "tier-catalog");
            Assert.Equal("Fast", explicitFast.ServiceTier);
            Assert.Equal(ServiceTierProvenance.RolloutExplicit, explicitFast.ServiceTierSource);

            File.AppendAllText(source, TokenLineAdvanced("tier-catalog", 1700203002,
                12, 3, 4, 1, "model-catalog", "standard") + "\n", new UTF8Encoding(false));
            ScanFileToEnd(indexer, source, "sessions/tier.jsonl", "tier-catalog");
            var laterStandard = database.LoadSessions().Single(item => item.ThreadId == "tier-catalog");
            Assert.Equal("Standard（默认）", laterStandard.ServiceTier);
            Assert.Equal(ServiceTierProvenance.RolloutExplicit, laterStandard.ServiceTierSource);

            database.MergeSessionMetadata(new SessionMetadata("tier-catalog", "Metadata", null, null,
                null, "model-catalog", null, "Fast", DateTimeOffset.UtcNow, null,
                ServiceTierSource: ServiceTierProvenance.LegacyPreserved));
            var merged = database.LoadSessions().Single(item => item.ThreadId == "tier-catalog");
            Assert.Equal("Standard（默认）", merged.ServiceTier);
            Assert.Equal(ServiceTierProvenance.RolloutExplicit, merged.ServiceTierSource);

            database.UpsertSession(new SessionMetadata("tier-legacy", "Legacy", null, null, null,
                "model-legacy", null, "Fast", DateTimeOffset.UtcNow, null));
            Assert.Equal(ServiceTierProvenance.LegacyPreserved,
                database.LoadSessions().Single(item => item.ThreadId == "tier-legacy").ServiceTierSource);
        }

        using (var restarted = new UsageDatabase(databasePath))
        {
            var persisted = restarted.LoadSessions().Single(item => item.ThreadId == "tier-catalog");
            Assert.Equal("Standard（默认）", persisted.ServiceTier);
            Assert.Equal(ServiceTierProvenance.RolloutExplicit, persisted.ServiceTierSource);
        }

        var explicitOnlyPath = Path.Combine(directory, "explicit-only.db");
        var explicitOnlySource = Path.Combine(directory, "explicit-only.jsonl");
        File.WriteAllText(explicitOnlySource, TokenLineAdvanced("tier-explicit-only", 1700203100,
            2, 1, 2, 1, "model-missing-catalog", "standard") + "\n", new UTF8Encoding(false));
        using var explicitOnly = new UsageDatabase(explicitOnlyPath);
        ScanFileToEnd(new RolloutIndexer(explicitOnly), explicitOnlySource,
            "sessions/explicit-only.jsonl", "tier-explicit-only");
        var explicitWithoutCatalog = explicitOnly.LoadSessions().Single();
        Assert.Equal("Standard（默认）", explicitWithoutCatalog.ServiceTier);
        Assert.Equal(ServiceTierProvenance.RolloutExplicit, explicitWithoutCatalog.ServiceTierSource);
    }

    private static void ScopedPrivacyEvidence(string runRoot)
    {
        var directory = Path.Combine(runRoot, "r3-07-scoped-privacy");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "usage.db");
        var source = Path.Combine(directory, "source.jsonl");
        File.WriteAllText(source,
            $"{{\"type\":\"message\",\"payload\":{{\"body\":\"{Sentinel}\"}}}}\n" +
            TokenLine("privacy-scope", 1700204000, 1, 1, 1, 1) + "\n",
            new UTF8Encoding(false));
        bool databaseClean;
        using (var database = new UsageDatabase(databasePath))
        {
            ScanFileToEnd(new RolloutIndexer(database), source,
                "sessions/source.jsonl", "privacy-scope");
            var forbidden = new HashSet<string>(new[]
            {
                "first_user_message", "preview", "prompt", "response", "raw_json", "credential",
            }, StringComparer.OrdinalIgnoreCase);
            databaseClean = !database.ContainsPrivacySentinel(Sentinel) &&
                            database.ReadSchemaColumnNames().Values.SelectMany(item => item)
                                .All(column => !forbidden.Contains(column)) &&
                            database.LoadSourceStates().All(item => !Path.IsPathRooted(item.RelativePath));
        }
        Assert.True(databaseClean);

        var logPath = Path.Combine(directory, "hud.log");
        using (var log = new PrivacyLog(logPath))
            log.WriteCode("source_io", @"C:\private\session.jsonl", 9);
        var logText = File.ReadAllText(logPath, Encoding.UTF8);
        var logClean = !logText.Contains(@"C:\private\session.jsonl", StringComparison.OrdinalIgnoreCase) &&
                       logText.Trim().Split('|').Length == 4;
        Assert.True(logClean);

        var now = DateTimeOffset.UtcNow;
        var snapshot = new HudSnapshot(new QuotaObservation(null, Array.Empty<QuotaBucket>(),
                QuotaSource.Unavailable, now, false), SampleSessions(now), null, now, false,
            "scoped privacy", Array.Empty<string>());
        var viewModel = new MainViewModel();
        viewModel.Apply(HudPresentation.BuildFrame(snapshot));
        var renderedClean = viewModel.RenderedStrings().All(value =>
            !value.Contains(Sentinel, StringComparison.Ordinal) && !Path.IsPathRooted(value));
        Assert.True(renderedClean);

        var program = File.ReadAllText(Path.Combine(ProjectRoot(), "tests", "CodexUsageHud.Tests", "Program.cs"));
        Assert.True(program.Contains("database_privacy_clean=", StringComparison.Ordinal));
        Assert.True(program.Contains("hud_log_privacy=", StringComparison.Ordinal));
        Assert.True(program.Contains("rendered_string_privacy=", StringComparison.Ordinal));
        var obsoleteCombinedLabel = " offsets_unchanged={result.SourceOffsetsUnchanged} " + "privacy_clean=";
        Assert.True(!program.Contains(obsoleteCombinedLabel,
            StringComparison.Ordinal));
        Console.WriteLine($"PRIVACY database={databaseClean} hud_log={logClean} rendered_strings={renderedClean}");
    }

    private static string TokenLineAdvanced(string threadId, long? timestamp, long totalInput,
        long totalOutput, long lastInput, long lastOutput, string model, string serviceTier)
    {
        var timestampPart = timestamp.HasValue ? $",\"timestamp\":{timestamp.Value}" : string.Empty;
        return $"{{\"type\":\"event_msg\",\"payload\":{{\"type\":\"token_count\",\"thread_id\":\"{threadId}\"{timestampPart},\"model\":\"{model}\",\"service_tier\":\"{serviceTier}\",\"info\":{{\"total_token_usage\":{{\"input_tokens\":{totalInput},\"output_tokens\":{totalOutput},\"total_tokens\":{totalInput + totalOutput}}},\"last_token_usage\":{{\"input_tokens\":{lastInput},\"output_tokens\":{lastOutput},\"total_tokens\":{lastInput + lastOutput}}},\"context_window\":128000}}}}}}";
    }

    private static void CreateLegacyV4Database(string databasePath, string privatePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE schema_info(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE source_files(
                source_key TEXT PRIMARY KEY, thread_id TEXT NOT NULL, volume_serial TEXT NOT NULL,
                file_id TEXT NOT NULL, fallback_key TEXT NOT NULL, is_degraded INTEGER NOT NULL,
                relative_path TEXT NOT NULL, generation INTEGER NOT NULL, complete_offset INTEGER NOT NULL,
                last_length INTEGER NOT NULL, last_write_utc_ticks INTEGER NOT NULL DEFAULT 0,
                cursor_turn_key TEXT, health_code TEXT NOT NULL);
            CREATE TABLE event_fingerprints(
                fingerprint TEXT PRIMARY KEY, thread_id TEXT NOT NULL, first_seen_utc TEXT NOT NULL,
                last_seen_utc TEXT NOT NULL, duplicate_count INTEGER NOT NULL DEFAULT 0,
                disposition TEXT NOT NULL DEFAULT 'accepted', diagnostic_code TEXT);
            CREATE TABLE token_samples(
                id INTEGER PRIMARY KEY AUTOINCREMENT, fingerprint TEXT NOT NULL UNIQUE,
                thread_id TEXT NOT NULL, input_tokens INTEGER NOT NULL, raw_input_tokens INTEGER NOT NULL,
                cached_input_tokens INTEGER NOT NULL, cache_write_input_tokens INTEGER NOT NULL,
                output_tokens INTEGER NOT NULL, reasoning_output_tokens INTEGER NOT NULL,
                cumulative_input_tokens INTEGER NOT NULL, cumulative_output_tokens INTEGER NOT NULL,
                cumulative_total_tokens INTEGER NOT NULL, canonical_total_tokens INTEGER NOT NULL,
                reported_total_tokens INTEGER NOT NULL, event_time_utc TEXT, observed_at_utc TEXT NOT NULL,
                source_key TEXT, source_offset INTEGER, turn_key TEXT, model TEXT,
                service_tier TEXT, confidence TEXT NOT NULL);
            INSERT INTO schema_info(key, value) VALUES
                ('version', '4'), ('windows_file_id_128', '1'),
                ('token_tuple_oracle_migration', '2'), ('token_tuple_dedup', '1'),
                ('token_cumulative_guard', '0'), ('deterministic_turn_keys', '1'),
                ('sample_source_offsets', '1');
            INSERT INTO source_files(source_key, thread_id, volume_serial, file_id, fallback_key,
                is_degraded, relative_path, generation, complete_offset, last_length,
                last_write_utc_ticks, cursor_turn_key, health_code)
            VALUES ('legacy-private-source', 'thread-legacy-private', 'fallback', 'fallback',
                $fallback, 1, 'sessions/legacy.jsonl', 0, 1234, 1234, 1, NULL, 'ok');
            INSERT INTO event_fingerprints(fingerprint, thread_id, first_seen_utc, last_seen_utc,
                duplicate_count, disposition)
            VALUES ($fingerprint, 'thread-legacy-private', $observed, $observed, 0, 'accepted');
            INSERT INTO token_samples(fingerprint, thread_id, input_tokens, raw_input_tokens,
                cached_input_tokens, cache_write_input_tokens, output_tokens, reasoning_output_tokens,
                cumulative_input_tokens, cumulative_output_tokens, cumulative_total_tokens,
                canonical_total_tokens, reported_total_tokens, event_time_utc, observed_at_utc,
                source_key, source_offset, turn_key, model, service_tier, confidence)
            VALUES ($fingerprint, 'thread-legacy-private', 10, 10, 0, 0, 2, 0,
                10, 2, 12, 12, 12, $event_time, $observed,
                'legacy-private-source', 42, NULL, NULL, NULL, 'trusted');
            """;
        var observed = DateTimeOffset.UtcNow;
        command.Parameters.AddWithValue("$fallback", privatePath + "|638900000000000000");
        command.Parameters.AddWithValue("$fingerprint", new string('b', 64));
        command.Parameters.AddWithValue("$observed", observed.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$event_time", observed.AddMinutes(-1).ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    private static void SeedPerformanceDatabase(string databasePath, int samples, int sessions,
        DateTimeOffset cycleStart)
    {
        using (var database = new UsageDatabase(databasePath))
        {
            for (var index = 0; index < sessions; index++)
            {
                var thread = $"perf-thread-{index:000}";
                database.UpsertSession(new SessionMetadata(thread, $"Perf {index:000}", "worker", "perf",
                    "performance", "model-perf", "high", "Standard（默认）",
                    DateTimeOffset.UtcNow.AddSeconds(-index), null), SessionStatus.Idle,
                    $"turn-{index:000}", 1, false);
            }
        }

        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using var fingerprint = connection.CreateCommand();
        fingerprint.Transaction = transaction;
        fingerprint.CommandText = """
            INSERT INTO event_fingerprints(fingerprint, thread_id, first_seen_utc, last_seen_utc,
                duplicate_count, disposition)
            VALUES ($fingerprint, $thread_id, $observed, $observed, 0, 'accepted');
            """;
        fingerprint.Parameters.Add("$fingerprint", SqliteType.Text);
        fingerprint.Parameters.Add("$thread_id", SqliteType.Text);
        fingerprint.Parameters.Add("$observed", SqliteType.Text);

        using var sample = connection.CreateCommand();
        sample.Transaction = transaction;
        sample.CommandText = """
            INSERT INTO token_samples(fingerprint, thread_id, input_tokens, raw_input_tokens,
                cached_input_tokens, cache_write_input_tokens, output_tokens, reasoning_output_tokens,
                cumulative_input_tokens, cumulative_output_tokens, cumulative_total_tokens,
                canonical_total_tokens, reported_total_tokens, event_time_utc, event_time_ticks,
                observed_at_utc, source_key, source_generation, source_offset, turn_key, model,
                service_tier, confidence, turn_confidence, event_order_confidence)
            VALUES ($fingerprint, $thread_id, 1, 1, 0, 0, 1, 0,
                $cumulative_input, $cumulative_output, $cumulative_total,
                2, 2, $event_time, $event_ticks, $observed, $source_key, 0, $source_offset,
                $turn_key, 'model-perf', 'Standard（默认）', 'trusted', 'reliable', 'reliable');
            """;
        foreach (var name in new[] { "$fingerprint", "$thread_id", "$cumulative_input",
                     "$cumulative_output", "$cumulative_total", "$event_time", "$event_ticks",
                     "$observed", "$source_key", "$source_offset", "$turn_key" })
            sample.Parameters.Add(name, name is "$event_ticks" or "$source_offset" or "$cumulative_input" or
                "$cumulative_output" or "$cumulative_total" ? SqliteType.Integer : SqliteType.Text);

        var observedText = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        for (var index = 0; index < samples; index++)
        {
            var sessionIndex = index % sessions;
            var thread = $"perf-thread-{sessionIndex:000}";
            var fingerprintText = (index + 1L).ToString("x64", CultureInfo.InvariantCulture);
            var eventTime = cycleStart.AddMinutes(index % 10080);
            var cumulative = ((index / sessions) + 1L) * 2L;
            fingerprint.Parameters["$fingerprint"].Value = fingerprintText;
            fingerprint.Parameters["$thread_id"].Value = thread;
            fingerprint.Parameters["$observed"].Value = observedText;
            fingerprint.ExecuteNonQuery();

            sample.Parameters["$fingerprint"].Value = fingerprintText;
            sample.Parameters["$thread_id"].Value = thread;
            sample.Parameters["$cumulative_input"].Value = cumulative / 2;
            sample.Parameters["$cumulative_output"].Value = cumulative / 2;
            sample.Parameters["$cumulative_total"].Value = cumulative;
            sample.Parameters["$event_time"].Value = eventTime.ToString("O", CultureInfo.InvariantCulture);
            sample.Parameters["$event_ticks"].Value = eventTime.UtcTicks;
            sample.Parameters["$observed"].Value = observedText;
            sample.Parameters["$source_key"].Value = "win-v2:" + sessionIndex.ToString("x64", CultureInfo.InvariantCulture);
            sample.Parameters["$source_offset"].Value = index;
            sample.Parameters["$turn_key"].Value = $"turn-{sessionIndex:000}";
            sample.ExecuteNonQuery();
        }
        using var reset = connection.CreateCommand();
        reset.Transaction = transaction;
        reset.CommandText = """
            INSERT INTO schema_info(key, value) VALUES ('aggregate_schema_version', '4')
                ON CONFLICT(key) DO UPDATE SET value = '4';
            DELETE FROM aggregate_rebuild_state;
            """;
        reset.ExecuteNonQuery();
        transaction.Commit();
    }

    private static ScanResult ScanFileToEnd(RolloutIndexer indexer, string fullPath,
        string relativePath, string threadId)
    {
        var result = indexer.ScanFile(fullPath, relativePath, threadId);
        Assert.True(!result.HasMoreData);
        return result;
    }

    private static void DegradeTurnAndSourceMarkers(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM schema_info WHERE key IN ('deterministic_turn_keys', 'sample_source_offsets');
            UPDATE token_samples SET turn_key = NULL, turn_confidence = 'unavailable',
                source_key = NULL, source_generation = 0, source_offset = NULL,
                event_order_confidence = 'unavailable';
            """;
        command.ExecuteNonQuery();
    }

    private static string NonTurnInvariantSnapshot(string databasePath, string threadId) => string.Join("#",
        QuerySnapshot(databasePath, """
            SELECT display_name, role, nickname, project_tag, model, reasoning_effort,
                   service_tier, service_tier_source, last_activity_utc, status, current_turn_key,
                   current_turn_reliable, turn_sequence, turn_open, pinned,
                   current_structural_event_identity, current_structural_source_hash,
                   current_structural_generation, current_structural_offset, current_structural_time_utc,
                   state_event_time_ticks, state_source_key, state_source_generation,
                   state_source_offset, state_event_kind
            FROM sessions WHERE thread_id = $thread ORDER BY thread_id;
            """, threadId),
        QuerySnapshot(databasePath, """
            SELECT input_tokens, raw_input_tokens, cached_input_tokens, cache_write_input_tokens,
                   output_tokens, reasoning_output_tokens, canonical_total_tokens, reported_total_tokens
            FROM session_token_aggregates WHERE thread_id = $thread;
            """, threadId),
        QuerySnapshot(databasePath, """
            SELECT maximum_cumulative_total, sample_id FROM cumulative_frontiers WHERE thread_id = $thread;
            """, threadId),
        QuerySnapshot(databasePath, """
            SELECT bucket_start_ticks, input_tokens, raw_input_tokens, cached_input_tokens,
                   cache_write_input_tokens, output_tokens, reasoning_output_tokens,
                   canonical_total_tokens, reported_total_tokens
            FROM token_time_buckets ORDER BY bucket_start_ticks;
            """, threadId),
        QuerySnapshot(databasePath, """
            SELECT cycle_start_ticks, cycle_end_ticks, input_tokens, raw_input_tokens,
                   cached_input_tokens, cache_write_input_tokens, output_tokens,
                   reasoning_output_tokens, canonical_total_tokens, reported_total_tokens
            FROM active_cycle_aggregate ORDER BY singleton;
            """, threadId));

    private static string LatestEventSnapshot(string databasePath, string threadId) =>
        QuerySnapshot(databasePath, """
            SELECT sample_id, turn_key, event_time_ticks, source_key, source_generation, source_offset,
                   turn_confidence, event_order_confidence, input_tokens, raw_input_tokens,
                   cached_input_tokens, cache_write_input_tokens, output_tokens,
                   reasoning_output_tokens, canonical_total_tokens, reported_total_tokens
            FROM latest_token_events WHERE thread_id = $thread;
            """, threadId);

    private static string TurnAggregateSnapshot(string databasePath, string threadId) =>
        QuerySnapshot(databasePath, """
            SELECT turn_key, input_tokens, raw_input_tokens, cached_input_tokens,
                   cache_write_input_tokens, output_tokens, reasoning_output_tokens,
                   canonical_total_tokens, reported_total_tokens
            FROM turn_token_aggregates WHERE thread_id = $thread ORDER BY turn_key;
            """, threadId);

    private static string TokenMetadataSnapshot(string databasePath, string threadId) =>
        QuerySnapshot(databasePath, """
            SELECT id, turn_key, turn_confidence, source_key, source_generation, source_offset,
                   event_order_confidence FROM token_samples WHERE thread_id = $thread ORDER BY id;
            """, threadId);

    private static string QuerySnapshot(string databasePath, string sql, string threadId)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$thread", threadId);
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read())
        {
            var values = new string[reader.FieldCount];
            for (var index = 0; index < reader.FieldCount; index++)
            {
                values[index] = reader.IsDBNull(index) ? "NULL" :
                    Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture) ?? string.Empty;
            }
            rows.Add(string.Join('|', values));
        }
        return string.Join('~', rows);
    }

    private static void SetPrivateMarkers(string databasePath, string? identity, string? scrub)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        using var transaction = connection.BeginTransaction();
        SetSchemaValue(connection, transaction, "private_source_identity_v2", identity);
        SetSchemaValue(connection, transaction, "private_source_scrub_v2", scrub);
        transaction.Commit();
    }

    private static void SetSchemaValue(SqliteConnection connection, SqliteTransaction transaction,
        string key, string? value)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = value is null
            ? "DELETE FROM schema_info WHERE key = $key;"
            : """
                INSERT INTO schema_info(key, value) VALUES ($key, $value)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """;
        command.Parameters.AddWithValue("$key", key);
        if (value is not null) command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private static Process StartMigrationCrashHelper(string databasePath, string point)
    {
        var executable = Path.ChangeExtension(typeof(Program).Assembly.Location, ".exe");
        Assert.True(File.Exists(executable));
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("--migration-crash-helper");
        start.ArgumentList.Add(databasePath);
        start.ArgumentList.Add(point);
        return Process.Start(start) ?? throw new InvalidOperationException("process_start_failed");
    }

    private static void AssertFileSetDoesNotContain(string databasePath, string sentinel)
    {
        foreach (var path in new[] { databasePath, databasePath + "-wal", databasePath + "-journal" })
        {
            if (!File.Exists(path)) continue;
            Assert.DoesNotContain(sentinel, Encoding.ASCII.GetString(File.ReadAllBytes(path)));
        }
    }

    private static Process StartGateHelper(string directory, int holdMilliseconds)
    {
        var executable = Path.ChangeExtension(typeof(Program).Assembly.Location, ".exe");
        Assert.True(File.Exists(executable));
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("--gate-helper");
        start.ArgumentList.Add(directory);
        start.ArgumentList.Add(holdMilliseconds.ToString(CultureInfo.InvariantCulture));
        return Process.Start(start) ?? throw new InvalidOperationException("process_start_failed");
    }

    private static string CreateAppServerHelperWrapper(string directory, string mode, string pidPath)
    {
        var executable = Path.ChangeExtension(typeof(Program).Assembly.Location, ".exe");
        Assert.True(File.Exists(executable));
        var wrapper = Path.Combine(directory, $"app-server-{mode}.cmd");
        File.WriteAllText(wrapper,
            $"@echo off\r\n\"{executable}\" --app-server-helper {mode} \"{pidPath}\"\r\n",
            Encoding.ASCII);
        return wrapper;
    }

    private static int ReadPidWithin(string path)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (File.Exists(path) && int.TryParse(File.ReadAllText(path), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var processId))
            {
                return processId;
            }
            Thread.Sleep(20);
        }
        throw new InvalidOperationException("helper_pid_missing");
    }

    private static void AssertProcessExited(int processId)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited) return;
            }
            catch (ArgumentException)
            {
                return;
            }
            Thread.Sleep(20);
        }
        throw new InvalidOperationException("helper_process_not_cleaned_up");
    }

    private static string ReadLineWithin(Process process, int milliseconds)
    {
        var read = process.StandardOutput.ReadLineAsync();
        if (!read.Wait(milliseconds)) throw new InvalidOperationException("process_output_timeout");
        return read.Result ?? throw new InvalidOperationException("process_output_missing");
    }

    private static DateTimeOffset FloorToMinute(DateTimeOffset value) =>
        new(value.UtcTicks - value.UtcTicks % TimeSpan.TicksPerMinute, TimeSpan.Zero);

    private static long Percentile95(IReadOnlyList<long> values)
    {
        Assert.True(values.Count > 0);
        var ordered = values.OrderBy(value => value).ToArray();
        var index = Math.Clamp((int)Math.Ceiling(ordered.Length * 0.95) - 1, 0, ordered.Length - 1);
        return ordered[index];
    }

    private sealed record PerformanceEvidence(int Samples, int Sessions,
        long RebuildP95Milliseconds, long FrameP95Milliseconds,
        long DispatcherApplyP95Milliseconds, long CycleActivationMilliseconds,
        long CycleBucketRows, long BoundaryRows, long ExistingCycleRows);

    private sealed record DatabaseRestartState(string SchemaVersion, int SessionCount,
        long SampleCount, long FingerprintCount, int SourceCount, CanonicalTokenUsage Total,
        bool AggregateReady, bool PrivacySchemaClean, bool RelativePathsClean,
        string PrivateIdentityState, string PrivateScrubState);

    private static IReadOnlyList<SessionAggregate> SampleSessions(DateTimeOffset now)
    {
        var first = new SessionMetadata("thread-running-0001", "Running", "worker", "Luna", "alpha", "model-a",
            null, "Standard（默认）", now.AddMinutes(-1), null);
        var second = new SessionMetadata("thread-recent-0002", "Recent", "reviewer", "Sol", "beta", "model-b",
            null, "不可用", now.AddMinutes(-10), null);
        var third = new SessionMetadata("thread-old-000003", "Old", null, null, "alpha", "model-c",
            null, "Fast", now.AddHours(-2), null);
        return new[]
        {
            new SessionAggregate(first, SessionStatus.Running, Usage(10), Usage(4), first.LastActivityUtc, "turn-1"),
            new SessionAggregate(second, SessionStatus.Idle, Usage(25), Usage(5), second.LastActivityUtc, "turn-2"),
            new SessionAggregate(third, SessionStatus.Idle, Usage(40), Usage(8), third.LastActivityUtc, "turn-3"),
        };
    }

    private static CanonicalTokenUsage Usage(long total) => new(total, total, 0, 0, 0, 0, total, total);

    private static TokenUsageSnapshot LineageSnapshot(long totalInput, long totalOutput, long lastInput,
        long lastOutput, long? contextWindow = 128000) => new(
        new TokenComponents(totalInput, null, null, null, totalOutput, null, totalInput + totalOutput),
        new TokenComponents(lastInput, null, null, null, lastOutput, null, lastInput + lastOutput),
        contextWindow);

    private static void DrainAggregates(UsageDatabase database)
    {
        for (var guard = 0; guard < 10_000 && !database.IsAggregateRebuildComplete; guard++)
            _ = database.RunAggregateRebuildBatch();
        Assert.True(database.IsAggregateRebuildComplete);
    }

    private static void AssertNonNegativeAggregates(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        foreach (var sql in new[]
                 {
                     "SELECT COALESCE(MIN(canonical_total_tokens), 0) FROM session_token_aggregates",
                     "SELECT COALESCE(MIN(canonical_total_tokens), 0) FROM turn_token_aggregates",
                     "SELECT COALESCE(MIN(canonical_total_tokens), 0) FROM token_time_buckets",
                     "SELECT COALESCE(MIN(canonical_total_tokens), 0) FROM active_cycle_aggregate",
                     "SELECT COALESCE(MIN(canonical_total_tokens), 0) FROM latest_token_events",
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            Assert.True(Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) >= 0);
        }
    }

    private static IReadOnlyList<(long Id, string ThreadId, string Identity, string Material, string Root, int Canonical)>
        ReadLineageRows(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, thread_id, semantic_identity, semantic_material, lineage_root_thread_id,
                   is_lineage_canonical
            FROM token_samples ORDER BY id;
            """;
        using var reader = command.ExecuteReader();
        var rows = new List<(long, string, string, string, string, int)>();
        while (reader.Read())
            rows.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? string.Empty : reader.GetString(3), reader.GetString(4),
                reader.GetInt32(5)));
        return rows;
    }

    private static string DescribeLineageFlags(string databasePath) =>
        string.Join(";", ReadLineageRows(databasePath)
            .OrderBy(row => row.Id)
            .Select(row => $"{row.Id}:{row.ThreadId}:{row.Canonical}:{row.Root}"));

    private static long CountContextBaselines(string databasePath, string threadId,
        string? detectionSource = null)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = detectionSource is null
            ? "SELECT COUNT(*) FROM context_baseline_observations WHERE thread_id = $thread_id;"
            : """
                SELECT COUNT(*) FROM context_baseline_observations
                WHERE thread_id = $thread_id AND detection_source = $source;
                """;
        command.Parameters.AddWithValue("$thread_id", threadId);
        if (detectionSource is not null) command.Parameters.AddWithValue("$source", detectionSource);
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<long> ReadExplicitBaselineInputs(string databasePath, string threadId)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT post_input_tokens FROM context_baseline_observations
            WHERE thread_id = $thread_id AND detection_source = 'explicit'
            ORDER BY event_time_ticks, post_sample_id;
            """;
        command.Parameters.AddWithValue("$thread_id", threadId);
        using var reader = command.ExecuteReader();
        var values = new List<long>();
        while (reader.Read()) values.Add(reader.GetInt64(0));
        return values;
    }

    private static void AssertSingleExplicitBaseline(string databasePath, string threadId, long input)
    {
        var inputs = ReadExplicitBaselineInputs(databasePath, threadId);
        Assert.Equal(1, inputs.Count);
        Assert.Equal(input, inputs[0]);
    }

    private static void LineageSemanticContractV9AndLiveReplay(string runRoot)
    {
        var missing = new TokenUsageSnapshot(
            new TokenComponents(10, null, null, null, 2, null, 12),
            new TokenComponents(10, null, null, null, 2, null, 12), 128000);
        var zeroed = new TokenUsageSnapshot(
            new TokenComponents(10, 0, 0, 0, 2, 0, 12),
            new TokenComponents(10, 0, 0, 0, 2, 0, 12), 128000);
        var zeroContext = new TokenUsageSnapshot(
            new TokenComponents(10, null, null, null, 2, null, 12),
            new TokenComponents(10, null, null, null, 2, null, 12), 0);
        var cachedOnly = new TokenUsageSnapshot(
            new TokenComponents(10, 4, null, null, 2, null, 12),
            new TokenComponents(10, 4, null, null, 2, null, 12), 128000);
        var cacheRead = new TokenUsageSnapshot(
            new TokenComponents(10, null, 4, null, 2, null, 12),
            new TokenComponents(10, null, 4, null, 2, null, 12), 128000);
        var cacheWrite = new TokenUsageSnapshot(
            new TokenComponents(10, null, null, 3, 2, null, 12),
            new TokenComponents(10, null, null, 3, 2, null, 12), 128000);
        var reasoning = new TokenUsageSnapshot(
            new TokenComponents(10, null, null, null, 2, 1, 12),
            new TokenComponents(10, null, null, null, 2, 1, 12), 128000);
        var reported = new TokenUsageSnapshot(
            new TokenComponents(10, null, null, null, 2, null, 99),
            new TokenComponents(10, null, null, null, 2, null, 99), 128000);
        Assert.True(missing.SemanticIdentity() != zeroed.SemanticIdentity());
        Assert.True(missing.SemanticMaterial() != zeroed.SemanticMaterial());
        Assert.True(missing.SemanticIdentity() != zeroContext.SemanticIdentity());
        Assert.True(missing.SemanticIdentity() != cachedOnly.SemanticIdentity());
        Assert.True(missing.SemanticIdentity() != cacheRead.SemanticIdentity());
        Assert.True(missing.SemanticIdentity() != cacheWrite.SemanticIdentity());
        Assert.True(missing.SemanticIdentity() != reasoning.SemanticIdentity());
        Assert.True(missing.SemanticIdentity() != reported.SemanticIdentity());
        Assert.Equal(missing.LegacySemanticIdentity(), zeroed.LegacySemanticIdentity());
        Assert.True(missing.SemanticMaterial().StartsWith(TokenUsageSnapshot.LineageSemanticPrefix + "|",
            StringComparison.Ordinal));
        Assert.True(missing.LegacySemanticMaterial().StartsWith(
            TokenUsageSnapshot.LineageSemanticLegacyPrefix + "|", StringComparison.Ordinal));
        Assert.Equal(missing.SemanticIdentity(), TokenUsageSnapshot.HashText(missing.SemanticMaterial()));
        Assert.Equal(missing.LegacySemanticIdentity(), TokenUsageSnapshot.HashText(missing.LegacySemanticMaterial()));
        var last = missing.LastUsage.ToCanonical();
        var total = missing.TotalUsage.ToCanonical();
        var storedMaterial = TokenUsageSnapshot.LineageSemanticMaterialFromStored(last, total.Input, total.Output,
            total.Total, missing.ContextWindow);
        Assert.Equal(missing.LegacySemanticMaterial(), storedMaterial);
        Assert.True(missing.SemanticMaterial() != storedMaterial);
        Assert.Equal(missing.LegacySemanticIdentity(),
            TokenUsageSnapshot.LineageSemanticIdentityFromStored(last, total.Input, total.Output, total.Total,
                missing.ContextWindow));
        Assert.True(missing.Fingerprint("thread-a") != missing.Fingerprint("thread-b"));
        var otherContext = LineageSnapshot(10, 2, 10, 2, null);
        Assert.True(missing.SemanticIdentity() != otherContext.SemanticIdentity());

        var directory = Path.Combine(runRoot, "lineage-v9");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "usage.db");
        CreateSchemaV9LineageFixture(databasePath);
        using (var migrated = new UsageDatabase(databasePath))
        {
            DrainAggregates(migrated);
            Assert.Equal("10", migrated.ReadSchemaValue("version"));
            Assert.Equal("2", migrated.ReadSchemaValue("lineage_semantic_version"));
            Assert.Equal(24L, migrated.GetSessionTotal("v9-parent").Total);
            Assert.Equal(5L, migrated.GetSessionTotal("v9-child").Total);
            Assert.Equal(2L, migrated.GetSampleCount("v9-parent"));
            Assert.Equal(2L, migrated.GetSampleCount("v9-child"));
            var rows = ReadLineageRows(databasePath);
            Assert.Equal(4, rows.Count);
            Assert.True(rows.All(row => row.Material.StartsWith(
                TokenUsageSnapshot.LineageSemanticLegacyPrefix + "|", StringComparison.Ordinal)));
            Assert.True(rows.All(row => row.Identity == TokenUsageSnapshot.HashText(row.Material)));
            Assert.Equal(3, rows.Count(row => row.Canonical == 1));
            Assert.Equal(2, rows.Count(row => row.Canonical == 1 && row.ThreadId == "v9-parent"));
            Assert.Equal(1, rows.Count(row => row.Canonical == 1 && row.ThreadId == "v9-child"));
            var live = LineageSnapshot(20, 4, 10, 2);
            Assert.Equal(live.LegacySemanticIdentity(), rows.Single(row =>
                row.ThreadId == "v9-parent" && row.Material.Contains("|20,4,24|", StringComparison.Ordinal)).Identity);
            migrated.UpsertSession(new SessionMetadata("v9-later", "Later", null, null, null, null, null, null,
                DateTimeOffset.Parse("2026-01-01T00:02:00Z", CultureInfo.InvariantCulture), null,
                Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "v9-parent", AgentDepth: 1));
            DrainAggregates(migrated);
            Assert.True(migrated.AcceptTokenSample("v9-later", live,
                DateTimeOffset.Parse("2026-01-01T00:02:00Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2026-01-01T00:02:00Z", CultureInfo.InvariantCulture),
                "turn-later", null, null));
            Assert.Equal(0L, migrated.GetSessionTotal("v9-later").Total);
            Assert.Equal(1L, migrated.GetSampleCount("v9-later"));
            Assert.Equal(24L, migrated.GetSessionTotal("v9-parent").Total);
            Assert.Equal(5L, migrated.GetSessionTotal("v9-child").Total);
            var afterLive = ReadLineageRows(databasePath);
            Assert.Equal(1, afterLive.Count(row => row.ThreadId == "v9-later"));
            Assert.True(afterLive.Single(row => row.ThreadId == "v9-later").Material.StartsWith(
                TokenUsageSnapshot.LineageSemanticPrefix + "|", StringComparison.Ordinal));
            Assert.Equal(0, afterLive.Single(row => row.ThreadId == "v9-later").Canonical);
            var zeroReplay = new TokenUsageSnapshot(
                new TokenComponents(20, 0, 0, 0, 4, 0, 24),
                new TokenComponents(10, 0, 0, 0, 2, 0, 12), 128000);
            Assert.True(migrated.AcceptTokenSample("v9-later", zeroReplay,
                DateTimeOffset.Parse("2026-01-01T00:02:01Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2026-01-01T00:02:01Z", CultureInfo.InvariantCulture),
                "turn-zero", null, null));
            Assert.Equal(12L, migrated.GetSessionTotal("v9-later").Total);
            Assert.Equal(2L, migrated.GetSampleCount("v9-later"));
            var afterZero = ReadLineageRows(databasePath);
            Assert.Equal(1, afterZero.Count(row => row.ThreadId == "v9-later" && row.Canonical == 1));
            Assert.Equal(1, afterZero.Count(row => row.ThreadId == "v9-later" && row.Canonical == 0));
            Assert.Equal(4, afterZero.Count(row => row.Canonical == 1));
            Assert.Equal(1, afterZero.Count(row => row.Canonical == 1 && row.ThreadId == "v9-later"));
            Assert.Equal(zeroReplay.SemanticIdentity(),
                afterZero.Single(row => row.ThreadId == "v9-later" && row.Canonical == 1).Identity);
            Assert.Equal(live.SemanticIdentity(),
                afterZero.Single(row => row.ThreadId == "v9-later" && row.Canonical == 0).Identity);
            Assert.Equal(0L, migrated.GetSessionTotal("v9-later", "turn-later").Total);
            Assert.Equal(12L, migrated.GetSessionTotal("v9-later", "turn-zero").Total);
            var mixedStart = DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture);
            var mixedNow = mixedStart.AddHours(1);
            Assert.Equal(41L, migrated.GetCycleTotal(mixedStart, mixedNow).Total);
            Assert.Equal(24L, migrated.GetCycleTotalForThreads(mixedStart, mixedNow, new[] { "v9-parent" }).Total);
            Assert.Equal(5L, migrated.GetCycleTotalForThreads(mixedStart, mixedNow, new[] { "v9-child" }).Total);
            Assert.Equal(12L, migrated.GetCycleTotalForThreads(mixedStart, mixedNow, new[] { "v9-later" }).Total);
            var mixedAggregates = migrated.LoadSessionAggregates(mixedNow);
            var mixedSnapshot = new HudSnapshot(
                new QuotaObservation(new QuotaBucket("codex", "Codex", 10, 10080, mixedNow.AddDays(7)),
                    Array.Empty<QuotaBucket>(), QuotaSource.OfficialAppServer, mixedNow, false),
                mixedAggregates, migrated.GetCycleTotal(mixedStart, mixedNow), mixedNow, false, "fresh",
                Array.Empty<string>(), Array.Empty<HudEvent>(),
                RunningCycleTotal: migrated.GetCycleTotalForThreads(mixedStart, mixedNow, new[] { "v9-parent" }));
            var mixedRows = HudPresentation.BuildRows(mixedSnapshot);
            Assert.Equal(41L, mixedRows.Single(row => row.ThreadId == "v9-parent").WorkTotal.Total);
            Assert.Equal(5L, mixedRows.Single(row => row.ThreadId == "v9-child").WorkTotal.Total);
            Assert.Equal(12L, mixedRows.Single(row => row.ThreadId == "v9-later").WorkTotal.Total);
            Assert.Equal("自身 24 + 子任务 17",
                mixedRows.Single(row => row.ThreadId == "v9-parent").WorkBreakdownText);
            Assert.True(HudPresentation.BuildCollapsedText(mixedSnapshot)
                .Contains("本周期 41 raw tokens", StringComparison.Ordinal));
            Assert.True(HudPresentation.BuildFrame(mixedSnapshot).OverviewText.Contains(
                "本额度周期全部会话合计：41 raw tokens", StringComparison.Ordinal));
            var mixedFlags = DescribeLineageFlags(databasePath);
            migrated.RebuildLineageCanonical();
            DrainAggregates(migrated);
            migrated.RebuildLineageCanonical();
            DrainAggregates(migrated);
            Assert.Equal(mixedFlags, DescribeLineageFlags(databasePath));
            Assert.Equal(24L, migrated.GetSessionTotal("v9-parent").Total);
            Assert.Equal(5L, migrated.GetSessionTotal("v9-child").Total);
            Assert.Equal(12L, migrated.GetSessionTotal("v9-later").Total);
            Assert.Equal(41L, migrated.GetCycleTotal(mixedStart, mixedNow).Total);
            AssertNonNegativeAggregates(databasePath);
        }

        using var restarted = new UsageDatabase(databasePath);
        DrainAggregates(restarted);
        Assert.Equal(24L, restarted.GetSessionTotal("v9-parent").Total);
        Assert.Equal(5L, restarted.GetSessionTotal("v9-child").Total);
        Assert.Equal(12L, restarted.GetSessionTotal("v9-later").Total);
        Assert.Equal(6L, restarted.GetSampleCount());
        restarted.RebuildLineageCanonical();
        DrainAggregates(restarted);
        Assert.Equal(24L, restarted.GetSessionTotal("v9-parent").Total);
        Assert.Equal(5L, restarted.GetSessionTotal("v9-child").Total);
        Assert.Equal(12L, restarted.GetSessionTotal("v9-later").Total);
        Assert.Equal(1, ReadLineageRows(databasePath).Count(row => row.ThreadId == "v9-later" && row.Canonical == 1));
        Assert.True(!restarted.ContainsPrivacySentinel(Sentinel));

        var extraPath = Path.Combine(directory, "alias-pair.db");
        CreateSchemaV9AliasPairFixture(extraPath);
        var t0 = DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture);
        using (var extra = new UsageDatabase(extraPath))
        {
            DrainAggregates(extra);
            Assert.Equal(12L, extra.GetSessionTotal("early-root").Total);
            Assert.Equal(12L, extra.GetSessionTotal("move-root").Total);
            extra.UpsertSession(new SessionMetadata("early-child", "Early child", null, null, null, null, null,
                null, t0.AddMinutes(2), null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "early-root", AgentDepth: 1));
            extra.UpsertSession(new SessionMetadata("move-child", "Move child", null, null, null, null, null,
                null, t0.AddMinutes(20), null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "move-root", AgentDepth: 1));
            DrainAggregates(extra);

            Assert.True(extra.AcceptTokenSample("early-child", missing, t0.AddMinutes(1), t0.AddMinutes(1),
                "turn-early-missing", null, null));
            Assert.Equal(0L, extra.GetSessionTotal("early-root").Total);
            Assert.Equal(12L, extra.GetSessionTotal("early-child").Total);
            Assert.Equal(12L, extra.GetSessionTotal("move-root").Total);
            Assert.True(extra.AcceptTokenSample("early-child", zeroed, t0.AddMinutes(2), t0.AddMinutes(2),
                "turn-early-zero", null, null));
            Assert.Equal(0L, extra.GetSessionTotal("early-root").Total);
            Assert.Equal(24L, extra.GetSessionTotal("early-child").Total);
            Assert.Equal(12L, extra.GetSessionTotal("move-root").Total);
            var earlyRows = ReadLineageRows(extraPath);
            Assert.Equal(0, earlyRows.Single(row => row.ThreadId == "early-root").Canonical);
            Assert.Equal(2, earlyRows.Count(row => row.ThreadId == "early-child" && row.Canonical == 1));
            Assert.Equal(1, earlyRows.Count(row => row.ThreadId == "move-root" && row.Canonical == 1));

            Assert.True(extra.AcceptTokenSample("move-child", zeroed, t0.AddMinutes(20), t0.AddMinutes(20),
                "turn-move-zero", null, null));
            Assert.Equal(12L, extra.GetSessionTotal("move-root").Total);
            Assert.Equal(0L, extra.GetSessionTotal("move-child").Total);
            Assert.Equal(0, ReadLineageRows(extraPath).Count(row => row.ThreadId == "move-child" && row.Canonical == 1));
            Assert.True(extra.AcceptTokenSample("move-child", missing, t0.AddMinutes(10), t0.AddMinutes(10),
                "turn-move-missing", null, null));
            Assert.Equal(12L, extra.GetSessionTotal("move-root").Total);
            Assert.Equal(12L, extra.GetSessionTotal("move-child").Total);
            Assert.Equal(0L, extra.GetSessionTotal("early-root").Total);
            Assert.Equal(24L, extra.GetSessionTotal("early-child").Total);
            var moved = ReadLineageRows(extraPath);
            Assert.Equal(1, moved.Count(row => row.ThreadId == "move-root" && row.Canonical == 1));
            Assert.Equal(1, moved.Count(row => row.ThreadId == "move-child" && row.Canonical == 1));
            Assert.Equal(1, moved.Count(row => row.ThreadId == "move-child" && row.Canonical == 0));
            Assert.Equal(zeroed.SemanticIdentity(),
                moved.Single(row => row.ThreadId == "move-child" && row.Canonical == 1).Identity);
            Assert.Equal(missing.SemanticIdentity(),
                moved.Single(row => row.ThreadId == "move-child" && row.Canonical == 0).Identity);
            Assert.Equal(2L, extra.GetSampleCount("move-child"));
            Assert.Equal(2L, extra.GetFingerprintCount("move-child"));
            var replayFlags = DescribeLineageFlags(extraPath);
            var replayChild = extra.GetSessionTotal("move-child").Total;
            var replayRoot = extra.GetSessionTotal("move-root").Total;
            var replayZeroTurn = extra.GetSessionTotal("move-child", "turn-move-zero").Total;
            var replayMissingTurn = extra.GetSessionTotal("move-child", "turn-move-missing").Total;
            Assert.True(!extra.AcceptTokenSample("move-child", missing, t0.AddMinutes(11), t0.AddMinutes(11),
                "turn-move-missing-dup", null, null));
            Assert.Equal(2L, extra.GetSampleCount("move-child"));
            Assert.Equal(2L, extra.GetFingerprintCount("move-child"));
            Assert.Equal(replayFlags, DescribeLineageFlags(extraPath));
            Assert.Equal(replayChild, extra.GetSessionTotal("move-child").Total);
            Assert.Equal(replayRoot, extra.GetSessionTotal("move-root").Total);
            Assert.Equal(replayZeroTurn, extra.GetSessionTotal("move-child", "turn-move-zero").Total);
            Assert.Equal(replayMissingTurn, extra.GetSessionTotal("move-child", "turn-move-missing").Total);
            Assert.Equal(12L, extra.GetSessionTotal("move-child").Total);
            Assert.Equal(12L, extra.GetSessionTotal("move-root").Total);
            Assert.Equal(1, ReadLineageRows(extraPath).Count(row => row.ThreadId == "move-child" && row.Canonical == 1));
            Assert.Equal(1, ReadLineageRows(extraPath).Count(row => row.ThreadId == "move-child" && row.Canonical == 0));

            Assert.True(missing.Fingerprint("move-sib") != missing.Fingerprint("move-child"));
            extra.UpsertSession(new SessionMetadata("move-sib", "Move sibling", null, null, null, null, null,
                null, t0.AddMinutes(11), null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "move-root", AgentDepth: 1));
            DrainAggregates(extra);
            Assert.True(extra.AcceptTokenSample("move-sib", missing, t0.AddMinutes(11), t0.AddMinutes(11),
                "turn-move-sib-missing", null, null));
            Assert.Equal(1L, extra.GetSampleCount("move-sib"));
            Assert.Equal(1L, extra.GetFingerprintCount("move-sib"));
            Assert.Equal(2L, extra.GetSampleCount("move-child"));
            Assert.Equal(0L, extra.GetSessionTotal("move-sib").Total);
            Assert.Equal(12L, extra.GetSessionTotal("move-child").Total);
            Assert.Equal(12L, extra.GetSessionTotal("move-root").Total);
            Assert.Equal(0L, extra.GetSessionTotal("early-root").Total);
            Assert.Equal(24L, extra.GetSessionTotal("early-child").Total);
            var afterSibling = ReadLineageRows(extraPath);
            var sibling = afterSibling.Single(row => row.ThreadId == "move-sib");
            Assert.Equal(0, sibling.Canonical);
            Assert.Equal("move-root", sibling.Root);
            Assert.Equal(missing.SemanticIdentity(), sibling.Identity);
            Assert.Equal(2, afterSibling.Count(row =>
                row.Root == "move-root" && row.Identity == missing.SemanticIdentity()));
            Assert.Equal(0, afterSibling.Count(row =>
                row.Root == "move-root" && row.Identity == missing.SemanticIdentity() && row.Canonical == 1));
            Assert.Equal(1, afterSibling.Count(row => row.ThreadId == "move-child" && row.Canonical == 1));
            Assert.Equal(zeroed.SemanticIdentity(),
                afterSibling.Single(row => row.ThreadId == "move-child" && row.Canonical == 1).Identity);
            Assert.Equal(missing.SemanticIdentity(),
                afterSibling.Single(row => row.ThreadId == "move-child" && row.Canonical == 0).Identity);
            Assert.True(afterSibling.Single(row => row.ThreadId == "move-child" && row.Canonical == 0).Id <
                        sibling.Id);
            Assert.Equal(1, afterSibling.Count(row => row.Root == "move-root" && row.Canonical == 1 &&
                row.Material.StartsWith(TokenUsageSnapshot.LineageSemanticPrefix + "|", StringComparison.Ordinal)));
            Assert.Equal(1, afterSibling.Count(row => row.Root == "move-root" && row.Canonical == 1 &&
                row.Material.StartsWith(TokenUsageSnapshot.LineageSemanticLegacyPrefix + "|",
                    StringComparison.Ordinal)));
            Assert.Equal(0L, extra.GetSessionTotal("move-sib", "turn-move-sib-missing").Total);
            Assert.Equal(12L, extra.GetSessionTotal("move-child", "turn-move-zero").Total);
            Assert.Equal(0L, extra.GetSessionTotal("move-child", "turn-move-missing").Total);
            AssertNonNegativeAggregates(extraPath);

            var pairNow = t0.AddHours(1);
            Assert.Equal(48L, extra.GetCycleTotal(t0, pairNow).Total);
            Assert.Equal(24L, extra.GetCycleTotalForThreads(t0, pairNow, new[] { "early-child" }).Total);
            Assert.Equal(12L, extra.GetCycleTotalForThreads(t0, pairNow, new[] { "move-root" }).Total);
            Assert.Equal(12L, extra.GetCycleTotalForThreads(t0, pairNow, new[] { "move-child" }).Total);
            Assert.Equal(0L, extra.GetCycleTotalForThreads(t0, pairNow, new[] { "move-sib" }).Total);
            var pairAggregates = extra.LoadSessionAggregates(pairNow);
            var pairSnapshot = new HudSnapshot(
                new QuotaObservation(new QuotaBucket("codex", "Codex", 10, 10080, pairNow.AddDays(7)),
                    Array.Empty<QuotaBucket>(), QuotaSource.OfficialAppServer, pairNow, false),
                pairAggregates, extra.GetCycleTotal(t0, pairNow), pairNow, false, "fresh",
                Array.Empty<string>(), Array.Empty<HudEvent>(),
                RunningCycleTotal: extra.GetCycleTotalForThreads(t0, pairNow, new[] { "early-root" }));
            var pairRows = HudPresentation.BuildRows(pairSnapshot);
            Assert.Equal(24L, pairRows.Single(row => row.ThreadId == "early-root").WorkTotal.Total);
            Assert.Equal(24L, pairRows.Single(row => row.ThreadId == "early-child").WorkTotal.Total);
            Assert.Equal(24L, pairRows.Single(row => row.ThreadId == "move-root").WorkTotal.Total);
            Assert.Equal(12L, pairRows.Single(row => row.ThreadId == "move-child").WorkTotal.Total);
            Assert.Equal(0L, pairRows.Single(row => row.ThreadId == "move-sib").WorkTotal.Total);
            Assert.Equal("自身 0 + 子任务 24",
                pairRows.Single(row => row.ThreadId == "early-root").WorkBreakdownText);
            Assert.Equal("自身 12 + 子任务 12",
                pairRows.Single(row => row.ThreadId == "move-root").WorkBreakdownText);
            Assert.True(HudPresentation.BuildCollapsedText(pairSnapshot)
                .Contains("本周期 48 raw tokens", StringComparison.Ordinal));

            var extraFlags = DescribeLineageFlags(extraPath);
            extra.RebuildLineageCanonical();
            DrainAggregates(extra);
            extra.RebuildLineageCanonical();
            DrainAggregates(extra);
            Assert.Equal(extraFlags, DescribeLineageFlags(extraPath));
            Assert.Equal(0L, extra.GetSessionTotal("early-root").Total);
            Assert.Equal(24L, extra.GetSessionTotal("early-child").Total);
            Assert.Equal(12L, extra.GetSessionTotal("move-root").Total);
            Assert.Equal(12L, extra.GetSessionTotal("move-child").Total);
            Assert.Equal(0L, extra.GetSessionTotal("move-sib").Total);
            Assert.Equal(48L, extra.GetCycleTotal(t0, pairNow).Total);
            AssertNonNegativeAggregates(extraPath);

            extra.UpsertSession(new SessionMetadata("other-root", "Other", null, null, null, null, null, null,
                t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            extra.UpsertSession(new SessionMetadata("early-child", "Early child", null, null, null, null, null,
                null, t0.AddMinutes(2), null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "other-root", AgentDepth: 1));
            DrainAggregates(extra);
            Assert.Equal(12L, extra.GetSessionTotal("early-root").Total);
            Assert.Equal(24L, extra.GetSessionTotal("early-child").Total);
            Assert.Equal(12L, extra.GetSessionTotal("move-root").Total);
            Assert.Equal(12L, extra.GetSessionTotal("move-child").Total);
            Assert.Equal(0L, extra.GetSessionTotal("move-sib").Total);
            var reparented = ReadLineageRows(extraPath);
            Assert.Equal(1, reparented.Single(row => row.ThreadId == "early-root").Canonical);
            Assert.Equal(2, reparented.Count(row => row.ThreadId == "early-child" && row.Canonical == 1));
            Assert.True(reparented.Where(row => row.ThreadId == "early-child").All(row => row.Root == "other-root"));
            Assert.True(reparented.Where(row => row.ThreadId == "move-child" || row.ThreadId == "move-root" ||
                    row.ThreadId == "move-sib")
                .All(row => row.Root == "move-root"));
            var reparentSnapshot = new HudSnapshot(
                new QuotaObservation(new QuotaBucket("codex", "Codex", 10, 10080, pairNow.AddDays(7)),
                    Array.Empty<QuotaBucket>(), QuotaSource.OfficialAppServer, pairNow, false),
                extra.LoadSessionAggregates(pairNow), extra.GetCycleTotal(t0, pairNow), pairNow, false, "fresh",
                Array.Empty<string>(), Array.Empty<HudEvent>(),
                RunningCycleTotal: extra.GetCycleTotalForThreads(t0, pairNow, new[] { "other-root" }));
            var reparentRows = HudPresentation.BuildRows(reparentSnapshot);
            Assert.Equal(12L, reparentRows.Single(row => row.ThreadId == "early-root").WorkTotal.Total);
            Assert.Equal(24L, reparentRows.Single(row => row.ThreadId == "other-root").WorkTotal.Total);
            Assert.Equal(24L, reparentRows.Single(row => row.ThreadId == "move-root").WorkTotal.Total);
            Assert.Equal(60L, extra.GetCycleTotal(t0, pairNow).Total);

            extra.UpsertSession(new SessionMetadata("early-child", "Early child", null, null, null, null, null,
                null, t0.AddMinutes(2), null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "early-root", AgentDepth: 1));
            DrainAggregates(extra);
            Assert.Equal(0L, extra.GetSessionTotal("early-root").Total);
            Assert.Equal(24L, extra.GetSessionTotal("early-child").Total);
            Assert.Equal(12L, extra.GetSessionTotal("move-root").Total);
            Assert.Equal(12L, extra.GetSessionTotal("move-child").Total);
            Assert.Equal(0L, extra.GetSessionTotal("move-sib").Total);
            Assert.Equal(0, ReadLineageRows(extraPath).Single(row => row.ThreadId == "early-root").Canonical);
            Assert.Equal(2, ReadLineageRows(extraPath).Count(row => row.ThreadId == "early-child" && row.Canonical == 1));
            AssertNonNegativeAggregates(extraPath);
            Assert.True(!extra.ContainsPrivacySentinel(Sentinel));
        }

        using var extraRestart = new UsageDatabase(extraPath);
        DrainAggregates(extraRestart);
        extraRestart.RebuildLineageCanonical();
        DrainAggregates(extraRestart);
        Assert.Equal(0L, extraRestart.GetSessionTotal("early-root").Total);
        Assert.Equal(24L, extraRestart.GetSessionTotal("early-child").Total);
        Assert.Equal(12L, extraRestart.GetSessionTotal("move-root").Total);
        Assert.Equal(12L, extraRestart.GetSessionTotal("move-child").Total);
        Assert.Equal(0L, extraRestart.GetSessionTotal("move-sib").Total);
        Assert.Equal(48L, extraRestart.GetCycleTotal(t0, t0.AddHours(1)).Total);
        Assert.Equal(0, ReadLineageRows(extraPath).Single(row => row.ThreadId == "early-root").Canonical);
        Assert.Equal(2, ReadLineageRows(extraPath).Count(row => row.ThreadId == "early-child" && row.Canonical == 1));
        Assert.Equal(1, ReadLineageRows(extraPath).Count(row => row.ThreadId == "move-child" && row.Canonical == 1));
        Assert.Equal(0, ReadLineageRows(extraPath).Single(row => row.ThreadId == "move-sib").Canonical);
        Assert.Equal(zeroed.SemanticIdentity(), ReadLineageRows(extraPath)
            .Single(row => row.ThreadId == "move-child" && row.Canonical == 1).Identity);
        AssertNonNegativeAggregates(extraPath);
        Assert.True(!extraRestart.ContainsPrivacySentinel(Sentinel));
        AssertSchemaV9ExplicitBoundaryMigration(runRoot);
    }

    private static void AssertSchemaV9ExplicitBoundaryMigration(string runRoot)
    {
        var directory = Path.Combine(runRoot, "v9-explicit-context");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "usage.db");
        CreateSchemaV9ExplicitContextFixture(databasePath);
        Assert.True(!File.Exists(Path.Combine(directory, "sessions")));
        const string threadId = "v9-explicit-context";
        const long window = 258_400;
        const long start = 1_786_320_000;
        SessionContextMetrics Capture(UsageDatabase database)
        {
            DrainAggregates(database);
            return database.LoadSessionContextMetrics()[threadId];
        }

        SessionContextMetrics first;
        using (var migrated = new UsageDatabase(databasePath))
        {
            first = Capture(migrated);
            AssertExplicitContinuationContract(first, window);
            Assert.Equal(3L, CountContextBaselines(databasePath, threadId, "explicit"));
            Assert.Equal(0L, CountContextBaselines(databasePath, threadId, "heuristic"));
            migrated.RebuildLineageCanonical();
            var second = Capture(migrated);
            AssertExplicitContinuationContract(second, window);
            migrated.RebuildLineageCanonical();
            var third = Capture(migrated);
            AssertExplicitContinuationContract(third, window);
            Assert.Equal(3L, CountContextBaselines(databasePath, threadId, "explicit"));
            Assert.Equal(first.PostCompactionInputTokens, third.PostCompactionInputTokens);
            Assert.Equal(first.PostCompactionSampleCount, third.PostCompactionSampleCount);
            Assert.Equal(first.BaselineTrendPercentagePoints, third.BaselineTrendPercentagePoints);
            AssertNonNegativeAggregates(databasePath);
            Assert.True(!migrated.ContainsPrivacySentinel(Sentinel));
        }

        using var restarted = new UsageDatabase(databasePath);
        var restored = Capture(restarted);
        AssertExplicitContinuationContract(restored, window);
        Assert.Equal(first.PostCompactionInputTokens, restored.PostCompactionInputTokens);
        Assert.Equal(3L, CountContextBaselines(databasePath, threadId, "explicit"));
        var row = ContinuationRow(threadId, restored, DateTimeOffset.FromUnixTimeSeconds(start + 122));
        Assert.Equal("B-", row.ContinuationGradeText);
        Assert.Equal("建议当前完整工作包结束后续接", row.ContinuationAdviceText);
        Assert.True(row.PostCompactionSourceText.Contains("官方压缩边界", StringComparison.Ordinal));
        Assert.True(!restarted.ContainsPrivacySentinel(Sentinel));
    }

    private static void AssertExplicitContinuationContract(SessionContextMetrics metrics, long window)
    {
        Assert.Equal(120_000L, metrics.PostCompactionInputTokens);
        Assert.Equal(window, metrics.PostCompactionWindowTokens);
        Assert.Equal(3, metrics.PostCompactionSampleCount);
        Assert.True(metrics.UsesExplicitCompactionBoundaries);
        Assert.Near(46.439, metrics.PostCompactionPercent!.Value, 0.01);
        Assert.Near(10.449, metrics.BaselineTrendPercentagePoints!.Value, 0.01);
        Assert.Near(-50, metrics.TurnRunwayChangePercent!.Value, 0.01);
        Assert.Near(-27.273, metrics.TokenRunwayChangePercent!.Value, 0.01);
    }

    private static SessionDisplayRow ContinuationRow(string threadId, SessionContextMetrics metrics,
        DateTimeOffset activity) =>
        new(threadId, "上下文测试", threadId[..Math.Min(12, threadId.Length)], "lead",
            "test", "gpt-test", "Standard", "运行中", "08-10 08:00:00", activity,
            Usage(1), "最近一轮", "reliable-turn", Usage(2_100_000_000), true, false,
            true, SessionKind.Primary, metrics);

    private static void CreateSchemaV9ExplicitContextFixture(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE schema_info(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE event_fingerprints(
                fingerprint TEXT PRIMARY KEY, thread_id TEXT NOT NULL, first_seen_utc TEXT NOT NULL,
                last_seen_utc TEXT NOT NULL, duplicate_count INTEGER NOT NULL DEFAULT 0,
                disposition TEXT NOT NULL DEFAULT 'accepted', diagnostic_code TEXT);
            CREATE TABLE token_samples(
                id INTEGER PRIMARY KEY AUTOINCREMENT, fingerprint TEXT NOT NULL UNIQUE,
                thread_id TEXT NOT NULL, input_tokens INTEGER NOT NULL, raw_input_tokens INTEGER NOT NULL,
                cached_input_tokens INTEGER NOT NULL, cache_write_input_tokens INTEGER NOT NULL,
                output_tokens INTEGER NOT NULL, reasoning_output_tokens INTEGER NOT NULL,
                cumulative_input_tokens INTEGER NOT NULL, cumulative_output_tokens INTEGER NOT NULL,
                cumulative_total_tokens INTEGER NOT NULL, canonical_total_tokens INTEGER NOT NULL,
                reported_total_tokens INTEGER NOT NULL, event_time_utc TEXT, event_time_ticks INTEGER,
                observed_at_utc TEXT NOT NULL, source_key TEXT, source_generation INTEGER NOT NULL DEFAULT 0,
                source_offset INTEGER, turn_key TEXT, model TEXT, service_tier TEXT,
                confidence TEXT NOT NULL, turn_confidence TEXT NOT NULL DEFAULT 'unavailable',
                event_order_confidence TEXT NOT NULL DEFAULT 'unavailable', context_window INTEGER);
            CREATE TABLE sessions(
                thread_id TEXT PRIMARY KEY, display_name TEXT, role TEXT, nickname TEXT, project_tag TEXT,
                model TEXT, reasoning_effort TEXT, service_tier TEXT,
                service_tier_source TEXT NOT NULL DEFAULT 'unavailable', last_activity_utc TEXT,
                status TEXT NOT NULL, current_turn_key TEXT, current_turn_reliable INTEGER NOT NULL DEFAULT 0,
                turn_sequence INTEGER NOT NULL DEFAULT 0, turn_open INTEGER NOT NULL DEFAULT 0,
                pinned INTEGER NOT NULL DEFAULT 0, session_kind TEXT NOT NULL DEFAULT 'Unknown',
                session_surface TEXT NOT NULL DEFAULT 'Unknown', parent_thread_id TEXT, agent_depth INTEGER);
            CREATE TABLE structural_events (
                event_identity TEXT PRIMARY KEY, base_identity TEXT NOT NULL, thread_id TEXT NOT NULL,
                event_kind TEXT NOT NULL, association_key TEXT, event_time_utc TEXT,
                source_identity_hash TEXT NOT NULL, source_generation INTEGER NOT NULL,
                source_offset INTEGER NOT NULL, occurrence INTEGER NOT NULL);
            INSERT INTO schema_info(key, value) VALUES
                ('version', '9'), ('aggregate_schema_version', '5'),
                ('windows_file_id_128', '1'), ('token_tuple_oracle_migration', '2'),
                ('deterministic_turn_keys', '1'), ('sample_source_offsets', '1'),
                ('context_capture_version', '2'), ('private_source_identity_v2', 'logical_complete'),
                ('private_source_scrub_v2', 'complete');
            INSERT INTO sessions(thread_id, display_name, last_activity_utc, status, session_kind,
                session_surface, model)
            VALUES ('v9-explicit-context', 'Explicit', '2026-08-10T00:02:02.0000000+00:00', 'Idle',
                'Primary', 'App', 'gpt-test');
            """;
        command.ExecuteNonQuery();

        const string threadId = "v9-explicit-context";
        const string sourceKey = "win-v2:v9-explicit-01";
        const long window = 258_400;
        const long start = 1_786_320_000;
        void InsertSample(string fingerprint, long lastIn, long cumIn, long unix, long offset, string turn)
        {
            var time = DateTimeOffset.FromUnixTimeSeconds(unix);
            var eventTime = time.ToString("O");
            using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO event_fingerprints(fingerprint, thread_id, first_seen_utc, last_seen_utc, duplicate_count, disposition)
                VALUES ($fingerprint, $thread, $observed, $observed, 0, 'accepted');
                INSERT INTO token_samples(fingerprint, thread_id, input_tokens, raw_input_tokens,
                    cached_input_tokens, cache_write_input_tokens, output_tokens, reasoning_output_tokens,
                    cumulative_input_tokens, cumulative_output_tokens, cumulative_total_tokens,
                    canonical_total_tokens, reported_total_tokens, event_time_utc, event_time_ticks,
                    observed_at_utc, source_key, source_generation, source_offset, turn_key, model, confidence,
                    turn_confidence, event_order_confidence, context_window)
                VALUES ($fingerprint, $thread, $last_in, $last_in, 0, 0, 0, 0,
                    $cum_in, 0, $cum_in, $last_in, $last_in, $event_time, $ticks,
                    $observed, $source, 0, $offset, $turn, 'gpt-test', 'trusted', 'reliable', 'reliable', $window);
                """;
            insert.Parameters.AddWithValue("$fingerprint", fingerprint);
            insert.Parameters.AddWithValue("$thread", threadId);
            insert.Parameters.AddWithValue("$last_in", lastIn);
            insert.Parameters.AddWithValue("$cum_in", cumIn);
            insert.Parameters.AddWithValue("$event_time", eventTime);
            insert.Parameters.AddWithValue("$ticks", time.UtcTicks);
            insert.Parameters.AddWithValue("$observed", eventTime);
            insert.Parameters.AddWithValue("$source", sourceKey);
            insert.Parameters.AddWithValue("$offset", offset);
            insert.Parameters.AddWithValue("$turn", turn);
            insert.Parameters.AddWithValue("$window", window);
            insert.ExecuteNonQuery();
        }

        void InsertCompact(string identity, long unix, long offset, string turn)
        {
            var time = DateTimeOffset.FromUnixTimeSeconds(unix);
            using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO structural_events(event_identity, base_identity, thread_id, event_kind,
                    association_key, event_time_utc, source_identity_hash, source_generation,
                    source_offset, occurrence)
                VALUES ($id, $id, $thread, 'event_msg:context_compacted', $turn, $event_time,
                    $source, 0, $offset, 1);
                """;
            insert.Parameters.AddWithValue("$id", identity);
            insert.Parameters.AddWithValue("$thread", threadId);
            insert.Parameters.AddWithValue("$turn", turn);
            insert.Parameters.AddWithValue("$event_time", time.ToString("O"));
            insert.Parameters.AddWithValue("$source", sourceKey);
            insert.Parameters.AddWithValue("$offset", offset);
            insert.ExecuteNonQuery();
        }

        InsertCompact("v9-compact-1", start + 1, 10, "turn-1");
        InsertSample(new string('1', 64), 105_000, 1_105_000, start + 2, 20, "turn-1");
        InsertSample(new string('2', 64), 60_000, 1_165_000, start + 21, 40, "turn-2");
        InsertCompact("v9-compact-2", start + 61, 50, "turn-3");
        InsertSample(new string('3', 64), 120_000, 1_285_000, start + 62, 60, "turn-3");
        InsertCompact("v9-compact-3", start + 121, 70, "turn-4");
        InsertSample(new string('4', 64), 132_000, 1_417_000, start + 122, 80, "turn-4");
    }

    private static void CreateSchemaV9LineageFixture(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE schema_info(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE event_fingerprints(
                fingerprint TEXT PRIMARY KEY, thread_id TEXT NOT NULL, first_seen_utc TEXT NOT NULL,
                last_seen_utc TEXT NOT NULL, duplicate_count INTEGER NOT NULL DEFAULT 0,
                disposition TEXT NOT NULL DEFAULT 'accepted', diagnostic_code TEXT);
            CREATE TABLE token_samples(
                id INTEGER PRIMARY KEY AUTOINCREMENT, fingerprint TEXT NOT NULL UNIQUE,
                thread_id TEXT NOT NULL, input_tokens INTEGER NOT NULL, raw_input_tokens INTEGER NOT NULL,
                cached_input_tokens INTEGER NOT NULL, cache_write_input_tokens INTEGER NOT NULL,
                output_tokens INTEGER NOT NULL, reasoning_output_tokens INTEGER NOT NULL,
                cumulative_input_tokens INTEGER NOT NULL, cumulative_output_tokens INTEGER NOT NULL,
                cumulative_total_tokens INTEGER NOT NULL, canonical_total_tokens INTEGER NOT NULL,
                reported_total_tokens INTEGER NOT NULL, event_time_utc TEXT, event_time_ticks INTEGER,
                observed_at_utc TEXT NOT NULL, source_key TEXT, source_generation INTEGER NOT NULL DEFAULT 0,
                source_offset INTEGER, turn_key TEXT, model TEXT, service_tier TEXT,
                confidence TEXT NOT NULL, turn_confidence TEXT NOT NULL DEFAULT 'unavailable',
                event_order_confidence TEXT NOT NULL DEFAULT 'unavailable', context_window INTEGER);
            CREATE TABLE sessions(
                thread_id TEXT PRIMARY KEY, display_name TEXT, role TEXT, nickname TEXT, project_tag TEXT,
                model TEXT, reasoning_effort TEXT, service_tier TEXT,
                service_tier_source TEXT NOT NULL DEFAULT 'unavailable', last_activity_utc TEXT,
                status TEXT NOT NULL, current_turn_key TEXT, current_turn_reliable INTEGER NOT NULL DEFAULT 0,
                turn_sequence INTEGER NOT NULL DEFAULT 0, turn_open INTEGER NOT NULL DEFAULT 0,
                pinned INTEGER NOT NULL DEFAULT 0, session_kind TEXT NOT NULL DEFAULT 'Unknown',
                session_surface TEXT NOT NULL DEFAULT 'Unknown', parent_thread_id TEXT, agent_depth INTEGER);
            INSERT INTO schema_info(key, value) VALUES
                ('version', '9'), ('aggregate_schema_version', '5'),
                ('windows_file_id_128', '1'), ('token_tuple_oracle_migration', '2'),
                ('deterministic_turn_keys', '1'), ('sample_source_offsets', '1'),
                ('context_capture_version', '2'), ('private_source_identity_v2', 'logical_complete'),
                ('private_source_scrub_v2', 'complete');
            INSERT INTO sessions(thread_id, display_name, last_activity_utc, status, session_kind,
                session_surface, parent_thread_id, agent_depth)
            VALUES
                ('v9-parent', 'Parent', '2026-01-01T00:00:00.0000000+00:00', 'Idle', 'Primary', 'App', NULL, NULL),
                ('v9-child', 'Child', '2026-01-01T00:01:00.0000000+00:00', 'Idle', 'InternalTask',
                    'InternalTask', 'v9-parent', 1);
            """;
        command.ExecuteNonQuery();

        void InsertSample(string threadId, string fingerprint, long lastIn, long lastOut, long cumIn, long cumOut,
            string eventTime, long ticks, string turnKey, long offset)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO event_fingerprints(fingerprint, thread_id, first_seen_utc, last_seen_utc, duplicate_count, disposition)
                VALUES ($fingerprint, $thread, $observed, $observed, 0, 'accepted');
                INSERT INTO token_samples(fingerprint, thread_id, input_tokens, raw_input_tokens,
                    cached_input_tokens, cache_write_input_tokens, output_tokens, reasoning_output_tokens,
                    cumulative_input_tokens, cumulative_output_tokens, cumulative_total_tokens,
                    canonical_total_tokens, reported_total_tokens, event_time_utc, event_time_ticks,
                    observed_at_utc, source_key, source_generation, source_offset, turn_key, confidence,
                    turn_confidence, event_order_confidence, context_window)
                VALUES ($fingerprint, $thread, $last_in, $last_in, 0, 0, $last_out, 0,
                    $cum_in, $cum_out, $cum_total, $last_total, $last_total, $event_time, $ticks,
                    $observed, $source, 0, $offset, $turn, 'trusted', 'reliable', 'reliable', 128000);
                """;
            insert.Parameters.AddWithValue("$fingerprint", fingerprint);
            insert.Parameters.AddWithValue("$thread", threadId);
            insert.Parameters.AddWithValue("$last_in", lastIn);
            insert.Parameters.AddWithValue("$last_out", lastOut);
            insert.Parameters.AddWithValue("$cum_in", cumIn);
            insert.Parameters.AddWithValue("$cum_out", cumOut);
            insert.Parameters.AddWithValue("$cum_total", cumIn + cumOut);
            insert.Parameters.AddWithValue("$last_total", lastIn + lastOut);
            insert.Parameters.AddWithValue("$event_time", eventTime);
            insert.Parameters.AddWithValue("$ticks", ticks);
            insert.Parameters.AddWithValue("$observed", eventTime);
            insert.Parameters.AddWithValue("$source", "win-v2:" + fingerprint[..16]);
            insert.Parameters.AddWithValue("$offset", offset);
            insert.Parameters.AddWithValue("$turn", turnKey);
            insert.ExecuteNonQuery();
        }

        var t0 = DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture);
        var t1 = t0.AddMinutes(1);
        InsertSample("v9-parent", new string('a', 64), 10, 2, 10, 2, t0.ToString("O"), t0.UtcTicks, "turn-p", 10);
        InsertSample("v9-parent", new string('b', 64), 10, 2, 20, 4, t0.AddSeconds(30).ToString("O"),
            t0.AddSeconds(30).UtcTicks, "turn-p", 20);
        InsertSample("v9-child", new string('c', 64), 10, 2, 20, 4, t1.ToString("O"), t1.UtcTicks, "turn-c", 10);
        InsertSample("v9-child", new string('d', 64), 5, 0, 25, 4, t1.AddSeconds(10).ToString("O"),
            t1.AddSeconds(10).UtcTicks, "turn-c", 20);
    }

    private static void CreateSchemaV9AliasPairFixture(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE schema_info(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE event_fingerprints(
                fingerprint TEXT PRIMARY KEY, thread_id TEXT NOT NULL, first_seen_utc TEXT NOT NULL,
                last_seen_utc TEXT NOT NULL, duplicate_count INTEGER NOT NULL DEFAULT 0,
                disposition TEXT NOT NULL DEFAULT 'accepted', diagnostic_code TEXT);
            CREATE TABLE token_samples(
                id INTEGER PRIMARY KEY AUTOINCREMENT, fingerprint TEXT NOT NULL UNIQUE,
                thread_id TEXT NOT NULL, input_tokens INTEGER NOT NULL, raw_input_tokens INTEGER NOT NULL,
                cached_input_tokens INTEGER NOT NULL, cache_write_input_tokens INTEGER NOT NULL,
                output_tokens INTEGER NOT NULL, reasoning_output_tokens INTEGER NOT NULL,
                cumulative_input_tokens INTEGER NOT NULL, cumulative_output_tokens INTEGER NOT NULL,
                cumulative_total_tokens INTEGER NOT NULL, canonical_total_tokens INTEGER NOT NULL,
                reported_total_tokens INTEGER NOT NULL, event_time_utc TEXT, event_time_ticks INTEGER,
                observed_at_utc TEXT NOT NULL, source_key TEXT, source_generation INTEGER NOT NULL DEFAULT 0,
                source_offset INTEGER, turn_key TEXT, model TEXT, service_tier TEXT,
                confidence TEXT NOT NULL, turn_confidence TEXT NOT NULL DEFAULT 'unavailable',
                event_order_confidence TEXT NOT NULL DEFAULT 'unavailable', context_window INTEGER);
            CREATE TABLE sessions(
                thread_id TEXT PRIMARY KEY, display_name TEXT, role TEXT, nickname TEXT, project_tag TEXT,
                model TEXT, reasoning_effort TEXT, service_tier TEXT,
                service_tier_source TEXT NOT NULL DEFAULT 'unavailable', last_activity_utc TEXT,
                status TEXT NOT NULL, current_turn_key TEXT, current_turn_reliable INTEGER NOT NULL DEFAULT 0,
                turn_sequence INTEGER NOT NULL DEFAULT 0, turn_open INTEGER NOT NULL DEFAULT 0,
                pinned INTEGER NOT NULL DEFAULT 0, session_kind TEXT NOT NULL DEFAULT 'Unknown',
                session_surface TEXT NOT NULL DEFAULT 'Unknown', parent_thread_id TEXT, agent_depth INTEGER);
            INSERT INTO schema_info(key, value) VALUES
                ('version', '9'), ('aggregate_schema_version', '5'),
                ('windows_file_id_128', '1'), ('token_tuple_oracle_migration', '2'),
                ('deterministic_turn_keys', '1'), ('sample_source_offsets', '1'),
                ('context_capture_version', '2'), ('private_source_identity_v2', 'logical_complete'),
                ('private_source_scrub_v2', 'complete');
            INSERT INTO sessions(thread_id, display_name, last_activity_utc, status, session_kind,
                session_surface, parent_thread_id, agent_depth)
            VALUES
                ('early-root', 'Early', '2026-01-01T00:10:00.0000000+00:00', 'Idle', 'Primary', 'App', NULL, NULL),
                ('move-root', 'Move', '2026-01-01T00:00:00.0000000+00:00', 'Idle', 'Primary', 'App', NULL, NULL);
            """;
        command.ExecuteNonQuery();

        void InsertSample(string threadId, string fingerprint, string eventTime, long ticks)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO event_fingerprints(fingerprint, thread_id, first_seen_utc, last_seen_utc, duplicate_count, disposition)
                VALUES ($fingerprint, $thread, $observed, $observed, 0, 'accepted');
                INSERT INTO token_samples(fingerprint, thread_id, input_tokens, raw_input_tokens,
                    cached_input_tokens, cache_write_input_tokens, output_tokens, reasoning_output_tokens,
                    cumulative_input_tokens, cumulative_output_tokens, cumulative_total_tokens,
                    canonical_total_tokens, reported_total_tokens, event_time_utc, event_time_ticks,
                    observed_at_utc, source_key, source_generation, source_offset, turn_key, confidence,
                    turn_confidence, event_order_confidence, context_window)
                VALUES ($fingerprint, $thread, 10, 10, 0, 0, 2, 0,
                    10, 2, 12, 12, 12, $event_time, $ticks,
                    $observed, $source, 0, 10, 'turn-p', 'trusted', 'reliable', 'reliable', 128000);
                """;
            insert.Parameters.AddWithValue("$fingerprint", fingerprint);
            insert.Parameters.AddWithValue("$thread", threadId);
            insert.Parameters.AddWithValue("$event_time", eventTime);
            insert.Parameters.AddWithValue("$ticks", ticks);
            insert.Parameters.AddWithValue("$observed", eventTime);
            insert.Parameters.AddWithValue("$source", "win-v2:" + fingerprint[..16]);
            insert.ExecuteNonQuery();
        }

        var early = DateTimeOffset.Parse("2026-01-01T00:10:00Z", CultureInfo.InvariantCulture);
        var move = DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture);
        InsertSample("early-root", new string('e', 64), early.ToString("O"), early.UtcTicks);
        InsertSample("move-root", new string('f', 64), move.ToString("O"), move.UtcTicks);
    }

    private static void AssertCommitScanLateParentMaterialization(string runRoot)
    {
        var directory = Path.Combine(runRoot, "lineage-timing-commit-scan");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "usage.db");
        var cycleStart = DateTimeOffset.Parse("2026-05-01T01:00:00Z", CultureInfo.InvariantCulture);
        var cycleEnd = cycleStart.AddHours(1);
        var ancestor = LineageSnapshot(10, 2, 10, 2);
        var tail = LineageSnapshot(13, 3, 3, 1);
        var parentUnix = cycleStart.AddMinutes(1).ToUnixTimeSeconds();
        var parentPath = Path.Combine(directory, "scan-parent.jsonl");
        File.WriteAllText(parentPath, TokenLine("scan-parent", parentUnix, 10, 2, 10, 2) + "\n",
            new UTF8Encoding(false));
        var unrelatedPath = Path.Combine(directory, "unrelated-root.jsonl");
        File.WriteAllText(unrelatedPath, TokenLine("unrelated-root", parentUnix + 30, 20, 1, 20, 1) + "\n",
            new UTF8Encoding(false));

        using (var database = new UsageDatabase(databasePath))
        {
            database.UpsertSession(new SessionMetadata("scan-child", "ScanChild", null, null, null, null, null,
                null, cycleStart, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "scan-parent", AgentDepth: 1));
            Assert.True(database.AcceptTokenSample("scan-child", ancestor, cycleStart.AddMinutes(8),
                cycleStart.AddMinutes(8), "turn-late", null, null));
            Assert.True(database.AcceptTokenSample("scan-child", tail, cycleStart.AddMinutes(9),
                cycleStart.AddMinutes(9), "turn-late", null, null));
            Assert.Equal(16L, database.GetSessionTotal("scan-child").Total);
            Assert.True(database.IsAggregateRebuildComplete);

            var indexer = new RolloutIndexer(database);
            var parentScan = indexer.ScanFile(parentPath, "sessions/scan-parent.jsonl", "scan-parent");
            Assert.Equal(1, parentScan.AcceptedSamples);
            Assert.True(!database.IsAggregateRebuildComplete);
            var pending = new HudSnapshot(
                new QuotaObservation(new QuotaBucket("codex", "Codex", 10, 10080, cycleEnd.AddDays(6)),
                    Array.Empty<QuotaBucket>(), QuotaSource.OfficialAppServer, cycleStart, false),
                Array.Empty<SessionAggregate>(), null, cycleStart, true, "索引中（统计迁移）",
                Array.Empty<string>(), AggregateMigrationPending: true);
            Assert.True(HudPresentation.BuildCollapsedText(pending).Contains("索引中", StringComparison.Ordinal));
            Assert.True(HudPresentation.BuildFrame(pending).FreshnessText.Contains("索引中", StringComparison.Ordinal));
            DrainAggregates(database);

            Assert.Equal(12L, database.GetSessionTotal("scan-parent").Total);
            Assert.Equal(4L, database.GetSessionTotal("scan-child").Total);
            Assert.Equal(4L, database.GetSessionTotal("scan-child", "turn-late").Total);
            Assert.Equal(16L, database.GetCycleTotal(cycleStart, cycleEnd).Total);
            Assert.Equal(12L, database.GetCycleTotalForThreads(cycleStart, cycleEnd, new[] { "scan-parent" }).Total);
            Assert.Equal(4L, database.GetCycleTotalForThreads(cycleStart, cycleEnd, new[] { "scan-child" }).Total);
            Assert.Equal(12L, ReadFrontierMaximum(databasePath, "scan-parent"));
            Assert.Equal(16L, ReadFrontierMaximum(databasePath, "scan-child"));
            Assert.Equal(16L, ReadBucketTotal(databasePath, cycleStart, cycleEnd));
            var rows = ReadLineageRows(databasePath);
            Assert.True(rows.Where(row => row.ThreadId is "scan-parent" or "scan-child")
                .All(row => row.Root == "scan-parent"));
            Assert.Equal(1, rows.Count(row => row.ThreadId == "scan-parent" && row.Canonical == 1));
            Assert.Equal(1, rows.Count(row => row.ThreadId == "scan-child" && row.Canonical == 1));
            var now = cycleStart.AddHours(2);
            var aggregates = database.LoadSessionAggregates(now);
            var parentAggregate = aggregates.Single(item => item.Metadata.ThreadId == "scan-parent");
            var childAggregate = aggregates.Single(item => item.Metadata.ThreadId == "scan-child");
            Assert.Equal(12L, parentAggregate.SessionTotal.Total);
            Assert.Equal(4L, childAggregate.SessionTotal.Total);
            Assert.Equal(4L, childAggregate.LatestTurnTotal.Total);
            Assert.Equal(SessionKind.Unknown, parentAggregate.Metadata.Kind);
            Assert.Equal(SessionSurface.Unknown, parentAggregate.Metadata.Surface);
            Assert.True(string.IsNullOrWhiteSpace(parentAggregate.Metadata.Model));
            Assert.True(parentAggregate.LifecycleMetrics is null);
            var snapshot = new HudSnapshot(
                new QuotaObservation(new QuotaBucket("codex", "Codex", 10, 10080, now.AddDays(7)),
                    Array.Empty<QuotaBucket>(), QuotaSource.OfficialAppServer, now, false),
                aggregates, database.GetCycleTotal(cycleStart, cycleEnd), now, false, "fresh",
                Array.Empty<string>(), Array.Empty<HudEvent>(),
                RunningCycleTotal: database.GetCycleTotalForThreads(cycleStart, cycleEnd, new[] { "scan-parent" }));
            var display = HudPresentation.BuildRows(snapshot);
            Assert.Equal(16L, display.Single(row => row.ThreadId == "scan-parent").WorkTotal.Total);
            Assert.Equal(4L, display.Single(row => row.ThreadId == "scan-child").WorkTotal.Total);
            Assert.Equal("自身 12 + 子任务 4",
                display.Single(row => row.ThreadId == "scan-parent").WorkBreakdownText);
            Assert.True(HudPresentation.BuildCollapsedText(snapshot)
                .Contains("本周期 16 raw tokens", StringComparison.Ordinal));
            Assert.True(HudPresentation.BuildFrame(snapshot).OverviewText.Contains(
                "本额度周期全部会话合计：16 raw tokens", StringComparison.Ordinal));
            var viewModel = new MainViewModel();
            viewModel.Apply(snapshot);
            viewModel.SetFilter("all");
            Assert.True(viewModel.ToggleChildren("scan-parent"));
            Assert.Equal(2, viewModel.Rows.Count);
            Assert.Equal(16L, viewModel.Rows.Single(row => row.ThreadId == "scan-parent").WorkTotal.Total);
            Assert.Equal(4L, viewModel.Rows.Single(row => row.ThreadId == "scan-child").WorkTotal.Total);

            var flags = DescribeLineageFlags(databasePath);
            Assert.True(indexer.MergeMetadata(new SessionMetadata("scan-parent", null, null, null, null,
                "gpt-test", null, null, null, null, Kind: SessionKind.Primary, Surface: SessionSurface.App)));
            Assert.True(database.IsAggregateRebuildComplete);
            AssertScanParentEnrichedLifecycle(database, now);
            var enriched = database.LoadSessionAggregates(now);
            Assert.Equal(12L, enriched.Single(item => item.Metadata.ThreadId == "scan-parent").SessionTotal.Total);
            Assert.Equal(4L, enriched.Single(item => item.Metadata.ThreadId == "scan-child").SessionTotal.Total);
            Assert.Equal(4L, enriched.Single(item => item.Metadata.ThreadId == "scan-child").LatestTurnTotal.Total);
            Assert.Equal(flags, DescribeLineageFlags(databasePath));
            Assert.Equal(12L, database.GetSessionTotal("scan-parent").Total);
            Assert.Equal(4L, database.GetSessionTotal("scan-child").Total);
            Assert.Equal(4L, database.GetSessionTotal("scan-child", "turn-late").Total);
            Assert.Equal(16L, database.GetCycleTotal(cycleStart, cycleEnd).Total);
            Assert.Equal(12L, database.GetCycleTotalForThreads(cycleStart, cycleEnd, new[] { "scan-parent" }).Total);
            Assert.Equal(4L, database.GetCycleTotalForThreads(cycleStart, cycleEnd, new[] { "scan-child" }).Total);
            Assert.Equal(12L, ReadFrontierMaximum(databasePath, "scan-parent"));
            Assert.Equal(16L, ReadFrontierMaximum(databasePath, "scan-child"));
            Assert.Equal(16L, ReadBucketTotal(databasePath, cycleStart, cycleEnd));
            Assert.True(ReadLineageRows(databasePath).Where(row => row.ThreadId is "scan-parent" or "scan-child")
                .All(row => row.Root == "scan-parent"));
            var enrichedSnapshot = snapshot with { Sessions = enriched };
            var enrichedDisplay = HudPresentation.BuildRows(enrichedSnapshot);
            Assert.Equal(16L, enrichedDisplay.Single(row => row.ThreadId == "scan-parent").WorkTotal.Total);
            Assert.Equal(4L, enrichedDisplay.Single(row => row.ThreadId == "scan-child").WorkTotal.Total);
            Assert.Equal("自身 12 + 子任务 4",
                enrichedDisplay.Single(row => row.ThreadId == "scan-parent").WorkBreakdownText);
            Assert.True(HudPresentation.BuildCollapsedText(enrichedSnapshot)
                .Contains("本周期 16 raw tokens", StringComparison.Ordinal));
            Assert.True(HudPresentation.BuildFrame(enrichedSnapshot).OverviewText.Contains(
                "本额度周期全部会话合计：16 raw tokens", StringComparison.Ordinal));

            var repeat = indexer.ScanFile(parentPath, "sessions/scan-parent.jsonl", "scan-parent");
            Assert.True(database.IsAggregateRebuildComplete);
            Assert.True(repeat.AcceptedSamples == 0 || repeat.WasSkipped);
            AssertScanParentEnrichedLifecycle(database, now);
            database.RebuildLineageCanonical();
            DrainAggregates(database);
            Assert.Equal(flags, DescribeLineageFlags(databasePath));
            Assert.Equal(12L, database.GetSessionTotal("scan-parent").Total);
            Assert.Equal(4L, database.GetSessionTotal("scan-child").Total);
            AssertScanParentEnrichedLifecycle(database, now);

            Assert.True(database.IsAggregateRebuildComplete);
            var unrelatedScan = indexer.ScanFile(unrelatedPath, "sessions/unrelated-root.jsonl", "unrelated-root");
            Assert.Equal(1, unrelatedScan.AcceptedSamples);
            Assert.True(database.IsAggregateRebuildComplete);
            Assert.Equal(12L, database.GetSessionTotal("scan-parent").Total);
            Assert.Equal(4L, database.GetSessionTotal("scan-child").Total);
            Assert.Equal(21L, database.GetSessionTotal("unrelated-root").Total);
            Assert.Equal(37L, database.GetCycleTotal(cycleStart, cycleEnd).Total);
            Assert.True(ReadLineageRows(databasePath).Single(row => row.ThreadId == "unrelated-root").Root ==
                        "unrelated-root");

            database.UpsertSession(new SessionMetadata("still-missing-child", "Missing", null, null, null, null,
                null, null, cycleStart, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "still-missing-parent", AgentDepth: 1));
            Assert.True(database.AcceptTokenSample("still-missing-child", ancestor, cycleStart.AddHours(2),
                cycleStart.AddHours(2), "turn-missing", null, null));
            Assert.Equal(12L, database.GetSessionTotal("still-missing-child").Total);
            Assert.True(ReadLineageRows(databasePath).Single(row => row.ThreadId == "still-missing-child").Root ==
                        "still-missing-child");

            database.UpsertSession(new SessionMetadata("cycle-scan-a", "A", null, null, null, null, null, null,
                cycleStart, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "cycle-scan-b", AgentDepth: 1));
            database.UpsertSession(new SessionMetadata("cycle-scan-b", "B", null, null, null, null, null, null,
                cycleStart, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "cycle-scan-a", AgentDepth: 1));
            DrainAggregates(database);
            Assert.True(database.AcceptTokenSample("cycle-scan-a", ancestor, cycleStart.AddHours(2).AddMinutes(1),
                cycleStart.AddHours(2).AddMinutes(1), "turn-a", null, null));
            Assert.True(database.AcceptTokenSample("cycle-scan-b", ancestor, cycleStart.AddHours(2).AddMinutes(2),
                cycleStart.AddHours(2).AddMinutes(2), "turn-b", null, null));
            Assert.Equal(12L, database.GetSessionTotal("cycle-scan-a").Total);
            Assert.Equal(12L, database.GetSessionTotal("cycle-scan-b").Total);
            Assert.True(ReadLineageRows(databasePath).Where(row => row.ThreadId is "cycle-scan-a" or "cycle-scan-b")
                .All(row => row.Root == row.ThreadId));
            AssertNonNegativeAggregates(databasePath);
            Assert.True(!database.ContainsPrivacySentinel(Sentinel));
        }

        using var restarted = new UsageDatabase(databasePath);
        DrainAggregates(restarted);
        restarted.RebuildLineageCanonical();
        DrainAggregates(restarted);
        Assert.Equal(12L, restarted.GetSessionTotal("scan-parent").Total);
        Assert.Equal(4L, restarted.GetSessionTotal("scan-child").Total);
        Assert.Equal(21L, restarted.GetSessionTotal("unrelated-root").Total);
        Assert.Equal(12L, restarted.GetSessionTotal("still-missing-child").Total);
        Assert.Equal(12L, restarted.GetSessionTotal("cycle-scan-a").Total);
        Assert.Equal(12L, restarted.GetSessionTotal("cycle-scan-b").Total);
        Assert.Equal(37L, restarted.GetCycleTotal(cycleStart, cycleEnd).Total);
        Assert.True(ReadLineageRows(databasePath).Where(row => row.ThreadId is "scan-parent" or "scan-child")
            .All(row => row.Root == "scan-parent"));
        Assert.Equal(1, ReadLineageRows(databasePath).Count(row => row.ThreadId == "scan-parent" && row.Canonical == 1));
        Assert.Equal(1, ReadLineageRows(databasePath).Count(row => row.ThreadId == "scan-child" && row.Canonical == 1));
        AssertScanParentEnrichedLifecycle(restarted, cycleStart.AddHours(2));
        AssertNonNegativeAggregates(databasePath);
        Assert.True(!restarted.ContainsPrivacySentinel(Sentinel));
    }

    private static void AssertScanParentEnrichedLifecycle(UsageDatabase database, DateTimeOffset nowUtc)
    {
        var parent = database.LoadSessions().Single(item => item.ThreadId == "scan-parent");
        Assert.Equal(SessionKind.Primary, parent.Kind);
        Assert.Equal(SessionSurface.App, parent.Surface);
        Assert.Equal("gpt-test", parent.Model);
        Assert.True(parent.ParentThreadId is null);
        var child = database.LoadSessions().Single(item => item.ThreadId == "scan-child");
        Assert.Equal("scan-parent", child.ParentThreadId);
        var aggregate = database.LoadSessionAggregates(nowUtc)
            .Single(item => item.Metadata.ThreadId == "scan-parent");
        if (aggregate.LifecycleMetrics is not { } life)
            throw new InvalidOperationException("lifecycle_metrics_missing");
        Assert.Equal(12L, life.Recent48HourTokens);
    }

    private static void AssertForkExplicitContextBoundaries(string runRoot)
    {
        static string TurnLine(string thread, long timestamp, string turn, string model) =>
            $"{{\"timestamp\":{timestamp},\"type\":\"turn_context\",\"payload\":{{\"thread_id\":\"{thread}\",\"turn_id\":\"{turn}\",\"model\":\"{model}\"}}}}";
        static string CompactLine(string thread, long timestamp) =>
            $"{{\"timestamp\":{timestamp},\"type\":\"event_msg\",\"payload\":{{\"type\":\"context_compacted\",\"thread_id\":\"{thread}\"}}}}";
        static string ContextTokenLine(string thread, long timestamp, string model, long totalInput,
            long lastInput, long contextWindow) => System.Text.Json.JsonSerializer.Serialize(new
            {
                type = "event_msg",
                payload = new
                {
                    type = "token_count", thread_id = thread, timestamp, model,
                    info = new
                    {
                        total_token_usage = new { input_tokens = totalInput, output_tokens = 0, total_tokens = totalInput },
                        last_token_usage = new { input_tokens = lastInput, output_tokens = 0, total_tokens = lastInput },
                        model_context_window = contextWindow,
                    },
                },
            });

        var directory = Path.Combine(runRoot, "context-fork-lineage");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "usage.db");
        const long window = 258_400;
        const long start = 1_786_400_000;
        var parentPath = Path.Combine(directory, "fork-parent.jsonl");
        var childPath = Path.Combine(directory, "fork-child.jsonl");
        var copyOnlyPath = Path.Combine(directory, "fork-copy-only.jsonl");
        var missingPath = Path.Combine(directory, "fork-missing.jsonl");
        File.WriteAllText(parentPath, string.Join('\n', new[]
        {
            $"{{\"type\":\"session_meta\",\"payload\":{{\"id\":\"fork-parent\"}}}}",
            TurnLine("fork-parent", start, "turn-parent", "gpt-test"),
            CompactLine("fork-parent", start + 1),
            ContextTokenLine("fork-parent", start + 2, "gpt-test", 1_105_000, 105_000, window),
        }) + "\n", new UTF8Encoding(false));
        File.WriteAllText(childPath, string.Join('\n', new[]
        {
            $"{{\"type\":\"session_meta\",\"payload\":{{\"id\":\"fork-child\"}}}}",
            TurnLine("fork-child", start, "turn-parent", "gpt-test"),
            CompactLine("fork-child", start + 1),
            ContextTokenLine("fork-child", start + 2, "gpt-test", 1_105_000, 105_000, window),
            TurnLine("fork-child", start + 80, "turn-child", "gpt-test"),
            CompactLine("fork-child", start + 81),
            ContextTokenLine("fork-child", start + 82, "gpt-test", 1_175_000, 70_000, window),
        }) + "\n", new UTF8Encoding(false));
        File.WriteAllText(copyOnlyPath, string.Join('\n', new[]
        {
            $"{{\"type\":\"session_meta\",\"payload\":{{\"id\":\"fork-copy-only\"}}}}",
            TurnLine("fork-copy-only", start, "turn-parent", "gpt-test"),
            CompactLine("fork-copy-only", start + 1),
            ContextTokenLine("fork-copy-only", start + 2, "gpt-test", 1_105_000, 105_000, window),
            TurnLine("fork-copy-only", start + 90, "turn-copy-tail", "gpt-test"),
            ContextTokenLine("fork-copy-only", start + 91, "gpt-test", 1_185_000, 80_000, window),
        }) + "\n", new UTF8Encoding(false));
        File.WriteAllText(missingPath, string.Join('\n', new[]
        {
            $"{{\"type\":\"session_meta\",\"payload\":{{\"id\":\"fork-missing\"}}}}",
            TurnLine("fork-missing", start + 200, "turn-missing", "gpt-test"),
            CompactLine("fork-missing", start + 201),
            ContextTokenLine("fork-missing", start + 202, "gpt-test", 900_000, 90_000, window),
        }) + "\n", new UTF8Encoding(false));

        using (var database = new UsageDatabase(databasePath))
        {
            var indexer = new RolloutIndexer(database);
            Assert.Equal(1, indexer.ScanFile(parentPath, "sessions/fork-parent.jsonl", "fork-parent").AcceptedSamples);
            database.UpsertSession(new SessionMetadata("fork-child", "ForkChild", null, null, null, "gpt-test",
                null, null, DateTimeOffset.FromUnixTimeSeconds(start), null, Kind: SessionKind.InternalTask,
                Surface: SessionSurface.InternalTask, ParentThreadId: "fork-parent", AgentDepth: 1));
            DrainAggregates(database);
            Assert.Equal(2, indexer.ScanFile(childPath, "sessions/fork-child.jsonl", "fork-child").AcceptedSamples);
            DrainAggregates(database);
            AssertForkInheritedCopyElection(databasePath);
            Assert.Equal(1L, CountContextBaselines(databasePath, "fork-parent", "explicit"));
            Assert.Equal(1L, CountContextBaselines(databasePath, "fork-child", "explicit"));
            Assert.Equal(0L, CountContextBaselines(databasePath, "fork-child", "heuristic"));
            AssertSingleExplicitBaseline(databasePath, "fork-parent", 105_000L);
            AssertSingleExplicitBaseline(databasePath, "fork-child", 70_000L);
            Assert.True(database.LoadSessionContextMetrics()["fork-child"].UsesExplicitCompactionBoundaries);

            database.UpsertSession(new SessionMetadata("fork-copy-only", "CopyOnly", null, null, null, "gpt-test",
                null, null, DateTimeOffset.FromUnixTimeSeconds(start), null, Kind: SessionKind.InternalTask,
                Surface: SessionSurface.InternalTask, ParentThreadId: "fork-parent", AgentDepth: 1));
            DrainAggregates(database);
            Assert.Equal(2, indexer.ScanFile(copyOnlyPath, "sessions/fork-copy-only.jsonl", "fork-copy-only")
                .AcceptedSamples);
            DrainAggregates(database);
            Assert.Equal(0L, CountContextBaselines(databasePath, "fork-copy-only", "explicit"));
            Assert.True(!database.LoadSessionContextMetrics().TryGetValue("fork-copy-only", out var copyMetrics) ||
                        copyMetrics.PostCompactionSampleCount == 0 || !copyMetrics.UsesExplicitCompactionBoundaries);

            database.ClearSessionParent("fork-child");
            DrainAggregates(database);
            database.RebuildLineageCanonical();
            DrainAggregates(database);
            Assert.True(CountContextBaselines(databasePath, "fork-child", "explicit") >= 1);
            database.UpsertSession(new SessionMetadata("fork-child", "ForkChild", null, null, null, "gpt-test",
                null, null, DateTimeOffset.FromUnixTimeSeconds(start), null, Kind: SessionKind.InternalTask,
                Surface: SessionSurface.InternalTask, ParentThreadId: "fork-parent", AgentDepth: 1));
            DrainAggregates(database);
            database.RebuildLineageCanonical();
            DrainAggregates(database);
            AssertForkInheritedCopyElection(databasePath);
            Assert.Equal(1L, CountContextBaselines(databasePath, "fork-parent", "explicit"));
            Assert.Equal(1L, CountContextBaselines(databasePath, "fork-child", "explicit"));
            AssertSingleExplicitBaseline(databasePath, "fork-parent", 105_000L);
            AssertSingleExplicitBaseline(databasePath, "fork-child", 70_000L);

            database.UpsertSession(new SessionMetadata("fork-missing", "Missing", null, null, null, "gpt-test",
                null, null, DateTimeOffset.FromUnixTimeSeconds(start), null, Kind: SessionKind.InternalTask,
                Surface: SessionSurface.InternalTask, ParentThreadId: "fork-absent-parent", AgentDepth: 1));
            DrainAggregates(database);
            Assert.Equal(1, indexer.ScanFile(missingPath, "sessions/fork-missing.jsonl", "fork-missing")
                .AcceptedSamples);
            DrainAggregates(database);
            Assert.Equal(1L, CountContextBaselines(databasePath, "fork-missing", "explicit"));
            Assert.True(ReadLineageRows(databasePath).Single(row => row.ThreadId == "fork-missing").Root ==
                        "fork-missing");

            database.UpsertSession(new SessionMetadata("fork-cycle-a", "CA", null, null, null, "gpt-test", null,
                null, DateTimeOffset.FromUnixTimeSeconds(start), null, Kind: SessionKind.InternalTask,
                Surface: SessionSurface.InternalTask, ParentThreadId: "fork-cycle-b", AgentDepth: 1));
            database.UpsertSession(new SessionMetadata("fork-cycle-b", "CB", null, null, null, "gpt-test", null,
                null, DateTimeOffset.FromUnixTimeSeconds(start), null, Kind: SessionKind.InternalTask,
                Surface: SessionSurface.InternalTask, ParentThreadId: "fork-cycle-a", AgentDepth: 1));
            DrainAggregates(database);
            var cycleA = Path.Combine(directory, "fork-cycle-a.jsonl");
            var cycleB = Path.Combine(directory, "fork-cycle-b.jsonl");
            File.WriteAllText(cycleA, string.Join('\n', new[]
            {
                TurnLine("fork-cycle-a", start + 300, "turn-a", "gpt-test"),
                CompactLine("fork-cycle-a", start + 301),
                ContextTokenLine("fork-cycle-a", start + 302, "gpt-test", 500_000, 55_000, window),
            }) + "\n", new UTF8Encoding(false));
            File.WriteAllText(cycleB, string.Join('\n', new[]
            {
                TurnLine("fork-cycle-b", start + 310, "turn-b", "gpt-test"),
                CompactLine("fork-cycle-b", start + 311),
                ContextTokenLine("fork-cycle-b", start + 312, "gpt-test", 510_000, 56_000, window),
            }) + "\n", new UTF8Encoding(false));
            Assert.Equal(1, indexer.ScanFile(cycleA, "sessions/fork-cycle-a.jsonl", "fork-cycle-a").AcceptedSamples);
            Assert.Equal(1, indexer.ScanFile(cycleB, "sessions/fork-cycle-b.jsonl", "fork-cycle-b").AcceptedSamples);
            DrainAggregates(database);
            Assert.Equal(1L, CountContextBaselines(databasePath, "fork-cycle-a", "explicit"));
            Assert.Equal(1L, CountContextBaselines(databasePath, "fork-cycle-b", "explicit"));
            Assert.True(ReadLineageRows(databasePath).Where(row => row.ThreadId is "fork-cycle-a" or "fork-cycle-b")
                .All(row => row.Root == row.ThreadId));
            Assert.True(!database.ContainsPrivacySentinel(Sentinel));
            AssertNonNegativeAggregates(databasePath);
        }

        using var restarted = new UsageDatabase(databasePath);
        DrainAggregates(restarted);
        restarted.RebuildLineageCanonical();
        DrainAggregates(restarted);
        AssertForkInheritedCopyElection(databasePath);
        Assert.Equal(1L, CountContextBaselines(databasePath, "fork-parent", "explicit"));
        Assert.Equal(1L, CountContextBaselines(databasePath, "fork-child", "explicit"));
        Assert.Equal(0L, CountContextBaselines(databasePath, "fork-copy-only", "explicit"));
        Assert.Equal(1L, CountContextBaselines(databasePath, "fork-missing", "explicit"));
        AssertSingleExplicitBaseline(databasePath, "fork-parent", 105_000L);
        AssertSingleExplicitBaseline(databasePath, "fork-child", 70_000L);
        Assert.True(!restarted.ContainsPrivacySentinel(Sentinel));
    }

    private static void AssertForkInheritedCopyElection(string databasePath)
    {
        var rows = ReadLineageRows(databasePath);
        var parent = rows.Where(row => row.ThreadId == "fork-parent").ToArray();
        Assert.Equal(1, parent.Length);
        Assert.Equal(1, parent[0].Canonical);
        var children = rows.Where(row => row.ThreadId == "fork-child").ToArray();
        Assert.Equal(2, children.Length);
        var copied = children.Single(row => row.Identity == parent[0].Identity);
        Assert.Equal(0, copied.Canonical);
        var tail = children.Single(row => row.Identity != parent[0].Identity);
        Assert.Equal(1, tail.Canonical);
    }

    private static void LineageFamilyAndUnrelatedRoots(string runRoot)
    {
        var directory = Path.Combine(runRoot, "lineage-family");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "usage.db");
        var t0 = DateTimeOffset.Parse("2026-02-01T00:00:00Z", CultureInfo.InvariantCulture);
        using var database = new UsageDatabase(databasePath);
        database.UpsertSession(new SessionMetadata("root", "Root", null, null, null, "gpt-test", null, null,
            t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
        database.UpsertSession(new SessionMetadata("child", "Child", null, null, null, "gpt-test", null, null,
            t0, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
            ParentThreadId: "root", AgentDepth: 1));
        database.UpsertSession(new SessionMetadata("grand", "Grand", null, null, null, "gpt-test", null, null,
            t0, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
            ParentThreadId: "child", AgentDepth: 2));
        database.UpsertSession(new SessionMetadata("sib", "Sibling", null, null, null, "gpt-test", null, null,
            t0, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
            ParentThreadId: "root", AgentDepth: 1));
        database.UpsertSession(new SessionMetadata("other", "Other", null, null, null, "gpt-test", null, null,
            t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.Cli));

        var shared = LineageSnapshot(10, 2, 10, 2);
        var childTail = LineageSnapshot(15, 3, 5, 1);
        var grandTail = LineageSnapshot(18, 4, 3, 1);
        var sibTail = LineageSnapshot(16, 2, 6, 0);
        Assert.True(database.AcceptTokenSample("root", shared, t0, t0, "turn-root", "gpt-test", null));
        Assert.True(database.AcceptTokenSample("child", shared, t0.AddMinutes(1), t0.AddMinutes(1),
            "turn-child", "gpt-test", null));
        Assert.True(database.AcceptTokenSample("grand", shared, t0.AddMinutes(2), t0.AddMinutes(2),
            "turn-grand", "gpt-test", null));
        Assert.True(database.AcceptTokenSample("sib", shared, t0.AddMinutes(1), t0.AddMinutes(1),
            "turn-sib", "gpt-test", null));
        Assert.True(database.AcceptTokenSample("child", childTail, t0.AddMinutes(3), t0.AddMinutes(3),
            "turn-child", "gpt-test", null));
        Assert.True(database.AcceptTokenSample("grand", grandTail, t0.AddMinutes(4), t0.AddMinutes(4),
            "turn-grand", "gpt-test", null));
        Assert.True(database.AcceptTokenSample("sib", sibTail, t0.AddMinutes(3), t0.AddMinutes(3),
            "turn-sib", "gpt-test", null));
        Assert.True(database.AcceptTokenSample("other", shared, t0, t0, "turn-other", "gpt-test", null));

        Assert.Equal(12L, database.GetSessionTotal("root").Total);
        Assert.Equal(6L, database.GetSessionTotal("child").Total);
        Assert.Equal(4L, database.GetSessionTotal("grand").Total);
        Assert.Equal(6L, database.GetSessionTotal("sib").Total);
        Assert.Equal(12L, database.GetSessionTotal("other").Total);
        Assert.Equal(8L, database.GetSampleCount());
        Assert.Equal(4, ReadLineageRows(databasePath).Count(row => row.Canonical == 1 && row.Root == "root"));
        Assert.Equal(1, ReadLineageRows(databasePath).Count(row => row.Canonical == 1 && row.Root == "other"));

        var now = t0.AddHours(1);
        var aggregates = database.LoadSessionAggregates(now);
        var snapshot = new HudSnapshot(
            new QuotaObservation(new QuotaBucket("codex", "Codex", 10, 10080, now.AddDays(7)),
                Array.Empty<QuotaBucket>(), QuotaSource.OfficialAppServer, now, false),
            aggregates, database.GetCycleTotal(t0, t0.AddHours(1)), now, false, "fresh",
            Array.Empty<string>(), Array.Empty<HudEvent>(), RunningCycleTotal: database.GetCycleTotalForThreads(
                t0, t0.AddHours(1), new[] { "root" }));
        var rows = HudPresentation.BuildRows(snapshot);
        Assert.Equal(28L, rows.Single(row => row.ThreadId == "root").WorkTotal.Total);
        Assert.Equal(10L, rows.Single(row => row.ThreadId == "child").WorkTotal.Total);
        Assert.Equal(12L, rows.Single(row => row.ThreadId == "other").WorkTotal.Total);
        Assert.Equal(40L, snapshot.CycleTotal!.Value.Total);
        Assert.Equal(0L, CountContextBaselines(databasePath, "child"));
        Assert.Equal(0L, CountContextBaselines(databasePath, "grand"));
        AssertNonNegativeAggregates(databasePath);
        var engineSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "CodexUsageHud.Core",
            "UsageEngine.cs"));
        Assert.True(engineSource.Contains("HudProduct.Version", StringComparison.Ordinal));
        Assert.Equal("1.0.4", HudProduct.Version);
    }

    private static void LineageCycleTimingAndIsolation(string runRoot)
    {
        var directory = Path.Combine(runRoot, "lineage-timing");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "usage.db");
        var cycleStart = DateTimeOffset.Parse("2026-03-01T01:00:00Z", CultureInfo.InvariantCulture);
        var cycleEnd = cycleStart.AddHours(1);
        using (var database = new UsageDatabase(databasePath))
        {
            database.UpsertSession(new SessionMetadata("early-root", "Early", null, null, null, null, null, null,
                cycleStart, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            database.UpsertSession(new SessionMetadata("early-child", "EarlyChild", null, null, null, null, null,
                null, cycleStart, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "early-root", AgentDepth: 1));
            var ancestor = LineageSnapshot(10, 2, 10, 2);
            var tail = LineageSnapshot(13, 3, 3, 1);
            Assert.True(database.AcceptTokenSample("early-root", ancestor, cycleStart.AddMinutes(-10),
                cycleStart.AddMinutes(-10), "turn-early", null, null));
            Assert.True(database.AcceptTokenSample("early-child", ancestor, cycleStart.AddMinutes(5),
                cycleStart.AddMinutes(5), "turn-child", null, null));
            Assert.True(database.AcceptTokenSample("early-child", tail, cycleStart.AddMinutes(6),
                cycleStart.AddMinutes(6), "turn-child", null, null));
            Assert.Equal(12L, database.GetSessionTotal("early-root").Total);
            Assert.Equal(4L, database.GetSessionTotal("early-child").Total);
            Assert.Equal(4L, database.GetCycleTotal(cycleStart, cycleEnd).Total);

            Assert.True(database.AcceptTokenSample("late-child", ancestor, cycleStart.AddMinutes(8),
                cycleStart.AddMinutes(8), "turn-late", null, null));
            Assert.True(database.AcceptTokenSample("late-child", tail, cycleStart.AddMinutes(9),
                cycleStart.AddMinutes(9), "turn-late", null, null));
            Assert.Equal(16L, database.GetSessionTotal("late-child").Total);
            Assert.True(database.IsAggregateRebuildComplete);
            database.UpsertSession(new SessionMetadata("late-parent", "LateParent", null, null, null, null, null,
                null, cycleStart, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            database.UpsertSession(new SessionMetadata("late-child", "LateChild", null, null, null, null, null,
                null, cycleStart, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "late-parent", AgentDepth: 1));
            Assert.True(!database.IsAggregateRebuildComplete);
            var pending = new HudSnapshot(
                new QuotaObservation(new QuotaBucket("codex", "Codex", 10, 10080, cycleEnd.AddDays(6)),
                    Array.Empty<QuotaBucket>(), QuotaSource.OfficialAppServer, cycleStart, false),
                Array.Empty<SessionAggregate>(), null, cycleStart, true, "索引中（统计迁移）",
                Array.Empty<string>(), AggregateMigrationPending: true);
            Assert.True(HudPresentation.BuildCollapsedText(pending).Contains("索引中", StringComparison.Ordinal));
            Assert.True(HudPresentation.BuildFrame(pending).FreshnessText.Contains("索引中", StringComparison.Ordinal));
            DrainAggregates(database);
            Assert.Equal(0L, database.GetSessionTotal("late-parent").Total);
            Assert.Equal(16L, database.GetSessionTotal("late-child").Total);

            Assert.True(database.AcceptTokenSample("late-parent", ancestor, cycleStart.AddMinutes(-5),
                cycleStart.AddMinutes(-5), "turn-parent", null, null));
            Assert.Equal(12L, database.GetSessionTotal("late-parent").Total);
            Assert.Equal(4L, database.GetSessionTotal("late-child").Total);
            Assert.Equal(8L, database.GetCycleTotal(cycleStart, cycleEnd).Total);

            database.UpsertSession(new SessionMetadata("missing-child", "Missing", "worker", null, "same-project",
                null, null, null, cycleStart, null, Kind: SessionKind.InternalTask,
                Surface: SessionSurface.InternalTask, ParentThreadId: "no-such-parent", AgentDepth: 1));
            Assert.True(database.AcceptTokenSample("missing-child", ancestor, cycleStart.AddHours(2),
                cycleStart.AddHours(2), "turn-missing", null, null));
            Assert.Equal(12L, database.GetSessionTotal("missing-child").Total);

            database.UpsertSession(new SessionMetadata("cycle-a", "A", null, null, null, null, null, null,
                cycleStart, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "cycle-b", AgentDepth: 1));
            database.UpsertSession(new SessionMetadata("cycle-b", "B", null, null, null, null, null, null,
                cycleStart, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "cycle-a", AgentDepth: 1));
            DrainAggregates(database);
            Assert.True(database.AcceptTokenSample("cycle-a", ancestor, cycleStart.AddHours(2).AddMinutes(1),
                cycleStart.AddHours(2).AddMinutes(1), "turn-a", null, null));
            Assert.True(database.AcceptTokenSample("cycle-b", ancestor, cycleStart.AddHours(2).AddMinutes(2),
                cycleStart.AddHours(2).AddMinutes(2), "turn-b", null, null));
            Assert.Equal(12L, database.GetSessionTotal("cycle-a").Total);
            Assert.Equal(12L, database.GetSessionTotal("cycle-b").Total);
            var cyclicRows = ReadLineageRows(databasePath).Where(row =>
                row.ThreadId is "cycle-a" or "cycle-b").ToArray();
            Assert.True(cyclicRows.All(row => row.Root == row.ThreadId));
            Assert.Equal(2, cyclicRows.Count(row => row.Canonical == 1));
        }

        using var restarted = new UsageDatabase(databasePath);
        DrainAggregates(restarted);
        Assert.Equal(12L, restarted.GetSessionTotal("early-root").Total);
        Assert.Equal(4L, restarted.GetSessionTotal("early-child").Total);
        Assert.Equal(12L, restarted.GetSessionTotal("late-parent").Total);
        Assert.Equal(4L, restarted.GetSessionTotal("late-child").Total);
        Assert.Equal(12L, restarted.GetSessionTotal("missing-child").Total);
        Assert.Equal(12L, restarted.GetSessionTotal("cycle-a").Total);
        Assert.Equal(12L, restarted.GetSessionTotal("cycle-b").Total);
        Assert.Equal(8L, restarted.GetCycleTotal(cycleStart, cycleEnd).Total);
        AssertNonNegativeAggregates(databasePath);
        AssertCommitScanLateParentMaterialization(runRoot);
    }

    private static void LineageConsumersRestartPrivacy(string runRoot)
    {
        var directory = Path.Combine(runRoot, "lineage-consumers");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "usage.db");
        var start = DateTimeOffset.Parse("2026-04-01T00:00:00Z", CultureInfo.InvariantCulture);
        using (var database = new UsageDatabase(databasePath))
        {
            database.UpsertSession(new SessionMetadata("inc-parent", "IncParent", null, null, null, "gpt-test",
                null, null, start, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            database.UpsertSession(new SessionMetadata("inc-child", "IncChild", null, null, null, "gpt-test",
                null, null, start, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "inc-parent", AgentDepth: 1));
            var shared = LineageSnapshot(10, 2, 10, 2);
            var tail = LineageSnapshot(14, 3, 4, 1);
            Assert.True(database.AcceptTokenSample("inc-child", shared, start.AddMinutes(10), start.AddMinutes(10),
                "turn-child", "gpt-test", null));
            Assert.Equal(12L, database.GetSessionTotal("inc-child").Total);
            Assert.True(database.IsAggregateRebuildComplete);
            Assert.True(database.AcceptTokenSample("inc-parent", shared, start.AddMinutes(1), start.AddMinutes(1),
                "turn-parent", "gpt-test", null));
            Assert.True(database.IsAggregateRebuildComplete);
            Assert.Equal(12L, database.GetSessionTotal("inc-parent").Total);
            Assert.Equal(0L, database.GetSessionTotal("inc-child").Total);
            Assert.True(database.AcceptTokenSample("inc-child", tail, start.AddMinutes(20), start.AddMinutes(20),
                "turn-child", "gpt-test", null));
            Assert.Equal(5L, database.GetSessionTotal("inc-child").Total);
            Assert.Equal(5L, database.GetSessionTotal("inc-child", "turn-child").Total);
            Assert.Equal(17L, database.GetCycleTotal(start, start.AddHours(1)).Total);
            Assert.Equal(5L, database.GetCycleTotalForThreads(start, start.AddHours(1),
                new[] { "inc-child" }).Total);
            Assert.Equal(12L, database.GetCycleTotalForThreads(start, start.AddHours(1),
                new[] { "inc-parent" }).Total);
            var latest = database.LoadSessionAggregates(start.AddHours(2))
                .Single(item => item.Metadata.ThreadId == "inc-child");
            Assert.Equal(5L, latest.SessionTotal.Total);
            Assert.Equal(5L, latest.LatestTurnTotal.Total);
            Assert.Equal(5L, latest.RecentUsage.Total);
            Assert.Equal(RecentUsageKind.LatestTurn, latest.RecentKind);
            Assert.Equal(0L, CountContextBaselines(databasePath, "inc-child"));
            var parentLifecycle = database.LoadSessionAggregates(start.AddHours(2))
                .Single(item => item.Metadata.ThreadId == "inc-parent");
            if (parentLifecycle.LifecycleMetrics is not { } parentMetrics)
                throw new InvalidOperationException("lifecycle_metrics_missing");
            Assert.Equal(12L, parentMetrics.Recent48HourTokens);
            Assert.Equal(12L, ReadFrontierMaximum(databasePath, "inc-parent"));
            Assert.Equal(17L, ReadFrontierMaximum(databasePath, "inc-child"));
            Assert.Equal(17L, ReadBucketTotal(databasePath, start, start.AddHours(1)));
            var now = start.AddHours(2);
            var snapshot = new HudSnapshot(
                new QuotaObservation(new QuotaBucket("codex", "Codex", 10, 10080, now.AddDays(7)),
                    Array.Empty<QuotaBucket>(), QuotaSource.OfficialAppServer, now, false),
                database.LoadSessionAggregates(now), database.GetCycleTotal(start, start.AddHours(1)), now, false,
                "fresh", Array.Empty<string>(), Array.Empty<HudEvent>(),
                RunningCycleTotal: database.GetCycleTotalForThreads(start, start.AddHours(1), new[] { "inc-parent" }));
            var rows = HudPresentation.BuildRows(snapshot);
            Assert.Equal(17L, rows.Single(row => row.ThreadId == "inc-parent").WorkTotal.Total);
            Assert.Equal(5L, rows.Single(row => row.ThreadId == "inc-child").WorkTotal.Total);
            Assert.Equal("自身 12 + 子任务 5",
                rows.Single(row => row.ThreadId == "inc-parent").WorkBreakdownText);
            var collapsed = HudPresentation.BuildCollapsedText(snapshot);
            Assert.True(collapsed.Contains("本周期 17 raw tokens", StringComparison.Ordinal));
            var frame = HudPresentation.BuildFrame(snapshot);
            Assert.True(frame.OverviewText.Contains("本额度周期全部会话合计：17 raw tokens", StringComparison.Ordinal));
            Assert.True(frame.OverviewText.Contains("运行中会话本额度周期合计：12 raw tokens", StringComparison.Ordinal));
            var viewModel = new MainViewModel();
            viewModel.Apply(snapshot);
            Assert.Equal(collapsed, viewModel.CollapsedText);
            Assert.True(viewModel.OverviewText.Contains("本额度周期全部会话合计：17 raw tokens", StringComparison.Ordinal));
            viewModel.SetFilter("all");
            Assert.True(viewModel.ToggleChildren("inc-parent"));
            Assert.Equal(2, viewModel.Rows.Count);
            Assert.Equal(17L, viewModel.Rows.Single(row => row.ThreadId == "inc-parent").WorkTotal.Total);
            Assert.Equal(5L, viewModel.Rows.Single(row => row.ThreadId == "inc-child").WorkTotal.Total);
            AssertNonNegativeAggregates(databasePath);

            var secondShared = LineageSnapshot(10, 2, 10, 2);
            Assert.True(!database.AcceptTokenSample("inc-child", secondShared, start.AddMinutes(11),
                start.AddMinutes(11), "turn-child", "gpt-test", null));
            Assert.Equal(5L, database.GetSessionTotal("inc-child").Total);
            Assert.Equal(2L, database.GetSampleCount("inc-child"));
            Assert.Equal(1L, database.GetSampleCount("inc-parent"));
        }

        using (var restarted = new UsageDatabase(databasePath))
        {
            DrainAggregates(restarted);
            Assert.Equal(12L, restarted.GetSessionTotal("inc-parent").Total);
            Assert.Equal(5L, restarted.GetSessionTotal("inc-child").Total);
            Assert.Equal(17L, restarted.GetCycleTotal(start, start.AddHours(1)).Total);
            var replay = LineageSnapshot(10, 2, 10, 2);
            Assert.True(!restarted.AcceptTokenSample("inc-child", replay, start.AddMinutes(12),
                start.AddMinutes(12), "turn-child", "gpt-test", null));
            Assert.Equal(5L, restarted.GetSessionTotal("inc-child").Total);
            Assert.Equal(3L, restarted.GetSampleCount());
            Assert.True(!restarted.ContainsPrivacySentinel(Sentinel));
            var schema = restarted.ReadSchemaColumnNames()["token_samples"];
            foreach (var column in new[] { "semantic_identity", "semantic_material", "legacy_semantic_identity",
                         "lineage_root_thread_id", "is_lineage_canonical" })
                Assert.True(schema.Contains(column, StringComparer.Ordinal));
        }

        using var named = new UsageDatabase(Path.Combine(directory, "privacy.db"));
        named.UpsertSession(new SessionMetadata("privacy-thread", Sentinel, "lead", null, null, null, null, null,
            start, null));
        Assert.True(named.ContainsPrivacySentinel(Sentinel));
        var lineageColumns = named.ReadSchemaColumnNames()["token_samples"];
        Assert.True(lineageColumns.All(column =>
            !column.Contains("prompt", StringComparison.OrdinalIgnoreCase) &&
            !column.Contains("message", StringComparison.OrdinalIgnoreCase) &&
            !column.Contains("preview", StringComparison.OrdinalIgnoreCase)));
    }

    private static void LineageTuplePresenceReelectionAndRender(string runRoot)
    {
        var directory = Path.Combine(runRoot, "lineage-presence");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "usage.db");
        var t0 = DateTimeOffset.Parse("2026-05-01T00:00:00Z", CultureInfo.InvariantCulture);
        var missing = new TokenUsageSnapshot(
            new TokenComponents(10, null, null, null, 2, null, 12),
            new TokenComponents(10, null, null, null, 2, null, 12), 128000);
        var zeroed = new TokenUsageSnapshot(
            new TokenComponents(10, 0, 0, 0, 2, 0, 12),
            new TokenComponents(10, 0, 0, 0, 2, 0, 12), 128000);
        using (var database = new UsageDatabase(databasePath))
        {
            database.UpsertSession(new SessionMetadata("presence", "Presence", null, null, null, "gpt-test",
                null, null, t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            database.UpsertSession(new SessionMetadata("unrelated", "Unrelated", null, null, null, "gpt-test",
                null, null, t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.Cli));
            Assert.True(database.AcceptTokenSample("presence", missing, t0, t0, "turn-m", null, null));
            Assert.True(database.AcceptTokenSample("presence", zeroed, t0.AddSeconds(1), t0.AddSeconds(1),
                "turn-z", null, null));
            Assert.True(database.AcceptTokenSample("unrelated", missing, t0, t0, "turn-u", null, null));
            Assert.Equal(24L, database.GetSessionTotal("presence").Total);
            Assert.Equal(12L, database.GetSessionTotal("unrelated").Total);
            Assert.Equal(2, ReadLineageRows(databasePath).Count(row =>
                row.ThreadId == "presence" && row.Canonical == 1));
            Assert.Equal(12L, database.GetSessionTotal("presence", "turn-m").Total);
            Assert.Equal(12L, database.GetSessionTotal("presence", "turn-z").Total);

            database.UpsertSession(new SessionMetadata("root", "Root", null, null, null, "gpt-test", null, null,
                t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            database.UpsertSession(new SessionMetadata("alpha", "Alpha", null, null, null, "gpt-test", null, null,
                t0, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "root", AgentDepth: 1), currentTurnKey: "turn-a");
            database.UpsertSession(new SessionMetadata("beta", "Beta", null, null, null, "gpt-test", null, null,
                t0, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "root", AgentDepth: 1), currentTurnKey: "turn-b");
            var shared = LineageSnapshot(10, 2, 10, 2);
            Assert.True(database.AcceptTokenSample("alpha", shared, t0.AddMinutes(1), t0.AddMinutes(1),
                "turn-a", null, null));
            Assert.True(database.AcceptTokenSample("beta", shared, t0.AddMinutes(2), t0.AddMinutes(2),
                "turn-b", null, null));
            Assert.Equal(12L, database.GetSessionTotal("alpha").Total);
            Assert.Equal(0L, database.GetSessionTotal("beta").Total);

            database.UpsertSession(new SessionMetadata("other", "Other", null, null, null, "gpt-test", null,
                null, t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            database.UpsertSession(new SessionMetadata("alpha", "Alpha", null, null, null, "gpt-test", null, null,
                t0, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "other", AgentDepth: 1), currentTurnKey: "turn-a");
            DrainAggregates(database);
            Assert.Equal(12L, database.GetSessionTotal("alpha").Total);
            Assert.Equal(12L, database.GetSessionTotal("beta").Total);
            Assert.Equal(0L, database.GetSessionTotal("root").Total);
            Assert.Equal(0L, database.GetSessionTotal("other").Total);
            Assert.Equal(12L, database.GetSessionTotal("alpha", "turn-a").Total);
            Assert.Equal(12L, database.GetSessionTotal("beta", "turn-b").Total);
            Assert.Equal(1, ReadLineageRows(databasePath).Count(row =>
                row.ThreadId == "alpha" && row.Canonical == 1 && row.Root == "other"));
            Assert.Equal(1, ReadLineageRows(databasePath).Count(row =>
                row.ThreadId == "beta" && row.Canonical == 1 && row.Root == "root"));
            Assert.Equal(1, ReadLineageRows(databasePath).Count(row =>
                row.Root == "other" && row.Canonical == 1));
            Assert.Equal(1, ReadLineageRows(databasePath).Count(row =>
                row.Root == "root" && row.Canonical == 1));
            var reparentNow = t0.AddHours(1);
            var reparentAggregates = database.LoadSessionAggregates(reparentNow);
            var alphaRow = reparentAggregates.Single(item => item.Metadata.ThreadId == "alpha");
            var betaRow = reparentAggregates.Single(item => item.Metadata.ThreadId == "beta");
            Assert.Equal(12L, alphaRow.SessionTotal.Total);
            Assert.Equal(12L, alphaRow.LatestTurnTotal.Total);
            Assert.Equal(12L, alphaRow.RecentUsage.Total);
            Assert.Equal(RecentUsageKind.LatestTurn, alphaRow.RecentKind);
            Assert.Equal(12L, betaRow.SessionTotal.Total);
            Assert.Equal(12L, betaRow.LatestTurnTotal.Total);
            var presenceLifeRow = reparentAggregates.Single(item => item.Metadata.ThreadId == "presence");
            var unrelatedLifeRow = reparentAggregates.Single(item => item.Metadata.ThreadId == "unrelated");
            if (presenceLifeRow.LifecycleMetrics is not { } presenceLife)
                throw new InvalidOperationException("lifecycle_metrics_missing");
            if (unrelatedLifeRow.LifecycleMetrics is not { } unrelatedLife)
                throw new InvalidOperationException("lifecycle_metrics_missing");
            Assert.Equal(24L, presenceLife.Recent48HourTokens);
            Assert.Equal(12L, unrelatedLife.Recent48HourTokens);
            Assert.Equal(12L, ReadFrontierMaximum(databasePath, "alpha"));
            Assert.Equal(12L, ReadFrontierMaximum(databasePath, "beta"));
            Assert.Equal(60L, ReadBucketTotal(databasePath, t0, t0.AddHours(1)));
            Assert.Equal(60L, database.GetCycleTotal(t0, t0.AddHours(1)).Total);
            Assert.Equal(12L, database.GetCycleTotalForThreads(t0, t0.AddHours(1), new[] { "alpha" }).Total);
            Assert.Equal(12L, database.GetCycleTotalForThreads(t0, t0.AddHours(1), new[] { "beta" }).Total);
            Assert.Equal(0L, database.GetCycleTotalForThreads(t0, t0.AddHours(1),
                Array.Empty<string>()).Total);
            Assert.Equal(0L, CountContextBaselines(databasePath, "alpha"));
            Assert.Equal(0L, CountContextBaselines(databasePath, "beta"));
            var reparentSnapshot = new HudSnapshot(
                new QuotaObservation(new QuotaBucket("codex", "Codex", 10, 10080, t0.AddDays(7)),
                    Array.Empty<QuotaBucket>(), QuotaSource.OfficialAppServer, reparentNow, false),
                reparentAggregates, database.GetCycleTotal(t0, t0.AddHours(1)), reparentNow, false, "fresh",
                Array.Empty<string>(), Array.Empty<HudEvent>(),
                RunningCycleTotal: database.GetCycleTotalForThreads(t0, t0.AddHours(1), new[] { "alpha" }));
            var reparentDisplay = HudPresentation.BuildRows(reparentSnapshot);
            Assert.Equal(12L, reparentDisplay.Single(row => row.ThreadId == "other").WorkTotal.Total);
            Assert.Equal(12L, reparentDisplay.Single(row => row.ThreadId == "root").WorkTotal.Total);
            Assert.Equal("自身 0 + 子任务 12",
                reparentDisplay.Single(row => row.ThreadId == "other").WorkBreakdownText);
            Assert.Equal("自身 0 + 子任务 12",
                reparentDisplay.Single(row => row.ThreadId == "root").WorkBreakdownText);
            var reparentCollapsed = HudPresentation.BuildCollapsedText(reparentSnapshot);
            Assert.True(reparentCollapsed.Contains("本周期 60 raw tokens", StringComparison.Ordinal));
            var reparentFrame = HudPresentation.BuildFrame(reparentSnapshot);
            Assert.True(reparentFrame.OverviewText.Contains("本额度周期全部会话合计：60 raw tokens",
                StringComparison.Ordinal));
            Assert.True(reparentFrame.OverviewText.Contains("运行中会话本额度周期合计：12 raw tokens",
                StringComparison.Ordinal));
            var reparentView = new MainViewModel();
            reparentView.Apply(reparentSnapshot);
            Assert.Equal(reparentCollapsed, reparentView.CollapsedText);
            reparentView.SetFilter("all");
            Assert.True(reparentView.ToggleChildren("other"));
            Assert.Equal(12L, reparentView.Rows.Single(row => row.ThreadId == "alpha").SessionTotal.Total);
            Assert.True(reparentView.ToggleChildren("root"));
            Assert.Equal(12L, reparentView.Rows.Single(row => row.ThreadId == "beta").SessionTotal.Total);

            Assert.True(database.ClearSessionParent("beta"));
            DrainAggregates(database);
            Assert.Equal(12L, database.GetSessionTotal("beta").Total);
            Assert.Equal(1, ReadLineageRows(databasePath).Count(row =>
                row.ThreadId == "beta" && row.Canonical == 1 && row.Root == "beta"));

            database.UpsertSession(new SessionMetadata("presence-fields", "Fields", null, null, null, "gpt-test",
                null, null, t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            var missingParts = new TokenComponents(10, null, null, null, 2, null, 12);
            var presenceCount = 0;
            void AcceptPresence(TokenUsageSnapshot snapshot, string turn)
            {
                Assert.True(database.AcceptTokenSample("presence-fields", snapshot, t0.AddSeconds(presenceCount + 2),
                    t0.AddSeconds(presenceCount + 2), turn, null, null));
                presenceCount++;
            }
            AcceptPresence(new TokenUsageSnapshot(missingParts, missingParts, 128000), "turn-base");
            AcceptPresence(new TokenUsageSnapshot(missingParts, missingParts, 0), "turn-ctx0");
            AcceptPresence(new TokenUsageSnapshot(missingParts, missingParts, null), "turn-ctxm");
            foreach (var field in new[] { "cached", "read", "write", "reasoning", "reported" })
            {
                AcceptPresence(new TokenUsageSnapshot(missingParts, WithPresenceField(missingParts, field, 0),
                    128000), "turn-last-" + field);
                AcceptPresence(new TokenUsageSnapshot(WithPresenceField(missingParts, field, 0), missingParts,
                    128000), "turn-total-" + field);
            }
            Assert.Equal(presenceCount, ReadLineageRows(databasePath).Count(row =>
                row.ThreadId == "presence-fields" && row.Canonical == 1));
            Assert.Equal(12L * presenceCount, database.GetSessionTotal("presence-fields").Total);

            database.UpsertSession(new SessionMetadata("conflict-root", "ConflictRoot", null, null, null,
                "gpt-test", null, null, t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            database.UpsertSession(new SessionMetadata("conflict-late", "ConflictLate", null, null, null,
                "gpt-test", null, null, t0, null, Kind: SessionKind.InternalTask,
                Surface: SessionSurface.InternalTask, ParentThreadId: "conflict-root", AgentDepth: 1));
            database.UpsertSession(new SessionMetadata("conflict-early", "ConflictEarly", null, null, null,
                "gpt-test", null, null, t0, null, Kind: SessionKind.InternalTask,
                Surface: SessionSurface.InternalTask, ParentThreadId: "conflict-root", AgentDepth: 1));
            var conflictShared = LineageSnapshot(11, 2, 11, 2);
            Assert.True(database.AcceptTokenSample("conflict-late", conflictShared, t0.AddMinutes(8),
                t0.AddMinutes(8), "turn-late-c", null, null));
            Assert.Equal(13L, database.GetSessionTotal("conflict-late").Total);
            Assert.True(database.AcceptTokenSample("conflict-early", conflictShared, t0.AddMinutes(4),
                t0.AddMinutes(4), "turn-early-c", null, null));
            Assert.Equal(13L, database.GetSessionTotal("conflict-early").Total);
            Assert.Equal(0L, database.GetSessionTotal("conflict-late").Total);

            var recoverNull = LineageSnapshot(10, 2, 10, 2, null);
            database.UpsertSession(new SessionMetadata("recover-root", "RecoverRoot", null, null, null,
                "gpt-test", null, null, t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            database.UpsertSession(new SessionMetadata("recover-win", "RecoverWin", null, null, null,
                "gpt-test", null, null, t0, null, Kind: SessionKind.InternalTask,
                Surface: SessionSurface.InternalTask, ParentThreadId: "recover-root", AgentDepth: 1));
            database.UpsertSession(new SessionMetadata("recover-lose", "RecoverLose", null, null, null,
                "gpt-test", null, null, t0, null, Kind: SessionKind.InternalTask,
                Surface: SessionSurface.InternalTask, ParentThreadId: "recover-root", AgentDepth: 1));
            Assert.True(database.AcceptTokenSample("recover-win", recoverNull, t0.AddMinutes(20),
                t0.AddMinutes(20), "turn-rw", null, null));
            Assert.True(database.AcceptTokenSample("recover-lose", recoverNull, t0.AddMinutes(21),
                t0.AddMinutes(21), "turn-rl", null, null));
            Assert.Equal(12L, database.GetSessionTotal("recover-win").Total);
            Assert.Equal(0L, database.GetSessionTotal("recover-lose").Total);
            Assert.True(database.RecoverPersistedContextWindow(recoverNull.Fingerprint("recover-win"), 128000));
            Assert.Equal(12L, database.GetSessionTotal("recover-win").Total);
            Assert.Equal(12L, database.GetSessionTotal("recover-lose").Total);

            var reverseNull = LineageSnapshot(14, 2, 14, 2, null);
            database.UpsertSession(new SessionMetadata("reverse-root", "ReverseRoot", null, null, null,
                "gpt-test", null, null, t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            database.UpsertSession(new SessionMetadata("reverse-win", "ReverseWin", null, null, null,
                "gpt-test", null, null, t0, null, Kind: SessionKind.InternalTask,
                Surface: SessionSurface.InternalTask, ParentThreadId: "reverse-root", AgentDepth: 1));
            database.UpsertSession(new SessionMetadata("reverse-lose", "ReverseLose", null, null, null,
                "gpt-test", null, null, t0, null, Kind: SessionKind.InternalTask,
                Surface: SessionSurface.InternalTask, ParentThreadId: "reverse-root", AgentDepth: 1));
            Assert.True(database.AcceptTokenSample("reverse-win", reverseNull, t0.AddMinutes(22),
                t0.AddMinutes(22), "turn-rev-w", null, null));
            Assert.True(database.AcceptTokenSample("reverse-lose", reverseNull, t0.AddMinutes(23),
                t0.AddMinutes(23), "turn-rev-l", null, null));
            Assert.Equal(16L, database.GetSessionTotal("reverse-win").Total);
            Assert.Equal(0L, database.GetSessionTotal("reverse-lose").Total);
            Assert.True(database.RecoverPersistedContextWindow(reverseNull.Fingerprint("reverse-lose"), 128000));
            Assert.Equal(16L, database.GetSessionTotal("reverse-win").Total);
            Assert.Equal(16L, database.GetSessionTotal("reverse-lose").Total);

            const string heuristicThread = "ctx-keep";
            const long window = 258_400;
            database.UpsertSession(new SessionMetadata(heuristicThread, "CtxKeep", null, null, null, "gpt-test",
                null, null, t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            long heuristicCumulative = 3_000_000;
            var heuristicTime = t0.AddHours(2);
            foreach (var index in Enumerable.Range(0, 3))
            {
                var turn = "heuristic-" + index;
                var time = heuristicTime.AddMinutes(index * 3);
                Assert.True(database.AcceptTokenSample(heuristicThread,
                    new TokenUsageSnapshot(new TokenComponents(heuristicCumulative, null, null, null, 0, null,
                        heuristicCumulative), new TokenComponents(232_000, null, null, null, 0, null, 232_000),
                        window), time, time, turn, "gpt-test", null));
                Assert.True(database.AcceptTokenSample(heuristicThread,
                    new TokenUsageSnapshot(new TokenComponents(heuristicCumulative, null, null, null, 0, null,
                        heuristicCumulative), new TokenComponents(0, null, null, null, 0, null, 0), window),
                    time.AddSeconds(10), time.AddSeconds(10), turn, "gpt-test", null));
                heuristicCumulative += 106_000 + index * 2_000;
                Assert.True(database.AcceptTokenSample(heuristicThread,
                    new TokenUsageSnapshot(new TokenComponents(heuristicCumulative, null, null, null, 0, null,
                        heuristicCumulative),
                        new TokenComponents(106_000 + index * 2_000, null, null, null, 0, null,
                            106_000 + index * 2_000), window),
                    time.AddSeconds(20), time.AddSeconds(20), turn, "gpt-test", null));
            }
            var heuristicBefore = database.LoadSessionContextMetrics()[heuristicThread];
            Assert.Equal(3, heuristicBefore.PostCompactionSampleCount);
            Assert.Equal(108_000L, heuristicBefore.PostCompactionInputTokens);
            Assert.Equal(window, heuristicBefore.PostCompactionWindowTokens);
            Assert.True(heuristicBefore.HasReliablePostCompactionBaseline);
            Assert.True(heuristicBefore.TurnRunwayChangePercent.HasValue);
            Assert.True(heuristicBefore.TokenRunwayChangePercent.HasValue);
            var heuristicLifecycleBefore = database.LoadSessionAggregates(heuristicTime.AddHours(1))
                .Single(item => item.Metadata.ThreadId == heuristicThread);
            if (heuristicLifecycleBefore.LifecycleMetrics is not { } heuristicLifeBefore)
                throw new InvalidOperationException("lifecycle_metrics_missing");
            var heuristicTokensBefore = heuristicLifeBefore.Recent48HourTokens;
            Assert.True(heuristicTokensBefore > 0);

            database.UpsertSession(new SessionMetadata("dummy-parent", "DummyParent", null, null, null,
                "gpt-test", null, null, t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            database.UpsertSession(new SessionMetadata("dummy-child", "DummyChild", null, null, null,
                "gpt-test", null, null, t0, null, Kind: SessionKind.InternalTask,
                Surface: SessionSurface.InternalTask, ParentThreadId: "dummy-parent", AgentDepth: 1));
            Assert.True(database.AcceptTokenSample("dummy-child", LineageSnapshot(9, 1, 9, 1),
                t0.AddMinutes(40), t0.AddMinutes(40), "turn-dummy", "gpt-test", null));
            Assert.True(database.ClearSessionParent("dummy-child"));
            DrainAggregates(database);
            var heuristicAfter = database.LoadSessionContextMetrics()[heuristicThread];
            Assert.Equal(heuristicBefore.PostCompactionSampleCount, heuristicAfter.PostCompactionSampleCount);
            Assert.Equal(heuristicBefore.PostCompactionInputTokens, heuristicAfter.PostCompactionInputTokens);
            Assert.Equal(heuristicBefore.PostCompactionWindowTokens, heuristicAfter.PostCompactionWindowTokens);
            Assert.Equal(heuristicBefore.TurnRunwayChangePercent, heuristicAfter.TurnRunwayChangePercent);
            Assert.Equal(heuristicBefore.TokenRunwayChangePercent, heuristicAfter.TokenRunwayChangePercent);
            var heuristicLifecycleAfter = database.LoadSessionAggregates(heuristicTime.AddHours(1))
                .Single(item => item.Metadata.ThreadId == heuristicThread);
            if (heuristicLifecycleAfter.LifecycleMetrics is not { } heuristicLifeAfter)
                throw new InvalidOperationException("lifecycle_metrics_missing");
            Assert.Equal(heuristicTokensBefore, heuristicLifeAfter.Recent48HourTokens);

            Assert.True(database.AcceptTokenSample("late-child", shared, t0.AddMinutes(10), t0.AddMinutes(10),
                "turn-late", null, null));
            Assert.Equal(12L, database.GetSessionTotal("late-child").Total);
            Assert.True(database.AcceptTokenSample("late-parent", shared, t0.AddMinutes(3), t0.AddMinutes(3),
                "turn-early", null, null));
            database.UpsertSession(new SessionMetadata("late-parent", "LateParent", null, null, null, "gpt-test",
                null, null, t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            database.UpsertSession(new SessionMetadata("late-child", "LateChild", null, null, null, "gpt-test",
                null, null, t0, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "late-parent", AgentDepth: 1));
            DrainAggregates(database);
            Assert.Equal(12L, database.GetSessionTotal("late-parent").Total);
            Assert.Equal(0L, database.GetSessionTotal("late-child").Total);

            var snapshot = new HudSnapshot(
                new QuotaObservation(new QuotaBucket("codex", "Codex", 10, 10080, t0.AddDays(7)),
                    Array.Empty<QuotaBucket>(), QuotaSource.OfficialAppServer, t0.AddHours(1), false),
                database.LoadSessionAggregates(t0.AddHours(1)),
                database.GetCycleTotal(t0, t0.AddHours(1)), t0.AddHours(1), false, "fresh",
                Array.Empty<string>());
            var display = HudPresentation.BuildRows(snapshot);
            Assert.Equal(24L, display.Single(row => row.ThreadId == "presence").SessionTotal.Total);
            Assert.Equal(12L, display.Single(row => row.ThreadId == "late-parent").WorkTotal.Total);
            Assert.Equal(0L, display.Single(row => row.ThreadId == "late-child").SessionTotal.Total);
            AssertNonNegativeAggregates(databasePath);
        }

        using var restarted = new UsageDatabase(databasePath);
        DrainAggregates(restarted);
        Assert.Equal(24L, restarted.GetSessionTotal("presence").Total);
        Assert.Equal(12L, restarted.GetSessionTotal("alpha").Total);
        Assert.Equal(12L, restarted.GetSessionTotal("beta").Total);
        Assert.Equal(12L, restarted.GetSessionTotal("late-parent").Total);
        Assert.Equal(0L, restarted.GetSessionTotal("late-child").Total);
    }

    private static void LineageIndependentOracleDefectInjection(string runRoot)
    {
        AssertLineageSampleSqlForms();
        AssertLiveSizedIndependentElection();

        var directory = Path.Combine(runRoot, "lineage-oracle");
        Directory.CreateDirectory(directory);
        var schema9Path = Path.Combine(directory, "schema9.db");
        CreateSchemaV9LineageFixture(schema9Path);
        var schema9 = CaptureLineageCheck(schema9Path);
        Assert.True(schema9.Text.Contains("schema=9", StringComparison.Ordinal));
        Assert.True(schema9.Text.Contains("samples=4", StringComparison.Ordinal));
        Assert.True(!schema9.Text.Contains("no such column", StringComparison.Ordinal));

        var databasePath = Path.Combine(directory, "usage.db");
        var t0 = DateTimeOffset.Parse("2026-06-01T00:00:00Z", CultureInfo.InvariantCulture);
        using (var database = new UsageDatabase(databasePath))
        {
            database.UpsertSession(new SessionMetadata("oracle-root", "Root", null, null, null, null, null, null,
                t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            database.UpsertSession(new SessionMetadata("oracle-child", "Child", null, null, null, null, null, null,
                t0, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "oracle-root", AgentDepth: 1));
            var shared = LineageSnapshot(10, 2, 10, 2);
            var tail = LineageSnapshot(13, 3, 3, 1);
            Assert.True(database.AcceptTokenSample("oracle-root", shared, t0, t0, "turn-root", null, null));
            Assert.True(database.AcceptTokenSample("oracle-child", shared, t0.AddMinutes(1), t0.AddMinutes(1),
                "turn-child", null, null));
            Assert.True(database.AcceptTokenSample("oracle-child", tail, t0.AddMinutes(2), t0.AddMinutes(2),
                "turn-child", null, null));
            Assert.Equal(12L, database.GetSessionTotal("oracle-root").Total);
            Assert.Equal(4L, database.GetSessionTotal("oracle-child").Total);
        }

        var matched = CaptureLineageCheck(databasePath);
        Assert.Equal(0, matched.Code);
        Assert.True(matched.Text.Contains("status=MATCH", StringComparison.Ordinal));

        FlipCanonicalOff(databasePath, "oracle-child");
        var zeroWinners = CaptureLineageCheck(databasePath);
        Assert.Equal(1, zeroWinners.Code);
        Assert.True(zeroWinners.Text.Contains("status=MISMATCH", StringComparison.Ordinal));

        RestoreLatestCanonical(databasePath, "oracle-child");
        CorruptStoredIdentity(databasePath, "oracle-root");
        var identityDefect = CaptureLineageCheck(databasePath);
        Assert.Equal(1, identityDefect.Code);
        Assert.True(identityDefect.Text.Contains("status=MISMATCH", StringComparison.Ordinal));

        var encoderPath = Path.Combine(directory, "encoder.db");
        using (var encoder = new UsageDatabase(encoderPath))
        {
            encoder.UpsertSession(new SessionMetadata("enc-a", "A", null, null, null, null, null, null,
                t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            encoder.UpsertSession(new SessionMetadata("enc-b", "B", null, null, null, null, null, null,
                t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.Cli));
            encoder.UpsertSession(new SessionMetadata("enc-c", "C", null, null, null, null, null, null,
                t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            Assert.True(encoder.AcceptTokenSample("enc-a", LineageSnapshot(10, 2, 10, 2), t0, t0,
                "turn-a", null, null));
            Assert.True(encoder.AcceptTokenSample("enc-b", LineageSnapshot(13, 3, 3, 1), t0, t0,
                "turn-b", null, null));
            Assert.True(encoder.AcceptTokenSample("enc-c", LineageSnapshot(15, 1, 15, 1), t0, t0,
                "turn-c", null, null));
        }

        var originalA = ReadLiveMaterial(encoderPath, "enc-a");
        var originalB = ReadLiveMaterial(encoderPath, "enc-b");
        InjectMatchingHashMaterial(encoderPath, "enc-a", ReorderLiveTupleFields(originalA));
        InjectMatchingHashMaterial(encoderPath, "enc-b", OmitLiveTupleField(originalB));
        InjectMatchingHashMaterial(encoderPath, "enc-c", "lineage-semantic-v2|not-a-tuple|m|m");
        var encoderDefect = CaptureLineageCheck(encoderPath);
        Assert.Equal(1, encoderDefect.Code);
        Assert.True(encoderDefect.Text.Contains("status=MISMATCH", StringComparison.Ordinal));

        AssertIndependentEqualTimeOrder(t0);
        AssertEqualTimeAncestorRebuild(directory, t0);
    }

    private static LineageCycleRow IndependentOrderRow(long id, string threadId, long? ticks, string? sourceKey,
        int depth, string root, string identity)
    {
        var last = new CanonicalTokenUsage(1, 1, 0, 0, 0, 0, 1, 1);
        var row = new LineageCycleRow(id, threadId, last, 1, 0, 1, null, ticks, sourceKey, 0, 0, "", "",
            root, false, "");
        row.IndependentRoot = root;
        row.IndependentDepth = depth;
        row.IndependentIdentity = identity;
        return row;
    }

    private static void AssertIndependentEqualTimeOrder(DateTimeOffset t0)
    {
        var ticks = t0.UtcTicks;
        var ancestor = IndependentOrderRow(1, "eq-parent", ticks, "win-v2:zzzz-parent", 0, "eq-parent", "shared");
        var descendant = IndependentOrderRow(2, "eq-child", ticks, "win-v2:aaaa-child", 1, "eq-parent", "shared");
        Assert.True(IndependentLineage.CompareOrder(ancestor, descendant) < 0);
        var elected = IndependentLineage.ElectCanonicalIds(new[] { descendant, ancestor });
        Assert.True(elected.Contains(1));
        Assert.True(!elected.Contains(2));

        var sibLateKey = IndependentOrderRow(3, "eq-sib-a", ticks, "win-v2:zzzz-sib", 1, "eq-sib-root", "sib");
        var sibEarlyKey = IndependentOrderRow(4, "eq-sib-b", ticks, "win-v2:aaaa-sib", 1, "eq-sib-root", "sib");
        Assert.True(IndependentLineage.CompareOrder(sibEarlyKey, sibLateKey) < 0);
        var sibElected = IndependentLineage.ElectCanonicalIds(new[] { sibLateKey, sibEarlyKey });
        Assert.True(sibElected.Contains(4));
        Assert.True(!sibElected.Contains(3));

        var earlierChild = IndependentOrderRow(5, "early-child", ticks, "win-v2:zzzz-child", 1, "early-parent",
            "early");
        var laterParent = IndependentOrderRow(6, "early-parent", ticks + 1, "win-v2:aaaa-parent", 0, "early-parent",
            "early");
        Assert.True(IndependentLineage.CompareOrder(earlierChild, laterParent) < 0);

        var unrelatedA = IndependentOrderRow(7, "unrelated-a", ticks, "win-v2:zzzz", 0, "unrelated-a", "shared");
        var unrelatedB = IndependentOrderRow(8, "unrelated-b", ticks, "win-v2:aaaa", 0, "unrelated-b", "shared");
        var unrelatedElected = IndependentLineage.ElectCanonicalIds(new[] { unrelatedA, unrelatedB });
        Assert.True(unrelatedElected.Contains(7) && unrelatedElected.Contains(8));

        var degradedParent = IndependentOrderRow(9, "deg-parent", null, "win-v2:zzzz-parent", 0, "deg-parent",
            "degraded");
        var degradedChild = IndependentOrderRow(10, "deg-child", null, "win-v2:aaaa-child", 1, "deg-parent",
            "degraded");
        Assert.True(IndependentLineage.CompareOrder(degradedChild, degradedParent) < 0);
        var degradedElected = IndependentLineage.ElectCanonicalIds(new[] { degradedParent, degradedChild });
        Assert.True(degradedElected.Contains(10));
        Assert.True(!degradedElected.Contains(9));

        var parents = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["eq-parent"] = null,
            ["eq-child"] = "eq-parent",
            ["eq-grand"] = "eq-child",
            ["missing-child"] = "no-such-parent",
            ["cycle-a"] = "cycle-b",
            ["cycle-b"] = "cycle-a",
        };
        Assert.Equal(0, IndependentLineage.ResolvedDepth("eq-parent", parents));
        Assert.Equal(1, IndependentLineage.ResolvedDepth("eq-child", parents));
        Assert.Equal(2, IndependentLineage.ResolvedDepth("eq-grand", parents));
        Assert.Equal(0, IndependentLineage.ResolvedDepth("missing-child", parents));
        Assert.Equal(0, IndependentLineage.ResolvedDepth("cycle-a", parents));
        Assert.Equal(0, IndependentLineage.ResolvedDepth("cycle-b", parents));
        Assert.Equal("missing-child", IndependentLineage.ResolveRoot("missing-child", parents));
        Assert.Equal("cycle-a", IndependentLineage.ResolveRoot("cycle-a", parents));
    }

    private static void AssertEqualTimeAncestorRebuild(string directory, DateTimeOffset t0)
    {
        var equalTimePath = Path.Combine(directory, "equal-time.db");
        var shared = LineageSnapshot(10, 2, 10, 2);
        var tail = LineageSnapshot(13, 3, 3, 1);
        using (var database = new UsageDatabase(equalTimePath))
        {
            database.UpsertSession(new SessionMetadata("eq-parent", "Parent", null, null, null, null, null, null,
                t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            database.UpsertSession(new SessionMetadata("eq-child", "Child", null, null, null, null, null, null,
                t0, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "eq-parent", AgentDepth: 1));
            database.UpsertSession(new SessionMetadata("eq-sib-root", "SibRoot", null, null, null, null, null, null,
                t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            database.UpsertSession(new SessionMetadata("eq-sib-a", "SibA", null, null, null, null, null, null,
                t0, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "eq-sib-root", AgentDepth: 1));
            database.UpsertSession(new SessionMetadata("eq-sib-b", "SibB", null, null, null, null, null, null,
                t0, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "eq-sib-root", AgentDepth: 1));
            database.UpsertSession(new SessionMetadata("early-parent", "EarlyParent", null, null, null, null, null,
                null, t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            database.UpsertSession(new SessionMetadata("early-child", "EarlyChild", null, null, null, null, null,
                null, t0, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "early-parent", AgentDepth: 1));
            database.UpsertSession(new SessionMetadata("unrelated-a", "UA", null, null, null, null, null, null,
                t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            database.UpsertSession(new SessionMetadata("unrelated-b", "UB", null, null, null, null, null, null,
                t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.Cli));
            database.UpsertSession(new SessionMetadata("missing-child", "Missing", null, null, null, null, null,
                null, t0, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "no-such-parent", AgentDepth: 1));
            database.UpsertSession(new SessionMetadata("cycle-a", "CA", null, null, null, null, null, null,
                t0, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "cycle-b", AgentDepth: 1));
            database.UpsertSession(new SessionMetadata("cycle-b", "CB", null, null, null, null, null, null,
                t0, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "cycle-a", AgentDepth: 1));
            database.UpsertSession(new SessionMetadata("deg-parent", "DegParent", null, null, null, null, null,
                null, t0, null, Kind: SessionKind.Primary, Surface: SessionSurface.App));
            database.UpsertSession(new SessionMetadata("deg-child", "DegChild", null, null, null, null, null, null,
                t0, null, Kind: SessionKind.InternalTask, Surface: SessionSurface.InternalTask,
                ParentThreadId: "deg-parent", AgentDepth: 1));
            var degraded = LineageSnapshot(14, 2, 14, 2);
            Assert.True(database.AcceptTokenSample("eq-parent", shared, t0, t0, "turn-parent", null, null));
            Assert.True(database.AcceptTokenSample("eq-child", shared, t0, t0, "turn-child", null, null));
            Assert.True(database.AcceptTokenSample("eq-child", tail, t0.AddMinutes(2), t0.AddMinutes(2),
                "turn-child", null, null));
            Assert.True(database.AcceptTokenSample("eq-sib-a", shared, t0, t0, "turn-sib-a", null, null));
            Assert.True(database.AcceptTokenSample("eq-sib-b", shared, t0, t0, "turn-sib-b", null, null));
            Assert.True(database.AcceptTokenSample("early-child", shared, t0, t0, "turn-early-child", null, null));
            Assert.True(database.AcceptTokenSample("early-parent", shared, t0.AddMinutes(1), t0.AddMinutes(1),
                "turn-early-parent", null, null));
            Assert.True(database.AcceptTokenSample("unrelated-a", shared, t0, t0, "turn-ua", null, null));
            Assert.True(database.AcceptTokenSample("unrelated-b", shared, t0, t0, "turn-ub", null, null));
            Assert.True(database.AcceptTokenSample("missing-child", shared, t0, t0, "turn-missing", null, null));
            Assert.True(database.AcceptTokenSample("cycle-a", shared, t0, t0, "turn-ca", null, null));
            Assert.True(database.AcceptTokenSample("cycle-b", shared, t0, t0, "turn-cb", null, null));
            Assert.True(database.AcceptTokenSample("deg-parent", degraded, null, t0, "turn-deg-parent", null, null));
            Assert.True(database.AcceptTokenSample("deg-child", degraded, null, t0, "turn-deg-child", null, null));
        }

        SetThreadSourceKey(equalTimePath, "eq-parent", "win-v2:zzzz-parent");
        SetThreadSourceKey(equalTimePath, "eq-child", "win-v2:aaaa-child");
        SetThreadSourceKey(equalTimePath, "eq-sib-a", "win-v2:zzzz-sib");
        SetThreadSourceKey(equalTimePath, "eq-sib-b", "win-v2:aaaa-sib");
        SetThreadSourceKey(equalTimePath, "early-parent", "win-v2:aaaa-early-parent");
        SetThreadSourceKey(equalTimePath, "early-child", "win-v2:zzzz-early-child");
        SetThreadSourceKey(equalTimePath, "deg-parent", "win-v2:zzzz-deg-parent");
        SetThreadSourceKey(equalTimePath, "deg-child", "win-v2:aaaa-deg-child");

        using (var database = new UsageDatabase(equalTimePath))
        {
            database.RebuildLineageCanonical();
            DrainAggregates(database);
            Assert.Equal(12L, database.GetSessionTotal("eq-parent").Total);
            Assert.Equal(4L, database.GetSessionTotal("eq-child").Total);
            Assert.Equal(0L, database.GetSessionTotal("eq-sib-a").Total);
            Assert.Equal(12L, database.GetSessionTotal("eq-sib-b").Total);
            Assert.Equal(0L, database.GetSessionTotal("early-parent").Total);
            Assert.Equal(12L, database.GetSessionTotal("early-child").Total);
            Assert.Equal(12L, database.GetSessionTotal("unrelated-a").Total);
            Assert.Equal(12L, database.GetSessionTotal("unrelated-b").Total);
            Assert.Equal(12L, database.GetSessionTotal("missing-child").Total);
            Assert.Equal(12L, database.GetSessionTotal("cycle-a").Total);
            Assert.Equal(12L, database.GetSessionTotal("cycle-b").Total);
            Assert.Equal(0L, database.GetSessionTotal("deg-parent").Total);
            Assert.Equal(16L, database.GetSessionTotal("deg-child").Total);
            var rows = ReadLineageRows(equalTimePath);
            var parent = rows.Single(row => row.ThreadId == "eq-parent");
            Assert.Equal(1, parent.Canonical);
            Assert.Equal("eq-parent", parent.Root);
            var copied = rows.Single(row => row.ThreadId == "eq-child" && row.Identity == parent.Identity);
            Assert.Equal(0, copied.Canonical);
            Assert.Equal("eq-parent", copied.Root);
            var childTail = rows.Single(row => row.ThreadId == "eq-child" && row.Identity != parent.Identity);
            Assert.Equal(1, childTail.Canonical);
            Assert.Equal(0, rows.Single(row => row.ThreadId == "eq-sib-a").Canonical);
            Assert.Equal(1, rows.Single(row => row.ThreadId == "eq-sib-b").Canonical);
            Assert.Equal(0, rows.Single(row => row.ThreadId == "early-parent").Canonical);
            Assert.Equal(1, rows.Single(row => row.ThreadId == "early-child").Canonical);
            Assert.Equal(1, rows.Single(row => row.ThreadId == "unrelated-a").Canonical);
            Assert.Equal(1, rows.Single(row => row.ThreadId == "unrelated-b").Canonical);
            Assert.Equal(1, rows.Single(row => row.ThreadId == "missing-child").Canonical);
            Assert.Equal("missing-child", rows.Single(row => row.ThreadId == "missing-child").Root);
            Assert.True(rows.Where(row => row.ThreadId is "cycle-a" or "cycle-b")
                .All(row => row.Root == row.ThreadId && row.Canonical == 1));
            Assert.Equal(0, rows.Single(row => row.ThreadId == "deg-parent").Canonical);
            Assert.Equal(1, rows.Single(row => row.ThreadId == "deg-child").Canonical);
            Assert.Equal("deg-parent", rows.Single(row => row.ThreadId == "deg-child").Root);
            Assert.True(!database.ContainsPrivacySentinel(Sentinel));
            AssertNonNegativeAggregates(equalTimePath);
        }

        using (var restarted = new UsageDatabase(equalTimePath))
        {
            DrainAggregates(restarted);
            Assert.Equal(12L, restarted.GetSessionTotal("eq-parent").Total);
            Assert.Equal(4L, restarted.GetSessionTotal("eq-child").Total);
            Assert.Equal(0L, restarted.GetSessionTotal("deg-parent").Total);
            Assert.Equal(16L, restarted.GetSessionTotal("deg-child").Total);
            var restartRows = ReadLineageRows(equalTimePath);
            Assert.Equal(1, restartRows.Single(row => row.ThreadId == "eq-parent").Canonical);
            Assert.Equal(0, restartRows.Single(row => row.ThreadId == "deg-parent").Canonical);
            Assert.Equal(1, restartRows.Single(row => row.ThreadId == "deg-child").Canonical);
            Assert.True(!restarted.ContainsPrivacySentinel(Sentinel));
            AssertNonNegativeAggregates(equalTimePath);
        }

        var matched = CaptureLineageCheck(equalTimePath);
        Assert.Equal(0, matched.Code);
        Assert.True(matched.Text.Contains("status=MATCH", StringComparison.Ordinal));
    }

    private static (int Code, string Text) CaptureLineageCheck(string databasePath)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            return (RunLineageCycleCheck(new[] { databasePath }), writer.ToString());
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private static void AssertLineageSampleSqlForms()
    {
        var schema9 = IndependentLineage.SampleSql(false, false);
        var schema10 = IndependentLineage.SampleSql(true, true);
        var schema10NoAlias = IndependentLineage.SampleSql(true, false);
        Assert.Equal(
            "SELECT id, thread_id, input_tokens, raw_input_tokens, cached_input_tokens, cache_write_input_tokens, output_tokens, reasoning_output_tokens, canonical_total_tokens, reported_total_tokens, cumulative_input_tokens, cumulative_output_tokens, cumulative_total_tokens, context_window, event_time_ticks, source_key, source_generation, source_offset FROM token_samples",
            NormalizeSql(schema9));
        Assert.Equal(
            "SELECT id, thread_id, input_tokens, raw_input_tokens, cached_input_tokens, cache_write_input_tokens, output_tokens, reasoning_output_tokens, canonical_total_tokens, reported_total_tokens, cumulative_input_tokens, cumulative_output_tokens, cumulative_total_tokens, context_window, event_time_ticks, source_key, source_generation, source_offset, semantic_identity, semantic_material, lineage_root_thread_id, is_lineage_canonical, legacy_semantic_identity FROM token_samples",
            NormalizeSql(schema10));
        Assert.Equal(
            "SELECT id, thread_id, input_tokens, raw_input_tokens, cached_input_tokens, cache_write_input_tokens, output_tokens, reasoning_output_tokens, canonical_total_tokens, reported_total_tokens, cumulative_input_tokens, cumulative_output_tokens, cumulative_total_tokens, context_window, event_time_ticks, source_key, source_generation, source_offset, semantic_identity, semantic_material, lineage_root_thread_id, is_lineage_canonical, '' FROM token_samples",
            NormalizeSql(schema10NoAlias));
        Assert.True(schema9.IndexOf("SELECT id", StringComparison.Ordinal) <
                    schema9.IndexOf("FROM token_samples", StringComparison.Ordinal));
        Assert.True(schema10.IndexOf("is_lineage_canonical", StringComparison.Ordinal) <
                    schema10.IndexOf("FROM token_samples", StringComparison.Ordinal));
        Assert.True(!schema9.Contains("semantic_identity", StringComparison.Ordinal));
    }

    private static string NormalizeSql(string sql) =>
        string.Join(' ', sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static void AssertLiveSizedIndependentElection()
    {
        const int count = 216_000;
        var rows = new List<LineageCycleRow>(count);
        var last = new CanonicalTokenUsage(1, 1, 0, 0, 0, 0, 1, 1);
        for (var index = 0; index < count; index++)
        {
            var row = new LineageCycleRow(index + 1, "thread-" + (index % 16), last, index, 0, index, null,
                index, "src", 0, index, "", "", "root-" + (index % 8), false, "");
            row.IndependentRoot = "root-" + (index % 8);
            rows.Add(row);
        }

        var stopwatch = Stopwatch.StartNew();
        foreach (var row in rows) IndependentLineage.Describe(row);
        var winners = IndependentLineage.ElectCanonicalIds(rows);
        stopwatch.Stop();
        if (stopwatch.ElapsedMilliseconds >= 5_000)
            throw new InvalidOperationException("live_sized_election_ms=" + stopwatch.ElapsedMilliseconds);
        Assert.Equal(count, winners.Count);
    }

    private static string ReadLiveMaterial(string databasePath, string threadId)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT semantic_material FROM token_samples
            WHERE thread_id = $thread_id AND semantic_material LIKE 'lineage-semantic-v2|%'
            ORDER BY id LIMIT 1;
            """;
        command.Parameters.AddWithValue("$thread_id", threadId);
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ??
               throw new InvalidOperationException("live_material_missing");
    }

    private static void InjectMatchingHashMaterial(string databasePath, string threadId, string material)
    {
        var identity = IndependentLineage.Hash(material);
        using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE token_samples
            SET semantic_material = $material, semantic_identity = $identity
            WHERE thread_id = $thread_id AND semantic_material LIKE 'lineage-semantic-v2|%';
            """;
        command.Parameters.AddWithValue("$material", material);
        command.Parameters.AddWithValue("$identity", identity);
        command.Parameters.AddWithValue("$thread_id", threadId);
        command.ExecuteNonQuery();
    }

    private static string ReorderLiveTupleFields(string material)
    {
        var parts = material.Split('|');
        if (parts.Length != 4) throw new InvalidOperationException("live_material_shape");
        var fields = parts[1].Split(',');
        if (fields.Length != 7) throw new InvalidOperationException("live_tuple_shape");
        (fields[0], fields[6]) = (fields[6], fields[0]);
        parts[1] = string.Join(',', fields);
        return string.Join('|', parts);
    }

    private static string OmitLiveTupleField(string material)
    {
        var parts = material.Split('|');
        if (parts.Length != 4) throw new InvalidOperationException("live_material_shape");
        var fields = parts[1].Split(',');
        if (fields.Length != 7) throw new InvalidOperationException("live_tuple_shape");
        parts[1] = string.Join(',', fields.Take(6));
        return string.Join('|', parts);
    }

    private static TokenComponents WithPresenceField(TokenComponents source, string field, long? value) =>
        field switch
        {
            "cached" => source with { CachedInputTokens = value },
            "read" => source with { CacheReadInputTokens = value },
            "write" => source with { CacheWriteInputTokens = value },
            "reasoning" => source with { ReasoningOutputTokens = value },
            "reported" => source with { TotalTokens = value },
            _ => throw new InvalidOperationException("unknown_presence_field"),
        };

    private static void SetThreadSourceKey(string databasePath, string threadId, string sourceKey)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE token_samples SET source_key = $source_key WHERE thread_id = $thread_id;
            """;
        command.Parameters.AddWithValue("$source_key", sourceKey);
        command.Parameters.AddWithValue("$thread_id", threadId);
        command.ExecuteNonQuery();
    }

    private static void FlipCanonicalOff(string databasePath, string threadId)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE token_samples SET is_lineage_canonical = 0
            WHERE thread_id = $thread_id AND is_lineage_canonical = 1;
            """;
        command.Parameters.AddWithValue("$thread_id", threadId);
        command.ExecuteNonQuery();
    }

    private static void RestoreLatestCanonical(string databasePath, string threadId)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE token_samples SET is_lineage_canonical = 1
            WHERE id = (SELECT MAX(id) FROM token_samples WHERE thread_id = $thread_id);
            """;
        command.Parameters.AddWithValue("$thread_id", threadId);
        command.ExecuteNonQuery();
    }

    private static void CorruptStoredIdentity(string databasePath, string threadId)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE token_samples
            SET semantic_identity = '0000000000000000000000000000000000000000000000000000000000000000'
            WHERE thread_id = $thread_id AND is_lineage_canonical = 1;
            """;
        command.Parameters.AddWithValue("$thread_id", threadId);
        command.ExecuteNonQuery();
    }

    private static long ReadFrontierMaximum(string databasePath, string threadId)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COALESCE(maximum_cumulative_total, 0) FROM cumulative_frontiers WHERE thread_id = $thread_id;";
        command.Parameters.AddWithValue("$thread_id", threadId);
        var value = command.ExecuteScalar();
        return value is null or DBNull ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static long ReadBucketTotal(string databasePath, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(SUM(canonical_total_tokens), 0) FROM token_time_buckets
            WHERE bucket_start_ticks >= $start AND bucket_start_ticks < $end;
            """;
        command.Parameters.AddWithValue("$start", startUtc.UtcTicks);
        command.Parameters.AddWithValue("$end", endUtc.UtcTicks);
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static string TokenLine(string threadId, long timestamp, long totalInput, long totalOutput,
        long lastInput, long lastOutput) =>
        $"{{\"type\":\"event_msg\",\"payload\":{{\"type\":\"token_count\",\"thread_id\":\"{threadId}\",\"timestamp\":{timestamp},\"info\":{{\"total_token_usage\":{{\"input_tokens\":{totalInput},\"output_tokens\":{totalOutput},\"total_tokens\":{totalInput + totalOutput}}},\"last_token_usage\":{{\"input_tokens\":{lastInput},\"output_tokens\":{lastOutput},\"total_tokens\":{lastInput + lastOutput}}},\"context_window\":128000}}}}}}";

    private static string QuotaJson(string id, string name, double used, long reset) =>
        $"{{\"result\":{{\"rate_limits\":[{{\"id\":\"{id}\",\"name\":\"{name}\",\"usedPercent\":{used.ToString(CultureInfo.InvariantCulture)},\"windowDurationMins\":10080,\"resetsAt\":{reset}}}]}}}}";

    private static UsageDatabase NewDatabase(string runRoot, string name)
    {
        var directory = Path.Combine(runRoot, name);
        Directory.CreateDirectory(directory);
        return new UsageDatabase(Path.Combine(directory, "usage.db"));
    }

    private static string NewRunRoot(string category)
    {
        var root = Path.Combine(ProjectRoot(), ".artifacts", "correction-04", category,
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string ProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "CodexUsageHud.sln")))
            current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("project_root_not_found");
    }

    private static string Fixture(string relative) => Path.Combine(ProjectRoot(), "tests", "fixtures", relative);

    private static class Assert
    {
        public static void True(bool condition)
        {
            if (!condition) throw new InvalidOperationException("assertion_failed");
        }

        public static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"assertion_failed expected={expected} actual={actual}");
        }

        public static void Near(double expected, double actual, double tolerance)
        {
            if (!double.IsFinite(actual) || Math.Abs(expected - actual) > tolerance)
                throw new InvalidOperationException(
                    $"assertion_failed expected={expected} actual={actual} tolerance={tolerance}");
        }

        public static void NotNull(object? value)
        {
            if (value is null) throw new InvalidOperationException("assertion_failed");
        }

        public static void DoesNotContain(string value, string text)
        {
            if (text.Contains(value, StringComparison.Ordinal)) throw new InvalidOperationException("assertion_failed");
        }

        public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        {
            if (!expected.SequenceEqual(actual)) throw new InvalidOperationException("assertion_failed");
        }
    }
}
