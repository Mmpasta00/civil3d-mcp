---
name: add_structure_and_pipe
category: pipe_networks
description: Add two structures and the pipe connecting them to an existing gravity network, using named part families/sizes
requires_write: true
parameters:
  - name: networkName
    type: string
    required: true
  - name: upstreamPoint
    type: array
    required: true
    description: "{x, y, rimElevation} for the upstream structure"
  - name: downstreamPoint
    type: array
    required: true
    description: "{x, y, rimElevation} for the downstream structure"
  - name: structurePartFamily
    type: string
    required: true
    description: Structure part family description from this network's parts list (see list_parts_lists)
  - name: pipePartFamily
    type: string
    required: true
    description: Pipe part family description from this network's parts list
---

## Code Template

```csharp
string networkName = "NETWORK_NAME";
string structureFamilyName = "STRUCTURE_FAMILY_NAME";
string pipeFamilyName = "PIPE_FAMILY_NAME";
double usX = 1000, usY = 2000, usRim = 105.0;
double dsX = 1100, dsY = 2000, dsRim = 103.0;

Network net = null;
foreach (ObjectId id in CivilDoc.GetPipeNetworkIds())
{
    var n = Transaction.GetObject(id, OpenMode.ForWrite) as Network;
    if (n != null && n.Name.Equals(networkName, StringComparison.OrdinalIgnoreCase)) { net = n; break; }
}
if (net == null) return new { error = "Network not found" };
if (net.PartsListId.IsNull) return new { error = "Network has no parts list assigned — set one first" };

var partsList = Transaction.GetObject(net.PartsListId, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.PartsList;

ObjectId FindFamily(DomainType domain, string familyName)
{
    foreach (ObjectId famId in partsList.GetPartFamilyIdsByDomain(domain))
    {
        var fam = Transaction.GetObject(famId, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.PartFamily;
        if (fam != null && fam.Description.Equals(familyName, StringComparison.OrdinalIgnoreCase)) return famId;
    }
    return ObjectId.Null;
}

var structFamilyId = FindFamily(DomainType.Structure, structureFamilyName);
var pipeFamilyId = FindFamily(DomainType.Pipe, pipeFamilyName);
if (structFamilyId.IsNull) return new { error = $"Structure family '{structureFamilyName}' not found in this network's parts list" };
if (pipeFamilyId.IsNull) return new { error = $"Pipe family '{pipeFamilyName}' not found in this network's parts list" };

ObjectId usStructId = ObjectId.Null, dsStructId = ObjectId.Null;
net.AddStructure(structFamilyId, ObjectId.Null, new Point3d(usX, usY, usRim), 0.0, ref usStructId, true);
net.AddStructure(structFamilyId, ObjectId.Null, new Point3d(dsX, dsY, dsRim), 0.0, ref dsStructId, true);

ObjectId newPipeId = ObjectId.Null;
var line = new LineSegment3d(new Point3d(usX, usY, usRim), new Point3d(dsX, dsY, dsRim));
net.AddLinePipe(pipeFamilyId, ObjectId.Null, line, ref newPipeId, true);

return new
{
    success = true,
    networkName = net.Name,
    upstreamStructure = (Transaction.GetObject(usStructId, OpenMode.ForRead) as Structure)?.Name,
    downstreamStructure = (Transaction.GetObject(dsStructId, OpenMode.ForRead) as Structure)?.Name,
    pipe = (Transaction.GetObject(newPipeId, OpenMode.ForRead) as Pipe)?.Name
};
```

## Usage Notes
- Passing `ObjectId.Null` for the size id and `applyRules: true` lets Civil 3D auto-select a default size and apply the network's design rules — resize immediately after with `resize_pipe` if a specific size is required.
- Family lookup matches on `PartFamily.Description` (its display name in the parts list editor) — **fixed 2026-09-15**: this previously used reflection to read a `Name` property that `Autodesk.Civil.DatabaseServices.Styles.PartFamily` does not have (confirmed by decompiling `AeccDbMgd.dll`; the class extends `DBObject` and exposes only `Description`, `GUID`, `Domain`, `PartType`, etc.), so the old lookup silently matched nothing and always returned "family not found" at runtime despite compiling clean. `Description` is the correct, verified member — see `build_pipe_network_from_json.skill.md` for the same fix applied to a from-scratch network build.
- Rim elevations passed here become the pipe endpoint elevations too (a simplified straight connection) — real designs typically set inverts explicitly afterward via `pipe.StartPoint`/structure sump depth.
