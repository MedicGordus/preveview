using System.Text.Json;
using System.Text.Json.Serialization;

namespace preveview;

public class Configuration
{
    [JsonPropertyName("windows")]
    public List<WindowConfigurationJson>? Windows { get; set; }

    [JsonPropertyName("millisecond-delay-to-move")]
    public int? MillisecondDelayToMove { get; set; }

    [JsonPropertyName("base-opacity")]
    public double? BaseOpacity { get; set; }

    [JsonPropertyName("magnetize-pixel-distance")]
    public int? MagnetizePixelDistance { get; set; }
    
    [JsonPropertyName("default-screen-index")]
    public int? DefaultScreenIndex { get; set; }

    [JsonPropertyName("monitor-millisecond-interval")]
    public int? MonitorMillisecondInterval { get; set; }

    [JsonPropertyName("base-border-width")]
    public int? BaseBorderWidth { get; set; }

    [JsonPropertyName("active-argb")]
    public ArgbConfiguration? BaseActiveArgb { get; set; }

    [JsonPropertyName("inactive-argb")]
    public ArgbConfiguration? BaseInactiveArgb { get; set; }

    [JsonPropertyName("minimized-argb")]
    public ArgbConfiguration? BaseMinimizedArgb { get; set; }

    public void Save(string? path = null)
    {
        File.WriteAllText(
            path ?? Program.CONFIG_PATH,
            JsonSerializer.Serialize(this)
        );
    }
}

public class WindowConfigurationJson
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("title-label-override")]
    public string? TitleLabelOverride { get; set; }

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }
    
    [JsonPropertyName("width")]
    public int Width { get; set; }
    
    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("hotkeys")]
    public List<string>? Hotkeys { get; set; }

    [JsonPropertyName("opacity")]
    public double? Opacity { get; set; }
    
    [JsonPropertyName("screen-index")]
    public int? ScreenIndex { get; set; }
    
    [JsonPropertyName("disabled")]
    public bool? Disabled { get; set; }
    
    [JsonPropertyName("show-title-bar")]
    public bool? ShowTitleBar { get; set; }

    [JsonPropertyName("border-width")]
    public int? BorderWidth { get; set; }

    [JsonPropertyName("active-argb")]
    public ArgbConfiguration? ActiveArgb { get; set; }

    [JsonPropertyName("inactive-argb")]
    public ArgbConfiguration? InactiveArgb { get; set; }

    [JsonPropertyName("minimized-argb")]
    public ArgbConfiguration? MinimizedArgb { get; set; }
}

public class ArgbConfiguration
{
    [JsonPropertyName("alpha")]
    public int Alpha { get; set; }
    
    [JsonPropertyName("red")]
    public int Red { get; set; }
    
    [JsonPropertyName("green")]
    public int Green { get; set; }
    
    [JsonPropertyName("blue")]
    public int Blue { get; set; }
}