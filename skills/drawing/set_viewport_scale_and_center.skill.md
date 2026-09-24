---
name: set_viewport_scale_and_center
category: drawing
description: Set a paper-space viewport's scale and pan it to center on a model-space point
requires_write: true
parameters:
  - name: layoutName
    type: string
    required: true
  - name: viewportHandle
    type: string
    required: true
  - name: scale
    type: number
    required: true
    description: Plot scale as a factor (e.g. 1/48.0 for 1"=48')
  - name: centerX
    type: double
    required: true
  - name: centerY
    type: double
    required: true
---

## Code Template

```csharp
string layoutName = "LAYOUT_NAME";
string viewportHandleStr = "VIEWPORT_HANDLE";
double scale = 1.0 / 48.0; // 1" = 48'
double centerX = 1000.0, centerY = 2000.0;

long handleValue = Convert.ToInt64(viewportHandleStr, 16);
if (!Database.TryGetObjectId(new Handle(handleValue), out ObjectId vpId))
    return new { error = "Viewport handle not found" };

var viewport = Transaction.GetObject(vpId, OpenMode.ForWrite) as Viewport;
if (viewport == null) return new { error = "Object is not a viewport" };

bool wasOn = viewport.On;
viewport.On = true; // must be on to accept view changes
viewport.CustomScale = scale;
viewport.ViewCenter = new Point2d(centerX, centerY);
viewport.On = wasOn;

return new
{
    success = true,
    layoutName,
    viewportHandle = viewportHandleStr,
    newScale = viewport.CustomScale,
    newCenter = new { x = viewport.ViewCenter.X, y = viewport.ViewCenter.Y }
};
```

## Usage Notes
- `Viewport.ViewCenter` is in MODEL space coordinates (the point in the drawing you want centered), not paper space.
- Locking a viewport (`viewport.Locked = true`) after setting scale/center prevents an accidental zoom from changing the plotted scale — consider adding that as a follow-up call once the sheet is finalized.
