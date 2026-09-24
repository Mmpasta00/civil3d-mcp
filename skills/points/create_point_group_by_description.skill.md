---
name: create_point_group_by_description
category: points
description: Create a COGO point group whose membership is a raw-description wildcard match (e.g. all "TOPO*" points)
requires_write: true
parameters:
  - name: groupName
    type: string
    required: true
  - name: descriptionPattern
    type: string
    required: true
    description: Wildcard pattern matched against each point's raw description, e.g. "TOPO*" or "CB,DI"
---

## Code Template

```csharp
string groupName = "GROUP_NAME_HERE";
string descriptionPattern = "TOPO*";

if (CivilDoc.PointGroups.Contains(groupName))
    return new { error = $"Point group '{groupName}' already exists" };

var groupId = CivilDoc.PointGroups.Add(groupName);
var group = Transaction.GetObject(groupId, OpenMode.ForWrite) as PointGroup;

var query = new StandardPointGroupQuery
{
    UseCaseSensitiveMatch = false,
    IncludeRawDescriptions = descriptionPattern
};
group.SetQuery(query);
group.Update();

return new
{
    success = true,
    groupName = group.Name,
    descriptionPattern,
    matchedPointCount = group.PointsCount
};
```

## Usage Notes
- `StandardPointGroupQuery.IncludeRawDescriptions` takes the same wildcard syntax as the "Raw Descriptions Matching" field on the Point Group Properties > Include tab (e.g. `TOPO*`, or a comma-separated list like `CB,DI`).
- Other `StandardPointGroupQuery` properties (`IncludeNumbers`, `IncludeElevations`, `ExcludeRawDescriptions`, etc.) combine with AND logic — set more than one for a narrower group.
- Alternative for a one-off group: build the point list manually with `list_cogo_points` (filtering client-side by description) then reference those point numbers directly in a report — a manual list does not need a live query.
