using System.Text.Json.Serialization;

namespace Civil3DMcpPlugin.Agent;

using Civil3DMcpPlugin; // DrawingContext

/// <summary>DTOs for the Gradon turn protocol (POST /v1/turn, GET /v1/health).</summary>

public sealed class TurnRequest
{
  [JsonPropertyName("session_id")]
  public string? SessionId { get; set; }

  [JsonPropertyName("user_id")]
  public string UserId { get; set; } = "";

  [JsonPropertyName("firm_id")]
  public string FirmId { get; set; } = "";

  [JsonPropertyName("message")]
  public string? Message { get; set; }

  [JsonPropertyName("drawing")]
  public DrawingContext? Drawing { get; set; }

  [JsonPropertyName("tool_result")]
  public ToolResult? ToolResult { get; set; }
}

public sealed class ToolResult
{
  [JsonPropertyName("call_id")]
  public string CallId { get; set; } = "";

  [JsonPropertyName("ok")]
  public bool Ok { get; set; }

  [JsonPropertyName("result")]
  public string? Result { get; set; }

  [JsonPropertyName("error")]
  public string? Error { get; set; }

  [JsonPropertyName("duration_ms")]
  public int DurationMs { get; set; }
}

public sealed class TurnResponse
{
  [JsonPropertyName("session_id")]
  public string SessionId { get; set; } = "";

  [JsonPropertyName("action")]
  public string Action { get; set; } = ""; // "run" | "ask" | "done" | "error"

  [JsonPropertyName("call_id")]
  public string? CallId { get; set; }

  [JsonPropertyName("code")]
  public string? Code { get; set; }

  [JsonPropertyName("read_only")]
  public bool ReadOnly { get; set; }

  [JsonPropertyName("description")]
  public string? Description { get; set; }

  [JsonPropertyName("text")]
  public string? Text { get; set; }

  [JsonPropertyName("summary")]
  public TurnSummary? Summary { get; set; }
}

public sealed class TurnSummary
{
  [JsonPropertyName("steps")]
  public int Steps { get; set; }

  [JsonPropertyName("writes")]
  public int Writes { get; set; }

  [JsonPropertyName("time_saved_min")]
  public double TimeSavedMin { get; set; }

  [JsonPropertyName("run_id")]
  public string RunId { get; set; } = "";
}

public sealed class HealthResponse
{
  [JsonPropertyName("status")]
  public string? Status { get; set; }

  [JsonPropertyName("ok")]
  public bool? Ok { get; set; }
}
