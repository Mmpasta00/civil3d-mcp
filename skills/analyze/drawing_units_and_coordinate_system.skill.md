---
name: drawing_units_and_coordinate_system
category: analyze
description: Report the drawing's linear/angular units, coordinate system code, and insertion scale — the first thing to check before trusting any coordinate math
requires_write: false
parameters: []
---

## Code Template

```csharp
var settings = CivilDoc.Settings.DrawingSettings;
var unitZone = settings.UnitZoneSettings;

return new
{
    drawingUnits = unitZone.DrawingUnits.ToString(),
    angularUnits = unitZone.AngularUnits.ToString(),
    coordinateSystemCode = unitZone.CoordinateSystemCode,
    insertionUnits = Database.Insunits.ToString(),
    measurement = Database.Measurement.ToString(),
    lunits = Database.Lunits,
    luprec = Database.Luprec,
    note = "If coordinateSystemCode is blank, no state-plane/projected CRS is assigned — LandXML imports and USGS elevation lookups may need a manual CRS assignment first."
};
```

## Usage Notes
- Run this before any skill that assumes feet vs. meters (most templates in this library assume feet) — rescale results if `drawingUnits` comes back Meter.
- A blank `coordinateSystemCode` is common on drawings that were never assigned a projected coordinate system — flag it to the engineer before trusting absolute lat/long conversions.
