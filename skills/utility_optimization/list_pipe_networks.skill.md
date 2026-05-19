---
name: list_pipe_networks
category: utility_optimization
description: List all pipe networks in the drawing with pipe and structure counts
requires_write: false
parameters: []
---

## Code Template

```csharp
var networks = new List<object>();

foreach (ObjectId netId in CivilDoc.GetPipeNetworkIds())
{
    var network = Transaction.GetObject(netId, OpenMode.ForRead) as Network;
    if (network == null) continue;

    int pipeCount = network.GetPipeIds().Count;
    int structureCount = network.GetStructureIds().Count;

    networks.Add(new
    {
        name = network.Name,
        handle = network.Handle.ToString(),
        partsListName = network.PartsListName,
        pipeCount,
        structureCount
    });
}

return new { count = networks.Count, networks };
```

## Usage Notes
- Pipe networks are gravity networks (storm, sanitary). Pressure networks use a different API.
- The `name` field is what the engineer uses in conversation (e.g. "the storm network").
- Combine with `export_pipe_network` to get the full pipe-level data.
