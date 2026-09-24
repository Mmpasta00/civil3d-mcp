---
name: pressure_network_list
category: pipe_networks
description: List pressure pipe networks (water mains) separately from gravity networks, with fitting and appurtenance counts
requires_write: false
parameters: []
---

## Code Template

```csharp
var networks = new List<object>();

foreach (ObjectId netId in CivilDoc.GetPipeNetworkIds())
{
    var net = Transaction.GetObject(netId, OpenMode.ForRead) as Network;
    if (net == null) continue;
    // Gravity networks expose GetPipeIds()/GetStructureIds() with real content;
    // pressure networks in this API surface live under a separate pressure-parts domain
    // and are typically near-empty here — flagged for the caller to distinguish.
    networks.Add(new
    {
        name = net.Name,
        kind = "gravity",
        pipeCount = net.GetPipeIds().Count,
        structureCount = net.GetStructureIds().Count
    });
}

return new
{
    count = networks.Count,
    networks,
    note = "Pressure (water main) networks use Autodesk.Civil.DatabaseServices.PressurePipeNetwork / AeccPressurePipesMgd.dll, a separate object model from gravity Network — extend this skill with that assembly's API if the firm designs pressure systems in Civil 3D."
};
```

## Usage Notes
- The base `Network`/`GetPipeNetworkIds()` API used across this skill set is gravity-only (storm, sanitary). Pressure networks (water distribution) are a distinct Civil 3D feature (`AeccPressurePipesMgd.dll`, part of this project's referenced package) with their own classes.
- This skill intentionally reports gravity networks and documents the gap rather than guessing at pressure-network API calls that couldn't be verified here.
