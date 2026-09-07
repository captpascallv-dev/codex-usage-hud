# Codex Usage HUD — package acceptance summary

Build: `1.0.5` — visual and readability update.

This summary describes the changed interface and its validation scope. It
contains no real local usage, session identifiers, credentials or machine paths.
The parser, quota adapter, database schema and token aggregation algorithms are
unchanged by this update.

## Interface

- Opaque light surfaces, deep-green normal state, explicit Chinese font
  selection, ClearType and layout rounding. No whole-panel bitmap effects or
  fractional text scaling.
- A quota overview, horizontal session filters, a name-first virtualized
  conversation list and a selected-session inspector replace the old sidebar.
- The inspector places recent-turn and session-lifetime values in separate
  columns. Precise counts and complete identifiers remain accessible; overly
  long text is ellipsized with complete-value tooltips.
- Continuation details and manual drift controls are expandable. Their
  underlying calculations and meanings are unchanged.
- Vertical compact size is 224 by 324 logical pixels; the top bar is 660 by 80.
  Both display remaining quota, reset countdown, running count and local cycle
  raw tokens, with settings, topmost, tray and expansion controls.
- Fullscreen increases the inspector width and font sizes directly, keeping
  native text rendering. Restoring returns to the prior window bounds.
- The tray uses a dedicated system-font mark instead of a shrunken screenshot
  of the entire compact card.

## Focused verification

The seven existing HUD-focused checks passed, covering display semantics,
native WPF construction and runtime interactions, a 1,000-session source with
rapid filtering/sorting and 30 collapse/expand pairs, left/right/top docking,
9-pixel handle restoration, and off-screen recovery.

Native pointer/keyboard checks exercised expand/collapse, running filtering,
child-task folding, sorting, continuation disclosure and scrolling, settings,
and fullscreen/restore. Separate captures checked normal, low, critical,
unavailable, empty and long-content states, and 100/125/150-percent render sizes.
Screenshots use synthetic metadata; they are not live quota observations.

The unchanged data-engine suite is not rerun merely for a visual update.
A green test result does not replace the real window and interaction checks.

## Package and operating boundaries

The existing publishing script builds a self-contained Windows x64 application,
scans the package for private paths and identifiers, verifies its internal
manifest, starts and exits an isolated instance, extracts the ZIP and checks
the SHA-256 sidecar. A source build or this document alone is not proof that a
particular package passed those steps; retain the publishing result alongside
the artifact.

Local installation and public GitHub publication are separate operations.
The presence of this file does not assert that a GitHub release exists.

Existing safeguards remain: one HUD writer per data directory; second launch
restores the first instance; source failures degrade to stale/unavailable;
startup, topmost and docking preferences persist; exit closes the runtime.

Quota and reset data come from the local Codex App Server read-only method.
Raw tokens never estimate platform quota or billing. The HUD never changes
service tier and never reads message bodies, credentials or browser storage.
The v1.0.2 lineage-aware deduplication and current-cycle filtering are retained.

## Limits

Windows 10/11 x64 only. The package is unsigned, so Windows SmartScreen may
prompt. Automated display checks do not represent every physical monitor,
multi-monitor DPI combination or hardware hot-plug event. Real quota freshness
depends on the installed Codex CLI and its current local interface.
