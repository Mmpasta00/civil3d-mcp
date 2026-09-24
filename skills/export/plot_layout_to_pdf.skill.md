---
name: plot_layout_to_pdf
category: export
description: Plot a paper-space layout to PDF using its existing page setup
requires_write: false
parameters:
  - name: layoutName
    type: string
    required: true
  - name: outputPdfPath
    type: string
    required: true
    description: Absolute Windows path for the output PDF
---

## Code Template

```csharp
string layoutName = "LAYOUT_NAME";
string outputPdfPath = @"C:\projects\export\sheet.pdf";

var layoutDict = Transaction.GetObject(Database.LayoutDictionaryId, OpenMode.ForRead) as DBDictionary;
if (!layoutDict.Contains(layoutName))
    return new { error = $"Layout '{layoutName}' not found" };

// Plotting to file is driven through the command line here since it touches the plot dialog /
// PlotEngine state machine, which is awkward to script reliably through the managed API alone.
Editor.Command(
    "_-PLOT",
    "_Y",              // yes, plot from a named layout (or "_N" for current tab depending on state)
    layoutName,
    "DWG To PDF.pc3",
    "",                // paper size — blank uses the layout's saved page setup
    "_M",              // millimeters/inches per page setup default
    "_L",              // landscape/portrait per page setup default
    "_N",              // plot upside down? no
    "_E",              // plot area: extents (or "_L" for layout, "_W" for window)
    "1:1",             // plot scale
    "_C",              // center the plot
    "_Y",              // plot with plot styles
    "_N",              // plot paperspace last? n/a for single sheet
    "_Y",              // save changes to page setup? yes
    outputPdfPath,
    "_Y"               // proceed / overwrite existing file
);

bool fileExists = System.IO.File.Exists(outputPdfPath);

return new
{
    success = fileExists,
    layoutName,
    outputPdfPath,
    warning = !fileExists ? "Plot command ran but the PDF was not found at the expected path — the -PLOT prompt sequence is highly configuration-dependent; verify interactively once for this drawing's page setups and adjust the argument list." : null
};
```

## Usage Notes
- **Command-based** and the most configuration-sensitive skill in this library — `-PLOT`'s prompt sequence depends on the layout's existing page setup, installed plotters/PC3 files, and whether "DWG To PDF.pc3" is registered on the work PC.
- For batch plotting many sheets, Civil 3D's Publish tool (`_PUBLISH`) with a DSD file is far more reliable than scripting `-PLOT` per sheet — consider that route if this needs to run unattended over dozens of layouts.
- Always verify the exact prompt sequence interactively once per drawing template before relying on this in production.
