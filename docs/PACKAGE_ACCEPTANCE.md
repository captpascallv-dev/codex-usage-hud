# Codex Usage HUD — package acceptance summary

Status: `PRE_RELEASE_CANDIDATE`

Release: `1.0.2` lineage-dedup hotfix (source and automated-test acceptance).

This is the package-safe acceptance summary for a pre-release candidate.
Detailed internal diagnostics, machine paths, local session identifiers, local
usage values, and private test records are intentionally not distributed with
the application.

Source and automated-test acceptance are recorded here. Package hash,
extraction, isolated startup, privacy scan, local-replacement, and GitHub
verification are performed only after final-audit PASS. This document is not a
final package-verified claim.

## Source and test acceptance

- Windows 10/11 x64, self-contained single-file application; no separately
  installed .NET runtime or administrator permission is required.
- One process owns the HUD data directory. Launching the same executable again
  sends only a show-card activation signal and does not start a second writer.
- The Edge Beacon compact card can expand, collapse, move, dock to the left,
  right, or top work-area edge, auto-hide to a 9-pixel recovery handle, hide to
  the tray, restore, and recover after a saved monitor disappears.
- Left/right docking accepts pointer-at-screen-edge intent in addition to exact
  card-edge proximity. Both vertical and top compact layouts show quota,
  countdown, running-session count, and local current-cycle raw total; the
  compact gear opens the real settings panel.
- Vertical and top compact layouts expose the same persisted window-topmost
  control as the expanded header. When enabled, the auto-hidden recovery handle
  remains above ordinary foreground windows. Mouse cancellation updates the
  active border immediately rather than retaining a stale pointer-focus outline.
- Collapsing a docked auto-hidden expanded panel restores the compact card
  directly to its preserved 9-pixel handle position. Expanded-window movement
  does not erase the compact dock anchor.
- Expanded mode can toggle between its prior bounds and the monitor work area.
  Fullscreen mode widens and scales the detail pane; restoring returns to the
  exact prior expanded size and position.
- Countdown updates do not rebuild the session list. Rows are virtualized and
  replaced as one immutable view, refresh requests are single-flight/coalesced,
  filter/sort input bursts are collapsed to their final requested view, and
  rollout/quota I/O remains off the WPF dispatcher. The more expensive
  long-session comparison inputs are cached for one minute between background
  refreshes; token indexing remains incremental.
- Always-on-top and the optional current-user login-start setting are persisted.
  Login-start can be enabled or disabled from the settings panel or tray menu.
  A desktop shortcut can be created, repaired, or removed without administrator
  permission; it contains only the exact executable path.
- Rollout input is restricted to allowlisted metadata and token-count fields.
  Message bodies, prompts, responses, cookies, credentials, and browser storage
  are neither collected nor stored.
- Official quota data comes only from the local Codex App Server read-only
  method. Raw token totals are never used to estimate platform quota and the HUD
  never changes the Codex service tier.
- App Server responses containing sibling `primary` and `secondary` quota
  windows select only the exact stable `primary` bucket. Multiple fuzzy Codex
  candidates remain unavailable rather than guessed. An expired persisted
  observation is no longer rendered as a stale percentage after refresh fails.
- User-facing running counts include only sessions in the derived Running state.
  Their adjacent token total is filtered to the same official current quota
  window; it is not a sum of those sessions' all-history totals. Non-positive
  official window durations are shown as unavailable rather than coerced.
- Main-session navigation is split into Running, Recent, and All. Main rows
  expose normalized APP/CLI source labels. Subagents with an available parent
  thread are expandable beneath that parent (including nested subagents), while
  old or missing-parent records remain in Orphan History without guessed
  attachment. Parent work totals show own plus descendant usage; global and
  quota-cycle totals still count every thread exactly once. Only normalized
  surface, parent thread ID, and bounded depth are stored; raw source JSON is
  never retained. Conversation name is the primary label and thread ID is secondary.
- One explicitly pinned session is placed before all other rows in either sort
  mode; the remaining rows obey the selected activity or token order. This
  session pin is independent of the window always-on-top setting.
- The selected-session pane separates metadata-only structural carrying capacity,
  local-relative long-session risk, and a three-state manual drift assessment.
  The composite `S` through `C` continuation grade takes the worst input rather
  than averaging away risk. The baseline sets the structural base grade, trend
  has second priority, and the two runway signals can only make a bounded supporting
  adjustment together. Long-session risk combines raw-token history with turn count
  and recent intensity; raw tokens never grade a session alone. Explicit
  `context_compacted` boundaries are preferred; the fallback is isolated and
  labeled. Comparisons never cross model/context-window segments. It displays
  neither current context occupancy nor compression count, inspects no message
  text, and never automates conversation switching. Manual drift stores only thread
  id, enum level, and timestamp; unassessed never means zero drift. Missing or
  insufficient evidence is shown as sample-insufficient rather than guessed.
- Current allowlisted metadata cannot reliably distinguish Codex from GPT Work
  or reproduce the exact Codex sidebar focus. The release therefore combines
  those sources and labels the bounded activity view Recent Sessions rather
  than making an unsupported Current claim.
- Malformed, partial, rotated, replaced, and replayed rollout files are handled
  without double-counting accepted token events. Cross-thread inherited-history
  replay is canonicalized to the earliest reliable occurrence in the resolved
  parent tree before quota-cycle filtering. Live semantic identity preserves
  missing-versus-zero tuple and context presence; schema v9 rows keep a
  deterministic legacy alias so equivalent later live ancestor replay matches
  migrated history without erasing every distinct live missing/zero event that
  shares the lossy alias. Compatibility pairing elects one live v2 identity per
  alias and keeps that demoted representative discoverable, so later distinct
  live identities are not cascade-suppressed. Exact replay of the same thread
  fingerprint is rejected before lineage election; a distinct descendant
  fingerprint with the same semantic identity loses exact-group election without
  a net aggregate delta when compatibility immediately re-demotes the
  representative. Independent lineage/cycle
  reconciliation parses live
  tuple material itself rather than accepting stored material verbatim.
  Database-busy and parser-error
  paths degrade to safe status codes instead of blocking the WPF dispatcher.
- The release workflow runs the complete automated suite, isolated diagnostics,
  an isolated launch/exit smoke check, internal file-manifest verification,
  extracted-ZIP verification, and a package privacy scan.
- The 84 automated tests mount a real WPF window with 1,000 synthetic sessions, exercises
  navigation and sorting, performs 30 collapse/expand pairs, submits 30 rapid
  refresh requests, performs 20 rapid manual-drift clicks under a single-flight
  guard, exercises a real parent-row expand click, and verifies all three dock
  directions plus handle reveal. It also verifies APP/CLI classification,
  nested rollup, cycle rejection, orphan degradation, restart persistence, and
  the no-double-counting boundary.
  In the final pre-publication run, an 80-event navigation/sort burst over a
  1,000-session source had 1ms input p95, produced one final row-view
  notification, and applied the requested final view in 131ms; 30
  collapse/expand pairs completed in 1,652ms.
  Screenshot comparison is a separate visual gate and is not used as evidence
  for interaction responsiveness.
- After final startup settled, a 20-second live sample consumed 0.0938 CPU
  seconds (about 0.47% of one logical core), used about 184.7MB working set,
  and remained responsive.

## Later package verification

Package hash, ZIP extraction, isolated startup, privacy scan, local-replacement,
and GitHub verification are not claimed here. Those gates run only after
final-audit PASS.

## Privacy and integrity boundary

Application state remains under the current user's local application-data
directory. Logs contain only safe error codes, relative source paths, offsets,
and timestamps. The release package contains no machine-specific user path,
stable local session identifier, real local database, rollout log, credential,
prompt, or response text.

The ZIP has a sidecar SHA-256 file, and the extracted directory has its own
`SHA256SUMS.txt`. Both are verified during publication.

## Known Windows limitation

The executable is unsigned. Windows SmartScreen may therefore warn on first
launch. A code-signing certificate is required to remove that warning; the
release does not claim otherwise.
