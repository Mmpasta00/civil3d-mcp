namespace Civil3DMcpPlugin;

/// <summary>
/// Shared helper for compiling and running a Roslyn script inside a Civil 3D
/// document lock/transaction. Used by both the MCP TCP path
/// (<see cref="CommandDispatcher"/>) and the Gradon chat agent
/// (<see cref="Agent.GradonConversation"/>), so the two entry points execute
/// code identically.
/// </summary>
public static class ScriptRunner
{
  /// <summary>
  /// Executes <paramref name="code"/> in Civil 3D and returns its result.
  /// When <paramref name="wrapUndoGroup"/> is true and the run is a write
  /// (readOnly is false), the whole script is wrapped in one AutoCAD UNDO
  /// group (_.UNDO _BEGIN / _END) so a single Ctrl+Z reverts every change
  /// the script made, even if the script performs several internal
  /// operations. See plugin/README.md "Known unverified points" — grouping
  /// UNDO via Editor.Command while a CivilExecution transaction is already
  /// open has not been exercised against a live Civil 3D session.
  /// </summary>
  public static Task<object?> ExecuteCodeAsync(string code, bool readOnly, bool wrapUndoGroup = false)
  {
    return CivilExecution.ExecuteAsync((doc, civilDoc, db, tr) =>
    {
      var context = new ScriptContext(doc, civilDoc, db, tr);
      var shouldWrap = wrapUndoGroup && !readOnly;

      if (shouldWrap)
      {
        doc.Editor.Command("_.UNDO", "_BEGIN");
      }

      try
      {
        var task = RoslynExecutor.ExecuteAsync(code, context);
        task.Wait(); // safe: already on the main thread via ExecuteInCommandContextAsync
        return task.Result;
      }
      finally
      {
        if (shouldWrap)
        {
          doc.Editor.Command("_.UNDO", "_END");
        }
      }
    }, write: !readOnly);
  }
}
