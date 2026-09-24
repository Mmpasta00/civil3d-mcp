---
name: rename_surface
category: surfaces
description: Rename an existing surface
requires_write: true
parameters:
  - name: currentName
    type: string
    required: true
  - name: newName
    type: string
    required: true
---

## Code Template

```csharp
string currentName = "CURRENT_SURFACE_NAME";
string newName = "NEW_SURFACE_NAME";

Autodesk.Civil.DatabaseServices.Surface surface = null;
foreach (ObjectId id in CivilDoc.GetSurfaceIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForWrite) as Autodesk.Civil.DatabaseServices.Surface;
    if (s != null && s.Name.Equals(currentName, StringComparison.OrdinalIgnoreCase)) { surface = s; break; }
}
if (surface == null) return new { error = "Surface not found" };

surface.Name = newName;

return new { success = true, oldName = currentName, newName = surface.Name };
```

## Usage Notes
- Renaming does not break references from profiles, corridors, or labels that point to this surface by ObjectId — only scripts/skills that look it up by name need updating.
- Names must be unique in the drawing; a duplicate name throws.
