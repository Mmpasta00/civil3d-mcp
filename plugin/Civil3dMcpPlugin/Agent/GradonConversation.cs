namespace Civil3DMcpPlugin.Agent;

using Civil3DMcpPlugin; // CivilExecution, DrawingContext, ScriptRunner

/// <summary>Whether a non-read-only "run" step must be confirmed by the user first.</summary>
public enum WriteMode
{
  AskBeforeWrite,
  RunWithoutAsking,
}

/// <summary>One entry in the palette's step log.</summary>
public sealed record StepLogItem(string Description, bool Ok, int DurationMs, string? Error);

/// <summary>
/// Drives one Gradon conversation: sends the user's message (with a fresh
/// drawing snapshot), then loops on "run" actions — executing the code the
/// service asks for and posting the result back — until the turn settles
/// into "ask", "done", or "error". UI hookup is via events so this class has
/// no WinForms dependency.
/// </summary>
public sealed class GradonConversation
{
  private readonly GradonClient _client;

  public WriteMode Mode { get; set; } = WriteMode.AskBeforeWrite;

  /// <summary>Fired with assistant-authored text to show in the transcript.</summary>
  public event Action<string>? AssistantText;

  /// <summary>Fired once per executed step, for the step log.</summary>
  public event Action<StepLogItem>? StepExecuted;

  /// <summary>Fired when the turn completes ("done").</summary>
  public event Action<TurnSummary>? Completed;

  /// <summary>Fired when the service reports an error ("error" action, or a transport failure).</summary>
  public event Action<string>? Failed;

  /// <summary>
  /// Asked before executing a non-read-only step when <see cref="Mode"/> is
  /// AskBeforeWrite. Return true to proceed, false to decline. Must be set
  /// by the host UI — a step is never silently skipped.
  /// </summary>
  public Func<string, Task<bool>>? ConfirmWriteAsync;

  public GradonConversation(GradonClient client)
  {
    _client = client;
  }

  /// <summary>Sends a user-typed message (new question or a reply during "ask") and runs the turn to completion.</summary>
  public async Task SendUserMessageAsync(string message, CancellationToken ct)
  {
    DrawingContext? drawing = null;
    try
    {
      drawing = await CivilExecution.ReadAsync((doc, civilDoc, db, tr) => DrawingContext.Capture(doc, civilDoc, db, tr));
    }
    catch
    {
      // Best-effort snapshot only — a drawing that can't be read (e.g. no
      // active document) still lets the user chat with Gradon.
    }

    TurnResponse response;
    try
    {
      response = await _client.SendMessageAsync(message, drawing, ct);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      Failed?.Invoke($"Could not reach the Gradon service: {ex.Message}");
      return;
    }

    await RunLoopAsync(response, ct);
  }

  private async Task RunLoopAsync(TurnResponse response, CancellationToken ct)
  {
    while (true)
    {
      ct.ThrowIfCancellationRequested();

      switch (response.Action)
      {
        case "run":
          if (!string.IsNullOrWhiteSpace(response.Text))
            AssistantText?.Invoke(response.Text!);

          if (!response.ReadOnly && Mode == WriteMode.AskBeforeWrite)
          {
            var proceed = ConfirmWriteAsync != null
              && await ConfirmWriteAsync(response.Description ?? "Gradon wants to make a change to this drawing.");

            if (!proceed)
            {
              response = await PostToolResultAsync(response.CallId, ok: false, result: null, error: "user declined", durationMs: 0, ct);
              continue;
            }
          }

          response = await ExecuteStepAsync(response, ct);
          continue;

        case "ask":
          if (!string.IsNullOrWhiteSpace(response.Text))
            AssistantText?.Invoke(response.Text!);
          return; // wait for the next user message on the same session

        case "done":
          if (!string.IsNullOrWhiteSpace(response.Text))
            AssistantText?.Invoke(response.Text!);
          if (response.Summary != null)
            Completed?.Invoke(response.Summary);
          return;

        case "error":
          Failed?.Invoke(response.Text ?? "Gradon service returned an error.");
          return;

        default:
          Failed?.Invoke($"Unknown action '{response.Action}' from Gradon service.");
          return;
      }
    }
  }

  private async Task<TurnResponse> ExecuteStepAsync(TurnResponse step, CancellationToken ct)
  {
    if (string.IsNullOrWhiteSpace(step.Code))
    {
      StepExecuted?.Invoke(new StepLogItem(step.Description ?? "Step", false, 0, "no code provided"));
      return await PostToolResultAsync(step.CallId, ok: false, result: null, error: "run action had no code", durationMs: 0, ct);
    }

    var sw = System.Diagnostics.Stopwatch.StartNew();
    string? resultJson = null;
    string? error = null;
    var ok = true;

    try
    {
      // Every write from the chat agent gets its own UNDO group (see ScriptRunner).
      var raw = await ScriptRunner.ExecuteCodeAsync(step.Code, step.ReadOnly, wrapUndoGroup: true);
      resultJson = GradonClient.TruncateResult(
        raw == null ? "null" : System.Text.Json.JsonSerializer.Serialize(raw));
    }
    catch (Exception ex)
    {
      ok = false;
      error = ex.Message;
    }
    finally
    {
      sw.Stop();
    }

    StepExecuted?.Invoke(new StepLogItem(step.Description ?? "Step", ok, (int)sw.ElapsedMilliseconds, error));

    return await PostToolResultAsync(step.CallId, ok, ok ? resultJson : null, error, (int)sw.ElapsedMilliseconds, ct);
  }

  private async Task<TurnResponse> PostToolResultAsync(
    string? callId, bool ok, string? result, string? error, int durationMs, CancellationToken ct)
  {
    try
    {
      return await _client.SendToolResultAsync(new ToolResult
      {
        CallId = callId ?? "",
        Ok = ok,
        Result = result,
        Error = error,
        DurationMs = durationMs,
      }, ct);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      // Don't raise Failed here — return an "error" action so the caller's
      // switch handles it once, instead of firing the event twice.
      return new TurnResponse { Action = "error", Text = $"Could not reach the Gradon service: {ex.Message}" };
    }
  }
}
