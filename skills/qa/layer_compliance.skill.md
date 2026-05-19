---
name: layer_compliance
category: qa
description: Check the drawing against a firm layer-naming standard; report violations (objects on wrong layers, unexpected layer names)
requires_write: false
parameters:
  - name: allowedPrefixes
    type: string
    required: true
    description: Comma-separated allowed layer-name prefixes for this firm/project (e.g. "C-,V-,U-")
---

## Code Template

```csharp
string prefixCsv = "ALLOWED_PREFIXES";
var allowed = prefixCsv.Split(',').Select(s => s.Trim().ToUpperInvariant()).ToList();

var layerTable = Transaction.GetObject(Database.LayerTableId, OpenMode.ForRead) as LayerTable;
var violations = new List<object>();
var allLayers = new List<string>();

foreach (ObjectId layerId in layerTable!)
{
    var layer = Transaction.GetObject(layerId, OpenMode.ForRead) as LayerTableRecord;
    if (layer == null) continue;
    allLayers.Add(layer.Name);

    if (layer.Name == "0" || layer.Name == "Defpoints") continue;

    bool ok = allowed.Any(p => layer.Name.ToUpperInvariant().StartsWith(p));
    if (!ok)
    {
        violations.Add(new
        {
            layer = layer.Name,
            isFrozen = layer.IsFrozen,
            isLocked = layer.IsLocked,
            isOff = layer.IsOff,
            isInUse = !layer.IsErased
        });
    }
}

return new
{
    totalLayers = allLayers.Count,
    allowedPrefixes = allowed,
    violationCount = violations.Count,
    violations,
    note = "Standard exclusions: '0' and 'Defpoints' are always allowed."
};
```

## Usage Notes
- Define the firm/project layer prefix standard once and reuse (e.g. NCS uses `C-` for civil, `V-` for survey, `U-` for utilities).
- This skill only checks **layer names**. To check object→layer assignment, extend the code to iterate model-space and verify each entity is on a permitted layer for its type.
- Pair with `drawing_health_check` for a comprehensive QA pass.
