# QueryPlus

A Windows desktop tool that runs multiple SQL scripts against multiple databases across
multiple servers, **in parallel**, and merges the output into one results grid. It is a
C#/WPF port of an existing PowerShell/WinForms tool.

Built for locked-down / team scenarios where a single signed binary beats a script: the
release artifact is **one self-contained `.exe`** with no .NET runtime install required.

---

## Solution layout

| Project | Purpose |
| --- | --- |
| `QueryPlus.Core` | The engine — domain types, parallel orchestration, GO-batch splitting, result merge. **No WPF/UI references; fully testable.** |
| `QueryPlus.App` | The WPF app (MVVM). References Core. |
| `QueryPlus.Core.Tests` | xUnit + FluentAssertions tests for Core. |

The engine talks to SQL Server only through `IBatchExecutor`
(`QueryPlus.Core/Execution/`). The production implementation is `SqlBatchExecutor`
(Microsoft.Data.SqlClient); tests use an in-memory fake, so parallelism, error isolation,
cancellation, progress and merge are all verified without a live database.

## Requirements

- **Windows** (WPF is Windows-only).
- **.NET 8 SDK** to build/test (`net8.0` for Core/Tests, `net8.0-windows` for the app).
  The published exe needs no SDK or runtime on the target machine.

> Note: this repo includes a solution-local `NuGet.config` that restores from nuget.org.
> It exists because the machine's global NuGet config had an empty `<packageSources>`.

## Build

```sh
dotnet build QueryPlus.sln -c Debug
```

Warnings are treated as errors in both Core and the App.

## Test

```sh
dotnet test QueryPlus.Core.Tests/QueryPlus.Core.Tests.csproj
```

Covers GO splitting (incl. comment/string-aware cases and `GO n` repeat counts), the
result merge (schema union, `DBNull`, reserved-column collision suffixing), and engine
orchestration (error isolation, cancellation, progress).

## Run

```sh
dotnet run --project QueryPlus.App
```

- Pick a **distribution list** (top-left); its `Server \ Database` targets appear in the tree.
- Open `.sql` files or add tabs in the center editor (AvalonEdit, TSQL highlighting).
  **Every open tab runs, in tab order.**
- Set **Parallelism**, then **Execute** (or press **F5**). **Stop** cancels in-flight work.
- Bottom tabs: **Results** (merged grid, `Server`/`Database`/`Script` lead columns),
  **Messages** (PRINT/info), **Errors**. Results stream in **live** as each target finishes.
- **Manage Targets…** edits lists and their targets (see below).

## Authentication & encryption

The settings at the top of Manage Targets… are the list's **current** credentials — used
for **Connect** and stamped onto each target as you add it. **Every target retains the
credentials it was added with**, so one list can mix servers that need different logins,
domains, or encryption, and each target connects with its own settings at run time (targets
with no snapshot fall back to the list defaults).

**Authentication mode:**

| Mode | Use |
| --- | --- |
| `Windows` | Integrated Security as the currently logged-in user. |
| `Sql` | SQL Server login — enter **User** + **Password**. |
| `WindowsCredentials` | Windows auth as a **different** account — enter **Domain** + **User** + **Password**. Works across an untrusted domain: like `runas /netonly`, the credentials are used only for outbound auth to the server (`LOGON32_LOGON_NEW_CREDENTIALS` + impersonation at connection-open). |

**Encryption mode** (+ optional **Trust Server Certificate**):

| Mode | Meaning |
| --- | --- |
| `Optional` | Encrypt only if the server requires it (legacy `Encrypt=false`). |
| `Mandatory` | Always encrypt (`Encrypt=true`). Default. |
| `Strict` | TDS 8.0 strict encryption — required by SQL Server 2022/2025 hosts set to "Force Strict Encryption". |

**Managing targets (the shuttle):** type a server and **Connect** to list its databases
using the list's current auth; multi-select (Ctrl/Shift-click or Ctrl+A) and move them in
bulk between **Available** and **Targets** with Add / Add-all / Remove. Each target shows the
credentials it carries after its name. **Manual add** is there for servers you can't reach.

**Passwords:** with **Remember** ticked (default), the list's password is persisted
**DPAPI-encrypted** — tied to the current Windows account on the current machine, never
plaintext, and unusable if the config file is copied elsewhere. Untick Remember for
memory-only passwords (re-enter each session). A target whose credential override lacks a
password shows `— needs password` and falls back to the list's credentials; use **Apply
current credentials to selected targets** to re-stamp overrides.

### Self-test (headless check)

```sh
dotnet run --project QueryPlus.App -- --selftest
```

Renders both windows, drives a run through an in-memory (no-DB) runner, and writes
`%APPDATA%\QueryPlus\selftest-result.txt`. Exits **0** only if there are **no WPF
binding errors** *and* the results grid populated (columns/rows past the lead columns);
exits **2** otherwise. Binding failures are also logged to `binding-errors.log` in the same
folder.

## Publish (single-file, self-contained, win-x64)

Using the included profile (`QueryPlus.App/Properties/PublishProfiles/win-x64.pubxml`):

```sh
dotnet publish QueryPlus.App/QueryPlus.App.csproj -p:PublishProfile=win-x64
```

Or fully explicit, without the profile:

```sh
dotnet publish QueryPlus.App/QueryPlus.App.csproj -c Release -r win-x64 ^
  --self-contained true -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

Output: a single `QueryPlus.exe` (~68 MB) in
`QueryPlus.App/bin/Release/net8.0-windows/publish/win-x64/`. Copy that one file to
the target machine and run it.

## Configuration & secrets

- Config lives at `%APPDATA%\QueryPlus\config.json` — **distribution lists and
  targets only**. Saved on exit.
- Non-secret connection fields persist at both the list level and **per target** (auth mode,
  user, domain, encryption mode, trust, timeouts).
- **Passwords are never stored as plaintext.** With **Remember** on they persist
  DPAPI-encrypted (current user + machine only); with Remember off they live in memory only.
  Applies to both SQL and `WindowsCredentials` auth, at list and target level.
- Upgrading from an older build: config is migrated automatically from
  `%APPDATA%\MultiScriptPlus\` on first launch.

## Status / scope

- The Core engine and the WPF shell are complete; `dotnet test` is green and the app
  launches with zero binding errors.
- **Real-fleet / live-database execution and code signing are validated/performed by the
  maintainer** — they are intentionally not exercised by the automated checks here.
