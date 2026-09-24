using System.Text.Json;
using System.Text.Json.Serialization;

namespace Civil3DMcpPlugin.Agent;

/// <summary>
/// Local config for the Gradon agent client, read from
/// %APPDATA%\Gradon\civil3d.json. Env vars GRADON_SERVICE_URL and
/// GRADON_API_KEY override the file values.
/// </summary>
public sealed class GradonConfig
{
  [JsonPropertyName("serviceUrl")]
  public string ServiceUrl { get; set; } = "http://localhost:8799";

  [JsonPropertyName("apiKey")]
  public string ApiKey { get; set; } = "";

  [JsonPropertyName("userId")]
  public string UserId { get; set; } = "";

  [JsonPropertyName("firmId")]
  public string FirmId { get; set; } = "";

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    WriteIndented = true,
  };

  public static string ConfigDirectory =>
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Gradon");

  public static string ConfigPath => Path.Combine(ConfigDirectory, "civil3d.json");

  /// <summary>
  /// Loads config from disk, creating a default file if none exists.
  /// Environment variables override file values when present.
  /// </summary>
  public static GradonConfig Load()
  {
    GradonConfig config;

    try
    {
      if (File.Exists(ConfigPath))
      {
        var json = File.ReadAllText(ConfigPath);
        config = JsonSerializer.Deserialize<GradonConfig>(json) ?? new GradonConfig();
      }
      else
      {
        config = new GradonConfig();
        Save(config);
      }
    }
    catch
    {
      // Corrupt or unreadable config — fall back to defaults without touching the file.
      config = new GradonConfig();
    }

    var envUrl = Environment.GetEnvironmentVariable("GRADON_SERVICE_URL");
    if (!string.IsNullOrWhiteSpace(envUrl)) config.ServiceUrl = envUrl;

    var envKey = Environment.GetEnvironmentVariable("GRADON_API_KEY");
    if (!string.IsNullOrWhiteSpace(envKey)) config.ApiKey = envKey;

    return config;
  }

  public static void Save(GradonConfig config)
  {
    Directory.CreateDirectory(ConfigDirectory);
    File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOptions));
  }
}
