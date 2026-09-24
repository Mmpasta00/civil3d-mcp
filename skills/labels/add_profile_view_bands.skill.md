---
name: add_profile_view_bands
category: labels
description: Add a data band (elevation, station, or profile grade band) to an existing profile view
requires_write: true
parameters:
  - name: profileViewHandle
    type: string
    required: true
  - name: bandSetStyleName
    type: string
    required: true
    description: Name of an existing band set style in the drawing
  - name: location
    type: string
    required: false
    description: "Top or Bottom (default Bottom)"
---

## Code Template

```csharp
string handleStr = "PROFILE_VIEW_HANDLE";
string bandSetStyleName = "BAND_SET_STYLE_NAME";
string location = "Bottom"; // Top | Bottom — informational only, see notes below

long handleValue = Convert.ToInt64(handleStr, 16);
if (!Database.TryGetObjectId(new Handle(handleValue), out ObjectId pvId))
    return new { error = "Profile view handle not found" };

var profileView = Transaction.GetObject(pvId, OpenMode.ForWrite) as ProfileView;
if (profileView == null) return new { error = "Object is not a profile view" };

ObjectId bandSetStyleId = ObjectId.Null;
foreach (ObjectId id in CivilDoc.Styles.ProfileViewBandSetStyles)
{
    var style = Transaction.GetObject(id, OpenMode.ForRead);
    var nameProp = style.GetType().GetProperty("Name");
    if (nameProp != null && (string)nameProp.GetValue(style) == bandSetStyleName) { bandSetStyleId = id; break; }
}
if (bandSetStyleId.IsNull) return new { error = $"Band set style '{bandSetStyleName}' not found" };

profileView.Bands.ImportBandSetStyle(bandSetStyleId);

return new { success = true, profileViewHandle = handleStr, bandSetStyleName, location };
```

## Usage Notes
- Band set styles bundle multiple bands (elevation, station, grade) into one reusable stack — build the style once in the drawing template, then apply it everywhere with this skill.
- `GraphBandSet.ImportBandSetStyle(ObjectId)` imports the whole style as defined (its own top/bottom band placement); there is no separate top/bottom override argument on the API — `location` is returned for the caller's reference only. To control top vs. bottom placement directly, use `profileView.Bands.GetTopBandItems()`/`SetBottomBandItems()` to move individual `ProfileViewBandItem`s after import.
- Band set styles live at `CivilDoc.Styles.ProfileViewBandSetStyles` (top-level under `Styles`, not nested under `LabelSetStyles`).
