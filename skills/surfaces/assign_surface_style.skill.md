---
name: assign_surface_style
category: surfaces
description: Assign an existing surface style (by name) to a surface, e.g. switching from "No display" to "Contours 2' and 10'"
requires_write: true
parameters:
  - name: surfaceName
    type: string
    required: true
  - name: styleName
    type: string
    required: true
---

## Code Template

```csharp
string surfaceName = "SURFACE_NAME";
string styleName = "STYLE_NAME_HERE";

Autodesk.Civil.DatabaseServices.Surface surface = null;
foreach (ObjectId id in CivilDoc.GetSurfaceIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForWrite) as Autodesk.Civil.DatabaseServices.Surface;
    if (s != null && s.Name.Equals(surfaceName, StringComparison.OrdinalIgnoreCase)) { surface = s; break; }
}
if (surface == null) return new { error = "Surface not found" };

string oldStyle = surface.StyleName;

try
{
    surface.StyleName = styleName;
}
catch (System.Exception ex)
{
    return new { error = $"Could not assign style '{styleName}': {ex.Message}. Confirm it exists in this drawing's surface style collection." };
}

return new { success = true, surfaceName = surface.Name, oldStyle, newStyle = surface.StyleName };
```

## Usage Notes
- The style must already exist in the drawing (imported via a template or another drawing) — this does not create new styles.
- `StyleName` setter resolves the name against `CivilDoc.Styles.SurfaceStyles`; a typo throws rather than silently failing.
