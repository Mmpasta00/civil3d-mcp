---
name: create_grading
category: grading
description: Create a daylight/offset grading from a feature line footprint using an existing grading criteria set
requires_write: true
parameters:
  - name: featureLineHandle
    type: string
    required: true
  - name: criteriaSetName
    type: string
    required: true
    description: Name of an existing grading criteria set (e.g. "Basic Set" or a firm daylight standard)
  - name: targetSurfaceName
    type: string
    required: true
    description: Surface the grading projects to (existing ground for daylight)
---

## Code Template

```csharp
string handleStr = "FEATURE_LINE_HANDLE";
string criteriaSetName = "Basic Set";
string targetSurfaceName = "SURFACE_NAME";

long handleValue = Convert.ToInt64(handleStr, 16);
if (!Database.TryGetObjectId(new Handle(handleValue), out ObjectId flId))
    return new { error = "Feature line handle not found" };

ObjectId surfId = ObjectId.Null;
foreach (ObjectId id in CivilDoc.GetSurfaceIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
    if (s != null && s.Name.Equals(targetSurfaceName, StringComparison.OrdinalIgnoreCase)) { surfId = id; break; }
}
if (surfId.IsNull) return new { error = "Target surface not found" };

// Civil 3D's Grading object model has no confirmed direct "Grading.Create(...)" factory in the
// referenced managed API (grading is normally built via the Grading Creation Tools palette state
// machine) — this runs the equivalent command sequence instead.
var featureLine = Transaction.GetObject(flId, OpenMode.ForRead) as FeatureLine;
Editor.Command(
    "_AeccCreateGrading",
    "_S", criteriaSetName,
    flId,
    "_Y",   // apply to entire feature line
    surfId,
    ""
);

return new
{
    success = true,
    featureLineName = featureLine?.Name,
    criteriaSetName,
    targetSurfaceName,
    note = "Grading created via command context — its ObjectId is not returned. Confirm with grading_group_list."
};
```

## Usage Notes
- **Command-based**: no confirmed direct managed-API `Grading.Create` factory exists in the referenced package for this version — Civil 3D normally builds gradings through an interactive tool-palette state machine, so this uses the equivalent command sequence per the plugin's documented fallback pattern.
- The exact prompt sequence for `_AeccCreateGrading` (criteria set selection, apply-to-entire-line vs. segment, target surface vs. elevation vs. relative) varies by Civil 3D version and by which Grading Creation Tools palette state is active — test interactively once and adjust the argument list.
- After creation, verify results with `grading_group_list` and check daylight lines with `surface_volume` once a grading surface exists.
