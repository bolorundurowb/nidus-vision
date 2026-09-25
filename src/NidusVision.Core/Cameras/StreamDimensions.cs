using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using NidusVision.Core.Roi;

namespace NidusVision.Core.Cameras;

public static class StreamDimensions
{
    private static readonly Regex Pattern = new(@"(\d{2,5})\s*[x×]\s*(\d{2,5})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool TryParse(string? resolution, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (string.IsNullOrWhiteSpace(resolution))
        {
            return false;
        }

        var match = Pattern.Match(resolution);
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out width)
            || !int.TryParse(match.Groups[2].Value, CultureInfo.InvariantCulture, out height)
            || width <= 0
            || height <= 0)
        {
            return false;
        }

        return true;
    }
}

public static class RoiJson
{
    public static IReadOnlyList<Point> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var points = JsonSerializer.Deserialize<List<RoiPoint>>(json, JsonOptions);
            if (points is null)
            {
                return [];
            }

            return [.. points.Select(p => new Point(p.X, p.Y))];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed record RoiPoint(double X, double Y);
}
