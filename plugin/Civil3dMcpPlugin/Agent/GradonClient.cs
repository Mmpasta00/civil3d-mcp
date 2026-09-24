using System.Net.Http.Json;
using System.Text.Json;

namespace Civil3DMcpPlugin.Agent;

using Civil3DMcpPlugin; // DrawingContext

/// <summary>
/// HTTP client for the Gradon turn protocol. One instance per palette;
/// tracks the session id returned by the service across turns.
/// </summary>
public sealed class GradonClient : IDisposable
{
  /// <summary>Result string is truncated to this many bytes before being sent back as a tool_result.</summary>
  public const int MaxResultBytes = 20 * 1024;

  private readonly HttpClient _http;
  private readonly GradonConfig _config;

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
  };

  public string? SessionId { get; private set; }

  public GradonClient(GradonConfig config)
  {
    _config = config;
    _http = new HttpClient
    {
      BaseAddress = new Uri(_config.ServiceUrl),
      Timeout = TimeSpan.FromSeconds(60),
    };
    if (!string.IsNullOrWhiteSpace(_config.ApiKey))
      _http.DefaultRequestHeaders.Add("X-Gradon-Key", _config.ApiKey);
  }

  /// <summary>Starts or continues a conversation turn with a user message.</summary>
  public Task<TurnResponse> SendMessageAsync(string message, DrawingContext? drawing, CancellationToken ct)
    => PostTurnAsync(new TurnRequest
    {
      SessionId = SessionId,
      UserId = _config.UserId,
      FirmId = _config.FirmId,
      Message = message,
      Drawing = drawing,
    }, ct);

  /// <summary>Continues a turn after executing the code the service asked for.</summary>
  public Task<TurnResponse> SendToolResultAsync(ToolResult toolResult, CancellationToken ct)
    => PostTurnAsync(new TurnRequest
    {
      SessionId = SessionId,
      UserId = _config.UserId,
      FirmId = _config.FirmId,
      ToolResult = toolResult,
    }, ct);

  /// <summary>Continues a turn after the user answers an "ask" prompt.</summary>
  public Task<TurnResponse> SendFollowUpAsync(string message, CancellationToken ct)
    => PostTurnAsync(new TurnRequest
    {
      SessionId = SessionId,
      UserId = _config.UserId,
      FirmId = _config.FirmId,
      Message = message,
    }, ct);

  private async Task<TurnResponse> PostTurnAsync(TurnRequest request, CancellationToken ct)
  {
    var httpResponse = await _http.PostAsJsonAsync("/v1/turn", request, JsonOptions, ct);
    httpResponse.EnsureSuccessStatusCode();

    var turn = await httpResponse.Content.ReadFromJsonAsync<TurnResponse>(JsonOptions, ct)
      ?? throw new InvalidOperationException("Gradon service returned an empty response.");

    SessionId = turn.SessionId;
    return turn;
  }

  /// <summary>GET /v1/health — used for the palette status line. Never throws; returns false on any failure.</summary>
  public async Task<(bool Ok, string Detail)> CheckHealthAsync(CancellationToken ct)
  {
    try
    {
      using var response = await _http.GetAsync("/v1/health", ct);
      if (!response.IsSuccessStatusCode)
        return (false, $"HTTP {(int)response.StatusCode}");

      var body = await response.Content.ReadAsStringAsync(ct);
      return (true, string.IsNullOrWhiteSpace(body) ? "connected" : body);
    }
    catch (Exception ex)
    {
      return (false, ex.Message);
    }
  }

  /// <summary>Caps a tool-result payload to <see cref="MaxResultBytes"/>, appending a truncation marker.</summary>
  public static string TruncateResult(string result)
  {
    var bytes = System.Text.Encoding.UTF8.GetByteCount(result);
    if (bytes <= MaxResultBytes) return result;

    // Trim by chars first (cheap upper bound), then re-check byte length for multibyte safety.
    var approxChars = Math.Min(result.Length, MaxResultBytes);
    var truncated = result[..approxChars];
    while (System.Text.Encoding.UTF8.GetByteCount(truncated) > MaxResultBytes && truncated.Length > 0)
      truncated = truncated[..^1];

    return truncated + "\n…[truncated, result exceeded 20 KB]";
  }

  public void Dispose() => _http.Dispose();
}
