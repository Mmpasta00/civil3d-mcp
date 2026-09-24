# Civil 3D skill library

106 skills across 20 categories (108 checked C# code blocks — two skills carry two alternate
templates each). Every skill compiles clean against the real Civil 3D 2026 .NET API
(`Speckle.Civil3D.API` 2026.0.0), verified by `../plugin/SkillCompileCheck`.

## What a skill is

A skill is a `<name>.skill.md` file: YAML frontmatter describing the operation, plus one or more
fenced ```csharp code templates the AI edits (replacing placeholder literals like
`"SURFACE_NAME_HERE"` with real values) and sends to the plugin's Roslyn executor
(`RoslynExecutor.ExecuteAsync`) for compilation and execution inside the live Civil 3D session.

### Frontmatter

```yaml
---
name: create_tin_surface          # matches the filename (without .skill.md)
category: surfaces                # matches the containing folder
description: Create a new TIN surface and optionally add 3D points to it
requires_write: true              # true if the code modifies the drawing
parameters:
  - name: surfaceName
    type: string                  # string | double | int | number | boolean | array
    required: true
    description: Name for the new surface
---
```

`parameters: []` for a skill that takes no inputs.

### Code Template

One or more ```csharp fenced blocks under a `## Code Template` heading (use a
`## Code Template — <variant name>` sub-heading, as in `alignments/station_offset.skill.md`, when a
skill offers more than one usable form — e.g. "station to point" vs. "point to station"). Code runs
with these globals available directly by name (see `../plugin/Civil3dMcpPlugin/ScriptContext.cs`):

| Global | Type |
|---|---|
| `Document` | `Autodesk.AutoCAD.ApplicationServices.Document` |
| `CivilDoc` | `Autodesk.Civil.ApplicationServices.CivilDocument` |
| `Database` | `Autodesk.AutoCAD.DatabaseServices.Database` |
| `Transaction` | `Autodesk.AutoCAD.DatabaseServices.Transaction` (already open — never call `Commit`/`Dispose`) |
| `Editor` | `Autodesk.AutoCAD.EditorInput.Editor` |

Auto-imported namespaces (see `RoslynExecutor.BuildOptions`): `System`, `System.Linq`,
`System.Collections.Generic`, `System.Text`, `Autodesk.AutoCAD.ApplicationServices`,
`Autodesk.AutoCAD.DatabaseServices`, `Autodesk.AutoCAD.EditorInput`, `Autodesk.AutoCAD.Geometry`,
`Autodesk.AutoCAD.Runtime`, `Autodesk.Civil`, `Autodesk.Civil.ApplicationServices`,
`Autodesk.Civil.DatabaseServices`, `Autodesk.Civil.Settings`.

Both `Autodesk.AutoCAD.DatabaseServices.Entity` and `Autodesk.Civil.DatabaseServices.Entity`
exist and collide under these imports — always fully-qualify `Entity` for a plain AutoCAD entity
(see any `select/` or `delete/` skill for the pattern).

Rules every template follows:
- Real, compilable C# — no pseudocode.
- Read before write; never call `Transaction.Commit()` or `.Dispose()` (the host owns the
  transaction lifecycle).
- Return a JSON-serializable value (anonymous object, string, list, or an `{ error = "..." }`
  shape for a handled failure).
- Where the real Civil 3D managed API has no direct method for an operation, the template uses
  `Editor.Command("_.COMMANDNAME", ...)` instead and says so explicitly in **Usage Notes**, since
  that runs in command context and its exact prompt sequence can vary by version/language.
- A short **Usage Notes** section follows every code block with real API caveats — read it before
  wiring a skill into a larger workflow.

### Sandbox

`ScriptSandbox.cs` blocks `System.Diagnostics.Process`, `System.IO.File.Delete`,
`System.IO.Directory.Delete`, `System.Net.Http`, `System.Net.Sockets`,
`System.Reflection.Assembly.Load`, `System.Runtime.InteropServices`, `Environment.Exit`,
`Registry.`, `Process.Start`, and `AppDomain.CreateDomain`. Plain `System.IO.File.WriteAllText`
and `System.IO.Directory.CreateDirectory` are **allowed** — `drawing/create_project_folder_structure`
uses them to scaffold a project folder tree on disk.

## Categories

| Category | Skills | What it covers |
|---|---|---|
| `alignments` | 8 | Create (from polyline/points), reverse, station equations, geometry report, station/offset conversion, list, delete |
| `analyze` | 6 | Earthwork, quantity takeoff, watersheds, true 3D pipe/structure interference, entity census, units/CRS |
| `corridors` | 6 | List, create, rebuild, retarget subassemblies, export feature lines as points, corridor surfaces |
| `delete` | 2 | Bulk erase by layer or by entity type |
| `drawing` | 10 | Drawing info/health, project folder scaffolding, layouts from template, xrefs, title blocks, layers, purge/audit, save-as, viewports |
| `export` | 5 | LandXML, alignment CSV, data shortcuts, plot to PDF, formatted volume report text |
| `geometry` | 1 | Raw 2D/3D polyline creation |
| `grading` | 5 | Feature lines, elevation editing, grading list/create, finished-grade offset shift |
| `hydrology` | 2 | Rational Method peak flow, composite runoff coefficient |
| `labels` | 5 | Alignment stations, surface contours, COGO points, profile view bands, label sets |
| `parcels` | 4 | Create from polylines, area table, area labels, delete |
| `pipe_networks` | 8 | Create network, add structure/pipe, parts lists, labeling, slope/cover report, tables, delete, pressure-network note |
| `points` | 7 | Create/list/export COGO points, point groups by description, CSV import, UDPs, delete by group |
| `profiles` | 5 | Surface profile, layout profile + PVIs, PVI edit, profile view, list |
| `qa` | 2 | Drawing health check, layer-naming compliance |
| `sections` | 5 | Sample line groups/lines, section views, section volumes (QTO), object projection |
| `select` | 4 | By layer, by window, current selection summary, by handle |
| `surfaces` | 15 | TIN create/from points/from LandXML, breaklines, boundaries, paste, volume surfaces, cut/fill, contours, sampling, styling, rename, list, elevation/volume queries |
| `utility_optimization` | 5 | Pipe cover, export for O-Pipes, conflict detection, network list, resize |
| `workflows` | 1 | Composite earthwork report example |

## `index.json`

Generated by the checker (never hand-edit): an array of `{Name, Category, Description,
RequiresWrite, Parameters, File}` for every skill, sorted by category then name. The web app reads
this to render the skill catalog.

## Running the compile check

```bash
cd ../plugin/SkillCompileCheck
dotnet run -c Release
```

Compiles every ```csharp block in every `*.skill.md` here against the real
`Speckle.Civil3D.API`/`Speckle.AutoCAD.API` metadata (no execution — see that project's own
comments for why it works this way on a non-Windows host), prints `[OK]`/`[FAIL]` per skill,
rewrites `index.json`, and exits 1 if anything fails to compile. Run it after adding or editing any
skill.

## Namespace collisions (compile-check findings)
`Entity` and `DBObject` exist in both `Autodesk.AutoCAD.DatabaseServices` and `Autodesk.Civil.DatabaseServices`; fully qualify them in scripts (`Autodesk.AutoCAD.DatabaseServices.DBObject`). `PartFamily` has `Description`, not `Name`. `Database.DxfIn(path, logPath)` is the DXF import member (there is no `ReadDxfFile`).

Counts as of 2026-09-16: 115 skills, 117 compiled blocks, 0 failures (`plugin/SkillCompileCheck`).
