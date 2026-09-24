---
name: list_layouts_and_viewports
category: drawing
description: List every paper-space layout in the drawing with its viewports, scale, and page setup
requires_write: false
parameters: []
---

## Code Template

```csharp
var layoutDict = Transaction.GetObject(Database.LayoutDictionaryId, OpenMode.ForRead) as DBDictionary;
var layouts = new List<object>();

foreach (DBDictionaryEntry entry in layoutDict)
{
    var layout = Transaction.GetObject(entry.Value, OpenMode.ForRead) as Layout;
    if (layout == null || layout.LayoutName == "Model") continue;

    var viewports = new List<object>();
    var layoutBtr = Transaction.GetObject(layout.BlockTableRecordId, OpenMode.ForRead) as BlockTableRecord;
    foreach (ObjectId id in layoutBtr)
    {
        var vp = Transaction.GetObject(id, OpenMode.ForRead) as Viewport;
        if (vp == null || vp.Number <= 1) continue; // skip the paper-space "viewport 1" placeholder
        viewports.Add(new
        {
            handle = vp.Handle.ToString(),
            customScale = vp.CustomScale,
            centerPoint = new { x = vp.ViewCenter.X, y = vp.ViewCenter.Y },
            isLocked = vp.Locked
        });
    }

    layouts.Add(new
    {
        name = layout.LayoutName,
        tabOrder = layout.TabOrder,
        pageSetupName = layout.CurrentStyleSheet,
        viewportCount = viewports.Count,
        viewports
    });
}

return new { count = layouts.Count, layouts };
```

## Usage Notes
- Skips the "Model" pseudo-layout and each layout's built-in overview viewport (`Number <= 1`).
- `customScale` is the plot-scale factor (e.g. 0.02083 = 1"=48') — compare across sheets to catch an accidentally mis-scaled viewport before plotting.
