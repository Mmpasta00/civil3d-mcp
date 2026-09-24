---
name: find_by_handle
category: select
description: Look up a single object by its drawing handle and return its type, layer, and key geometry/Civil 3D properties
requires_write: false
parameters:
  - name: handle
    type: string
    required: true
---

## Code Template

```csharp
string handleStr = "OBJECT_HANDLE";

long handleValue;
try { handleValue = Convert.ToInt64(handleStr, 16); }
catch { return new { error = $"'{handleStr}' is not a valid hex handle" }; }

if (!Database.TryGetObjectId(new Handle(handleValue), out ObjectId id))
    return new { error = $"No object found with handle {handleStr}" };

var obj = Transaction.GetObject(id, OpenMode.ForRead);
var result = new Dictionary<string, object>
{
    ["handle"] = handleStr,
    ["type"] = obj.GetType().Name
};

if (obj is Autodesk.AutoCAD.DatabaseServices.Entity ent)
{
    result["layer"] = ent.Layer;
    result["color"] = ent.Color.ToString();
    try { var ext = ent.GeometricExtents; result["extentsMin"] = new { x = ext.MinPoint.X, y = ext.MinPoint.Y }; result["extentsMax"] = new { x = ext.MaxPoint.X, y = ext.MaxPoint.Y }; }
    catch { /* not all entities support extents */ }
}

// Surface a few common Civil 3D-specific names when applicable
switch (obj)
{
    case Alignment a: result["civilName"] = a.Name; result["civilKind"] = "Alignment"; break;
    case Autodesk.Civil.DatabaseServices.Surface s: result["civilName"] = s.Name; result["civilKind"] = "Surface"; break;
    case Pipe p: result["civilName"] = p.Name; result["civilKind"] = "Pipe"; break;
    case Structure st: result["civilName"] = st.Name; result["civilKind"] = "Structure"; break;
    case Parcel pc: result["civilName"] = pc.Name; result["civilKind"] = "Parcel"; break;
}

return result;
```

## Usage Notes
- Handles are the stable cross-reference used throughout this skill library (`select_by_layer`, `select_objects_in_window`, `get_selected_objects_summary` all return them) — this skill is the general-purpose "what is this thing" lookup for one.
- Handle format is hex without the "0x" prefix, e.g. "2A4", matching what `Handle.ToString()` returns elsewhere in this library.
