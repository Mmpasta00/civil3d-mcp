---
name: create_pipe_network_table
category: pipe_networks
description: Create a pipe or structure schedule table (annotative table entity) for a network, placed at a point in the drawing
requires_write: true
parameters:
  - name: networkName
    type: string
    required: true
  - name: tableKind
    type: string
    required: true
    description: "pipe or structure"
  - name: insertX
    type: double
    required: true
  - name: insertY
    type: double
    required: true
---

## Code Template

```csharp
string networkName = "NETWORK_NAME";
string tableKind = "pipe"; // pipe | structure
double insertX = 0.0, insertY = 0.0; // Replace — pick empty drawing space for the table

Network net = null;
foreach (ObjectId id in CivilDoc.GetPipeNetworkIds())
{
    var n = Transaction.GetObject(id, OpenMode.ForRead) as Network;
    if (n != null && n.Name.Equals(networkName, StringComparison.OrdinalIgnoreCase)) { net = n; break; }
}
if (net == null) return new { error = "Network not found" };

// Civil 3D's managed API does not expose a direct PipeTable/StructureTable.Create factory in this
// package version — table creation is driven through the command line, which still runs inside
// this same transaction/document context.
string command = tableKind.Equals("structure", StringComparison.OrdinalIgnoreCase)
    ? "_AeccCreateStructureTable"
    : "_AeccCreatePipeTable";

Editor.Command(command, "_A", networkName, "", new Point3d(insertX, insertY, 0));

return new
{
    success = true,
    networkName = net.Name,
    tableKind,
    note = "Table created via command context — its ObjectId is not returned by this call. Use list_layouts_and_viewports or select_objects_in_window near the insertion point to confirm."
};
```

## Usage Notes
- **Command-based**: this operation has no confirmed direct managed-API factory in the referenced package version, so it runs through `Editor.Command` inside the script's command context, per the plugin's documented fallback pattern.
- Command name, prompt sequence, and required "select all in network" input (`"_A"`) can vary by Civil 3D language/version — verify interactively once (`AECCCREATEPIPETABLE` / `AECCCREATESTRUCTURETABLE` on the command line) and adjust the string sequence to match if it doesn't run cleanly.
- For a scriptable, guaranteed-reliable alternative, use `export_pipe_network` / `pipe_slope_and_cover_report` to get the same data as JSON and format the schedule outside Civil 3D.
