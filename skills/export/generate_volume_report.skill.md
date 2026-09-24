---
name: generate_volume_report
category: export
description: Generate a formatted, plan-check-ready earthwork volume report as text (surfaces, cut/fill, net) for pasting into a submittal letter
requires_write: false
parameters:
  - name: existingSurfaceName
    type: string
    required: true
  - name: proposedSurfaceName
    type: string
    required: true
  - name: projectName
    type: string
    required: false
---

## Code Template

```csharp
string existingSurfaceName = "EG_SURFACE";
string proposedSurfaceName = "FG_SURFACE";
string projectName = "Project";

TinSurface eg = null, fg = null;
foreach (ObjectId id in CivilDoc.GetSurfaceIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForRead) as TinSurface;
    if (s == null) continue;
    if (s.Name.Equals(existingSurfaceName, StringComparison.OrdinalIgnoreCase)) eg = s;
    if (s.Name.Equals(proposedSurfaceName, StringComparison.OrdinalIgnoreCase)) fg = s;
}
if (eg == null) return new { error = "Existing ground surface not found" };
if (fg == null) return new { error = "Proposed surface not found" };

// No direct surface-to-surface volume call exists — build a scratch TinVolumeSurface,
// read its properties, then erase it so nothing persists in the drawing.
var scratchName = $"~volscratch-{Guid.NewGuid():N}";
var volSurfId = TinVolumeSurface.Create(scratchName, eg.ObjectId, fg.ObjectId);
var volSurf = Transaction.GetObject(volSurfId, OpenMode.ForWrite) as TinVolumeSurface;
var vol = volSurf.GetVolumeProperties();
double cutCy = vol.UnadjustedCutVolume / 27.0;
double fillCy = vol.UnadjustedFillVolume / 27.0;
double netCy = cutCy - fillCy;
volSurf.Erase();

var sb = new StringBuilder();
sb.AppendLine($"EARTHWORK VOLUME REPORT — {projectName}");
sb.AppendLine($"Existing Ground Surface: {eg.Name}");
sb.AppendLine($"Proposed (Finished Grade) Surface: {fg.Name}");
sb.AppendLine(new string('-', 50));
sb.AppendLine($"Cut Volume:   {cutCy,12:N0} CY");
sb.AppendLine($"Fill Volume:  {fillCy,12:N0} CY");
sb.AppendLine(new string('-', 50));
sb.AppendLine($"Net Volume:   {Math.Abs(netCy),12:N0} CY  ({(netCy > 0 ? "Export" : netCy < 0 ? "Import" : "Balanced")})");
sb.AppendLine();
sb.AppendLine("Note: unadjusted volumes shown — apply project shrinkage/swell factors per the geotechnical report before using for bidding.");

return new { reportText = sb.ToString(), cutVolume_cy = Math.Round(cutCy, 0), fillVolume_cy = Math.Round(fillCy, 0), netVolume_cy = Math.Round(netCy, 0) };
```

## Usage Notes
- Produces both structured numbers and a ready-to-paste text block — use the text block directly in a plan-check response letter or submittal memo.
- For the underlying numeric data alone (no formatting), use `surface_cut_fill_report` or `earthwork_between_surfaces`.
