using System.Globalization;
using Avalonia;
using Avalonia.Media;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Symbols;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Rendering.Avalonia.Viewport;

namespace UI_Unilineal.Rendering.Avalonia.Rendering;

public sealed class AvaloniaSceneRenderer
{
    public IReadOnlyList<SceneElement> BuildRenderList(
        ViewportState viewport,
        SceneSpatialIndex index)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(index);

        var viewportDip = new Rect(
            0,
            0,
            viewport.ViewportDip.Width,
            viewport.ViewportDip.Height);
        MmRect viewportMm =
            ViewportTransform.DipRectToSceneMm(
                viewportDip,
                viewport);

        return index.Query(viewportMm)
            .Where(element =>
                element.Visibility != SceneVisibility.Print)
            .OrderBy(element => element.Layer)
            .ThenBy(element => element.ZIndex)
            .ThenBy(
                element => element.Id.Value,
                StringComparer.Ordinal)
            .ToArray();
    }

    public void Render(
        DrawingContext context,
        DiagramScene scene,
        RIC18DrawingProfile profile,
        ViewportState viewport,
        SceneSpatialIndex index,
        AvaloniaRenderResources resources)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(resources);

        if (!ReferenceEquals(scene, index.Scene))
        {
            throw new ArgumentException(
                "Spatial index must belong to the rendered scene.",
                nameof(index));
        }

        string expectedProfileFingerprint =
            DrawingProfileFingerprint.Compute(profile);

        if (!string.Equals(
                expectedProfileFingerprint,
                resources.ProfileFingerprint,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Render resources must belong to the supplied drawing profile.",
                nameof(resources));
        }

        IReadOnlyList<SceneElement> renderList =
            BuildRenderList(
                viewport,
                index);
        double scale =
            ViewportTransform.Scale(viewport);
        var transform = new Matrix(
            scale,
            0,
            0,
            scale,
            viewport.PanDip.X,
            viewport.PanDip.Y);

        using (context.PushTransform(transform))
        {
            foreach (SceneElement element in renderList)
            {
                DrawElement(
                    context,
                    scene,
                    profile,
                    resources,
                    element);
            }
        }
    }

    private static void DrawElement(
        DrawingContext context,
        DiagramScene scene,
        RIC18DrawingProfile profile,
        AvaloniaRenderResources resources,
        SceneElement element)
    {
        switch (element)
        {
            case LineSceneElement line:
                context.DrawLine(
                    resources.ResolvePen(line.LineStyleId),
                    Point(line.Start),
                    Point(line.End));
                break;

            case PolylineSceneElement polyline:
                DrawPolyline(
                    context,
                    polyline,
                    resources.ResolvePen(polyline.LineStyleId));
                break;

            case RectangleSceneElement rectangle:
                context.DrawRectangle(
                    resources.ResolvePen(rectangle.LineStyleId),
                    Rect(rectangle.Bounds),
                    0);
                break;

            case CircleSceneElement circle:
                context.DrawEllipse(
                    null,
                    resources.ResolvePen(circle.LineStyleId),
                    Point(circle.Center),
                    circle.RadiusMm,
                    circle.RadiusMm);
                break;

            case PathSceneElement path:
                context.DrawGeometry(
                    null,
                    resources.ResolvePen(path.LineStyleId),
                    StreamGeometry.Parse(path.Data));
                break;

            case TextSceneElement text:
                DrawText(
                    context,
                    profile,
                    resources,
                    text);
                break;

            case SymbolSceneElement symbol:
                DrawSymbol(
                    context,
                    scene,
                    profile,
                    resources,
                    symbol);
                break;

            case GroupSceneElement:
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported scene element '{element.GetType().FullName}'.");
        }
    }

    private static void DrawPolyline(
        DrawingContext context,
        PolylineSceneElement polyline,
        Pen pen)
    {
        for (int index = 0;
             index < polyline.Points.Count - 1;
             index++)
        {
            context.DrawLine(
                pen,
                Point(polyline.Points[index]),
                Point(polyline.Points[index + 1]));
        }
    }

    private static void DrawText(
        DrawingContext context,
        RIC18DrawingProfile profile,
        AvaloniaRenderResources resources,
        TextSceneElement text)
    {
        TextStyleDefinition style =
            profile.TextStyles.SingleOrDefault(candidate =>
                string.Equals(
                    candidate.Id,
                    text.TextStyleId,
                    StringComparison.Ordinal))
            ?? throw new KeyNotFoundException(
                $"Drawing profile does not contain text style '{text.TextStyleId}'.");

        IBrush brush =
            resources.ResolveTextBrush(text.TextStyleId);
        var formatted = new FormattedText(
            text.Text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            resources.ResolveTypeface(text.TextStyleId),
            style.HeightMm,
            brush);

        context.DrawText(
            formatted,
            new Point(
                text.Bounds.X,
                text.Bounds.Y));
    }

    private static void DrawSymbol(
        DrawingContext context,
        DiagramScene scene,
        RIC18DrawingProfile profile,
        AvaloniaRenderResources resources,
        SymbolSceneElement symbol)
    {
        if (HasExpandedPrimitives(
                scene,
                symbol))
        {
            return;
        }

        SymbolDefinition definition =
            profile.Symbols.SingleOrDefault(candidate =>
                string.Equals(
                    candidate.Id,
                    symbol.SymbolDefinitionId,
                    StringComparison.Ordinal))
            ?? throw new KeyNotFoundException(
                $"Drawing profile does not contain symbol '{symbol.SymbolDefinitionId}'.");

        SymbolPrimitive firstPrimitive =
            definition.Primitives.FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Symbol '{definition.Id}' contains no drawable primitives.");
        Pen pen =
            resources.ResolvePen(firstPrimitive.LineStyleId);
        Geometry geometry =
            resources.ResolveSymbolGeometry(
                symbol.SymbolDefinitionId);

        double scaleX =
            symbol.Bounds.Width /
            definition.NominalBounds.Width;
        double scaleY =
            symbol.Bounds.Height /
            definition.NominalBounds.Height;
        double offsetX =
            symbol.Bounds.X -
            (definition.NominalBounds.X * scaleX);
        double offsetY =
            symbol.Bounds.Y -
            (definition.NominalBounds.Y * scaleY);
        Matrix symbolTransform = new(
            scaleX,
            0,
            0,
            scaleY,
            offsetX,
            offsetY);

        if (Math.Abs(symbol.RotationDegrees) > double.Epsilon)
        {
            var center = new Point(
                symbol.Bounds.X +
                    (symbol.Bounds.Width / 2.0),
                symbol.Bounds.Y +
                    (symbol.Bounds.Height / 2.0));
            symbolTransform *=
                Matrix.CreateRotation(
                    symbol.RotationDegrees *
                    Math.PI /
                    180.0,
                    center);
        }

        using (context.PushTransform(symbolTransform))
        {
            context.DrawGeometry(
                null,
                pen,
                geometry);
        }
    }

    private static bool HasExpandedPrimitives(
        DiagramScene scene,
        SymbolSceneElement symbol)
    {
        if (!symbol.Metadata.TryGetValue(
                "partId",
                out string? partId) ||
            !symbol.Metadata.TryGetValue(
                "symbolId",
                out string? symbolId))
        {
            return false;
        }

        return scene.Elements.Any(element =>
            element.Id != symbol.Id &&
            element is LineSceneElement or
                PolylineSceneElement or
                RectangleSceneElement or
                CircleSceneElement or
                PathSceneElement &&
            element.Metadata.TryGetValue(
                "partId",
                out string? candidatePartId) &&
            element.Metadata.TryGetValue(
                "symbolId",
                out string? candidateSymbolId) &&
            string.Equals(
                candidatePartId,
                partId,
                StringComparison.Ordinal) &&
            string.Equals(
                candidateSymbolId,
                symbolId,
                StringComparison.Ordinal));
    }

    private static Point Point(
        MmPoint point) =>
        new(
            point.X,
            point.Y);

    private static Rect Rect(
        MmRect bounds) =>
        new(
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height);
}
