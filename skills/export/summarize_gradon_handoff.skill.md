---
name: summarize_gradon_handoff
category: export
description: Read-only summary of a Gradon Design generative-design handoff folder — lists the files present and returns handoff.md's content
requires_write: false
parameters:
  - name: handoffFolder
    type: string
    required: true
    description: Absolute path to the handoff folder written by the Gradon Design service (e.g. "C:/projects/livermore/handoff")
---

## Code Template

```csharp
string handoffFolder = @"HANDOFF_FOLDER_HERE";

if (!System.IO.Directory.Exists(handoffFolder))
    return new { error = $"Folder not found: {handoffFolder}" };

var files = System.IO.Directory.GetFiles(handoffFolder)
    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
    .Select(f => new
    {
        name = System.IO.Path.GetFileName(f),
        sizeBytes = new System.IO.FileInfo(f).Length
    })
    .ToList();

string handoffMdPath = System.IO.Path.Combine(handoffFolder, "handoff.md");
string handoffMarkdown = null;
string handoffError = null;
if (System.IO.File.Exists(handoffMdPath))
{
    try { handoffMarkdown = System.IO.File.ReadAllText(handoffMdPath); }
    catch (System.Exception ex) { handoffError = $"Could not read handoff.md: {ex.Message}"; }
}
else
{
    handoffError = "handoff.md not found in this folder";
}

bool hasEg = files.Any(f => f.name.Equals("EG.xml", StringComparison.OrdinalIgnoreCase));
bool hasGrading = files.Any(f => f.name.Equals("grading.xml", StringComparison.OrdinalIgnoreCase));
var networkJsonFiles = files.Where(f => f.name.StartsWith("network-", StringComparison.OrdinalIgnoreCase) && f.name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).Select(f => f.name).ToList();
var networkXmlFiles = files.Where(f => f.name.StartsWith("network-", StringComparison.OrdinalIgnoreCase) && f.name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).Select(f => f.name).ToList();

return new
{
    handoffFolder,
    fileCount = files.Count,
    files,
    hasEgXml = hasEg,
    hasGradingXml = hasGrading,
    networkJsonFiles,
    networkXmlFiles,
    handoffMarkdown,
    handoffError
};
```

## Usage Notes
- Read-only — makes no changes to the drawing or the filesystem. Safe to call before deciding whether to run `import_gradon_design_surfaces` / `build_pipe_network_from_json` / `import_gradon_design`.
- `networkJsonFiles`/`networkXmlFiles` list every `network-<name>.json`/`network-<name>.xml` pair the folder holds, so a caller can see how many pipe networks the handoff contains before importing them.
- `handoffError` is set (non-fatal) if `handoff.md` is missing or unreadable — the rest of the summary (file listing) still returns.
- Uses `System.IO.Directory.GetFiles`/`System.IO.File.ReadAllText`, both permitted by the plugin sandbox (only `File.Delete`/`Directory.Delete` and process/network/registry access are blocked).
