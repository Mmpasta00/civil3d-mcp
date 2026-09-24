---
name: create_alignment_from_polyline
category: alignments
description: Convert an existing polyline into a Civil 3D alignment
requires_write: true
parameters:
  - name: polylineHandle
    type: string
    required: true
    description: Drawing handle of the source polyline (from select_by_layer or get_selected_objects_summary)
  - name: alignmentName
    type: string
    required: true
    description: Name for the new alignment
---

## Code Template

```csharp
string handleStr = "POLYLINE_HANDLE";
string alignName = "ALIGNMENT_NAME_HERE";

long handleValue = Convert.ToInt64(handleStr, 16);
var handle = new Handle(handleValue);
if (!Database.TryGetObjectId(handle, out ObjectId plineId))
    return new { error = $"No object found with handle {handleStr}" };

var plineOptions = new PolylineOptions
{
    PlineId = plineId,
    EraseExistingEntities = false,
    AddCurvesBetweenTangents = false
};

var alignId = Alignment.Create(
    CivilDoc,
    plineOptions,
    alignName,
    ObjectId.Null,      // siteId — null = create in the (none) site
    Database.Clayer,    // layerId — current layer
    ObjectId.Null,      // styleId — default alignment style
    ObjectId.Null        // labelSetId — default label set
);

var alignment = Transaction.GetObject(alignId, OpenMode.ForRead) as Alignment;

return new
{
    success = true,
    name = alignment.Name,
    handle = alignment.Handle.ToString(),
    length = alignment.Length,
    startStation = alignment.StartingStation,
    endStation = alignment.EndingStation
};
```

## Usage Notes
- Source polyline can stay in the drawing (EraseExistingEntities = false) or be consumed (set true).
- Passing ObjectId.Null for siteId/styleId/labelSetId uses the "none" site and drawing default styles — reassign a real style afterward if the firm has an alignment standard.
- Get the polyline's handle first with `select_by_layer` or `find_by_handle`.
