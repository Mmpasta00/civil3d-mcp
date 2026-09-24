---
name: create_layout_from_template
category: drawing
description: Create a new paper-space layout by copying a title-block layout from a DWT/DWG template file
requires_write: true
parameters:
  - name: templatePath
    type: string
    required: true
    description: Absolute path to the .dwt/.dwg containing the title-block layout
  - name: templateLayoutName
    type: string
    required: true
    description: Name of the layout inside the template to copy
  - name: newLayoutName
    type: string
    required: true
---

## Code Template

```csharp
string templatePath = @"C:\Templates\FirmTitleBlock.dwt";
string templateLayoutName = "24x36 Title Block";
string newLayoutName = "C-101";

if (!System.IO.File.Exists(templatePath))
    return new { error = $"Template file not found: {templatePath}" };

var sideDb = new Database(false, true);
sideDb.ReadDwgFile(templatePath, FileOpenMode.OpenForReadAndAllShare, true, null);

using (var sideTr = sideDb.TransactionManager.StartTransaction())
{
    var layoutDict = sideTr.GetObject(sideDb.LayoutDictionaryId, OpenMode.ForRead) as DBDictionary;
    if (!layoutDict.Contains(templateLayoutName))
    {
        sideDb.Dispose();
        return new { error = $"Layout '{templateLayoutName}' not found in template" };
    }

    var idPairs = new ObjectIdCollection { layoutDict.GetAt(templateLayoutName) };
    var mapping = new IdMapping();
    Database.WblockCloneObjects(idPairs, Database.LayoutDictionaryId, mapping, DuplicateRecordCloning.Replace, false);

    sideTr.Commit();
}
sideDb.Dispose();

var layoutManager = LayoutManager.Current;
var finalLayoutDict = Transaction.GetObject(Database.LayoutDictionaryId, OpenMode.ForRead) as DBDictionary;
bool cloned = finalLayoutDict.Contains(templateLayoutName);

if (cloned && !templateLayoutName.Equals(newLayoutName, StringComparison.Ordinal))
{
    layoutManager.RenameLayout(templateLayoutName, newLayoutName);
}

return new { success = cloned, newLayoutName, sourceTemplate = templatePath };
```

## Usage Notes
- Uses `WblockCloneObjects` (a side database read-only clone) rather than `INSERT`-based layout copy — the standard, non-destructive way to pull one layout (with its title block, viewports, and referenced styles) from a template into the active drawing.
- If a layout with the target name already exists, `RenameLayout` throws — check `list_layouts_and_viewports` first and pick a unique sheet name.
- Follow with `attach_xref` (to bring in the model), `set_title_block_attributes`, and `set_viewport_scale_and_center` to finish the sheet.
