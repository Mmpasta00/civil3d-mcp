---
name: apply_station_equation
category: alignments
description: Add a station equation (back station / ahead station jump) to an alignment, e.g. for a match-line or re-stationing
requires_write: true
parameters:
  - name: alignmentName
    type: string
    required: true
  - name: rawStationBack
    type: double
    required: true
    description: The raw (continuous) station where the equation is inserted
  - name: stationAhead
    type: double
    required: true
    description: The new station value to assign going forward from that point
  - name: equationType
    type: string
    required: false
    description: "Increasing, Decreasing, or Continuous (default Increasing)"
---

## Code Template

```csharp
Alignment alignment = null;
foreach (ObjectId id in CivilDoc.GetAlignmentIds())
{
    var a = Transaction.GetObject(id, OpenMode.ForWrite) as Alignment;
    if (a != null && a.Name.Equals("ALIGNMENT_NAME", StringComparison.OrdinalIgnoreCase))
    { alignment = a; break; }
}
if (alignment == null) return new { error = "Alignment not found" };

double rawStationBack = 1000.0; // Replace
double stationAhead = 1100.0;   // Replace
var equationType = StationEquationType.Increasing; // Increasing station equation

var equation = alignment.StationEquations.Add(rawStationBack, stationAhead, equationType);

return new
{
    alignmentName = alignment.Name,
    rawStationBack = equation.RawStationBack,
    stationBack = equation.StationBack,
    stationAhead = equation.StationAhead,
    equationType = equation.EquationType.ToString(),
    totalEquations = alignment.StationEquations.Count
};
```

## Usage Notes
- `StationEquationType` options include increasing (`EqEqualsGreater`), decreasing (`EqEqualsLess`), and equal-value equations — check the enum for the exact member names in your Civil 3D version.
- Station equations affect every downstream station-based query (profiles, labels, sample lines) — regenerate those after adding one.
