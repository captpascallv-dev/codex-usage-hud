# Codex Usage HUD MVP Technical Specification

Version: 1.3-continuation-risk
Date: 2026-08-12
Platform: Windows 10/11 x64
Runtime: self-contained .NET 8 WPF
UI language: Simplified Chinese

## 1. Product outcome

Deliver a low-resource, always-on-top Windows HUD that lets one person observe
official Codex quota windows and privacy-safe raw-token metadata across many
concurrent local Codex sessions. It must remain useful when Codex is minimized,
survive log/app/computer restarts without losing or doubling totals, and fail
open at the UI layer when one source is damaged or unavailable.

The default experience is an independent draggable HUD. An optional
`宠物伴随模式` may position the collapsed HUD beside the Codex pet when a valid
pet anchor is mechanically available; the HUD must remain independently
usable and must not disappear merely because the pet is hidden.

## 2. Explicit non-goals

No cloud sync, team/multi-account management, billing estimates, currency,
complex charts, cross-platform shell, direct ChatGPT HTTP scraping, browser
cookies, login automation, credential extraction, prompt/response indexing,
message search, auto-update service, or deliberate service-tier changes.

## 3. Solution shape

Create this structure unless a small naming adjustment is needed by tooling:

```text
CodexUsageHud.sln
src/
  CodexUsageHud.Core/       domain, parsers, state machines, SQLite
  CodexUsageHud.App/        WPF shell, tray, composition root
tests/
  CodexUsageHud.Tests/      unit and integration tests
tests/fixtures/             synthetic privacy-safe JSONL
scripts/                    build, publish, and acceptance helpers
docs/                       specification and acceptance evidence
dist/                       ignored/generated release output
.artifacts/                 ignored/generated test and evidence output
.tools/                     ignored/project-local .NET SDK and NuGet cache
```

Use `Microsoft.Data.Sqlite`; WPF may use `System.Windows.Forms.NotifyIcon` for
the tray. Keep parsing/domain/storage testable without creating a WPF window.
Use dependency injection only to the extent it improves source isolation and
tests; avoid a large framework.

## 4. Runtime read allowlist

Resolve `CODEX_HOME` from that environment variable when present, otherwise
`%USERPROFILE%\.codex`. Runtime adapters may read only:

- `sessions/**/*.jsonl`
- `archived_sessions/**/*.jsonl`
- `state_5.sqlite` through an allowlisted read-only query
- `session_index.jsonl` as a metadata fallback
- `.codex-global-state.json` only for the optional pet anchor fields
- the local Codex executable and its App Server stdin/stdout protocol

Never open `auth.json`, `.env`, cookies, credential/key/token files, shell
history, browser storage, or unrelated Codex logs. Never enumerate or read any
Concerto path.

For `state_5.sqlite`, inspect `PRAGMA table_info(threads)` and select only the
intersection of these columns: `id`, `rollout_path`, `created_at`,
`updated_at`, `cwd`, `model`, `reasoning_effort`, `source`, `agent_nickname`,
`agent_role`, and `name`. Never select `title`, `first_user_message`,
`preview`, or any unlisted column. Prefer `name`, then `session_index`'s
`thread_name`, then a shortened thread id for display. Derive the project tag
from the final directory name of `cwd`; do not expose full paths in the UI.

## 5. Privacy-preserving JSONL reader

Unknown records must not be turned into .NET strings or retained whole.
Implement a streaming newline reader with these properties:

1. Read bytes from a shared-read file stream in bounded blocks.
2. Retain only a bounded candidate prefix while structurally determining the
   top-level event category. If it is not allowlisted, drain bytes through the
   next newline and discard them without UTF-8 decoding.
3. Allowlisted records are `session_meta` (thread id only), `turn_context`
   (model and approved settings only), and `event_msg` payload types
   `token_count`, `task_started`, `task_complete`, `thread_settings_applied`,
   and `context_compacted`. For `context_compacted`, extract only its empty
   structural boundary metadata; never inspect a compacted response item or
   encrypted/plaintext summary.
4. Candidate prefix limit: 64 KiB. Full allowlisted-record limit: 256 KiB.
   Over-limit or structurally changed records are skipped with a metadata-only
   error; they must not block subsequent lines.
5. Advance the durable offset only through the last complete newline. A
   partial tail is reread after the next append.
6. Never log a line, JSON fragment, string value outside approved fields, or
   exception message that could embed source content.

A `Utf8JsonReader`-based structural classifier is acceptable. Tests must prove
that an apparent `token_count` string inside an unknown message body does not
become an event.

## 6. Token event semantics

For a valid `event_msg/token_count`, parse both:

- `payload.info.total_token_usage`: cumulative snapshot
- `payload.info.last_token_usage`: latest model-call increment

Allowlisted components are `input_tokens`, `cached_input_tokens`,
`cache_read_input_tokens`, `cache_write_input_tokens`, `output_tokens`,
`reasoning_output_tokens`, and `total_tokens`, plus the context-window value
when present.

Canonical display math:

```text
cached_input = max(cached_input_tokens, cache_read_input_tokens)
raw_input    = max(input_tokens - cached_input, 0)
output       = output_tokens                 # includes reasoning
reasoning    = reasoning_output_tokens       # subset of output
total        = input_tokens + output_tokens
```

Cached input is a subset of input and reasoning is a subset of output; never
add displayed columns together. Preserve reported `total_tokens` only for an
anomaly check. Cache-write input may be retained as metadata but is not added
to canonical total.

### 6.1 Global event deduplication

Do not sum cumulative snapshots. Create a canonical SHA-256 fingerprint from:

```text
schema-version | thread-id |
total(input,cached,cache_read,cache_write,output,reasoning,total) |
last(input,cached,cache_read,cache_write,output,reasoning,total) |
context-window
```

Semantic v2 identity uses the same seven independent total/last/context fields
(`input`, `cached_input`, `cache_read_input_tokens`, `cache_write`, `output`,
`reasoning`, `total`) on both tuples plus context-window presence; missing is
distinct from explicit zero. Raw thread fingerprints remain durable metadata;
lineage consumption uniqueness is `(parent-tree root, semantic identity)` and
does not replace the thread-scoped fingerprint.

Normalize missing numeric fields distinctly from zero. Exclude timestamp,
path, and byte offset so that the same snapshot replayed later or copied to an
archive remains a duplicate. Insert the fingerprint under a SQLite unique
constraint before any quota-window filter or aggregate update. Only the first
accepted fingerprint contributes `last_token_usage` to totals. Later matches
may increment a duplicate-observation counter but never tokens.

The normal trusted path requires both cumulative and last snapshots. If a new
schema supplies last usage without cumulative usage, persist a degraded
metadata observation keyed by file identity and byte offset, do not silently
treat it as globally reliable, and surface a schema warning.

### 6.2 Turn and session aggregates

Use the latest structural `turn_context`/task boundary to assign a monotonic
turn key per thread. Aggregate all unique model-call increments in that turn
for `最近一轮增量`. If no reliable turn boundary exists, show the latest event
as `最近事件（降级）` rather than asserting a turn.

`会话累计` is the sum of canonical own events for that thread after parent-tree
semantic uniqueness. `父工作合计` adds descendant canonical events. `本额度周期全部会话合计`
is the sum of canonical sample event timestamps within the mechanically determined
current quota window. Lineage canonicalization happens after thread-scoped
fingerprint insert and before quota-window filtering, so an ancestor snapshot
replayed onto a child thread during a new window does not count again.

### 6.3 Context continuation reference

Treat `event_msg/context_compacted` as the preferred completed compaction
boundary. The post-compaction baseline is the first reliable non-zero token
sample later in the same source generation. If that event is absent, a
same-turn high/zero/lower sequence may provide a labeled heuristic fallback.
Never infer a boundary from cumulative raw-token volume alone.

Lineage migration, root reselection, and explicit rebuild reconstruct eligible
`context_compacted` boundaries from durable `structural_events` together with
canonical token samples. Explicit evidence keeps priority over heuristics.
Reconstruction does not require deleted rollout logs. Inherited or noncanonical
ancestor evidence cannot create a false child baseline; genuine child-specific
boundaries remain attributed once.

Persist only boundary identity/time/source order, baseline token/window/model,
detection source, and the reliable turn/token work between adjacent comparable
baselines. Compare only observations with the same model and context window;
model/window changes reset sample sufficiency. Prefer three or more explicit
observations; use heuristic observations only as a separate group and never mix
the two groups into one trend.

The UI exposes the recent-three baseline median, latest-vs-earliest baseline
trend, and latest interval vs prior interval runway trend. It deliberately does
not expose current context occupancy or compaction count. Fewer than three
comparable observations is unavailable. No metric may claim that content was
forgotten.

Structural continuation guidance is an explainable six-level hierarchy. The post-compaction
baseline sets the base grade: `<25% = S`, `25%–<35% = A+`, `35%–<45% = A-`,
`45%–<55% = B+`, `55%–<65% = B-`, and `>=65% = C`. The baseline trend is the
second-order signal: `-3` to `+3` percentage points is stable, a larger increase
worsens one grade, and a larger decrease improves one grade. Runway is supporting
evidence only. Turn and compression-interval raw-token changes are meaningful at
`20%`; both improving may improve one grade, both shrinking may worsen one grade,
and split signals do not change the grade. A meaningful baseline trend takes
precedence over runway, and the final grade may differ from the baseline grade by
at most one level. No single runway metric may trigger a recommendation.

Grade actions are: `S` light/continue confidently, `A+` healthy/continue, `A-`
continue while watching the baseline trend, `B+` continue but prepare a handoff,
`B-` move to a continuation conversation after finishing the current complete
work package, and `C` move as soon as practical without opening a new large work
package in the current conversation. The UI shows the final grade, action, base
grade, trend state, and combined runway state.

The structural grade is not a semantic-reliability score. Add a local-relative
long-session risk using cumulative canonical raw tokens, aggregated turn count,
and the last 48 hours of reliable-timestamp token/turn activity. Compare only
primary sessions with the same model and require at least ten candidates. Raw
tokens never act alone: both lifetime volume and turns must be high relative to
the local median and rank distribution before this risk imposes a `B+` or `B-`
minimum grade.

Actual semantic drift is manual because detecting it would require reading message
content. Persist only thread id, one of `occasional` or `repeated`, and observation
time; absence means `unassessed`, never zero drift. Occasional drift imposes a
`B-` minimum and repeated/obvious drift imposes `C`. The composite continuation
grade is the worst of structural grade, long-session minimum, and manual drift
minimum. The UI must expose all three inputs so the composite is explainable.

## 7. File identity, offsets, rotation, and restart

Persist a source identity composed of thread id, Windows volume serial, and
128-bit file id obtained with a shared-read handle. If file-id APIs fail,
degrade to canonical path + creation time + allowlisted session id and record
the degraded identity; never hash message-bearing file contents.

Required state-machine behavior:

- Append with the same identity: resume at the durable complete-line offset.
- Path changes with the same identity: update the path and retain the offset.
- Same path with a new identity: create a new source generation and read it
  from zero.
- Same identity becomes shorter than its offset: mark truncation, increment
  generation, and rescan from zero; event fingerprints prevent double count.
- Active log copied/moved to archive: discover both roots; copied content may
  be reread but global fingerprints prevent double count.
- Restart: reload offsets, file identities, fingerprints, and aggregates from
  SQLite before scanning.

Initial historical indexing runs in bounded background batches and displays
`索引中`; it must not freeze the HUD. Subsequent append scans default to eight
seconds. A single pass should have a byte/time budget and yield cancellation.

## 8. Session metadata and status

Per session expose: safe display name/short id, role/nickname, project tag,
model, reasoning effort where useful, service tier, last activity, and status.
Latest rollout context overrides stale database model metadata.

Status rules:

- `运行中`: latest accepted task-start is newer than task-complete and the
  source has activity within 120 seconds.
- `未知`: a task appears open but the source is stale beyond 120 seconds.
- `空闲`: task-complete closes the latest start, or there is no open task.

`运行中会话` only means a session whose mechanically derived status is
`运行中`; an idle session is never counted merely because it had activity in
the last 15 minutes. `当前会话` is a user-pinned row; until pinned it is the
most recently active visible session and must be labeled as inferred in its
tooltip.

Service tier resolution:

1. Use an explicit allowlisted rollout service-tier field when present.
2. Otherwise, use the App Server model catalog only if it mechanically shows
   that model's default service tier is null/default; display
   `Standard（默认）`.
3. Explicit `priority`/`fast` is displayed as `Fast` and never enabled by the
   HUD.
4. If evidence is insufficient, display `不可用`.

## 9. Official quota adapter

The application itself must not implement an authenticated HTTP client.
Discover a local `codex.exe` from configured override, PATH, the npm package,
or the installed Codex Desktop package. Launch a short-lived child process:

```text
codex -s read-only -a untrusted app-server
```

Do not pass a service-tier override. Use JSON-RPC over stdin/stdout:

1. `initialize` with the HUD name/version
2. `account/rateLimits/read`
3. model catalog read only when needed for default-tier evidence

Use a bounded startup timeout (10 s), per-request timeout (5 s), cancellation,
and process-tree termination. Do not retain stdout lines beyond the matching
response. Quota polling defaults to 60 seconds; countdown rendering uses the
local clock and does not repoll each second.

Select the main Codex bucket by stable limit id/name, not by array position.
If selection is ambiguous, show unavailable. Preserve additional model buckets
as observations but do not mix them into the primary collapsed display.

For the selected window:

```text
remainingPercent = clamp(100 - usedPercent, 0, 100)
cycleStart        = resetsAt - windowDurationMins
```

Store every valid official observation. Passive `rate_limits` values already
present in allowlisted rollout token events may serve only as a timestamped
fallback marked `本机最后观测/陈旧`. Never estimate quota from raw tokens.

If remaining quota increases by at least 15 percentage points or another
window-identity change indicates a substantial jump, record
`疑似 reset 信号` with before/after metadata. Do not label it a confirmed TIBO
or reset event.

## 10. SQLite contract

Default database: `%LOCALAPPDATA%\CodexUsageHUD\usage.db`. Tests and
diagnostics must support an explicit data-directory override inside the
project. Enable WAL, foreign keys, a bounded busy timeout, migrations, and
short single-writer transactions.

At minimum model these durable concepts:

- schema/migration version
- source files: identity, current path, thread, generation, complete offset,
  length/write observation, health/error code
- event fingerprints with first/last seen and duplicate count
- token samples with approved components, turn key, source offset, model/tier,
  event time, observation time, and confidence
- lineage metadata on token samples: live semantic identity/material (nullable
  total tuple, nullable last tuple, context-window, missing-versus-zero, no
  thread id), SHA-256 identity matching stored material, a deterministic v1
  legacy compatibility alias for schema-v9 rows, resolved parent-tree root, and
  canonical flag. Consumption uniqueness is `(root thread id, semantic identity)`;
  earliest reliable `(event time, source, generation, offset, id)` is canonical.
  Schema v9 rows are rebuilt from allowlisted stored token columns without
  requiring deleted logs; equivalent later live ancestor replay matches migrated
  v9 through a bounded, non-transitive legacy alias that pairs the legacy
  exact-group winner with at most one live v2 identity. The paired live
  representative stays discoverable after compatibility demotion, later inserts,
  restart, root reassignment, and full rebuild, so additional distinct live
  missing/zero/presence identities remain canonical. Pairing follows existing
  canonical order and moves reversibly when an earlier representative arrives.
  Combined exact-election and compatibility-overlay flag changes for one insert
  are applied as a net canonical delta: a sample both promoted and retired has
  no aggregate effect. Merging session metadata preserves an existing
  `parent_thread_id` when the incoming value is omitted; an explicit
  `ClearSessionParent` write is the distinguished parent-removal path. Inserting a
  previously missing parent through rollout `CommitScan`, or otherwise changing
  root-relevant session membership, marks lineage unavailable and schedules the
  same deterministic rebuild used by other late-parent paths. Identity,
  alias, or root changes re-elect both the old and new groups so a group never
  has zero or two winners. Missing, malformed, or cyclic `parent_thread_id`
  chains remain isolated roots. Context and lifecycle consumers rebuild from
  durable metadata: canonical samples including persisted model, plus explicit
  `context_compacted` rows in `structural_events`. Derived context replay is
  lossless and idempotent across lineage migration, reparent, rebuild, and restart.
- safe session metadata and derived status
- quota observations and suspected-reset events
- metadata-only parser/source errors
- local UI settings such as window position, collapse state, filters, pinned
  thread, and optional pet companion mode
- manual session drift assessment containing only thread id, enum level, and timestamp

Use integer token columns and checked/saturating aggregation. Add indexes for
thread/time, quota window/time, and source identity. No table may contain
message body, prompt, response, raw JSON, token/credential, browser data, or
full exception dumps.

## 11. UI contract

The borderless WPF window is always-on-top, draggable, DPI-aware, and remembers
position. Closing hides to the system tray; tray commands are Show/Hide,
Refresh, Settings, and Exit. Exit cancels workers and closes SQLite cleanly.

Collapsed form shows one line equivalent to:

```text
剩余 84% · 6天18时 · 运行中 3 · 本周期 12.4M raw tokens
```

The collapsed visual may use the community-inspired two-potion/compact-meter
motif, but must retain readable text and not copy assets or code.

Expanded form contains:

1. overview card: used/remaining, official reset time, countdown, running count,
   current-cycle aggregate for running sessions, all-session cycle aggregate,
   source freshness. The running-session aggregate must never use those
   sessions' all-history totals.
2. switches: current / running / all
3. session table with identity/model/tier/status/activity plus grouped columns
   for latest turn and session cumulative: raw input, cached input, output,
   reasoning, total
4. sorting by total tokens or recent activity and grouping by project
5. recent quota/reset/error events
6. permanent note: `raw token 与平台额度不是同一单位，不能直接换算`
7. selected-session continuation risk: structural grade, local-relative long-session
   risk, three-state manual drift control, and worst-case composite recommendation

Color is used only for normal, below 20%, below 10%, and unavailable/stale.
Below 20% must be conspicuous; below 10% stronger. Provide readable text/icon
state so color is not the sole signal.

Optional pet companion mode parses only `electron-avatar-overlay-open` and
the approved mascot/anchor bounds from `.codex-global-state.json`. Validate
coordinates, support negative-coordinate monitors and mixed DPI, and position
the collapsed HUD near but not over the pet. On invalid/hidden state, retain
the last safe independent HUD position rather than hiding.

## 12. Reliability and resource behavior

All file/App Server/SQLite work runs off the dispatcher. Use cancellation and
contain failures per source. A malformed log, locked database, missing field,
or App Server timeout changes one source to unavailable/degraded and records a
safe error; it must not crash or freeze the window.

Default cadence:

- UI countdown: 1 second, no I/O
- append/session refresh: 8 seconds
- quota refresh: 60 seconds plus manual refresh
- source discovery: 30 seconds or when metadata changes

Avoid FileSystemWatcher as the sole truth source; periodic reconciliation is
required. Keep buffers pooled/bounded and do not keep rollout files open
between scans.

## 13. Required automated fixtures and tests

Create at least these three named synthetic fixture groups:

1. `cumulative-sequential.jsonl`: cumulative snapshots plus last increments;
   unique increment sum must equal the terminal cumulative total.
2. `duplicate-stale-replay.jsonl`: exact duplicate at a later timestamp and an
   older cumulative snapshot interleaved; duplicates/stale replay must not add.
3. `rotation/`: active file, copied archive, replacement/truncation, and a
   partial final line; moving/copying/restarting must not add twice.

Also test:

- cached/reasoning subset math and overflow protection
- complete-line offsets and corrupt/oversized-line recovery
- event-like text embedded in an unknown message body is ignored
- missing/changed fields become degraded/unavailable without exceptions
- SQLite migration and two consecutive index runs preserve identical totals
- active/archive discovery and same-path new-file identity
- quota parsing, bucket selection, countdown, staleness, and suspected reset
- status transitions and stale open-task -> unknown
- session metadata query contains no forbidden column names
- long-session ranking, manual drift override, clear/restart persistence, and rapid-click single-flight behavior
- lineage missing-versus-zero tuples and context, v9 plus equivalent live replay,
  re-election after identity/root change, independent lineage-cycle oracle
  defect injection, and exact session/turn/latest/frontier/bucket/cycle/context/
  lifecycle/parent-work/render values
- a high-entropy secret/message sentinel in an unknown record never appears in
  database rows, app logs, diagnostics, or rendered view models

Tests must not open the real `auth.json` or make direct network requests.

## 14. Real-machine and UI acceptance

Provide metadata-only diagnostic commands/scripts that:

1. select at least two recent real rollout files without printing messages
2. calculate each thread's deduplicated component totals
3. compare them to mechanically reconstructed rollout evidence
4. index into an isolated acceptance database, stop, restart, and prove totals
   are unchanged
5. inspect every text/blob value in that acceptance database and HUD-owned log
   for the privacy sentinel

Lead acceptance will additionally exercise: drag, fold/unfold, topmost, close
to tray, restore, manual refresh, unavailable state, threshold presentation,
multi-monitor/DPI positioning where available, and launch from the packaged
artifact. Automated tests are evidence, not substitutes for this path.

## 15. Build and packaging

Because no system .NET SDK is installed, bootstrap an official .NET 8 SDK into
`.tools/dotnet` only. Pin the resolved SDK version in `global.json`; set
`DOTNET_CLI_HOME`, `NUGET_PACKAGES`, and build artifacts to project-local
ignored paths, and opt out of CLI telemetry. Do not require administrator
rights or change machine-wide PATH/registry during build.

Provide `scripts/build.ps1`, `scripts/test.ps1`, and `scripts/publish.ps1`.
Publish self-contained `win-x64`, single-file where WPF/native SQLite permits,
with trimming disabled. The final `dist/CodexUsageHUD-win-x64` must launch on
this machine without an installed .NET runtime. Include a ZIP and SHA-256
manifest. Unsigned/SmartScreen limitations must be stated honestly.

README must document data sources, exact token field/subset meanings, quota
source and fallback, privacy boundary, storage paths, known limits, controls,
how to verify numbers, how to remove app-owned data, and that raw tokens cannot
be converted to platform quota.
