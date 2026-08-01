using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StartUPs.Services;

/// <summary>One brand glyph: a vector path plus the brand colour.</summary>
public record IconEntry(string Title, string Color, string Path);

/// <summary>
/// Supplies a visual for every app: a real brand glyph where we have one,
/// otherwise a generated letter tile. Nothing is ever blank.
/// </summary>
public static class IconService
{
    private const string ResourceName = "StartUPs.icons.json";

    /// <summary>Simple Icons glyphs are authored on a 24x24 canvas.</summary>
    public const double GlyphCanvasSize = 24;

    private static Dictionary<string, IconEntry> _icons = new();

    /// <summary>
    /// Colours for generated letter tiles, chosen to read well on the dark theme.
    /// Picked deterministically so an app always gets the same colour.
    /// </summary>
    private static readonly Color[] FallbackPalette =
    {
        Color.FromRgb(0x63, 0x66, 0xF1), // indigo
        Color.FromRgb(0x8B, 0x5C, 0xF6), // violet
        Color.FromRgb(0xEC, 0x48, 0x99), // pink
        Color.FromRgb(0xF4, 0x3F, 0x5E), // rose
        Color.FromRgb(0xF5, 0x9E, 0x0B), // amber
        Color.FromRgb(0x10, 0xB9, 0x81), // emerald
        Color.FromRgb(0x06, 0xB6, 0xD4), // cyan
        Color.FromRgb(0x3B, 0x82, 0xF6)  // blue
    };

    /// <summary>Used when a brand colour is too dark to show against the card.</summary>
    private static readonly Color LightSubstitute = Color.FromRgb(0xE4, 0xE4, 0xEE);

    public static void Load()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using Stream? stream = assembly.GetManifestResourceStream(ResourceName);
            if (stream is null) return;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var file = JsonSerializer.Deserialize<IconFile>(stream, options);

            if (file?.Icons is not null)
                _icons = file.Icons;
        }
        catch
        {
            // A missing or malformed icon file must never stop the app - every
            // app simply falls back to a letter tile.
        }
    }

    /// <summary>
    /// A raster icon for apps with no vector glyph, extracted from the official
    /// installer and embedded as a PNG. Null when the app has no bundled bitmap.
    /// </summary>
    public static ImageSource? GetBitmap(string wingetId)
    {
        var safeName = Regex.Replace(wingetId, "[^A-Za-z0-9]", "_");
        var resource = $"StartUPs.Assets.AppIcons.{safeName}.png";

        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using Stream? stream = assembly.GetManifestResourceStream(resource);
            if (stream is null) return null;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;   // read now; the stream closes below
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>The brand glyph for an app, or null when we don't have one.</summary>
    public static Geometry? GetGlyph(string wingetId)
    {
        if (!_icons.TryGetValue(wingetId, out var entry) || string.IsNullOrWhiteSpace(entry.Path))
            return null;

        try
        {
            var geometry = Geometry.Parse(entry.Path);
            geometry.Freeze();
            return geometry;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// The colour to draw an app's icon in. Brand colour where we have one,
    /// lightened when the brand is near-black, otherwise a palette colour.
    /// </summary>
    public static Brush GetAccent(string wingetId)
    {
        if (_icons.TryGetValue(wingetId, out var entry) && TryParseColor(entry.Color, out var brand))
        {
            var usable = Luminance(brand) < 0.22 ? LightSubstitute : brand;
            return Frozen(usable);
        }

        // Deterministic palette pick so an app keeps the same colour every launch.
        int hash = 0;
        foreach (char c in wingetId)
            hash = (hash * 31 + c) & 0x7FFFFFFF;

        return Frozen(FallbackPalette[hash % FallbackPalette.Length]);
    }

    // ------------------------------------------------------------------ helpers

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static bool TryParseColor(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex)) return false;

        try
        {
            var converted = ColorConverter.ConvertFromString(hex);
            if (converted is Color parsed)
            {
                color = parsed;
                return true;
            }
        }
        catch
        {
            // Not a colour we can read.
        }

        return false;
    }

    /// <summary>Perceived brightness, 0 (black) to 1 (white).</summary>
    private static double Luminance(Color c)
        => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;

    private sealed class IconFile
    {
        public Dictionary<string, IconEntry>? Icons { get; set; }
    }
}
