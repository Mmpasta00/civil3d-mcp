---
name: surface_volume
category: surfaces
description: Compute cut/fill volumes between two TIN surfaces
requires_write: false
parameters:
  - name: baseSurface
    type: string
    required: true
    description: Name of the base (existing ground) surface
  - name: comparisonSurface
    type: string
    required: true
    description: Name of the comparison (proposed) surface
---

## Code Template

```csharp
TinSurface baseSurf = null, compSurf = null;

foreach (ObjectId id in CivilDoc.GetSurfaceIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForRead);
    if (s is TinSurface tin)
    {
        if (tin.Name.Equals("BASE_SURFACE_NAME", StringComparison.OrdinalIgnoreCase))
            baseSurf = tin;
        if (tin.Name.Equals("COMPARISON_SURFACE_NAME", StringComparison.OrdinalIgnoreCase))
            compSurf = tin;
    }
}

if (baseSurf == null) return new { error = "Base surface not found" };
if (compSurf == null) return new { error = "Comparison surface not found" };

// There is no direct surface-to-surface GetVolumeProperties(otherSurface) call — volumes are
// computed via a TinVolumeSurface object. Create one as a scratch object, read its properties,
// then erase it in the same transaction so nothing persists in the drawing.
var scratchName = $"~volscratch-{Guid.NewGuid():N}";
var volSurfId = TinVolumeSurface.Create(scratchName, baseSurf.ObjectId, compSurf.ObjectId);
var volSurf = Transaction.GetObject(volSurfId, OpenMode.ForWrite) as TinVolumeSurface;
var volumeProps = volSurf.GetVolumeProperties();
double cutVol = volumeProps.UnadjustedCutVolume;
double fillVol = volumeProps.UnadjustedFillVolume;
volSurf.Erase();

return new {
    baseSurface = baseSurf.Name,
    comparisonSurface = compSurf.Name,
    cutVolume = cutVol,
    fillVolume = fillVol,
    netVolume = cutVol - fillVol
};
```

## Usage Notes
- Both surfaces must be TIN surfaces
- Net volume positive = more cut than fill
- Surfaces must overlap for meaningful results
- Units depend on drawing settings (typically m³ or ft³)
- Internally builds a scratch `TinVolumeSurface` (Civil 3D's only volume-computation object) and erases it before returning — nothing extra is left in the drawing. For a volume surface that STAYS in the drawing (e.g. for a color-banded cut/fill plot), use `create_volume_surface` instead.
- `VolumeSurfaceProperties` only exposes cut/fill/net volumes (plus adjusted variants and shrink/swell factors) — no cut/fill area breakdown is available at this level; for area figures, extract elevation-banded contours from the volume surface's cut/fill analysis instead.
