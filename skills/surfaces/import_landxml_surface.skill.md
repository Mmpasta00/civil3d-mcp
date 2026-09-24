---
name: import_landxml_surface
category: surfaces
description: Import a surface from a LandXML file. Unblocks the external Surface Creation workflow.
requires_write: true
parameters:
  - name: landXmlPath
    type: string
    required: true
    description: Absolute path to the LandXML file on the engineer's machine (e.g. "C:/projects/livermore/EG.xml")
  - name: surfaceName
    type: string
    required: false
    description: Name for the new Civil 3D surface (default derived from the file name)
  - name: landXmlSurfaceName
    type: string
    required: false
    description: Name of the specific surface inside the LandXML file, if it contains more than one
---

## Code Template

```csharp
string xmlPath = @"LANDXML_PATH";
string surfaceName = "";        // optional — defaults to the file name below
string landXmlSurfaceName = ""; // optional — only needed if the file has multiple surfaces

if (!System.IO.File.Exists(xmlPath))
    return new { error = $"File not found: {xmlPath}" };

if (string.IsNullOrWhiteSpace(surfaceName))
    surfaceName = System.IO.Path.GetFileNameWithoutExtension(xmlPath);

ObjectId newSurfaceId;
try
{
    newSurfaceId = string.IsNullOrWhiteSpace(landXmlSurfaceName)
        ? TinSurface.CreateFromLandXML(Database, surfaceName, xmlPath)
        : TinSurface.CreateFromLandXML(Database, surfaceName, xmlPath, landXmlSurfaceName);
}
catch (System.Exception ex)
{
    return new { error = $"LandXML import failed: {ex.Message}" };
}

var surface = Transaction.GetObject(newSurfaceId, OpenMode.ForRead) as TinSurface;
var props = surface.GetGeneralProperties();

return new
{
    success = true,
    file = xmlPath,
    surfaceName = surface.Name,
    pointCount = props.NumberOfPoints,
    minElevation = props.MinimumElevation,
    maxElevation = props.MaximumElevation
};
```

## Usage Notes
- LandXML is the universal surface exchange format. `TinSurface.CreateFromLandXML` imports the TIN (points/breaklines/boundaries baked into that surface's triangulation) directly as a new named surface.
- Path must be Windows-style on the work PC (e.g. `C:/projects/...` or `C:\\projects\\...`).
- If the file contains multiple surfaces, pass `landXmlSurfaceName` to pick one — otherwise Civil 3D imports its first/default surface.
- After import, run `list_surfaces` to confirm names and statistics look right.
- If the LandXML uses a different coordinate system than the drawing, the surface may land in the wrong location — verify coordinates before continuing downstream work.
