---
name: section_volumes
category: sections
description: Compute material (earthwork) volumes along a sample line group between two surfaces using the QTO material list
requires_write: true
parameters:
  - name: groupName
    type: string
    required: true
  - name: alignmentName
    type: string
    required: true
  - name: existingSurfaceName
    type: string
    required: true
  - name: proposedSurfaceName
    type: string
    required: true
  - name: materialListName
    type: string
    required: false
---

## Code Template

```csharp
string groupName = "SAMPLE_LINE_GROUP_NAME";
string alignName = "ALIGNMENT_NAME";
string existingSurfaceName = "EG_SURFACE";
string proposedSurfaceName = "FG_SURFACE";
string materialListName = "Earthwork";

Alignment alignment = null;
foreach (ObjectId id in CivilDoc.GetAlignmentIds())
{
    var a = Transaction.GetObject(id, OpenMode.ForRead) as Alignment;
    if (a != null && a.Name.Equals(alignName, StringComparison.OrdinalIgnoreCase)) { alignment = a; break; }
}
if (alignment == null) return new { error = "Alignment not found" };

ObjectId groupId = ObjectId.Null;
foreach (ObjectId id in alignment.GetSampleLineGroupIds())
{
    var g = Transaction.GetObject(id, OpenMode.ForRead) as SampleLineGroup;
    if (g != null && g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase)) { groupId = id; break; }
}
if (groupId.IsNull) return new { error = "Sample line group not found" };

var group = Transaction.GetObject(groupId, OpenMode.ForWrite) as SampleLineGroup;

var materialList = group.MaterialLists.Count > 0 && group.MaterialLists[0] != null
    ? group.MaterialLists[materialListName]
    : group.MaterialLists.Add(materialListName);

// A real material list needs the two surfaces registered as "existing"/"corridor/proposed" sources
// per Civil 3D's Quantity Takeoff setup (Object > Corridor/Surface material mapping) — this template
// assumes that mapping already exists from the interactive QTO wizard run once for this project.
var mappingNames = group.GetQTOMappingNames();
if (mappingNames.Length == 0)
    return new { error = "No QTO material mapping found on this sample line group — configure one via Sections > Compute Materials once, then re-run." };

var mappingGuid = group.GetMappingGuid(mappingNames[0]);
var result = group.GetTotalVolumeResultDataForMaterialList(mappingGuid);
var perStation = group.GetSampleLineIds().Cast<ObjectId>().Select(slId =>
{
    var sl = Transaction.GetObject(slId, OpenMode.ForRead) as SampleLine;
    return new { station = sl.Station };
}).ToList();

return new
{
    groupName = group.Name,
    mappingUsed = mappingNames[0],
    existingSurfaceName,
    proposedSurfaceName,
    sampleLineCount = perStation.Count,
    note = "Use GetResult(station) on the returned QuantityTakeoffResult per-station for cut/fill at each cross section."
};
```

## Usage Notes
- Civil 3D's cross-section QTO material system requires a one-time interactive "Compute Materials" setup (Sections ribbon) that defines how surfaces map to cut/fill materials — this skill reads that existing mapping rather than recreating it, since the mapping wizard has no simple one-call API equivalent.
- For a surface-to-surface volume without cross sections, use `surface_volume` or `create_volume_surface` instead — much simpler when a full section-by-section breakdown isn't needed.
