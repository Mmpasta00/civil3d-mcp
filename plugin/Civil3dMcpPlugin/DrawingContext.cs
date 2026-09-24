using System.Text.Json.Serialization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;

namespace Civil3DMcpPlugin;

/// <summary>Minimal (name, type) pair for surfaces and similar entities.</summary>
public sealed record NamedEntity(
  [property: JsonPropertyName("name")] string Name,
  [property: JsonPropertyName("type")] string Type);

/// <summary>Current selection entry, kept lightweight (type + handle only).</summary>
public sealed record SelectionEntry(
  [property: JsonPropertyName("type")] string Type,
  [property: JsonPropertyName("handle")] string Handle);

/// <summary>
/// Read-only snapshot of the active drawing, sent to the Gradon service on
/// every turn so the agent has situational awareness without a round trip.
/// Field names are snake_case on the wire per the shared protocol.
/// </summary>
public sealed record DrawingContext(
  [property: JsonPropertyName("drawing")] string Drawing,
  [property: JsonPropertyName("path")] string? Path,
  [property: JsonPropertyName("units")] string Units,
  [property: JsonPropertyName("civil_version")] string CivilVersion,
  [property: JsonPropertyName("surfaces")] IReadOnlyList<NamedEntity> Surfaces,
  [property: JsonPropertyName("alignments")] IReadOnlyList<string> Alignments,
  [property: JsonPropertyName("profile_views")] int ProfileViews,
  [property: JsonPropertyName("corridors")] IReadOnlyList<string> Corridors,
  [property: JsonPropertyName("pipe_networks")] IReadOnlyList<string> PipeNetworks,
  [property: JsonPropertyName("pressure_networks")] IReadOnlyList<string> PressureNetworks,
  [property: JsonPropertyName("parcels")] int Parcels,
  [property: JsonPropertyName("cogo_points")] int CogoPoints,
  [property: JsonPropertyName("sample_line_groups")] int SampleLineGroups,
  [property: JsonPropertyName("layouts")] IReadOnlyList<string> Layouts,
  [property: JsonPropertyName("xrefs")] IReadOnlyList<string> Xrefs,
  [property: JsonPropertyName("layers")] int Layers,
  [property: JsonPropertyName("selection")] IReadOnlyList<SelectionEntry> Selection
)
{
  /// <summary>
  /// Builds the snapshot from the active document. Must run inside
  /// CivilExecution.ReadAsync (main-thread, doc-locked, transaction open).
  /// Every section is individually guarded so one failing API call never
  /// blocks the rest of the snapshot.
  /// </summary>
  public static DrawingContext Capture(Document doc, CivilDocument civilDoc, Database db, Transaction tr)
  {
    string name = doc.Name;
    string? path = null;
    string units = "Unknown";
    string civilVersion = "Unknown";
    var surfaces = new List<NamedEntity>();
    var alignments = new List<string>();
    int profileViews = 0;
    var corridors = new List<string>();
    var pipeNetworks = new List<string>();
    var pressureNetworks = new List<string>();
    int parcels = 0;
    int cogoPoints = 0;
    int sampleLineGroups = 0;
    var layouts = new List<string>();
    var xrefs = new List<string>();
    int layers = 0;
    var selection = new List<SelectionEntry>();

    try { name = string.IsNullOrEmpty(doc.Name) ? "Drawing1.dwg" : Path_GetFileName(doc.Name); } catch { }
    try { path = File.Exists(doc.Name) ? doc.Name : null; } catch { }
    try { units = db.Insunits.ToString(); } catch { }
    // No CivilDocument.CivilVersion in the managed API — the AutoCAD platform
    // version (e.g. 25.1.x for Civil 3D 2026) is the closest stable proxy.
    try { civilVersion = Autodesk.AutoCAD.ApplicationServices.Application.Version?.ToString() ?? "Unknown"; } catch { }

    try
    {
      foreach (ObjectId id in civilDoc.GetSurfaceIds())
      {
        try
        {
          var obj = tr.GetObject(id, OpenMode.ForRead);
          if (obj is Autodesk.Civil.DatabaseServices.Surface surf)
            surfaces.Add(new NamedEntity(surf.Name, surf.GetType().Name));
        }
        catch { }
      }
    }
    catch { }

    try
    {
      // One pass per alignment: name, plus profile-view and sample-line-group
      // counts, which are hosted off the alignment rather than the document.
      foreach (ObjectId id in civilDoc.GetAlignmentIds())
      {
        try
        {
          if (tr.GetObject(id, OpenMode.ForRead) is not Autodesk.Civil.DatabaseServices.Alignment align)
            continue;

          try { alignments.Add(align.Name); } catch { }
          try { profileViews += align.GetProfileViewIds().Count; } catch { }
          try { sampleLineGroups += align.GetSampleLineGroupIds().Count; } catch { }
        }
        catch { }
      }
    }
    catch { }

    try
    {
      // CivilDocument has no GetCorridorIds() — CorridorCollection enumerates ObjectIds directly.
      foreach (ObjectId id in civilDoc.CorridorCollection)
      {
        try
        {
          var obj = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Corridor;
          if (obj != null) corridors.Add(obj.Name);
        }
        catch { }
      }
    }
    catch { }

    try
    {
      foreach (ObjectId id in civilDoc.GetPipeNetworkIds())
      {
        try
        {
          var obj = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Network;
          if (obj != null) pipeNetworks.Add(obj.Name);
        }
        catch { }
      }
    }
    catch { }

    try
    {
      // Pressure networks are a separate collection (AeccPressurePipesMgd extension method).
      foreach (ObjectId id in civilDoc.GetPressurePipeNetworkIds())
      {
        try
        {
          var obj = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.PressurePipeNetwork;
          if (obj != null) pressureNetworks.Add(obj.Name);
        }
        catch { }
      }
    }
    catch { }

    try
    {
      foreach (ObjectId siteId in civilDoc.GetSiteIds())
      {
        try
        {
          var site = tr.GetObject(siteId, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Site;
          if (site != null) parcels += site.GetParcelIds().Count;
        }
        catch { }
      }
    }
    catch { }

    try { cogoPoints = (int)civilDoc.CogoPoints.Count; } catch { }

    try
    {
      var layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
      foreach (DBDictionaryEntry entry in layoutDict)
      {
        try
        {
          var layout = tr.GetObject(entry.Value, OpenMode.ForRead) as Layout;
          if (layout != null) layouts.Add(layout.LayoutName);
        }
        catch { }
      }
    }
    catch { }

    try
    {
      var xrefGraph = doc.Database.GetHostDwgXrefGraph(false);
      // Node 0 is always the host drawing itself — start at 1 for actual xrefs.
      for (int i = 1; i < xrefGraph.NumNodes; i++)
      {
        try
        {
          var node = xrefGraph.GetXrefNode(i);
          if (!string.IsNullOrEmpty(node.Name))
            xrefs.Add(node.Name);
        }
        catch { }
      }
    }
    catch { }

    try
    {
      var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
      layers = layerTable.Cast<ObjectId>().Count();
    }
    catch { }

    try
    {
      var ed = doc.Editor;
      var selResult = ed.SelectImplied();
      if (selResult.Status == PromptStatus.OK)
      {
        foreach (var selId in selResult.Value.GetObjectIds().Take(50))
        {
          try
          {
            var ent = tr.GetObject(selId, OpenMode.ForRead);
            selection.Add(new SelectionEntry(ent.GetType().Name, selId.Handle.ToString()));
          }
          catch { }
        }
      }
    }
    catch { }

    return new DrawingContext(
      name, path, units, civilVersion,
      surfaces, alignments, profileViews, corridors, pipeNetworks, pressureNetworks,
      parcels, cogoPoints, sampleLineGroups, layouts, xrefs, layers, selection
    );
  }

  private static string Path_GetFileName(string p)
  {
    try { return System.IO.Path.GetFileName(p); } catch { return p; }
  }
}
