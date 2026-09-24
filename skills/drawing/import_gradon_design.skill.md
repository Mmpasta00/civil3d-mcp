---
name: import_gradon_design
category: drawing
description: One-message import of a full Gradon Design handoff folder — EG/FG surfaces plus every pipe network — then zooms to extents
requires_write: true
parameters:
  - name: handoffFolder
    type: string
    required: true
    description: Absolute path to the handoff folder written by the Gradon Design service (e.g. "C:/projects/livermore/handoff")
  - name: egSurfaceName
    type: string
    required: false
    description: Name for the imported existing-ground surface (default "EG")
  - name: fgSurfaceName
    type: string
    required: false
    description: Name for the imported finished-grade surface (default "FG")
  - name: egStyleName
    type: string
    required: false
  - name: fgStyleName
    type: string
    required: false
  - name: partsListName
    type: string
    required: false
    description: Parts list to assign to every network built from this handoff (default "Standard")
  - name: structureFamily
    type: string
    required: false
  - name: structureSize
    type: string
    required: false
  - name: pipeFamily
    type: string
    required: false
---

## Code Template

```csharp
using System.Text.Json;

string handoffFolder = @"HANDOFF_FOLDER_HERE";
string egSurfaceName = "EG";
string fgSurfaceName = "FG";
string egStyleName = "";
string fgStyleName = "";
string partsListName = "Standard";
string structureFamilyName = "";
string structureSizeName = "";
string pipeFamilyName = "";

if (!System.IO.Directory.Exists(handoffFolder))
    return new { error = $"Folder not found: {handoffFolder}" };

// ---- Step 1: surfaces (inlined from import_gradon_design_surfaces — skills cannot call skills) ----
object ImportSurface(string fileName, string surfaceName, string styleName)
{
    string xmlPath = System.IO.Path.Combine(handoffFolder, fileName);
    if (!System.IO.File.Exists(xmlPath))
        return new { error = $"File not found: {xmlPath}" };

    ObjectId surfaceId;
    try { surfaceId = TinSurface.CreateFromLandXML(Database, surfaceName, xmlPath); }
    catch (System.Exception ex) { return new { error = $"LandXML import failed for {fileName}: {ex.Message}" }; }

    var surface = Transaction.GetObject(surfaceId, OpenMode.ForWrite) as TinSurface;
    string appliedStyle = surface.StyleName;
    string styleError = null;
    if (!string.IsNullOrWhiteSpace(styleName))
    {
        try { surface.StyleName = styleName; appliedStyle = surface.StyleName; }
        catch (System.Exception ex) { styleError = $"Could not assign style '{styleName}': {ex.Message}"; }
    }

    var props = surface.GetGeneralProperties();
    return new
    {
        success = true,
        file = xmlPath,
        surfaceName = surface.Name,
        style = appliedStyle,
        styleError,
        pointCount = props.NumberOfPoints,
        minElevation = props.MinimumElevation,
        maxElevation = props.MaximumElevation
    };
}

var egResult = ImportSurface("EG.xml", egSurfaceName, egStyleName);
var fgResult = ImportSurface("grading.xml", fgSurfaceName, fgStyleName);

// ---- Step 2: every pipe network (inlined from build_pipe_network_from_json) ----
var networkResults = new List<object>();
var networkJsonPaths = System.IO.Directory.GetFiles(handoffFolder, "network-*.json")
    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
    .ToList();

foreach (var jsonPath in networkJsonPaths)
{
    JsonDocument doc;
    try { doc = JsonDocument.Parse(System.IO.File.ReadAllText(jsonPath)); }
    catch (System.Exception ex) { networkResults.Add(new { file = jsonPath, error = $"Could not parse JSON: {ex.Message}" }); continue; }

    var root = doc.RootElement;
    string networkName = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : System.IO.Path.GetFileNameWithoutExtension(jsonPath);
    string units = root.TryGetProperty("units", out var unitsEl) ? unitsEl.GetString() : "ft";
    if (!string.IsNullOrWhiteSpace(units) && !units.Equals("ft", StringComparison.OrdinalIgnoreCase))
    {
        networkResults.Add(new { file = jsonPath, error = $"Unsupported units '{units}' — this skill assumes feet." });
        continue;
    }

    var nameRef = networkName;
    var networkId = Network.Create(CivilDoc, ref nameRef);
    var net = Transaction.GetObject(networkId, OpenMode.ForWrite) as Network;

    try { net.PartsListName = partsListName; }
    catch (System.Exception ex) { networkResults.Add(new { file = jsonPath, networkName = net.Name, error = $"Parts list '{partsListName}' assignment failed: {ex.Message}" }); continue; }

    if (net.PartsListId.IsNull)
    {
        networkResults.Add(new { file = jsonPath, networkName = net.Name, error = $"No parts list resolved after assigning '{partsListName}'" });
        continue;
    }

    var partsList = Transaction.GetObject(net.PartsListId, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.PartsList;

    ObjectId ResolveFamily(DomainType domain, string wantedDescription)
    {
        var ids = partsList.GetPartFamilyIdsByDomain(domain);
        if (ids.Count == 0) return ObjectId.Null;
        if (string.IsNullOrWhiteSpace(wantedDescription)) return ids[0];
        foreach (ObjectId id in ids)
        {
            var fam = Transaction.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.PartFamily;
            if (fam != null && fam.Description.Equals(wantedDescription, StringComparison.OrdinalIgnoreCase)) return id;
        }
        return ObjectId.Null;
    }

    var structFamilyId = ResolveFamily(DomainType.Structure, structureFamilyName);
    var pipeFamilyId = ResolveFamily(DomainType.Pipe, pipeFamilyName);
    if (structFamilyId.IsNull || pipeFamilyId.IsNull)
    {
        networkResults.Add(new { file = jsonPath, networkName = net.Name, error = "No usable structure and/or pipe part family found in the assigned parts list" });
        continue;
    }

    var structFamily = Transaction.GetObject(structFamilyId, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.PartFamily;
    ObjectId structSizeId = ObjectId.Null;
    if (!string.IsNullOrWhiteSpace(structureSizeName))
    {
        try { structSizeId = structFamily[structureSizeName]; } catch (System.Exception) { /* fall back to auto size */ }
    }
    else if (structFamily.PartSizeCount > 0)
    {
        structSizeId = structFamily[0];
    }

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
            try { net.AddStructure(structFamilyId, structSizeId, new Point3d(x, y, rim), 0.0, ref newStructId, true); }
            catch (System.Exception ex) { structureErrors.Add(new { id, error = ex.Message }); continue; }

            var structObj = Transaction.GetObject(newStructId, OpenMode.ForWrite) as Structure;
            try { structObj.RimElevation = rim; } catch (System.Exception) { }
            if (sump.HasValue) { try { structObj.SumpElevation = sump.Value; } catch (System.Exception) { } }

            structureIds[id] = newStructId;
            structuresCreated.Add(new { id, name = structObj.Name, rim = structObj.RimElevation });
        }
    }

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
            try { net.AddLinePipe(pipeFamilyId, ObjectId.Null, line, ref newPipeId, true); }
            catch (System.Exception ex) { pipeErrors.Add(new { id, error = ex.Message }); continue; }

            var pipe = Transaction.GetObject(newPipeId, OpenMode.ForWrite) as Pipe;

            try { pipe.ResizeByInnerDiameterOrWidth(sizeIn / 12.0, true); }
            catch (System.Exception ex) { pipesUnsized.Add(new { id, pipeName = pipe.Name, requestedSize_in = sizeIn, error = ex.Message }); }

            try
            {
                pipe.StartPoint = new Point3d(pipe.StartPoint.X, pipe.StartPoint.Y, invertUp);
                pipe.EndPoint = new Point3d(pipe.EndPoint.X, pipe.EndPoint.Y, invertDown);
            }
            catch (System.Exception) { }

            try { pipe.ConnectToStructure(ConnectorPositionType.Start, upStructId, true); } catch (System.Exception) { }
            try { pipe.ConnectToStructure(ConnectorPositionType.End, downStructId, true); } catch (System.Exception) { }

            pipesCreated.Add(new { id, pipeName = pipe.Name, partSize = pipe.PartSizeName, upStructure = upStruct.Name, downStructure = downStruct.Name });
        }
    }

    networkResults.Add(new
    {
        file = jsonPath,
        networkName = net.Name,
        structuresCreated = structuresCreated.Count,
        structureErrors,
        pipesCreated = pipesCreated.Count,
        pipesUnsized,
        pipeErrors
    });
}

// ---- Step 3: zoom to extents so the engineer immediately sees what was imported ----
string zoomError = null;
try { Editor.Command("_.ZOOM", "_Extents"); }
catch (System.Exception ex) { zoomError = ex.Message; }

return new
{
    handoffFolder,
    eg = egResult,
    fg = fgResult,
    networksFound = networkJsonPaths.Count,
    networks = networkResults,
    zoomError
};
```

## Usage Notes
- Composes `import_gradon_design_surfaces` and `build_pipe_network_from_json` by inlining their logic (per this library's rule that a skill script cannot call another skill script) and adds a `network-*.json` folder scan so it handles however many pipe networks a given handoff contains, plus a final zoom-extents.
- See `build_pipe_network_from_json`'s Usage Notes for the full list of verified vs. unverified API members (`Structure.RimElevation`/`SumpElevation`, `Network.AddStructure`/`AddLinePipe`, `Pipe.StartPoint`/`EndPoint`/`ResizeByInnerDiameterOrWidth`/`ConnectToStructure`, `PartFamily.Description`) — identical caveats apply here since the code is the same logic inlined.
- `zoomError` is non-fatal and separate from the import results: `Editor.Command("_.ZOOM", "_Extents")` is the command-context fallback noted in this library's README for operations without a confirmed direct managed-API call; if it doesn't run cleanly on the installed Civil 3D version/language, the surfaces and networks are still imported — just re-run `ZOOM Extents` manually.
- A malformed or unreadable `network-*.json` file is recorded as an `error` entry in `networks` rather than aborting the whole import — the surfaces and any other, valid network files still get imported.
- Runs everything inside the plugin's single existing `Transaction` — a partial failure partway through (e.g. the third of five networks) still leaves the earlier ones committed when the host closes the transaction, so check `networks[].error`/`pipeErrors`/`structureErrors` rather than assuming an all-or-nothing result.
