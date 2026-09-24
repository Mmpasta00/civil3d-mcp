using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using App = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: ExtensionApplication(typeof(Civil3DMcpPlugin.PluginEntry))]
[assembly: CommandClass(typeof(Civil3DMcpPlugin.PluginEntry))]

namespace Civil3DMcpPlugin;

/// <summary>
/// Entry point for the Civil 3D MCP plugin.
/// Loads automatically when Civil 3D starts (via NETLOAD) and starts
/// a TCP socket server that listens for JSON-RPC commands from the MCP server.
/// </summary>
public sealed class PluginEntry : IExtensionApplication
{
  private static readonly Guid GradonPaletteId = new("B6E1A6C4-6F0E-4B8E-9B7E-9C6B9A6E7B31");

  private static PaletteSet? _palette;
  private static Palette.GradonPaletteControl? _paletteControl;

  public void Initialize()
  {
    try
    {
      PluginRuntime.StartServer();
      WriteMessage("Civil3D MCP plugin initialized.");
    }
    catch (System.Exception ex)
    {
      WriteMessage($"Civil3D MCP plugin failed to initialize: {ex.Message}");
    }
  }

  public void Terminate()
  {
    PluginRuntime.StopServer();
    _paletteControl?.Shutdown();
  }

  /// <summary>Opens (or brings forward) the Gradon chat palette.</summary>
  [CommandMethod("GRADON")]
  public void GradonCommand()
  {
    if (_palette == null)
    {
      _paletteControl = new Palette.GradonPaletteControl();
      _palette = new PaletteSet("Gradon", GradonPaletteId)
      {
        Style = PaletteSetStyles.ShowPropertiesMenu
          | PaletteSetStyles.ShowAutoHideButton
          | PaletteSetStyles.ShowCloseButton,
        MinimumSize = new System.Drawing.Size(320, 480),
      };
      _palette.Add("Gradon", _paletteControl);
      _paletteControl.Initialize();
    }

    _palette.Visible = true;
  }

  /// <summary>Tears down the Gradon palette entirely. A later GRADON creates a fresh one.</summary>
  [CommandMethod("GRADONSTOP")]
  public void GradonStopCommand()
  {
    _paletteControl?.Shutdown();
    _palette?.Dispose();
    _palette = null;
    _paletteControl = null;
  }

  /// <summary>Manually start the MCP TCP listener.</summary>
  [CommandMethod("C3DMCPSTART")]
  public void StartCommand()
  {
    PluginRuntime.StartServer();
    WriteMessage($"Civil3D MCP listener started on port {PluginRuntime.Port}.");
  }

  /// <summary>Manually stop the MCP TCP listener.</summary>
  [CommandMethod("C3DMCPSTOP")]
  public void StopCommand()
  {
    PluginRuntime.StopServer();
    WriteMessage("Civil3D MCP listener stopped.");
  }

  /// <summary>Check the status of the MCP TCP listener.</summary>
  [CommandMethod("C3DMCPSTATUS")]
  public void StatusCommand()
  {
    var status = PluginRuntime.GetStatus();
    WriteMessage(
      $"Civil3D MCP listener running: {status.IsRunning}; " +
      $"pending: {status.QueueDepth}; " +
      $"active: {status.OperationInProgress}; " +
      $"current: {status.CurrentOperation ?? "<none>"}"
    );
  }

  private static void WriteMessage(string message)
  {
    var doc = App.DocumentManager.MdiActiveDocument;
    doc?.Editor.WriteMessage($"\n{message}");
  }
}
