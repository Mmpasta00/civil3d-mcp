using System.Text.Json.Nodes;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Civil.ApplicationServices;
using App = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Civil3DMcpPlugin;

/// <summary>
/// Routes JSON-RPC methods. With code execution architecture,
/// only 2 methods are needed: executeCode and listSkills.
/// </summary>
public static class CommandDispatcher
{
  public static async Task<object?> DispatchAsync(
    string method,
    JsonObject? parameters,
    CancellationToken cancellationToken)
  {
    return method switch
    {
      "executeCode" => await ExecuteCodeAsync(parameters),
      "getCivil3DHealth" => await GetHealthAsync(),
      "getDrawingContext" => await GetDrawingContextAsync(),

      _ => throw new JsonRpcDispatchException(
        "CIVIL3D.INVALID_INPUT",
        $"Unknown method '{method}'. Available: executeCode, getCivil3DHealth, getDrawingContext"
      ),
    };
  }

  /// <summary>
  /// Execute C# code via Roslyn in the Civil 3D context.
  /// </summary>
  private static async Task<object?> ExecuteCodeAsync(JsonObject? parameters)
  {
    var code = PluginRuntime.GetRequiredString(parameters, "code");
    var readOnly = parameters?["readOnly"]?.GetValue<bool>() ?? false;
    var description = PluginRuntime.GetOptionalString(parameters, "description") ?? "Script execution";

    // Log the execution
    System.Diagnostics.Debug.WriteLine($"[C3D-MCP] {(readOnly ? "QUERY" : "EXECUTE")}: {description}");

    // Execute on Civil 3D main thread with proper document locking
    // (same code path the Gradon chat agent uses — see ScriptRunner)
    return await ScriptRunner.ExecuteCodeAsync(code, readOnly);
  }

  /// <summary>Read-only snapshot of the active drawing (surfaces, alignments, etc).</summary>
  private static Task<object?> GetDrawingContextAsync()
  {
    return CivilExecution.ReadAsync<object?>(
      (doc, civilDoc, db, tr) => DrawingContext.Capture(doc, civilDoc, db, tr));
  }

  /// <summary>Health check — verifies the plugin is alive and Civil 3D is responsive.</summary>
  private static Task<object?> GetHealthAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      return new
      {
        connected = true,
        drawingName = doc.Name,
        mode = "code_execution",
        roslyn = true,
      };
    });
  }
}
