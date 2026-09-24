---
name: assign_label_set
category: labels
description: Apply a label set (bundle of geometry-point, curve, and line labels) to an alignment
requires_write: true
parameters:
  - name: alignmentName
    type: string
    required: true
  - name: labelSetName
    type: string
    required: true
---

## Code Template

```csharp
string alignName = "ALIGNMENT_NAME";
string labelSetName = "LABEL_SET_NAME";

Alignment alignment = null;
foreach (ObjectId id in CivilDoc.GetAlignmentIds())
{
    var a = Transaction.GetObject(id, OpenMode.ForWrite) as Alignment;
    if (a != null && a.Name.Equals(alignName, StringComparison.OrdinalIgnoreCase)) { alignment = a; break; }
}
if (alignment == null) return new { error = "Alignment not found" };

try
{
    alignment.ImportLabelSet(labelSetName);
}
catch (System.Exception ex)
{
    return new { error = $"Could not apply label set '{labelSetName}': {ex.Message}" };
}

return new { success = true, alignmentName = alignment.Name, labelSetName };
```

## Usage Notes
- `ImportLabelSet` replaces the alignment's current label set wholesale — existing individually-added labels not part of a set may remain alongside it, which can look duplicated. Clear stray labels first if switching standards mid-project.
- Label sets are defined once (Toolspace > Settings > Alignment > Label Sets) and reused across every alignment in the drawing for consistency.
