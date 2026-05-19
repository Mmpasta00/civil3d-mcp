---
name: rational_peak_flow
category: hydrology
description: Compute peak runoff Q using the Rational Method (Q = C × i × A) for a list of catchment areas. Pure calculation — does not read the drawing.
requires_write: false
parameters:
  - name: catchments
    type: string
    required: true
    description: JSON array of catchments. Each item needs {name, area_ac, c, intensity_in_per_hr}. Example "[{\"name\":\"Sub-1\",\"area_ac\":2.3,\"c\":0.85,\"intensity_in_per_hr\":4.2}]"
---

## Code Template

```csharp
using System.Text.Json;

string catchmentsJson = "CATCHMENTS_JSON";

using var doc = JsonDocument.Parse(catchmentsJson);
var results = new List<object>();
double totalQ = 0;
double totalArea = 0;

foreach (var item in doc.RootElement.EnumerateArray())
{
    string name = item.GetProperty("name").GetString() ?? "?";
    double area = item.GetProperty("area_ac").GetDouble();
    double c = item.GetProperty("c").GetDouble();
    double i = item.GetProperty("intensity_in_per_hr").GetDouble();

    // Rational Method: Q (cfs) = C × i × A   (with C unitless, i in in/hr, A in acres → Q in cfs because 1.008 ≈ 1)
    double q = c * i * area;

    results.Add(new
    {
        name,
        area_ac = area,
        runoffCoefficient = c,
        intensity_in_per_hr = i,
        peakQ_cfs = Math.Round(q, 2)
    });

    totalQ += q;
    totalArea += area;
}

return new
{
    method = "Rational Method (Q = C × i × A)",
    catchments = results,
    totals = new
    {
        catchmentCount = results.Count,
        totalArea_ac = Math.Round(totalArea, 2),
        sumPeakQ_cfs = Math.Round(totalQ, 2)
    },
    notes = "Q is summed assuming concurrent peaks — for realistic design use the Modified Rational Method when catchments have different Tc values."
};
```

## Usage Notes
- C values (runoff coefficient) typical ranges: 0.15–0.25 lawns, 0.50–0.70 single-family residential, 0.70–0.95 commercial/paved.
- Composite C = Σ(C_i × A_i) / Σ(A_i) when a catchment has mixed cover.
- Intensity i comes from local IDF curves (NOAA Atlas 14 for US). Lookup Tc → i.
- Tc (time of concentration) = sheet flow + shallow concentrated + channel flow segments. Compute separately.
