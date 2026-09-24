---
name: surface_cut_fill_report
category: surfaces
description: Full cut/fill statistics between two surfaces, including shrink/swell-adjusted volumes and factors
requires_write: false
parameters:
  - name: baseSurface
    type: string
    required: true
  - name: comparisonSurface
    type: string
    required: true
---

## Code Template

```csharp
string baseSurfaceName = "BASE_SURFACE_NAME";
string comparisonSurfaceName = "COMPARISON_SURFACE_NAME";

TinSurface baseSurf = null, compSurf = null;
foreach (ObjectId id in CivilDoc.GetSurfaceIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForRead) as TinSurface;
    if (s == null) continue;
    if (s.Name.Equals(baseSurfaceName, StringComparison.OrdinalIgnoreCase)) baseSurf = s;
    if (s.Name.Equals(comparisonSurfaceName, StringComparison.OrdinalIgnoreCase)) compSurf = s;
}
if (baseSurf == null) return new { error = "Base surface not found" };
if (compSurf == null) return new { error = "Comparison surface not found" };

// No direct surface-to-surface volume call exists — build a scratch TinVolumeSurface,
// read its properties, then erase it so nothing persists in the drawing.
var scratchName = $"~volscratch-{Guid.NewGuid():N}";
var volSurfId = TinVolumeSurface.Create(scratchName, baseSurf.ObjectId, compSurf.ObjectId);
var volSurf = Transaction.GetObject(volSurfId, OpenMode.ForWrite) as TinVolumeSurface;
var vol = volSurf.GetVolumeProperties();
double net = vol.UnadjustedCutVolume - vol.UnadjustedFillVolume;
volSurf.Erase();

return new
{
    baseSurface = baseSurf.Name,
    comparisonSurface = compSurf.Name,
    cutVolume = Math.Round(vol.UnadjustedCutVolume, 1),
    fillVolume = Math.Round(vol.UnadjustedFillVolume, 1),
    netVolume = Math.Round(net, 1),
    netDirection = net > 0 ? "Net Cut" : net < 0 ? "Net Fill" : "Balanced",
    cutFactor = vol.CutFactor,
    fillFactor = vol.FillFactor,
    adjustedCutVolume = Math.Round(vol.AdjustedCutVolume, 1),
    adjustedFillVolume = Math.Round(vol.AdjustedFillVolume, 1),
    adjustedNetVolume = Math.Round(vol.AdjustedNetVolume, 1)
};
```

## Usage Notes
- This is `surface_volume` plus the surface's configured shrink/swell (`CutFactor`/`FillFactor`) applied — `adjustedFillVolume`/`adjustedCutVolume` already have those factors baked in, `cutVolume`/`fillVolume` are the raw ("unadjusted") numbers.
- `CutFactor`/`FillFactor` default to 1.0 unless set on the base surface's volume calculation settings — if adjusted and unadjusted volumes come back identical, no shrink/swell factor has been configured for this surface pair.
- `VolumeSurfaceProperties` has no cut/fill area breakdown — for area figures, use the volume surface's cut/fill analysis (contour extraction) directly.
