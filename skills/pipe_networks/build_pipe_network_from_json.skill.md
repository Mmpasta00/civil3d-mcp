---
name: build_pipe_network_from_json
category: pipe_networks
description: Build a full gravity pipe network (structures + pipes, rims/sumps/inverts, sizes) from a Gradon Design network-<name>.json handoff file
requires_write: true
parameters:
  - name: jsonPath
    type: string
    required: true
    description: Absolute path to the network-<name>.json handoff file (e.g. "C:/projects/livermore/handoff/network-storm.json")
  - name: partsListName
    type: string
    required: false
    description: Parts list to assign to the new network (default "Standard")
  - name: structureFamily
    type: string
    required: false
    description: Structure part family description to use for every structure (default the first structure family in the parts list)
  - name: structureSize
    type: string
    required: false
    description: Part size description within the structure family to use (default the first size in that family)
  - name: pipeFamily
    type: string
    required: false
    description: Pipe part family description to use for every pipe (default the first pipe family in the parts list)
---

## Code Template

```csharp
using System.Text.Json;

string jsonPath = @"NETWORK_JSON_PATH_HERE";
string partsListName = "Standard";
string structureFamilyName = "";
string structureSizeName = "";
string pipeFamilyName = "";

if (!System.IO.File.Exists(jsonPath))
    return new { error = $"File not found: {jsonPath}" };

JsonDocument doc;
try
{
    doc = JsonDocument.Parse(System.IO.File.ReadAllText(jsonPath));
}
catch (System.Exception ex)
{
    return new { error = $"Could not parse JSON: {ex.Message}" };
}

var root = doc.RootElement;
string networkName = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : System.IO.Path.GetFileNameWithoutExtension(jsonPath);
string units = root.TryGetProperty("units", out var unitsEl) ? unitsEl.GetString() : "ft";
if (!string.IsNullOrWhiteSpace(units) && !units.Equals("ft", StringComparison.OrdinalIgnoreCase))
    return new { error = $"Unsupported units '{units}' — this skill assumes the JSON and the drawing are both in feet." };

// 1. Create the network and assign its parts list.
var nameRef = networkName;
var networkId = Network.Create(CivilDoc, ref nameRef);
var net = Transaction.GetObject(networkId, OpenMode.ForWrite) as Network;

try { net.PartsListName = partsListName; }
catch (System.Exception ex) { return new { error = $"Network '{net.Name}' created but parts list '{partsListName}' assignment failed: {ex.Message}" }; }

if (net.PartsListId.IsNull)
    return new { error = $"Network '{net.Name}' has no parts list resolved after assigning '{partsListName}' — confirm that parts list exists in this drawing." };

var partsList = Transaction.GetObject(net.PartsListId, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.PartsList;

// 2. Resolve the structure family/size and pipe family to use for every part (Gradon Design's
//    JSON carries geometry/elevations/sizes but not Civil 3D part-family names).
ObjectId ResolveFamily(DomainType domain, string wantedDescription)
{
    var ids = partsList.GetPartFamilyIdsByDomain(domain);
    if (ids.Count == 0) return ObjectId.Null;
    if (string.IsNullOrWhiteSpace(wantedDescription))
        return (ObjectId)ids[0];
    foreach (ObjectId id in ids)
    {
        var fam = Transaction.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.PartFamily;
        if (fam != null && fam.Description.Equals(wantedDescription, StringComparison.OrdinalIgnoreCase))
            return id;
    }
    return ObjectId.Null;
}

var structFamilyId = ResolveFamily(DomainType.Structure, structureFamilyName);
var pipeFamilyId = ResolveFamily(DomainType.Pipe, pipeFamilyName);
if (structFamilyId.IsNull) return new { error = $"No usable structure part family found in parts list '{net.PartsListName}'" + (string.IsNullOrWhiteSpace(structureFamilyName) ? "" : $" matching '{structureFamilyName}'") };
if (pipeFamilyId.IsNull) return new { error = $"No usable pipe part family found in parts list '{net.PartsListName}'" + (string.IsNullOrWhiteSpace(pipeFamilyName) ? "" : $" matching '{pipeFamilyName}'") };

var structFamily = Transaction.GetObject(structFamilyId, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.PartFamily;
ObjectId structSizeId = ObjectId.Null;
if (!string.IsNullOrWhiteSpace(structureSizeName))
{
    try { structSizeId = structFamily[structureSizeName]; }
    catch (System.Exception ex) { return new { error = $"Structure size '{structureSizeName}' not found in family '{structFamily.Description}': {ex.Message}" }; }
}
else if (structFamily.PartSizeCount > 0)
{
    structSizeId = structFamily[0];
}

// 3. Add every structure, keyed by the JSON's own "id" so pipes below can look them up.
var structureIds = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
var structuresCreated = new List<object>();
var structureErrors = new List<object>();

if (root.TryGetProperty("structures", out var structuresEl))
{
    foreach (var s in structuresEl.EnumerateArray())
    {
        string id = s.GetProperty("id").GetString();
        double x = s.GetProperty("x").GetDouble();
        double y = s.GetProperty("y").GetDouble();
        double rim = s.GetProperty("rim").GetDouble();
        double? sump = s.TryGetProperty("sump", out var sumpEl) && sumpEl.ValueKind != JsonValueKind.Null ? sumpEl.GetDouble() : (double?)null;

        ObjectId newStructId = ObjectId.Null;
        try
        {
            net.AddStructure(structFamilyId, structSizeId, new Point3d(x, y, rim), 0.0, ref newStructId, true);
        }
        catch (System.Exception ex)
        {
            structureErrors.Add(new { id, error = ex.Message });
            continue;
        }

        var structObj = Transaction.GetObject(newStructId, OpenMode.ForWrite) as Structure;
        try { structObj.RimElevation = rim; } catch (System.Exception) { /* some structure types compute rim from other fields */ }
        if (sump.HasValue)
        {
            try { structObj.SumpElevation = sump.Value; } catch (System.Exception) { /* not all structure shapes support a sump */ }
        }

        structureIds[id] = newStructId;
        structuresCreated.Add(new { id, name = structObj.Name, rim = structObj.RimElevation });
    }
}

// 4. Add every pipe, connecting to the structures created above and sizing to the nearest
//    catalog size to size_in via the same ResizeByInnerDiameterOrWidth path used by resize_pipe.
var pipesCreated = new List<object>();
var pipesUnsized = new List<object>();
var pipeErrors = new List<object>();

if (root.TryGetProperty("pipes", out var pipesEl))
{
    foreach (var p in pipesEl.EnumerateArray())
    {
        string id = p.GetProperty("id").GetString();
        string upId = p.GetProperty("up").GetString();
        string downId = p.GetProperty("down").GetString();
        double sizeIn = p.GetProperty("size_in").GetDouble();
        double invertUp = p.GetProperty("invert_up").GetDouble();
        double invertDown = p.GetProperty("invert_down").GetDouble();

        if (!structureIds.TryGetValue(upId, out var upStructId) || !structureIds.TryGetValue(downId, out var downStructId))
        {
            pipeErrors.Add(new { id, error = $"Upstream/downstream structure not found (up='{upId}', down='{downId}')" });
            continue;
        }

        var upStruct = Transaction.GetObject(upStructId, OpenMode.ForRead) as Structure;
        var downStruct = Transaction.GetObject(downStructId, OpenMode.ForRead) as Structure;
        var line = new LineSegment3d(
            new Point3d(upStruct.Location.X, upStruct.Location.Y, invertUp),
            new Point3d(downStruct.Location.X, downStruct.Location.Y, invertDown));

        ObjectId newPipeId = ObjectId.Null;
        try
        {
            net.AddLinePipe(pipeFamilyId, ObjectId.Null, line, ref newPipeId, true);
        }
        catch (System.Exception ex)
        {
            pipeErrors.Add(new { id, error = ex.Message });
            continue;
        }

        var pipe = Transaction.GetObject(newPipeId, OpenMode.ForWrite) as Pipe;

        try
        {
            pipe.ResizeByInnerDiameterOrWidth(sizeIn / 12.0, true);
        }
        catch (System.Exception ex)
        {
            pipesUnsized.Add(new { id, pipeName = pipe.Name, requestedSize_in = sizeIn, error = ex.Message });
        }

        // Re-assert the exact inverts from the handoff — AddLinePipe/resize can nudge endpoints.
        try
        {
            pipe.StartPoint = new Point3d(pipe.StartPoint.X, pipe.StartPoint.Y, invertUp);
            pipe.EndPoint = new Point3d(pipe.EndPoint.X, pipe.EndPoint.Y, invertDown);
        }
        catch (System.Exception) { /* leave as auto-computed if the API rejects an explicit set here */ }

        try { pipe.ConnectToStructure(ConnectorPositionType.Start, upStructId, true); } catch (System.Exception) { }
        try { pipe.ConnectToStructure(ConnectorPositionType.End, downStructId, true); } catch (System.Exception) { }

        pipesCreated.Add(new
        {
            id,
            pipeName = pipe.Name,
            partSize = pipe.PartSizeName,
            innerDiameter_in = Math.Round(pipe.InnerDiameterOrWidth * 12.0, 2),
            upStructure = upStruct.Name,
            downStructure = downStruct.Name
        });
    }
}

return new
{
    success = true,
    networkName = net.Name,
    partsListName = net.PartsListName,
    structureFamily = structFamily.Description,
    pipeFamily = (Transaction.GetObject(pipeFamilyId, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.PartFamily)?.Description,
    structuresCreated = structuresCreated.Count,
    structureErrors,
    pipesCreated = pipesCreated.Count,
    pipesUnsized,
    pipeErrors,
    structures = structuresCreated,
    pipes = pipesCreated
};
```

## Usage Notes
- **Verified members** (confirmed by decompiling `AeccDbMgd.dll` for this build — `Structure`, `Pipe`, `Network`, `PartFamily`, `PartsList`): `Network.Create(CivilDocument, ref string)`, `Network.AddStructure(ObjectId, ObjectId, Point3d, double, ref ObjectId, bool)`, `Network.AddLinePipe(ObjectId, ObjectId, LineSegment3d, ref ObjectId, bool)`, `Network.PartsListName`, `Network.PartsListId`, `Structure.RimElevation` (settable), `Structure.SumpElevation` (settable), `Pipe.StartPoint`/`EndPoint` (settable `Point3d`), `Pipe.ResizeByInnerDiameterOrWidth(double, bool)`, `Pipe.ConnectToStructure(ConnectorPositionType, ObjectId, bool)`, `PartsList.GetPartFamilyIdsByDomain(DomainType)`, `PartFamily.Description` (the family's display name — `PartFamily` has **no** `Name` property, only `Description`; this replaces the reflection-based family lookup in `add_structure_and_pipe` with the confirmed real member), `PartFamily.PartSizeCount` and its `this[int]`/`this[string]` indexers for picking a size by position or description.
- **Unverified / assumption points** — could not execute against a live Civil 3D session, only compile-check the metadata:
  1. **Invert vs. centerline on `Pipe.StartPoint`/`EndPoint`.** This skill writes `invert_up`/`invert_down` straight into `StartPoint.Z`/`EndPoint.Z`, matching how `pipe_slope_and_cover_report.skill.md` already reads `pipe.StartPoint.Z` back out as `usInvert_ft`/`dsInvert_ft` elsewhere in this library — but the real Civil 3D pipe geometry model stores the *centerline* and derives invert from wall thickness in some configurations. Verify on a live session that a pipe's reported invert equals what was set here; if it's off by half the wall thickness, add an offset here (or better, resize first, then compute wall thickness from `Pipe.OuterDiameterOrWidth`/`InnerDiameterOrWidth` and add it before setting).
  2. **Sizing by nearest catalog size.** `AddLinePipe` is called with `ObjectId.Null` for the size (auto/rules-based pick), then `ResizeByInnerDiameterOrWidth(sizeIn / 12.0, useClosestSize: true)` snaps to the nearest catalog size — this is the same path `utility_optimization/resize_pipe.skill.md` uses and was chosen over reaching into `PartSize`/`PartDataRecord` internals (whose nominal-diameter field name wasn't confirmed in the decompile). A pipe that can't resize lands in `pipesUnsized` with the underlying exception message rather than failing the whole run.
  3. **`is_outfall` is not read.** The JSON schema's `is_outfall` flag on structures isn't consumed — Civil 3D outfall structures may need a distinct part family/behavior; every structure here is added as a regular junction/manhole. Flag any `is_outfall: true` structure for manual review after import.
  4. **`length_ft` and `slope` from the JSON are not applied or checked** — pipe length/slope in Civil 3D are derived from the endpoint geometry that's set here, not settable directly. Run `pipe_slope_and_cover_report` afterward and compare against the handoff JSON to confirm the network reproduces the intended design.
  5. **Structure/pipe naming** comes from Civil 3D's own `StructureNameTemplate`/pipe-naming defaults, not the JSON's `id` values — this skill returns a `{id → generated name}` map in `structures`/`pipes` so callers can cross-reference.
- A structure or pipe family/size search that finds nothing returns a clear `error` naming the parts list — run `list_parts_lists` first to see what's actually available if this fails.
- Run `pipe_slope_and_cover_report` and `check_pipe_cover`/`find_pipe_conflicts` immediately after this skill to QA the network it built.
