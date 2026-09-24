---
name: set_subassembly_targets
category: corridors
description: Assign a surface as the target for every subassembly in a corridor baseline region (typical daylight/grading target)
requires_write: true
parameters:
  - name: corridorName
    type: string
    required: true
  - name: baselineName
    type: string
    required: true
  - name: targetSurfaceName
    type: string
    required: true
---

## Code Template

```csharp
string corridorName = "CORRIDOR_NAME";
string baselineName = "BASELINE_NAME";
string targetSurfaceName = "SURFACE_NAME";

Corridor corridor = null;
foreach (ObjectId id in CivilDoc.CorridorCollection)
{
    var c = Transaction.GetObject(id, OpenMode.ForWrite) as Corridor;
    if (c != null && c.Name.Equals(corridorName, StringComparison.OrdinalIgnoreCase)) { corridor = c; break; }
}
if (corridor == null) return new { error = "Corridor not found" };

Baseline baseline = null;
foreach (Baseline bl in corridor.Baselines)
{
    if (bl.Name.Equals(baselineName, StringComparison.OrdinalIgnoreCase)) { baseline = bl; break; }
}
if (baseline == null) return new { error = "Baseline not found on that corridor" };

ObjectId targetSurfId = ObjectId.Null;
foreach (ObjectId id in CivilDoc.GetSurfaceIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
    if (s != null && s.Name.Equals(targetSurfaceName, StringComparison.OrdinalIgnoreCase)) { targetSurfId = id; break; }
}
if (targetSurfId.IsNull) return new { error = "Target surface not found" };

var targets = baseline.GetTargets();
int updated = 0;
foreach (SubassemblyTargetInfo info in targets)
{
    if (info.TargetType == SubassemblyLogicalNameType.Surface)
    {
        info.TargetIds.Clear();
        info.TargetIds.Add(targetSurfId);
        updated++;
    }
}
baseline.SetTargets(targets);

return new { corridorName, baselineName, targetSurfaceName, surfaceTargetsUpdated = updated };
```

## Usage Notes
- Only surface-type targets are updated — width/slope/elevation targets (offset alignments, other profiles) are left untouched.
- Rebuild the corridor after retargeting: run `rebuild_all_corridors`.
- `SubassemblyTargetInfoCollection`/`SubassemblyTargetInfo` shapes vary slightly by Civil 3D release — verify member names against the installed API if this fails to compile against an older version.
