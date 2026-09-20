using System.Globalization;
using System.Text;
using Avalonia.Media;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Symbols;
using UI_Unilineal.Engine.Composition;

namespace UI_Unilineal.Rendering.Avalonia.Rendering;

public enum InteractiveThemeKind
{
    Light,
    Dark
}

public sealed class AvaloniaRenderResources
{
    private readonly RIC18DrawingProfile _profile;
    private readonly InteractiveThemeKind _theme;
    private readonly int _maxCachedSymbols;
    private readonly Dictionary<string, Pen> _pens =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, Typeface> _typefaces =
        new(StringComparer.Ordinal);
    private readonly Dictionary<SymbolCacheKey, Geometry> _symbolCache = [];
    private readonly Queue<SymbolCacheKey> _symbolInsertionOrder = [];

    public AvaloniaRenderResources(
        RIC18DrawingProfile profile,
        InteractiveThemeKind theme,
        int maxCachedSymbols = 256,
        int maxCachedTextEntries = 2048)
    {
        _profile = profile ??
            throw new ArgumentNullException(nameof(profile));

        if (maxCachedSymbols <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCachedSymbols));
        }

        if (maxCachedTextEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCachedTextEntries));
        }

        _theme = theme;
        _maxCachedSymbols = maxCachedSymbols;
        ProfileFingerprint =
            DrawingProfileFingerprint.Compute(profile);
    }

    public string ProfileFingerprint { get; }

    public Pen ResolvePen(string lineStyleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lineStyleId);

        if (_pens.TryGetValue(
                lineStyleId,
                out Pen? cached))
        {
            return cached;
        }

        LineStyleDefinition style =
            _profile.LineStyles.SingleOrDefault(
                value => string.Equals(
                    value.Id,
                    lineStyleId,
                    StringComparison.Ordinal))
            ?? throw new KeyNotFoundException(
                $"Drawing profile does not contain line style '{lineStyleId}'.");

        IBrush brush =
            BrushFor(style.Role);
        IDashStyle? dash =
            style.Pattern switch
            {
                LinePattern.Solid => null,
                LinePattern.Dashed =>
                    new DashStyle([4.0, 2.0], 0),
                LinePattern.DashDot =>
                    new DashStyle([4.0, 2.0, 1.0, 2.0], 0),
                LinePattern.Dotted =>
                    new DashStyle([1.0, 2.0], 0),
                _ => throw new InvalidOperationException(
                    $"Unsupported line pattern '{style.Pattern}'.")
            };

        var pen = new Pen(
            brush,
            style.WidthMm,
            dash,
            PenLineCap.Square,
            PenLineJoin.Miter,
            10);

        _pens.Add(
            lineStyleId,
            pen);

        return pen;
    }

    public Typeface ResolveTypeface(string textStyleId)
    {
        TextStyleDefinition style =
            RequireTextStyle(textStyleId);

        if (_typefaces.TryGetValue(
                textStyleId,
                out Typeface cached))
        {
            return cached;
        }

        var typeface = new Typeface(
            style.FontFamily,
            FontStyle.Normal,
            style.Bold
                ? FontWeight.Bold
                : FontWeight.Normal,
            FontStretch.Normal);

        _typefaces.Add(
            textStyleId,
            typeface);

        return typeface;
    }

    public IBrush ResolveTextBrush(string textStyleId)
    {
        _ = RequireTextStyle(textStyleId);

        return _theme == InteractiveThemeKind.Dark
            ? Brushes.White
            : Brushes.Black;
    }

    public IBrush ResolveStatusBrush(string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        return status.ToUpperInvariant() switch
        {
            "ERROR" => Brushes.OrangeRed,
            "WARNING" => Brushes.Goldenrod,
            "OK" => Brushes.ForestGreen,
            "STALE" => Brushes.DarkGoldenrod,
            "PENDING" => Brushes.DodgerBlue,
            _ => _theme == InteractiveThemeKind.Dark
                ? Brushes.LightGray
                : Brushes.DimGray
        };
    }

    public Geometry ResolveSymbolGeometry(
        string symbolDefinitionId,
        string variant = "default")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbolDefinitionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(variant);

        var key = new SymbolCacheKey(
            ProfileFingerprint,
            symbolDefinitionId,
            variant);

        if (_symbolCache.TryGetValue(
                key,
                out Geometry? cached))
        {
            return cached;
        }

        SymbolDefinition symbol =
            _profile.Symbols.SingleOrDefault(
                value => string.Equals(
                    value.Id,
                    symbolDefinitionId,
                    StringComparison.Ordinal))
            ?? throw new KeyNotFoundException(
                $"Drawing profile does not contain symbol '{symbolDefinitionId}'.");

        Geometry geometry =
            BuildGeometry(symbol);
        AddSymbolToCache(
            key,
            geometry);

        return geometry;
    }

    private void AddSymbolToCache(
        SymbolCacheKey key,
        Geometry geometry)
    {
        while (_symbolCache.Count >= _maxCachedSymbols)
        {
            SymbolCacheKey oldest =
                _symbolInsertionOrder.Dequeue();
            _symbolCache.Remove(oldest);
        }

        _symbolCache.Add(
            key,
            geometry);
        _symbolInsertionOrder.Enqueue(key);
    }

    private TextStyleDefinition RequireTextStyle(
        string textStyleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(textStyleId);

        return _profile.TextStyles.SingleOrDefault(
                   value => string.Equals(
                       value.Id,
                       textStyleId,
                       StringComparison.Ordinal))
               ?? throw new KeyNotFoundException(
                   $"Drawing profile does not contain text style '{textStyleId}'.");
    }

    private IBrush BrushFor(
        LineSemanticRole role)
    {
        if (role is LineSemanticRole.Reference or
            LineSemanticRole.Annotation)
        {
            return _theme == InteractiveThemeKind.Dark
                ? Brushes.LightGray
                : Brushes.DimGray;
        }

        return _theme == InteractiveThemeKind.Dark
            ? Brushes.White
            : Brushes.Black;
    }

    private static Geometry BuildGeometry(
        SymbolDefinition symbol)
    {
        var path =
            new StringBuilder();

        foreach (SymbolPrimitive primitive in symbol.Primitives)
        {
            AppendPrimitive(
                path,
                primitive);
        }

        if (path.Length == 0)
        {
            throw new InvalidOperationException(
                $"Symbol '{symbol.Id}' contains no drawable primitives.");
        }

        return StreamGeometry.Parse(
            path.ToString());
    }

    private static void AppendPrimitive(
        StringBuilder output,
        SymbolPrimitive primitive)
    {
        if (output.Length > 0)
        {
            output.Append(' ');
        }

        switch (primitive)
        {
            case LineSymbolPrimitive line:
                MoveTo(output, line.Start);
                output.Append(" L ");
                Point(output, line.End);
                break;

            case PolylineSymbolPrimitive polyline:
                if (polyline.Points.Count < 2)
                {
                    throw new InvalidOperationException(
                        "Polyline symbol primitive requires at least two points.");
                }

                MoveTo(
                    output,
                    polyline.Points[0]);
                for (int index = 1;
                     index < polyline.Points.Count;
                     index++)
                {
                    output.Append(" L ");
                    Point(
                        output,
                        polyline.Points[index]);
                }

                break;

            case RectangleSymbolPrimitive rectangle:
                double x =
                    rectangle.Rectangle.X;
                double y =
                    rectangle.Rectangle.Y;
                double right =
                    rectangle.Rectangle.Right;
                double bottom =
                    rectangle.Rectangle.Bottom;
                output.Append("M ");
                Number(output, x);
                output.Append(' ');
                Number(output, y);
                output.Append(" L ");
                Number(output, right);
                output.Append(' ');
                Number(output, y);
                output.Append(" L ");
                Number(output, right);
                output.Append(' ');
                Number(output, bottom);
                output.Append(" L ");
                Number(output, x);
                output.Append(' ');
                Number(output, bottom);
                output.Append(" Z");
                break;

            case CircleSymbolPrimitive circle:
                double left =
                    circle.Center.X - circle.Radius;
                double rightCircle =
                    circle.Center.X + circle.Radius;
                MoveTo(
                    output,
                    new Domain.Scene.MmPoint(
                        rightCircle,
                        circle.Center.Y));
                AppendArc(
                    output,
                    circle.Radius,
                    largeArc: true,
                    sweep: true,
                    new Domain.Scene.MmPoint(
                        left,
                        circle.Center.Y));
                AppendArc(
                    output,
                    circle.Radius,
                    largeArc: true,
                    sweep: true,
                    new Domain.Scene.MmPoint(
                        rightCircle,
                        circle.Center.Y));
                output.Append(" Z");
                break;

            case ArcSymbolPrimitive arc:
                double startRadians =
                    arc.StartDegrees *
                    Math.PI /
                    180.0;
                double endRadians =
                    (arc.StartDegrees + arc.SweepDegrees) *
                    Math.PI /
                    180.0;
                var start =
                    new Domain.Scene.MmPoint(
                        arc.Center.X +
                            (arc.Radius *
                             Math.Cos(startRadians)),
                        arc.Center.Y +
                            (arc.Radius *
                             Math.Sin(startRadians)));
                var end =
                    new Domain.Scene.MmPoint(
                        arc.Center.X +
                            (arc.Radius *
                             Math.Cos(endRadians)),
                        arc.Center.Y +
                            (arc.Radius *
                             Math.Sin(endRadians)));

                MoveTo(
                    output,
                    start);
                AppendArc(
                    output,
                    arc.Radius,
                    Math.Abs(arc.SweepDegrees) > 180,
                    arc.SweepDegrees >= 0,
                    end);
                break;

            case PathSymbolPrimitive path:
                output.Append(path.Data);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported symbol primitive '{primitive.GetType().FullName}'.");
        }
    }

    private static void MoveTo(
        StringBuilder output,
        Domain.Scene.MmPoint point)
    {
        output.Append("M ");
        Point(
            output,
            point);
    }

    private static void AppendArc(
        StringBuilder output,
        double radius,
        bool largeArc,
        bool sweep,
        Domain.Scene.MmPoint end)
    {
        output.Append(" A ");
        Number(output, radius);
        output.Append(' ');
        Number(output, radius);
        output
            .Append(" 0 ")
            .Append(largeArc ? '1' : '0')
            .Append(' ')
            .Append(sweep ? '1' : '0')
            .Append(' ');
        Point(
            output,
            end);
    }

    private static void Point(
        StringBuilder output,
        Domain.Scene.MmPoint point)
    {
        Number(output, point.X);
        output.Append(' ');
        Number(output, point.Y);
    }

    private static void Number(
        StringBuilder output,
        double value) =>
        output.Append(
            value.ToString(
                "R",
                CultureInfo.InvariantCulture));

    private readonly record struct SymbolCacheKey(
        string ProfileFingerprint,
        string SymbolId,
        string Variant);
}
