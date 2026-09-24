using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;

namespace SkillCompileCheck;

/// <summary>
/// Mirrors Civil3DMcpPlugin.ScriptContext exactly (same 5 globals, same types) without
/// referencing the plugin project itself (per the file fence for this build task).
/// If ScriptContext ever changes, update this class to match.
/// </summary>
public class ScriptGlobals
{
  public Document? Document { get; set; }
  public CivilDocument? CivilDoc { get; set; }
  public Database? Database { get; set; }
  public Transaction? Transaction { get; set; }
  public Editor? Editor { get; set; }
}
