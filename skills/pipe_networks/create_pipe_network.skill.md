---
name: create_pipe_network
category: pipe_networks
description: Create a new empty gravity pipe network, ready for add_structure_and_pipe to populate
requires_write: true
parameters:
  - name: networkName
    type: string
    required: true
  - name: partsListName
    type: string
    required: false
    description: Name of an existing parts list to assign (e.g. "Storm Sewer Parts")
---

## Code Template

```csharp
string networkName = "NETWORK_NAME_HERE";
string partsListName = "";

var nameRef = networkName;
var networkId = Network.Create(CivilDoc, ref nameRef);
var network = Transaction.GetObject(networkId, OpenMode.ForWrite) as Network;

if (!string.IsNullOrWhiteSpace(partsListName))
{
    try { network.PartsListName = partsListName; }
    catch (System.Exception ex) { return new { error = $"Network created but parts list assignment failed: {ex.Message}" }; }
}

return new
{
    success = true,
    name = network.Name,
    handle = network.Handle.ToString(),
    partsListName = network.PartsListName
};
```

## Usage Notes
- `Network.Create` takes the desired name by `ref` — Civil 3D may append a suffix if the name collides, so read back `network.Name` rather than assuming the exact requested name.
- Assign a parts list before adding structures/pipes with `add_structure_and_pipe`, since part sizes are resolved from the network's parts list.
- Follow with `add_structure_and_pipe` to actually build the network geometry.
