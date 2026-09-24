---
name: get_selected_objects_summary
category: select
description: Summarize whatever the engineer currently has selected in the Civil 3D session (type counts, handles, layers)
requires_write: false
parameters: []
---

## Code Template

```csharp
var selResult = Editor.SelectImplied();
if (selResult.Status != PromptStatus.OK || selResult.Value == null || selResult.Value.Count == 0)
{
    return new { count = 0, message = "Nothing is currently selected in the drawing." };
}

var items = new List<object>();
foreach (SelectedObject selObj in selResult.Value)
{
    if (selObj == null) continue;
    var ent = Transaction.GetObject(selObj.ObjectId, OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Entity;
    if (ent == null) continue;
    items.Add(new { handle = ent.Handle.ToString(), type = ent.GetType().Name, layer = ent.Layer });
}

var byType = items.GroupBy(i => i.GetType().GetProperty("type").GetValue(i).ToString())
                   .Select(g => new { type = g.Key, count = g.Count() });

return new { count = items.Count, byType, items };
```

## Usage Notes
- `Editor.SelectImplied()` reads whatever the engineer picked BEFORE running this (a "pickfirst" selection) — nothing selected returns an empty, non-error result.
- Good bridge between the engineer clicking things in the Civil 3D UI and handing the AI a concrete handle list to act on with other skills.
