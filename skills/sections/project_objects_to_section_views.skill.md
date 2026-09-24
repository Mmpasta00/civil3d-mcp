---
name: project_objects_to_section_views
category: sections
description: Project nearby drawing objects (utility crossings, easements) onto section views for clash visualization
requires_write: true
parameters:
  - name: sectionViewHandle
    type: string
    required: true
  - name: objectHandles
    type: string
    required: true
    description: Comma-separated handles of objects to project (e.g. an offsite pipe not part of the sampled network)
---

## Code Template

```csharp
string sectionViewHandleStr = "SECTION_VIEW_HANDLE";
string objectHandlesCsv = "HANDLE1,HANDLE2";

long svHandleValue = Convert.ToInt64(sectionViewHandleStr, 16);
if (!Database.TryGetObjectId(new Handle(svHandleValue), out ObjectId svId))
    return new { error = "Section view handle not found" };

var objectIds = new ObjectIdCollection();
foreach (var h in objectHandlesCsv.Split(',').Select(s => s.Trim()))
{
    long handleValue = Convert.ToInt64(h, 16);
    if (Database.TryGetObjectId(new Handle(handleValue), out ObjectId id)) objectIds.Add(id);
}
if (objectIds.Count == 0) return new { error = "No valid object handles resolved" };

// Civil 3D's "Project Objects to Section View" is a command-line tool in this API surface —
// there is no confirmed direct SectionView.ProjectObjects managed method in the referenced package.
var args = new List<object> { "_AeccSectionProjectObjects", svId };
foreach (ObjectId id in objectIds) args.Add(id);
args.Add("");

Editor.Command(args.ToArray());

return new { success = true, sectionViewHandle = sectionViewHandleStr, objectsRequested = objectIds.Count };
```

## Usage Notes
- **Command-based**: object projection onto a section view runs through the command line here since a direct managed factory wasn't confirmed in the referenced package — verify the command name/prompt sequence against your installed Civil 3D (`AECCSECTIONPROJECTOBJECTS` family of commands) and adjust.
- Typical use: showing an existing utility (not part of the sampled corridor/network) crossing a proposed section for a conflict check, alongside `detect_pipe_interference`.
