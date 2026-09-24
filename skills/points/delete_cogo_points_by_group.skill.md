---
name: delete_cogo_points_by_group
category: points
description: Delete every COGO point belonging to a specific point group
requires_write: true
parameters:
  - name: pointGroupName
    type: string
    required: true
---

## Code Template

```csharp
string pointGroupName = "POINT_GROUP_NAME";

if (!CivilDoc.PointGroups.Contains(pointGroupName))
    return new { error = $"Point group '{pointGroupName}' not found" };

var groupId = CivilDoc.PointGroups[pointGroupName];
var group = Transaction.GetObject(groupId, OpenMode.ForWrite) as PointGroup;

int countBefore = (int)group.PointsCount;
group.DeletePoints();

return new
{
    success = true,
    pointGroupName,
    deletedCount = countBefore,
    warning = "This deletes the underlying COGO points, not just the group membership."
};
```

## Usage Notes
- `PointGroup.DeletePoints()` deletes the actual COGO points in the group from the drawing — it does NOT just remove them from the group. Double-check the group's membership with `list_cogo_points` (filtered) before running.
- The point group itself remains (now empty) after this call — it is not deleted.
