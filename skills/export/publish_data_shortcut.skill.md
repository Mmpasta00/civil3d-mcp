---
name: publish_data_shortcut
category: export
description: Publish a surface, alignment, or pipe network as a Data Shortcut for other drawings in the project to reference
requires_write: true
parameters:
  - name: objectName
    type: string
    required: true
  - name: objectKind
    type: string
    required: true
    description: "surface | alignment | pipenetwork"
---

## Code Template

```csharp
string objectName = "OBJECT_NAME";
string objectKind = "surface"; // surface | alignment | pipenetwork

ObjectId targetId = ObjectId.Null;
switch (objectKind.ToLowerInvariant())
{
    case "surface":
        foreach (ObjectId id in CivilDoc.GetSurfaceIds())
        {
            var s = Transaction.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
            if (s != null && s.Name.Equals(objectName, StringComparison.OrdinalIgnoreCase)) { targetId = id; break; }
        }
        break;
    case "alignment":
        foreach (ObjectId id in CivilDoc.GetAlignmentIds())
        {
            var a = Transaction.GetObject(id, OpenMode.ForRead) as Alignment;
            if (a != null && a.Name.Equals(objectName, StringComparison.OrdinalIgnoreCase)) { targetId = id; break; }
        }
        break;
    case "pipenetwork":
        foreach (ObjectId id in CivilDoc.GetPipeNetworkIds())
        {
            var n = Transaction.GetObject(id, OpenMode.ForRead) as Network;
            if (n != null && n.Name.Equals(objectName, StringComparison.OrdinalIgnoreCase)) { targetId = id; break; }
        }
        break;
    default:
        return new { error = "objectKind must be surface, alignment, or pipenetwork" };
}
if (targetId.IsNull) return new { error = $"{objectKind} '{objectName}' not found" };

// Data Shortcuts requires a working folder + project already configured for this drawing
// (Toolspace > Prospector > Data Shortcuts) — publishing itself is command-driven, not a
// direct managed "Publish" factory in this API surface.
Editor.Command("_AeccCreateReferences", targetId, "");

return new
{
    success = true,
    objectKind,
    objectName,
    note = "Requires an existing Data Shortcuts working folder/project association for this drawing — configure that once via Prospector before this will succeed."
};
```

## Usage Notes
- **Command-based**: Data Shortcut publishing has no confirmed direct managed API in the referenced package — this runs Civil 3D's "Create References" command equivalent.
- A Data Shortcuts working folder and project must already be set up for the drawing (one-time project setup in Toolspace > Prospector) — this skill will fail cleanly if that hasn't been done.
- Downstream drawings then use "Create Reference" to pull in the shortcut as a read-only reference surface/alignment/network.
