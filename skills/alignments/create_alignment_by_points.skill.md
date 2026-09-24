---
name: create_alignment_by_points
category: alignments
description: Create an alignment from a list of tangent points (no polyline needed) using layout entities
requires_write: true
parameters:
  - name: alignmentName
    type: string
    required: true
  - name: points
    type: array
    required: true
    description: Ordered array of {x, y} points defining the alignment tangents
---

## Code Template

```csharp
string alignName = "ALIGNMENT_NAME_HERE";

// Create an empty alignment, then draw a temporary polyline through the points
// and add it as the alignment's geometry. This is the most reliable path since
// Alignment has no direct "add tangent by points" entity constructor.
var pts = new Point3dCollection();
pts.Add(new Point3d(0, 0, 0));
pts.Add(new Point3d(500, 0, 0));
pts.Add(new Point3d(500, 500, 0));
// Add more points as needed — replace with the caller's point list

var bt = (BlockTable)Transaction.GetObject(Database.BlockTableId, OpenMode.ForRead);
var btr = (BlockTableRecord)Transaction.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

var polyline = new Polyline();
for (int i = 0; i < pts.Count; i++)
    polyline.AddVertexAt(i, new Point2d(pts[i].X, pts[i].Y), 0, 0, 0);

btr.AppendEntity(polyline);
Transaction.AddNewlyCreatedDBObject(polyline, true);

var plineOptions = new PolylineOptions
{
    PlineId = polyline.ObjectId,
    EraseExistingEntities = true,
    AddCurvesBetweenTangents = false
};

var alignId = Alignment.Create(CivilDoc, plineOptions, alignName, ObjectId.Null, Database.Clayer, ObjectId.Null, ObjectId.Null);
var alignment = Transaction.GetObject(alignId, OpenMode.ForRead) as Alignment;

return new
{
    success = true,
    name = alignment.Name,
    length = alignment.Length,
    tangentPointCount = pts.Count
};
```

## Usage Notes
- Civil 3D's alignment API has no direct "build from raw points" constructor, so this draws a scratch polyline first (erased once converted, per `EraseExistingEntities = true`).
- For curved alignments, set `AddCurvesBetweenTangents = true` and follow up with `edit_profile_pvi`-style entity edits, or use `create_alignment_from_polyline` on a polyline that already has arcs.
