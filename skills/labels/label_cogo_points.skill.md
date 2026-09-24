---
name: label_cogo_points
category: labels
description: Assign a point label style to a group of COGO points (controls what text/symbol shows next to each point)
requires_write: true
parameters:
  - name: pointGroupName
    type: string
    required: true
  - name: labelStyleName
    type: string
    required: true
    description: Name of an existing point label style in the drawing
---

## Code Template

```csharp
string pointGroupName = "POINT_GROUP_NAME";
string labelStyleName = "LABEL_STYLE_NAME";

if (!CivilDoc.PointGroups.Contains(pointGroupName))
    return new { error = $"Point group '{pointGroupName}' not found" };

var groupId = CivilDoc.PointGroups[pointGroupName];
var group = Transaction.GetObject(groupId, OpenMode.ForWrite) as PointGroup;
int pointCount = group.GetPointNumbers().Length;

ObjectId styleId = ObjectId.Null;
foreach (ObjectId id in CivilDoc.Styles.LabelStyles.PointLabelStyles.LabelStyles)
{
    var style = Transaction.GetObject(id, OpenMode.ForRead);
    var nameProp = style.GetType().GetProperty("Name");
    if (nameProp != null && (string)nameProp.GetValue(style) == labelStyleName) { styleId = id; break; }
}
if (styleId.IsNull) return new { error = $"Point label style '{labelStyleName}' not found" };

group.PointLabelStyleId = styleId;

return new
{
    success = true,
    pointGroupName,
    labelStyleName,
    pointsAffected = pointCount
};
```

## Usage Notes
- Sets the label style at the point-GROUP level (`PointGroup.PointLabelStyleId`) so every member updates together in one call.
- Reflection is used to read each style's `Name` because the exact style wrapper type name can vary by release — replace with the direct property once confirmed for your installed version.
- For a one-off exception on a single point, set `CogoPoint.LabelStyleIdOverride` on that point instead of changing the whole group's style.
