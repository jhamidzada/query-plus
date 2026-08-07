# Changelog

All notable changes to QueryPlus. This project uses [Semantic Versioning](https://semver.org):
`MAJOR.MINOR.PATCH` — MAJOR for breaking changes, MINOR for new features, PATCH for fixes.

The running version is shown in the status bar (bottom-right). To check a deployed copy without
launching it:

```powershell
(Get-Item "C:\Program Files\QueryPlus\QueryPlus.exe").VersionInfo.ProductVersion
```

## 1.1.0

### Added
- **Save button in Manage Targets.** Lists, targets and credentials can now be saved explicitly,
  with a `Saved HH:mm:ss` confirmation, instead of only being written when the dialog closes.

### Changed
- **Authentication modes renamed to match SQL Server Management Studio**: `Windows Authentication`,
  `SQL Server Authentication`, and `Windows Authentication (different account)` for the
  alternate-credentials mode. Encryption modes now read `Optional (only if server requires)`,
  `Mandatory (always encrypt)` and `Strict (TDS 8.0)`. Saved configuration is unaffected — only
  the on-screen wording changed.
- **Export buttons moved next to the Script filter** in the Results tab, instead of being pushed
  to the far right edge where they were easy to miss.
- **Visual refresh**: semantic button colours (primary blue for Execute/Save, red for Stop,
  outlined blue for secondary actions), tinted toolbars, coloured panel headings, and consistent
  compact buttons in the Targets/Scripts panels.

## 1.0.2

### Changed
- Moved **Export CSV…** and **Export Excel…** out of the main toolbar and into the Results tab,
  to the right of the Script filter — they act on the results, so they now sit with them.

## 1.0.1

### Fixed
- **Results grid appeared empty (or showed only one row) for wide text values.** Columns holding
  large text (e.g. a captured SQL query of 50 KB) prevented the grid from rendering rows. Grid
  cells are now truncated to 2,000 characters for display; exports are unaffected and still
  contain the complete values.
- **Excel exports opened with a "repair" prompt.** Cells beyond Excel's hard limit of 32,767
  characters produced a workbook Excel reported as corrupt. Oversized values are now truncated to
  the limit and marked `… [truncated]`. Use CSV export when the full text is needed.
- **Results grid could go blank after a run.** Rebuilding the *Script* filter list cleared the
  drop-down's selection, which applied a filter matching no rows. The list is now rebuilt in
  place, and an unrecognised filter value falls back to "show all" instead of hiding everything.

### Changed
- Version is now reported as a semantic version instead of a build timestamp in the title bar.
  It appears in the status bar (bottom-right, with the build id in its tooltip) and in the exe's
  Windows file properties.

## 1.0.0

Initial release.

- Runs multiple SQL scripts against multiple databases across multiple servers in parallel,
  merging output into one results grid (`Server` / `Database` / `Script` lead columns).
- Distribution lists with per-target credentials; Windows, SQL, and alternate-domain
  (`runas /netonly`-style) authentication; Optional/Mandatory/Strict encryption.
- Passwords persisted DPAPI-encrypted per Windows user, or memory-only.
- Comment- and string-aware `GO` batch splitting with `GO n` repeat counts.
- Selectable scripts and targets, per-script progress, configurable on-error policy.
- Live-streaming results, CSV export, and typed Excel (.xlsx) export.
- Ships as a single self-contained `QueryPlus.exe`.
