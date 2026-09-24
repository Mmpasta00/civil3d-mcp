---
name: reverse_alignment
category: alignments
description: Reverse the direction (start/end) of an existing alignment
requires_write: true
parameters:
  - name: alignmentName
    type: string
    required: true
---

## Code Template

```csharp
Alignment alignment = null;
foreach (ObjectId id in CivilDoc.GetAlignmentIds())
{
    var a = Transaction.GetObject(id, OpenMode.ForWrite) as Alignment;
    if (a != null && a.Name.Equals("ALIGNMENT_NAME", StringComparison.OrdinalIgnoreCase))
    { alignment = a; break; }
}
if (alignment == null) return new { error = "Alignment not found" };

double oldStart = alignment.StartingStation;
double oldEnd = alignment.EndingStation;

alignment.Reverse();

return new
{
    name = alignment.Name,
    oldStartStation = oldStart,
    oldEndStation = oldEnd,
    newStartStation = alignment.StartingStation,
    newEndStation = alignment.EndingStation
};
```

## Usage Notes
- Reversing an alignment also reverses any dependent profiles' station direction — re-check profile PVIs afterward.
- Any station-based labels or referenced sample lines should be regenerated after a reversal.
