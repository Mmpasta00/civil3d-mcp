---
name: delete_alignment
category: alignments
description: Delete an alignment from the drawing by name
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

string deletedName = alignment.Name;
int profileCount = alignment.GetProfileIds().Count;

alignment.Erase();

return new
{
    success = true,
    deletedAlignment = deletedName,
    warning = profileCount > 0
        ? $"{profileCount} dependent profile(s) were also removed with this alignment"
        : (string)null
};
```

## Usage Notes
- Erasing an alignment also erases its profiles, profile views, and sample line groups — always confirm with the engineer first (`requires_write: true`).
- To recover, `Undo` in the Civil 3D session before the transaction commits, or reload the drawing without saving.
