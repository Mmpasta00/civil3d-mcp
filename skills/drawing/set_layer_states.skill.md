---
name: set_layer_states
category: drawing
description: Create layers if missing and set color/freeze/lock/plot state for a batch of layers in one call
requires_write: true
parameters:
  - name: layers
    type: array
    required: true
    description: "Array of {name, colorIndex, frozen, locked, plottable} — only provided fields are changed"
---

## Code Template

```csharp
var layerSpecs = new (string name, int? colorIndex, bool? frozen, bool? locked, bool? plottable)[]
{
    ("C-ROAD", 2, false, false, true),
    ("C-ROAD-TEXT", 7, false, false, true),
    ("C-TOPO-EXIST", 8, true, false, false)
};

var layerTable = Transaction.GetObject(Database.LayerTableId, OpenMode.ForWrite) as LayerTable;
var results = new List<object>();

foreach (var spec in layerSpecs)
{
    LayerTableRecord layer;
    bool created = false;
    if (layerTable.Has(spec.name))
    {
        layer = Transaction.GetObject(layerTable[spec.name], OpenMode.ForWrite) as LayerTableRecord;
    }
    else
    {
        layer = new LayerTableRecord { Name = spec.name };
        layerTable.Add(layer);
        Transaction.AddNewlyCreatedDBObject(layer, true);
        created = true;
    }

    if (spec.colorIndex.HasValue) layer.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, (short)spec.colorIndex.Value);
    if (spec.frozen.HasValue) layer.IsFrozen = spec.frozen.Value;
    if (spec.locked.HasValue) layer.IsLocked = spec.locked.Value;
    if (spec.plottable.HasValue) layer.IsPlottable = spec.plottable.Value;

    results.Add(new { name = spec.name, created, frozen = layer.IsFrozen, locked = layer.IsLocked, plottable = layer.IsPlottable });
}

return new { success = true, layerCount = results.Count, layers = results };
```

## Usage Notes
- Freezing the CURRENT layer throws — if a spec targets `Database.Clayer`'s name, switch the current layer first or skip freezing it.
- Color index uses the AutoCAD Color Index (ACI) palette (1=red, 2=yellow, 3=green, 4=cyan, 5=blue, 6=magenta, 7=white/black, 8/9=grays) — for true-color/RGB use `Color.FromRgb` instead.
- Good first step in `create_project_folder_structure` → `create_layout_from_template` → `set_layer_states` new-sheet setup flow.
