---
name: earthwork_between_surfaces
category: analyze
description: Full earthwork summary between existing and proposed surfaces, with shrink/swell-adjusted volumes
requires_write: false
parameters:
  - name: existingSurfaceName
    type: string
    required: true
  - name: proposedSurfaceName
    type: string
    required: true
  - name: shrinkageFactor
    type: number
    required: false
    description: Multiplier applied to fill volume to account for compaction (e.g. 1.15 = 15% shrinkage); default 1.0
---

## Code Template

```csharp
string existingSurfaceName = "EG_SURFACE";
string proposedSurfaceName = "FG_SURFACE";
double shrinkageFactor = 1.0;

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
double rawCut = vol.UnadjustedCutVolume;
double rawFill = vol.UnadjustedFillVolume;
double adjustedFill = rawFill * shrinkageFactor;
double net = rawCut - adjustedFill;
volSurf.Erase();

return new
{
    existingSurface = eg.Name,
    proposedSurface = fg.Name,
    rawCutVolume_cy = Math.Round(rawCut / 27.0, 1),
    rawFillVolume_cy = Math.Round(rawFill / 27.0, 1),
    shrinkageFactor,
    adjustedFillVolume_cy = Math.Round(adjustedFill / 27.0, 1),
    netVolume_cy = Math.Round(net / 27.0, 1),
    netDirection = net > 0 ? "Export (net cut)" : net < 0 ? "Import (net fill)" : "Balanced"
};
```

## Usage Notes
- Assumes surface units are feet (volumes divided by 27 to convert cubic feet to cubic yards) — adjust if the drawing uses metric units.
- Shrinkage/swell factors are project- and soil-specific; confirm with the geotechnical report before using this for a bid-level earthwork estimate.
- For a section-by-section breakdown instead of one drawing-wide number, use `section_volumes`.
