---
name: set_cogo_point_udp
category: points
description: Set a User-Defined Property (UDP) value on one or more COGO points by point number
requires_write: true
parameters:
  - name: pointNumbers
    type: string
    required: true
    description: Comma-separated point numbers, e.g. "101,102,103"
  - name: udpName
    type: string
    required: true
    description: Name of an existing text/string UDP defined for points in this drawing
  - name: value
    type: string
    required: true
---

## Code Template

```csharp
string pointNumbersCsv = "101,102,103";
string udpName = "UDP_NAME_HERE";
string value = "VALUE_HERE";

UDP targetUdp = null;
foreach (UDP udp in CivilDoc.PointUDPs)
{
    if (udp.Name.Equals(udpName, StringComparison.OrdinalIgnoreCase)) { targetUdp = udp; break; }
}
if (targetUdp == null) return new { error = $"UDP '{udpName}' not found on points" };
if (!(targetUdp is UDPString stringUdp))
    return new { error = $"UDP '{udpName}' is not a text UDP — use the matching numeric/boolean setter instead" };

var pointNumbers = pointNumbersCsv.Split(',').Select(s => uint.Parse(s.Trim())).ToList();
var updated = new List<uint>();

foreach (ObjectId id in CivilDoc.CogoPoints)
{
    var pt = Transaction.GetObject(id, OpenMode.ForWrite) as CogoPoint;
    if (pt == null || !pointNumbers.Contains(pt.PointNumber)) continue;
    pt.SetUDPValue(stringUdp, value);
    updated.Add(pt.PointNumber);
}

return new { success = true, udpName, value, updatedPoints = updated };
```

## Usage Notes
- The UDP must already be defined for points in this drawing (Toolspace > Points > right-click > Manage Point Groups... or drawing settings). This skill sets values, not definitions.
- For numeric/boolean UDPs, swap the cast to `UDPDouble`/`UDPInteger`/`UDPBoolean` and call the matching `SetUDPValue` overload.
