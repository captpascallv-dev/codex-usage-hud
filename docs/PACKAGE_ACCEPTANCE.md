# Codex Usage HUD — package acceptance summary

Build: `1.1.0` — five-slot quota rail and Grok CLI renewal.

This summary describes the published interface and its validation scope. It
contains no real local usage, session identifiers, credentials or machine paths.

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
- Vertical compact card remains 224 by 324 logical pixels; the top bar is 660 by 80.
  The default collapsed layout is a five-slot rail, 252 logical pixels wide, with
  height measured from the five provider rows and capped to the current work area
  (scroll appears only when that cap is tighter than the content). The top rail
  prefers 1100 by 78 and stacks to a 96-high layout near 640; it does not force a
  640 minimum that would overflow a narrower work area. The previous card remains
  available in settings.
- Fullscreen increases the inspector width and font sizes directly, keeping
  native text rendering. Restoring returns to the prior window bounds.
- The tray uses a dedicated system-font mark instead of a shrunken screenshot
  of the entire compact card.

## Five quota slots

- Primary Codex uses the local App Server `account/rateLimits/read`. Session
  analysis stays on the owner-confirmed primary home.
- The second Codex slot is quota-only and may read PI's existing ChatGPT/Codex
  (`openai-codex`) login against `chatgpt.com/backend-api/wham/usage`. It does
  not require a second `CODEX_HOME`.
- Cursor, Grok and Grok Bot default off. After the user enables a slot, the HUD
  reads only the necessary existing-login field and calls that service's own
  quota endpoint. Missing login is 未连接, never a fake 0% or 100%.
- Grok access-key expiry with a remaining refresh grant runs the official
  installed `grok models` CLI so Grok can lock and rewrite its own login file.
  The HUD does not write `auth.json` and does not treat a file timestamp as
  logout.
- Published packages do not contain `ISOLATED_PREVIEW.marker`, local launchers,
  or anyone's enabled-slot choices. Double-clicking the EXE uses
  `%LOCALAPPDATA%\CodexUsageHUD` and the default Codex home. `--data-dir` and
  `--codex-home` are packaging/QA overrides only.

## Focused verification

v1.0.5 retained seven HUD-focused checks covering display semantics, native WPF
construction and runtime interactions, a 1,000-session source with rapid
filtering/sorting and 30 collapse/expand pairs, left/right/top docking,
9-pixel handle restoration, and off-screen recovery. Synthetic captures are
not live quota observations.

v1.1.0 adds focused provider and Grok-renewal checks (expiry then renew, reread,
single-flight, timeout/failure vs revocation, billing 401 retry, unsupported
login shape, isolated-preview marker gating). Grok live renewal was verified
on the maintainer machine with sanitized status/expiry/window/percent fields
only. That local install is separate from this public package. Physical
multi-monitor DPI soak and overnight soak were not claimed for this release.

A green test result does not replace the real window and interaction checks.

## Package and operating boundaries

The existing publishing script builds a self-contained Windows x64 application,
scans the package for private paths and identifiers, verifies its internal
manifest, starts and exits an isolated instance, extracts the ZIP and checks
the SHA-256 sidecar. A source build or this document alone is not proof that a
particular package passed those steps; retain the publishing result alongside
the artifact.

Local installation and public GitHub publication are separate operations.

Existing safeguards remain: one HUD writer per data directory; second launch
restores the first instance; source failures degrade to stale/unavailable;
startup, topmost and docking preferences persist; exit closes the runtime.

Quota and reset data for the current Codex App account come from the local
Codex App Server read-only method. Raw tokens never estimate platform quota
or billing. Official quota can be Live while `account/read` identity is still
missing; the primary popup then says 主目录已确认绑定 and does not claim
per-row machine verification. Nested ChatGPT `account.email` is hashed only in
the email namespace for equality, never as account_id history proof. Session
analysis defaults to the owner-confirmed primary directory minus classifiable
foreign rows. If the live App identity later changes, historical rows are not
rebound to the new identity. The HUD never changes service tier and never reads
message bodies or browser storage. Runtime may keep an in-memory copy of an
enabled provider's necessary login field for that request only. The v1.0.2
lineage-aware deduplication and current-cycle filtering are retained.

The automated catalog currently includes 127 checks. That count is evidence,
not product PASS.

## Limits

Windows 10/11 x64 only. The package is unsigned, so Windows SmartScreen may
prompt. Automated display checks do not represent every physical monitor,
multi-monitor DPI combination or hardware hot-plug event. Real quota freshness
depends on each enabled provider's installed client and current local login.
Grok renewal requires the official Grok CLI; a missing binary or failed refresh
is shown as unavailable, not as a fake Live percentage.
