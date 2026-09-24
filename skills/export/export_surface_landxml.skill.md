---
name: export_surface_landxml
category: export
description: Export a surface to a LandXML file for handoff to another consultant or software
requires_write: false
parameters:
  - name: surfaceName
    type: string
    required: true
  - name: outputPath
    type: string
    required: true
    description: Absolute Windows path for the output .xml file (e.g. "C:/projects/livermore/EG.xml")
---

## Code Template

```csharp
string surfaceName = "SURFACE_NAME";
string outputPath = @"C:\projects\export\surface.xml";

Autodesk.Civil.DatabaseServices.Surface surface = null;
foreach (ObjectId id in CivilDoc.GetSurfaceIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
    if (s != null && s.Name.Equals(surfaceName, StringComparison.OrdinalIgnoreCase)) { surface = s; break; }
}
if (surface == null) return new { error = "Surface not found" };

// LandXML export has no confirmed direct "Surface.ExportToLandXML(path)" managed method in the
// referenced package — Civil 3D exposes this through the LandXML Export command, which honors
// CivilDoc.Settings' SettingsLandXMLExport tree for what gets written.
Editor.Command("_-ExportLandXML", outputPath, "_S", surface.Name, "", "");

bool fileExists = System.IO.File.Exists(outputPath);

return new
{
    success = fileExists,
    surfaceName = surface.Name,
    outputPath,
    warning = !fileExists ? "Export command ran but output file was not found — verify the command's prompt sequence interactively (AECCEXPORTLANDXML) for this Civil 3D version/language." : null
};
```

## Usage Notes
- **Command-based**: LandXML export/import in this API surface is settings-driven and command-triggered (`Autodesk.Civil.Settings.SettingsLandXMLExport`), with no confirmed one-call managed export method — this runs the equivalent command.
- The command's exact prompt sequence varies by Civil 3D version/language pack — test once interactively (`-ExportLandXML` on the command line) and adjust the argument list to match your installation.
- `outputPath` must be a Windows-style path reachable from the engineer's machine (the plugin runs on the work PC, not this build environment).
