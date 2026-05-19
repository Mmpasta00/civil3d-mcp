---
name: composite_runoff_coefficient
category: hydrology
description: Compute composite runoff coefficient C from a mix of land cover types within a catchment (weighted by area)
requires_write: false
parameters:
  - name: covers
    type: string
    required: true
    description: JSON array. Each item {type, area_ac, c}. Example "[{\"type\":\"impervious\",\"area_ac\":1.2,\"c\":0.95},{\"type\":\"lawn\",\"area_ac\":0.8,\"c\":0.20}]"
---

## Code Template

```csharp
using System.Text.Json;

string coversJson = "COVERS_JSON";

using var doc = JsonDocument.Parse(coversJson);
double totalArea = 0;
double weightedSum = 0;
var breakdown = new List<object>();

foreach (var item in doc.RootElement.EnumerateArray())
{
    string type = item.GetProperty("type").GetString() ?? "?";
    double area = item.GetProperty("area_ac").GetDouble();
    double c = item.GetProperty("c").GetDouble();

    totalArea += area;
    weightedSum += c * area;
    breakdown.Add(new { type, area_ac = area, runoffCoefficient = c, contribution = Math.Round(c * area, 3) });
}

if (totalArea <= 0) return new { error = "Total area is zero" };

double compositeC = weightedSum / totalArea;

return new
{
    method = "Composite C = Σ(C_i × A_i) / Σ(A_i)",
    totalArea_ac = Math.Round(totalArea, 3),
    compositeC = Math.Round(compositeC, 3),
    breakdown
};
```

## Usage Notes
- Use this when a single catchment contains mixed land cover (e.g. building + lawn + parking lot).
- Feed the resulting `compositeC` into `rational_peak_flow`.
- C reference values: roof/concrete 0.90–0.95, asphalt 0.85, gravel 0.50–0.70, lawn (sandy/flat) 0.10, lawn (clay/steep) 0.35, forest 0.10–0.20.
