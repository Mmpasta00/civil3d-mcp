---
name: save_as_dwg
category: drawing
description: Save the current drawing to a new path/filename (e.g. splitting a working file into a client-deliverable copy)
requires_write: true
parameters:
  - name: outputPath
    type: string
    required: true
    description: Absolute Windows path for the saved copy, e.g. "C:/projects/livermore/deliverables/Livermore-Grading-v3.dwg"
  - name: fileVersion
    type: string
    required: false
    description: "AutoCAD file version, e.g. Current, AC1032 (2018+); default Current"
---

## Code Template

```csharp
string outputPath = @"C:\projects\deliverables\output.dwg";
var version = DwgVersion.Current;

var dir = System.IO.Path.GetDirectoryName(outputPath);
if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
    System.IO.Directory.CreateDirectory(dir);

Database.SaveAs(outputPath, version);

return new { success = true, outputPath, version = version.ToString() };
```

## Usage Notes
- `SaveAs` writes a copy of the CURRENT in-memory database to the new path — it does not switch the active document's file association (the plugin keeps working against the original open drawing).
- Use `DwgVersion.Current` unless the recipient needs an older format (some jurisdictions/consultants still request AutoCAD 2013/2018 format specifically).
- Creates the destination folder if it doesn't exist yet — pairs well with `create_project_folder_structure`.
