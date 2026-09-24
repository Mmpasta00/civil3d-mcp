---
name: create_corridor
category: corridors
description: Create a new corridor from an alignment, profile, and named assembly
requires_write: true
parameters:
  - name: corridorName
    type: string
    required: true
  - name: baselineName
    type: string
    required: true
  - name: alignmentName
    type: string
    required: true
  - name: profileName
    type: string
    required: true
  - name: assemblyName
    type: string
    required: true
  - name: regionName
    type: string
    required: false
---

## Code Template

```csharp
string corridorName = "CORRIDOR_NAME_HERE";
string baselineName = "BASELINE_NAME_HERE";
string alignName = "ALIGNMENT_NAME";
string profileName = "PROFILE_NAME";
string assemblyName = "ASSEMBLY_NAME";
string regionName = "Region 1";

ObjectId alignId = ObjectId.Null;
foreach (ObjectId id in CivilDoc.GetAlignmentIds())
{
    var a = Transaction.GetObject(id, OpenMode.ForRead) as Alignment;
    if (a != null && a.Name.Equals(alignName, StringComparison.OrdinalIgnoreCase)) { alignId = id; break; }
}
if (alignId.IsNull) return new { error = "Alignment not found" };

var alignEntity = Transaction.GetObject(alignId, OpenMode.ForRead) as Alignment;
ObjectId profileId = ObjectId.Null;
foreach (ObjectId pid in alignEntity.GetProfileIds())
{
    var p = Transaction.GetObject(pid, OpenMode.ForRead) as Profile;
    if (p != null && p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase)) { profileId = pid; break; }
}
if (profileId.IsNull) return new { error = "Profile not found on that alignment" };

ObjectId assemblyId = ObjectId.Null;
foreach (ObjectId id in CivilDoc.AssemblyCollection)
{
    var asm = Transaction.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Assembly;
    if (asm != null && asm.Name.Equals(assemblyName, StringComparison.OrdinalIgnoreCase)) { assemblyId = id; break; }
}
if (assemblyId.IsNull) return new { error = "Assembly not found" };

var corridorId = CivilDoc.CorridorCollection.Add(corridorName, baselineName, alignId, profileId, regionName, assemblyId);
var corridor = Transaction.GetObject(corridorId, OpenMode.ForRead) as Corridor;

return new
{
    success = true,
    name = corridor.Name,
    handle = corridor.Handle.ToString(),
    baselineCount = corridor.Baselines.Count
};
```

## Usage Notes
- The assembly must already exist in the drawing (imported from a tool palette or another drawing) — use `ImportAssembly` on `CivilDoc.AssemblyCollection` if it lives in a template.
- New corridors are built at the drawing's default target/frequency settings — set subassembly targets with `set_subassembly_targets` before the first rebuild for a real design.
- Corridor is created but not yet rebuilt — call `rebuild_all_corridors` after targets are set.
