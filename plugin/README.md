# Civil 3D Plugin

Two things live in one .NET project (`Civil3dMcpPlugin/`):

1. **MCP path** (pre-existing, unchanged): a TCP JSON-RPC listener on port 8080
   that runs AI-generated C# against the Civil 3D API. Commands `C3DMCPSTART`,
   `C3DMCPSTOP`, `C3DMCPSTATUS`. Methods `executeCode`, `getCivil3DHealth`,
   `getDrawingContext`.
2. **Gradon path** (this build): a docked chat palette (`GRADON` command) that
   talks to the Gradon agent service over HTTP and executes the code it sends
   back, right inside the open drawing.

Both paths share the same code-execution core (`ScriptRunner` → `CivilExecution`
→ `RoslynExecutor` → `ScriptContext`), so a script behaves identically whether
it was triggered by an MCP client or by a chat message in the palette.

## Gradon protocol

The palette speaks to a Python service (built separately) at
`{serviceUrl}` (default `http://localhost:8799`), header `X-Gradon-Key`.

**`GET /v1/health`** — used for the palette's status line.

**`POST /v1/turn`** — request:

```json
{
  "session_id": "string or null",
  "user_id": "string",
  "firm_id": "string",
  "message": "string or null",
  "drawing": { "...": "DrawingContext, see below" },
  "tool_result": {
    "call_id": "string",
    "ok": true,
    "result": "any (JSON-serialised)",
    "error": "string or null",
    "duration_ms": 0
  }
}
```

response:

```json
{
  "session_id": "string",
  "action": "run | ask | done | error",
  "call_id": "string or null",
  "code": "string or null",
  "read_only": false,
  "description": "string or null",
  "text": "string or null",
  "summary": { "steps": 0, "writes": 0, "time_saved_min": 0, "run_id": "string" }
}
```

Loop (implemented in `Agent/GradonConversation.cs`):

1. User sends a message → client POSTs `{message, drawing}` (fresh
   `DrawingContext` snapshot on every send, including replies to an "ask").
2. While `action == "run"`: show `text`/`description`, execute `code`
   (`read_only` ⇒ no transaction commit), POST `{tool_result}` back. Every
   write step gets its own AutoCAD UNDO group (see "Undo grouping" below).
   The tool-result string is capped at 20 KB with a truncation marker
   (`GradonClient.TruncateResult`).
3. `action == "ask"`: show `text`, wait for the next user message on the
   same `session_id`.
4. `action == "done"`: show `text` + `summary`.
5. `action == "error"`: show the error.

When write mode is "Ask before every write" and a `run` step has
`read_only: false`, the palette shows an inline Yes/No prompt
(`description`) before executing. "No" sends
`tool_result {ok:false, error:"user declined"}` and the loop continues.

### DrawingContext (the `drawing` field)

Built by `DrawingContext.Capture` (native Civil 3D API calls, not Roslyn),
run through `CivilExecution.ReadAsync` (main thread, doc lock, no commit).
Every section is individually try/caught so one failing API call never
blocks the rest of the snapshot:

| Field | Source |
|---|---|
| `drawing`, `path` | `Document.Name` |
| `units` | `Database.Insunits` |
| `civil_version` | `Application.Version` (AutoCAD platform version — see note below) |
| `surfaces` | `CivilDocument.GetSurfaceIds()` |
| `alignments`, `profile_views`, `sample_line_groups` | `CivilDocument.GetAlignmentIds()` → per-alignment `GetProfileViewIds()` / `GetSampleLineGroupIds()` |
| `corridors` | `CivilDocument.CorridorCollection` (no `GetCorridorIds()` in this API) |
| `pipe_networks` | `CivilDocument.GetPipeNetworkIds()` |
| `pressure_networks` | `CivilDocument.GetPressurePipeNetworkIds()` (extension method from the pressure-pipes assembly) |
| `parcels` | `CivilDocument.GetSiteIds()` → per-site `GetParcelIds()` |
| `cogo_points` | `CivilDocument.CogoPoints.Count` |
| `layouts` | `Database.LayoutDictionaryId` |
| `xrefs` | `Database.GetHostDwgXrefGraph` (node 0 is the host drawing, skipped) |
| `layers` | `Database.LayerTableId` count |
| `selection` | `Editor.SelectImplied()`, capped at 50 |

Also exposed over the MCP TCP path as JSON-RPC method `getDrawingContext`.

## Undo grouping (known unverified point)

Task requirement: every Gradon write executes inside one AutoCAD UNDO group,
so a single Ctrl+Z reverts the whole step even if the script itself performs
several internal operations. `ScriptRunner.ExecuteCodeAsync(code, readOnly,
wrapUndoGroup: true)` wraps execution with
`Editor.Command("_.UNDO", "_BEGIN")` / `"_END"`, called **inside** the
already-open `CivilExecution` transaction (doc-locked, main thread).

This compiles and is the standard AutoCAD approach for grouping multiple
undo-stack entries into one, but issuing `Editor.Command` while a
`Transaction` from `CivilExecution.ExecuteAsync` is still open, uncommitted,
has not been exercised against a live Civil 3D session — there is no Windows
box available from here to test it. If it turns out to behave oddly in
practice (e.g. because the transaction hasn't committed yet when UNDO BEGIN
fires), the fallback is to drop `wrapUndoGroup` and rely on the fact that a
single transaction commit is already usually one undo entry on its own —
change `wrapUndoGroup: true` to `false` in `GradonConversation.ExecuteStepAsync`.
The MCP TCP path (`CommandDispatcher.ExecuteCodeAsync` → `ScriptRunner`) does
**not** wrap in an UNDO group — that path is unchanged from before this build.

## Build

```bash
cd Civil3dMcpPlugin
dotnet build -c Release
```

Builds clean (0 errors) on macOS via `Speckle.Civil3D.API` /
`Speckle.AutoCAD.API` NuGet packages — no work-PC DLL copy needed. Same
command works on Windows. `UseWindowsForms` + `EnableWindowsTargeting` are
set in the `.csproj` so the WinForms palette (not WPF, deliberately — WPF
project types don't cross-compile the same way) builds on both platforms.
Expect ~220 `CA1416` "only supported on windows" warnings from the
cross-platform analyzer — harmless, since the TFM is `net10.0-windows`.

## Install

On the Windows machine running Civil 3D 2026:

```powershell
.\install.ps1
```

Builds the project (unless `-SkipBuild`), copies the bundle into
`%APPDATA%\Autodesk\ApplicationPlugins\Gradon.bundle\Contents\`, and writes
`%APPDATA%\Gradon\civil3d.json` with defaults if it doesn't already exist
(never overwrites an existing config). `PackageContents.xml` sets
`LoadOnAutoCADStartup="True"` and targets `SeriesMin/Max="R25.1"` (the
AutoCAD platform release Civil 3D 2026 runs on), so the plugin loads
automatically the next time Civil 3D starts — no `NETLOAD` needed once
installed this way.

```powershell
.\uninstall.ps1
```

Removes the bundle folder. Leaves `civil3d.json` in place.

## Commands

| Command | Effect |
|---|---|
| `GRADON` | Opens (or brings forward) the docked Gradon chat palette |
| `GRADONSTOP` | Tears down the palette and cancels any in-flight turn. A later `GRADON` creates a fresh one |
| `C3DMCPSTART` / `C3DMCPSTOP` / `C3DMCPSTATUS` | MCP TCP listener lifecycle (unchanged) |

## Config

`%APPDATA%\Gradon\civil3d.json`:

```json
{ "serviceUrl": "http://localhost:8799", "apiKey": "...", "userId": "...", "firmId": "..." }
```

Env var overrides: `GRADON_SERVICE_URL`, `GRADON_API_KEY`
(`Agent/GradonConfig.cs`). Created with defaults on first load if missing.

## Known unverified points

Nothing here has run inside a live Civil 3D session — there's no Windows box
in this environment. Flagged specifically:

- **UNDO grouping** around Gradon writes — see "Undo grouping" above.
- **PaletteSet hosting a WinForms `UserControl`** — compiles, and this is the
  documented pattern for AutoCAD .NET palettes, but layout/rendering inside
  the actual palette dock has not been visually checked.
- **`Application.Version` as the `civil_version` field** — returns the
  AutoCAD platform assembly version (e.g. `25.1.x.x`), not a literal
  "Civil 3D 2026" string. `CivilDocument` has no version property in this
  managed API surface. If the service needs a friendlier string, map the
  major version number to a product year on the service side.
- **`Editor.Command` inside a nested `ExecuteInCommandContextAsync` +
  document-lock + open transaction** (used both for UNDO grouping and inside
  `DrawingContext.Capture`'s `Editor.SelectImplied()` call) — expected to
  work based on how `CivilExecution` is already structured, but only a real
  Civil 3D session can confirm there's no reentrancy issue.
- **Bundle autoload path** (`%APPDATA%\Autodesk\ApplicationPlugins\`) and
  `SeriesMin/Max="R25.1"` — this is the documented Civil 3D 2026 bundle
  convention, not verified against an actual `ApplicationPlugins` folder on
  a Windows machine.
