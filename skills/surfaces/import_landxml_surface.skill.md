---
name: import_landxml_surface
category: surfaces
description: Import one or more surfaces from a LandXML file. Unblocks the external Surface Creation workflow.
requires_write: true
parameters:
  - name: landXmlPath
    type: string
    required: true
    description: Absolute path to the LandXML file on the engineer's machine (e.g. "C:/projects/livermore/EG.xml")
---

## Code Template

```csharp
using Autodesk.Civil.Land.Net;

string xmlPath = @"LANDXML_PATH";

if (!System.IO.File.Exists(xmlPath))
    return new { error = $"File not found: {xmlPath}" };

// Capture surface IDs BEFORE import so we can diff
var before = new HashSet<ObjectId>();
foreach (ObjectId id in CivilDoc.GetSurfaceIds()) before.Add(id);

try
{
    CivilDoc.LandXMLImport(xmlPath);
}
catch (System.Exception ex)
{
    return new { error = $"LandXML import failed: {ex.Message}" };
}

var imported = new List<object>();
foreach (ObjectId id in CivilDoc.GetSurfaceIds())
{
    if (before.Contains(id)) continue;
    var s = Transaction.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
    if (s == null) continue;
    imported.Add(new { name = s.Name, type = s.GetType().Name, layer = s.Layer });
}

return new
{
    file = xmlPath,
    importedSurfaceCount = imported.Count,
    importedSurfaces = imported
};
```

## Usage Notes
- LandXML is the universal surface exchange format. Civil 3D imports TIN, breaklines, and boundaries.
- Path must be Windows-style on the work PC (e.g. `C:/projects/...` or `C:\\projects\\...`).
- After import, run `list_surfaces` to confirm names and statistics look right.
- If the LandXML uses a different coordinate system than the drawing, surfaces may land in the wrong location — verify coordinates before continuing downstream work.
