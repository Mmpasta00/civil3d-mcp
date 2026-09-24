---
name: set_title_block_attributes
category: drawing
description: Set attribute values (sheet title, project number, date, drawn-by, etc.) on a title-block block reference in a layout
requires_write: true
parameters:
  - name: layoutName
    type: string
    required: true
  - name: attributeValues
    type: array
    required: true
    description: Array of {tag, value} pairs matching the title block's attribute tags
---

## Code Template

```csharp
string layoutName = "LAYOUT_NAME";
var attributeValues = new (string tag, string value)[]
{
    ("SHEET_TITLE", "GRADING PLAN"),
    ("PROJECT_NO", "2026-014"),
    ("DATE", DateTime.Now.ToString("MM/dd/yyyy"))
};

var layoutDict = Transaction.GetObject(Database.LayoutDictionaryId, OpenMode.ForRead) as DBDictionary;
if (!layoutDict.Contains(layoutName)) return new { error = $"Layout '{layoutName}' not found" };

var layout = Transaction.GetObject(layoutDict.GetAt(layoutName), OpenMode.ForRead) as Layout;
var layoutBtr = Transaction.GetObject(layout.BlockTableRecordId, OpenMode.ForRead) as BlockTableRecord;

var updated = new List<string>();
var notFound = new List<string>(attributeValues.Select(a => a.tag));

foreach (ObjectId entId in layoutBtr)
{
    var blockRef = Transaction.GetObject(entId, OpenMode.ForRead) as BlockReference;
    if (blockRef == null) continue;

    foreach (ObjectId attId in blockRef.AttributeCollection)
    {
        var attRef = Transaction.GetObject(attId, OpenMode.ForWrite) as AttributeReference;
        if (attRef == null) continue;

        var match = attributeValues.FirstOrDefault(a => a.tag.Equals(attRef.Tag, StringComparison.OrdinalIgnoreCase));
        if (match.tag != null)
        {
            attRef.TextString = match.value;
            updated.Add(match.tag);
            notFound.Remove(match.tag);
        }
    }
}

return new { success = true, layoutName, updatedTags = updated.Distinct(), tagsNotFound = notFound };
```

## Usage Notes
- Scans every block reference placed directly in the layout's paper space for attribute tags — works regardless of which block is the "title block" as long as tags are unique across the sheet.
- Tag names must match exactly (case-insensitive) what was defined when the title block was created — use a text editor or `ATTDEF` list on the template once to confirm tag names.
- If two blocks in the layout share an attribute tag name, both get updated — narrow the outer loop to a specific block name if that's a problem for a given title block family.
