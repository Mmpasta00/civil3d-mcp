---
name: import_gradon_design_surfaces
category: surfaces
description: Import the existing-ground and finished-grade surfaces from a Gradon Design generative-design handoff folder (EG.xml, grading.xml) in one call
requires_write: true
parameters:
  - name: handoffFolder
    type: string
    required: true
    description: Absolute path to the handoff folder containing EG.xml and grading.xml (e.g. "C:/projects/livermore/handoff")
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
    description: Existing surface style to assign to the EG surface, if it already exists in the drawing
  - name: fgStyleName
    type: string
    required: false
    description: Existing surface style to assign to the FG surface, if it already exists in the drawing
---

## Code Template

```csharp
string handoffFolder = @"HANDOFF_FOLDER_HERE";
string egSurfaceName = "EG";
string fgSurfaceName = "FG";
string egStyleName = "";
string fgStyleName = "";

object ImportOne(string fileName, string surfaceName, string styleName)
{
    string xmlPath = System.IO.Path.Combine(handoffFolder, fileName);
    if (!System.IO.File.Exists(xmlPath))
        return new { error = $"File not found: {xmlPath}" };

    ObjectId surfaceId;
    try
    {
        surfaceId = TinSurface.CreateFromLandXML(Database, surfaceName, xmlPath);
    }
    catch (System.Exception ex)
    {
        return new { error = $"LandXML import failed for {fileName}: {ex.Message}" };
    }

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

var egResult = ImportOne("EG.xml", egSurfaceName, egStyleName);
var fgResult = ImportOne("grading.xml", fgSurfaceName, fgStyleName);

return new
{
    handoffFolder,
    eg = egResult,
    fg = fgResult
};
```

## Usage Notes
- Thin wrapper around `import_landxml_surface`'s verified `TinSurface.CreateFromLandXML(Database, surfaceName, xmlPath)` call, run twice for the two fixed handoff filenames (`EG.xml`, `grading.xml`) that the Gradon Design service always writes into the handoff folder.
- Each import is independent: if `EG.xml` is missing or fails to parse, `fg` still attempts to import — check both `eg.error`/`fg.error` before trusting `success`.
- Style assignment reuses `assign_surface_style`'s pattern (`surface.StyleName = styleName`, resolved against `CivilDoc.Styles.SurfaceStyles`); the style must already exist in the drawing (e.g. from the firm's template) — a missing style name comes back as `styleError` without failing the import.
- `handoffFolder` must be a Windows-style path on the work PC (e.g. `C:/projects/...`).
- If `grading.xml`'s LandXML surface name collides with `EG.xml`'s default surface name, Civil 3D may append a numeric suffix — read back `surfaceName` in each result rather than assuming the exact requested name, same caveat as `import_landxml_surface`.
- Run `list_surfaces` afterward to confirm both surfaces landed in the drawing with sane point counts.
