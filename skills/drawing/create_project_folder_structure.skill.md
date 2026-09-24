---
name: create_project_folder_structure
category: drawing
description: Create a firm-standard folder tree on disk for a new project (drawings, survey, correspondence, etc.) from a JSON folder spec
requires_write: true
parameters:
  - name: rootPath
    type: string
    required: true
    description: Absolute Windows path for the new project root, e.g. "C:/Projects/2026-Fremont-Site"
  - name: folderTreeJson
    type: string
    required: true
    description: "JSON array of relative subfolder paths, e.g. [\"01-Drawings\",\"02-Survey\",\"03-Correspondence\",\"04-Calcs\"]"
---

## Code Template

```csharp
using System.Text.Json;

string rootPath = @"C:\Projects\NEW_PROJECT_NAME";
string folderTreeJson = "[\"01-Drawings\",\"02-Survey\",\"03-Correspondence\",\"04-Calcs\",\"05-Permits\"]";

var created = new List<string>();

if (!System.IO.Directory.Exists(rootPath))
{
    System.IO.Directory.CreateDirectory(rootPath);
    created.Add(rootPath);
}

var doc = JsonDocument.Parse(folderTreeJson);
foreach (var item in doc.RootElement.EnumerateArray())
{
    string relPath = item.GetString();
    if (string.IsNullOrWhiteSpace(relPath)) continue;
    string fullPath = System.IO.Path.Combine(rootPath, relPath);
    if (!System.IO.Directory.Exists(fullPath))
    {
        System.IO.Directory.CreateDirectory(fullPath);
        created.Add(fullPath);
    }
}

return new { success = true, rootPath, foldersCreated = created.Count, folders = created };
```

## Usage Notes
- `System.IO.Directory.CreateDirectory` and `System.IO.File.WriteAllText` are permitted by the plugin's sandbox (only `Delete` operations and process/network/registry access are blocked) — this is the one skill in the library that touches the filesystem directly rather than the drawing database.
- Idempotent: re-running with the same tree only creates what's missing.
- Pair with `create_layout_from_template` and `attach_xref` for the full "new project setup" flow described in the plugin's demo script.
