using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Documents;

namespace UI_Unilineal.Export.Pdf;

public sealed class PdfExporter
{
    private const double PointsPerMillimeter = 72d / 25.4d;
    private const double CircleKappa = 0.5522847498307936d;

    private static readonly Encoding Ascii = Encoding.ASCII;

    private static readonly Regex PathTokenPattern = new(
        @"[A-Za-z]|[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?",
        RegexOptions.CultureInvariant);

    public async ValueTask ExportAsync(
        DrawingDocument document,
        ResolvedDrawingStyleSet styles,
        Stream destination,
        PdfExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(styles);
        ArgumentNullException.ThrowIfNull(destination);

        if (!destination.CanWrite)
        {
            throw new ArgumentException(
                "Destination stream must be writable.",
                nameof(destination));
        }

        options ??= new PdfExportOptions();
        cancellationToken.ThrowIfCancellationRequested();

        ExportPreflightResult preflight =
            new DocumentPreflight().Validate(
                document,
                styles,
                options.Preflight);

        if (preflight.HasErrors)
        {
            string diagnostics = string.Join(
                "; ",
                preflight.Issues.Select(issue =>
                    $"{issue.Code}: {issue.Message}"));

            throw new InvalidOperationException(
                $"PDF export preflight failed. {diagnostics}");
        }

        byte[] bytes = BuildDocument(
            document,
            styles,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        await destination.WriteAsync(
            bytes.AsMemory(),
            cancellationToken);
    }

    private static byte[] BuildDocument(
        DrawingDocument document,
        ResolvedDrawingStyleSet styles,
        CancellationToken cancellationToken)
    {
        DrawingSheet[] sheets = document.Sheets
            .OrderBy(sheet => sheet.SheetNumber)
            .ToArray();

        string[] contents = sheets
            .Select(sheet => BuildPageContent(
                sheet,
                styles,
                cancellationToken))
            .ToArray();

        var objects = new List<string>(4 + (sheets.Length * 2))
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            BuildPagesObject(sheets.Length),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>"
        };

        for (int index = 0; index < sheets.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int pageObjectNumber = 5 + (index * 2);
            int contentObjectNumber = pageObjectNumber + 1;
            DrawingSheet sheet = sheets[index];
            string content = contents[index];

            objects.Add(BuildPageObject(
                sheet,
                contentObjectNumber));
            objects.Add(
                $"<< /Length {Ascii.GetByteCount(content)} >>\n" +
                "stream\n" +
                content +
                "endstream");
        }

        return WritePdf(objects, cancellationToken);
    }

    private static string BuildPagesObject(int sheetCount)
    {
        string kids = string.Join(
            " ",
            Enumerable.Range(0, sheetCount)
                .Select(index => $"{5 + (index * 2)} 0 R"));

        return
            $"<< /Type /Pages /Count {sheetCount} /Kids [{kids}] >>";
    }

    private static string BuildPageObject(
        DrawingSheet sheet,
        int contentObjectNumber)
    {
        double width = sheet.Paper.WidthMm * PointsPerMillimeter;
        double height = sheet.Paper.HeightMm * PointsPerMillimeter;

        return
            "<< /Type /Page /Parent 2 0 R " +
            $"/MediaBox [0 0 {Number(width)} {Number(height)}] " +
            "/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> " +
            $"/Contents {contentObjectNumber} 0 R >>";
    }

    private static byte[] WritePdf(
        IReadOnlyList<string> objects,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        WriteAscii(stream, "%PDF-1.7\n%UI-Unilineal\n");

        var offsets = new List<long>(objects.Count + 1)
        {
            0
        };

        for (int index = 0; index < objects.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            offsets.Add(stream.Position);

            int objectNumber = index + 1;
            WriteAscii(
                stream,
                $"{objectNumber} 0 obj\n{objects[index]}\nendobj\n");
        }

        long xrefOffset = stream.Position;
        WriteAscii(
            stream,
            $"xref\n0 {objects.Count + 1}\n");
        WriteAscii(stream, "0000000000 65535 f \n");

        for (int objectNumber = 1;
             objectNumber <= objects.Count;
             objectNumber++)
        {
            WriteAscii(
                stream,
                offsets[objectNumber].ToString(
                    "D10",
                    CultureInfo.InvariantCulture) +
                " 00000 n \n");
        }

        WriteAscii(
            stream,
            "trailer\n" +
            $"<< /Size {objects.Count + 1} /Root 1 0 R >>\n" +
            "startxref\n" +
            $"{xrefOffset}\n" +
            "%%EOF\n");

        return stream.ToArray();
    }

    private static string BuildPageContent(
        DrawingSheet sheet,
        ResolvedDrawingStyleSet styles,
        CancellationToken cancellationToken)
    {
        var output = new StringBuilder(4096);
        var mapper = new PageMapper(sheet);

        PdfRect clip = mapper.MapRect(sheet.ViewBox);
        output.Append("q\n");
        AppendRectPath(output, clip);
        output.Append("W\nn\n");

        foreach (SceneElement element in sheet.Scene.Elements
                     .Where(IsPrintable)
                     .OrderBy(value => value.Layer)
                     .ThenBy(value => value.ZIndex)
                     .ThenBy(value => value.Id.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendElement(
                output,
                element,
                styles,
                mapper);
        }

        output.Append("Q\n");
        return output.ToString();
    }

    private static void AppendElement(
        StringBuilder output,
        SceneElement element,
        ResolvedDrawingStyleSet styles,
        PageMapper mapper)
    {
        switch (element)
        {
            case LineSceneElement line:
                AppendStrokeStyle(
                    output,
                    styles.ResolveLine(line.LineStyleId));
                AppendLine(output, line, mapper);
                break;

            case PolylineSceneElement polyline:
                AppendStrokeStyle(
                    output,
                    styles.ResolveLine(polyline.LineStyleId));
                AppendPolyline(output, polyline, mapper);
                break;

            case RectangleSceneElement rectangle:
                AppendStrokeStyle(
                    output,
                    styles.ResolveLine(rectangle.LineStyleId));
                AppendRectangle(output, rectangle, mapper);
                break;

            case CircleSceneElement circle:
                AppendStrokeStyle(
                    output,
                    styles.ResolveLine(circle.LineStyleId));
                AppendCircle(output, circle, mapper);
                break;

            case PathSceneElement path:
                AppendStrokeStyle(
                    output,
                    styles.ResolveLine(path.LineStyleId));
                AppendPath(output, path, mapper);
                break;

            case TextSceneElement text:
                AppendText(
                    output,
                    text,
                    styles.ResolveText(text.TextStyleId),
                    mapper);
                break;

            case SymbolSceneElement:
            case GroupSceneElement:
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported scene element '{element.GetType().FullName}'.");
        }
    }

    private static void AppendLine(
        StringBuilder output,
        LineSceneElement line,
        PageMapper mapper)
    {
        PdfPoint start = mapper.MapPoint(line.Start);
        PdfPoint end = mapper.MapPoint(line.End);

        AppendPointOperator(output, start, "m");
        AppendPointOperator(output, end, "l");
        output.Append("S\n");
    }

    private static void AppendPolyline(
        StringBuilder output,
        PolylineSceneElement polyline,
        PageMapper mapper)
    {
        PdfPoint first = mapper.MapPoint(polyline.Points[0]);
        AppendPointOperator(output, first, "m");

        for (int index = 1; index < polyline.Points.Count; index++)
        {
            AppendPointOperator(
                output,
                mapper.MapPoint(polyline.Points[index]),
                "l");
        }

        output.Append("S\n");
    }

    private static void AppendRectangle(
        StringBuilder output,
        RectangleSceneElement rectangle,
        PageMapper mapper)
    {
        AppendRectPath(
            output,
            mapper.MapRect(rectangle.Bounds));
        output.Append("S\n");
    }

    private static void AppendCircle(
        StringBuilder output,
        CircleSceneElement circle,
        PageMapper mapper)
    {
        PdfPoint center = mapper.MapPoint(circle.Center);
        double radiusX = mapper.MapWidth(circle.RadiusMm);
        double radiusY = mapper.MapHeight(circle.RadiusMm);
        double controlX = radiusX * CircleKappa;
        double controlY = radiusY * CircleKappa;

        AppendPointOperator(
            output,
            new PdfPoint(center.X + radiusX, center.Y),
            "m");

        AppendCurve(
            output,
            center.X + radiusX,
            center.Y + controlY,
            center.X + controlX,
            center.Y + radiusY,
            center.X,
            center.Y + radiusY);
        AppendCurve(
            output,
            center.X - controlX,
            center.Y + radiusY,
            center.X - radiusX,
            center.Y + controlY,
            center.X - radiusX,
            center.Y);
        AppendCurve(
            output,
            center.X - radiusX,
            center.Y - controlY,
            center.X - controlX,
            center.Y - radiusY,
            center.X,
            center.Y - radiusY);
        AppendCurve(
            output,
            center.X + controlX,
            center.Y - radiusY,
            center.X + radiusX,
            center.Y - controlY,
            center.X + radiusX,
            center.Y);

        output.Append("S\n");
    }

    private static void AppendPath(
        StringBuilder output,
        PathSceneElement path,
        PageMapper mapper)
    {
        string remainder = PathTokenPattern
            .Replace(path.Data, string.Empty)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (remainder.Length > 0)
        {
            throw new InvalidOperationException(
                $"Unsupported PDF path syntax in '{path.Id.Value}'.");
        }

        string[] tokens = PathTokenPattern
            .Matches(path.Data)
            .Select(match => match.Value)
            .ToArray();

        int index = 0;
        char command = '\0';
        double currentX = 0;
        double currentY = 0;
        double subpathX = 0;
        double subpathY = 0;

        while (index < tokens.Length)
        {
            if (IsCommand(tokens[index]))
            {
                command = tokens[index][0];
                index++;
            }
            else if (command == '\0')
            {
                throw new InvalidOperationException(
                    $"Path '{path.Id.Value}' does not begin with a command.");
            }

            bool relative = char.IsLower(command);

            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                    {
                        double x = ReadNumber(tokens, ref index);
                        double y = ReadNumber(tokens, ref index);
                        ResolvePoint(
                            relative,
                            ref x,
                            ref y,
                            currentX,
                            currentY);

                        currentX = x;
                        currentY = y;
                        subpathX = x;
                        subpathY = y;
                        AppendPointOperator(
                            output,
                            mapper.MapPoint(new MmPoint(x, y)),
                            "m");
                        command = relative ? 'l' : 'L';
                        break;
                    }

                case 'L':
                    {
                        double x = ReadNumber(tokens, ref index);
                        double y = ReadNumber(tokens, ref index);
                        ResolvePoint(
                            relative,
                            ref x,
                            ref y,
                            currentX,
                            currentY);

                        currentX = x;
                        currentY = y;
                        AppendPointOperator(
                            output,
                            mapper.MapPoint(new MmPoint(x, y)),
                            "l");
                        break;
                    }

                case 'H':
                    {
                        double x = ReadNumber(tokens, ref index);
                        if (relative)
                        {
                            x += currentX;
                        }

                        currentX = x;
                        AppendPointOperator(
                            output,
                            mapper.MapPoint(new MmPoint(
                                currentX,
                                currentY)),
                            "l");
                        break;
                    }

                case 'V':
                    {
                        double y = ReadNumber(tokens, ref index);
                        if (relative)
                        {
                            y += currentY;
                        }

                        currentY = y;
                        AppendPointOperator(
                            output,
                            mapper.MapPoint(new MmPoint(
                                currentX,
                                currentY)),
                            "l");
                        break;
                    }

                case 'A':
                    {
                        double radiusX = ReadNumber(tokens, ref index);
                        double radiusY = ReadNumber(tokens, ref index);
                        double rotationDegrees = ReadNumber(tokens, ref index);
                        bool largeArc = ReadArcFlag(tokens, ref index);
                        bool sweep = ReadArcFlag(tokens, ref index);
                        double x = ReadNumber(tokens, ref index);
                        double y = ReadNumber(tokens, ref index);

                        ResolvePoint(
                            relative,
                            ref x,
                            ref y,
                            currentX,
                            currentY);

                        AppendCircularArc(
                            output,
                            mapper,
                            new MmPoint(currentX, currentY),
                            new MmPoint(x, y),
                            radiusX,
                            radiusY,
                            rotationDegrees,
                            largeArc,
                            sweep,
                            path.Id);

                        currentX = x;
                        currentY = y;
                        break;
                    }

                case 'C':
                    {
                        double x1 = ReadNumber(tokens, ref index);
                        double y1 = ReadNumber(tokens, ref index);
                        double x2 = ReadNumber(tokens, ref index);
                        double y2 = ReadNumber(tokens, ref index);
                        double x = ReadNumber(tokens, ref index);
                        double y = ReadNumber(tokens, ref index);

                        ResolvePoint(
                            relative,
                            ref x1,
                            ref y1,
                            currentX,
                            currentY);
                        ResolvePoint(
                            relative,
                            ref x2,
                            ref y2,
                            currentX,
                            currentY);
                        ResolvePoint(
                            relative,
                            ref x,
                            ref y,
                            currentX,
                            currentY);

                        PdfPoint control1 =
                            mapper.MapPoint(new MmPoint(x1, y1));
                        PdfPoint control2 =
                            mapper.MapPoint(new MmPoint(x2, y2));
                        PdfPoint end =
                            mapper.MapPoint(new MmPoint(x, y));

                        AppendCurve(
                            output,
                            control1.X,
                            control1.Y,
                            control2.X,
                            control2.Y,
                            end.X,
                            end.Y);

                        currentX = x;
                        currentY = y;
                        break;
                    }

                case 'Z':
                    output.Append("h\n");
                    currentX = subpathX;
                    currentY = subpathY;
                    command = '\0';
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported PDF path command '{command}' " +
                        $"in '{path.Id.Value}'.");
            }
        }

        output.Append("S\n");
    }

    private static void AppendText(
        StringBuilder output,
        TextSceneElement text,
        ResolvedTextStyle style,
        PageMapper mapper)
    {
        double fontSize = style.HeightMm * PointsPerMillimeter;
        double textWidth =
            EstimateHelveticaTextWidth(
                text.Text,
                fontSize);
        PdfRect bounds =
            mapper.MapRect(text.Bounds);

        double x = text.HorizontalAlignment switch
        {
            SceneTextHorizontalAlignment.Start => bounds.X,
            SceneTextHorizontalAlignment.Center =>
                bounds.X + ((bounds.Width - textWidth) / 2.0),
            SceneTextHorizontalAlignment.End =>
                bounds.X + bounds.Width - textWidth,
            _ => throw new InvalidOperationException(
                $"Unsupported horizontal text alignment '{text.HorizontalAlignment}'.")
        };
        double baselineY = text.VerticalAlignment switch
        {
            SceneTextVerticalAlignment.Top =>
                bounds.Y + bounds.Height - fontSize,
            SceneTextVerticalAlignment.Center =>
                bounds.Y + ((bounds.Height - fontSize) / 2.0),
            SceneTextVerticalAlignment.Bottom =>
                bounds.Y,
            _ => throw new InvalidOperationException(
                $"Unsupported vertical text alignment '{text.VerticalAlignment}'.")
        };
        string font = style.Bold ? "/F2" : "/F1";

        output.Append("0 g\n");
        output.Append("BT\n");
        output
            .Append(font)
            .Append(' ')
            .Append(Number(fontSize))
            .Append(" Tf\n");
        output
            .Append("1 0 0 1 ")
            .Append(Number(x))
            .Append(' ')
            .Append(Number(baselineY))
            .Append(" Tm\n");
        output
            .Append('(')
            .Append(EscapePdfLiteral(text.Text))
            .Append(") Tj\n");
        output.Append("ET\n");
    }

    private static double EstimateHelveticaTextWidth(
        string text,
        double fontSize)
    {
        double emUnits = 0;

        foreach (char character in text)
        {
            emUnits += character switch
            {
                >= '0' and <= '9' => 0.556,
                'I' or 'i' or 'l' or '!' or '.' or ',' or ':' or ';' => 0.278,
                'M' or 'W' or 'm' or 'w' => 0.833,
                ' ' => 0.278,
                _ => 0.556
            };
        }

        return emUnits * fontSize;
    }

    private static void AppendStrokeStyle(
        StringBuilder output,
        ResolvedLineStyle style)
    {
        output.Append("0 G\n");
        output
            .Append(Number(style.WidthMm * PointsPerMillimeter))
            .Append(" w\n");

        switch (style.Pattern)
        {
            case LinePattern.Solid:
                output.Append("[] 0 d\n");
                break;

            case LinePattern.Dashed:
                output
                    .Append('[')
                    .Append(Number(4d * PointsPerMillimeter))
                    .Append(' ')
                    .Append(Number(2d * PointsPerMillimeter))
                    .Append("] 0 d\n");
                break;

            case LinePattern.Dotted:
                output
                    .Append('[')
                    .Append(Number(1d * PointsPerMillimeter))
                    .Append(' ')
                    .Append(Number(2d * PointsPerMillimeter))
                    .Append("] 0 d\n");
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported line pattern '{style.Pattern}'.");
        }
    }

    private static void AppendPointOperator(
        StringBuilder output,
        PdfPoint point,
        string operation)
    {
        output
            .Append(Number(point.X))
            .Append(' ')
            .Append(Number(point.Y))
            .Append(' ')
            .Append(operation)
            .Append('\n');
    }

    private static void AppendRectPath(
        StringBuilder output,
        PdfRect rectangle)
    {
        output
            .Append(Number(rectangle.X))
            .Append(' ')
            .Append(Number(rectangle.Y))
            .Append(' ')
            .Append(Number(rectangle.Width))
            .Append(' ')
            .Append(Number(rectangle.Height))
            .Append(" re\n");
    }

    private static void AppendCurve(
        StringBuilder output,
        double x1,
        double y1,
        double x2,
        double y2,
        double x3,
        double y3)
    {
        output
            .Append(Number(x1))
            .Append(' ')
            .Append(Number(y1))
            .Append(' ')
            .Append(Number(x2))
            .Append(' ')
            .Append(Number(y2))
            .Append(' ')
            .Append(Number(x3))
            .Append(' ')
            .Append(Number(y3))
            .Append(" c\n");
    }

    private static void ResolvePoint(
        bool relative,
        ref double x,
        ref double y,
        double currentX,
        double currentY)
    {
        if (!relative)
        {
            return;
        }

        x += currentX;
        y += currentY;
    }

    private static void AppendCircularArc(
        StringBuilder output,
        PageMapper mapper,
        MmPoint start,
        MmPoint end,
        double radiusX,
        double radiusY,
        double rotationDegrees,
        bool largeArc,
        bool sweep,
        SceneId pathId)
    {
        if (!double.IsFinite(radiusX) ||
            !double.IsFinite(radiusY) ||
            radiusX <= 0 ||
            radiusY <= 0 ||
            Math.Abs(radiusX - radiusY) > 0.000001 ||
            Math.Abs(rotationDegrees) > 0.000001)
        {
            throw new InvalidOperationException(
                $"PDF path '{pathId.Value}' uses an SVG arc outside the supported circular, zero-rotation subset.");
        }

        double dx = (start.X - end.X) / 2.0;
        double dy = (start.Y - end.Y) / 2.0;
        double radius = radiusX;
        double chordSquared = (dx * dx) + (dy * dy);

        if (chordSquared <= 0.000000000001)
        {
            return;
        }

        double lambda = chordSquared / (radius * radius);
        if (lambda > 1)
        {
            radius *= Math.Sqrt(lambda);
        }

        double radiusSquared = radius * radius;
        double numerator =
            Math.Max(
                0,
                radiusSquared - chordSquared);
        double coefficient =
            Math.Sqrt(
                numerator /
                chordSquared);

        if (largeArc == sweep)
        {
            coefficient = -coefficient;
        }

        double centerX =
            ((start.X + end.X) / 2.0) +
            (coefficient * dy);
        double centerY =
            ((start.Y + end.Y) / 2.0) -
            (coefficient * dx);

        double startAngle =
            Math.Atan2(
                start.Y - centerY,
                start.X - centerX);
        double endAngle =
            Math.Atan2(
                end.Y - centerY,
                end.X - centerX);
        double sweepAngle =
            endAngle - startAngle;

        if (sweep && sweepAngle < 0)
        {
            sweepAngle += Math.PI * 2;
        }
        else if (!sweep && sweepAngle > 0)
        {
            sweepAngle -= Math.PI * 2;
        }

        int segmentCount =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    Math.Abs(sweepAngle) /
                    (Math.PI / 2.0)));
        double segmentAngle =
            sweepAngle /
            segmentCount;
        double angle = startAngle;

        for (int segment = 0;
             segment < segmentCount;
             segment++)
        {
            double nextAngle =
                angle + segmentAngle;
            double alpha =
                (4.0 / 3.0) *
                Math.Tan(
                    (nextAngle - angle) /
                    4.0);

            var control1 =
                new MmPoint(
                    centerX +
                    (radius *
                     (Math.Cos(angle) -
                      (alpha * Math.Sin(angle)))),
                    centerY +
                    (radius *
                     (Math.Sin(angle) +
                      (alpha * Math.Cos(angle)))));
            var control2 =
                new MmPoint(
                    centerX +
                    (radius *
                     (Math.Cos(nextAngle) +
                      (alpha * Math.Sin(nextAngle)))),
                    centerY +
                    (radius *
                     (Math.Sin(nextAngle) -
                      (alpha * Math.Cos(nextAngle)))));
            var segmentEnd =
                new MmPoint(
                    centerX +
                    (radius * Math.Cos(nextAngle)),
                    centerY +
                    (radius * Math.Sin(nextAngle)));

            PdfPoint mappedControl1 =
                mapper.MapPoint(control1);
            PdfPoint mappedControl2 =
                mapper.MapPoint(control2);
            PdfPoint mappedEnd =
                mapper.MapPoint(segmentEnd);

            AppendCurve(
                output,
                mappedControl1.X,
                mappedControl1.Y,
                mappedControl2.X,
                mappedControl2.Y,
                mappedEnd.X,
                mappedEnd.Y);

            angle = nextAngle;
        }
    }

    private static bool ReadArcFlag(
        IReadOnlyList<string> tokens,
        ref int index)
    {
        double value =
            ReadNumber(
                tokens,
                ref index);

        return value switch
        {
            0 => false,
            1 => true,
            _ => throw new InvalidOperationException(
                "SVG arc flags must be 0 or 1.")
        };
    }

    private static double ReadNumber(
        IReadOnlyList<string> tokens,
        ref int index)
    {
        if (index >= tokens.Count ||
            IsCommand(tokens[index]) ||
            !double.TryParse(
                tokens[index],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double value) ||
            !double.IsFinite(value))
        {
            throw new InvalidOperationException(
                "Malformed scene path data.");
        }

        index++;
        return value;
    }

    private static bool IsCommand(string token) =>
        token.Length == 1 &&
        char.IsLetter(token[0]);

    private static bool IsPrintable(SceneElement element) =>
        element.Visibility is
            SceneVisibility.Print or
            SceneVisibility.Both;

    private static string EscapePdfLiteral(string value)
    {
        var output = new StringBuilder(value.Length);

        foreach (char character in value)
        {
            switch (character)
            {
                case '\\':
                    output.Append("\\\\");
                    break;
                case '(':
                    output.Append("\\(");
                    break;
                case ')':
                    output.Append("\\)");
                    break;
                case '\n':
                    output.Append("\\n");
                    break;
                case '\r':
                    output.Append("\\r");
                    break;
                case '\t':
                    output.Append("\\t");
                    break;
                default:
                    output.Append(
                        character is >= ' ' and <= '~'
                            ? character
                            : '?');
                    break;
            }
        }

        return output.ToString();
    }

    private static string Number(double value) =>
        value.ToString(
            "0.######",
            CultureInfo.InvariantCulture);

    private static void WriteAscii(
        Stream stream,
        string value)
    {
        byte[] bytes = Ascii.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    private readonly record struct PdfPoint(
        double X,
        double Y);

    private readonly record struct PdfRect(
        double X,
        double Y,
        double Width,
        double Height);

    private sealed class PageMapper
    {
        private readonly DrawingSheet sheet;
        private readonly double scaleX;
        private readonly double scaleY;

        public PageMapper(DrawingSheet sheet)
        {
            this.sheet = sheet;
            scaleX =
                sheet.SceneViewport.Width /
                sheet.ViewBox.Width;
            scaleY =
                sheet.SceneViewport.Height /
                sheet.ViewBox.Height;
        }

        public PdfPoint MapPoint(MmPoint point)
        {
            double pageX =
                sheet.SceneViewport.X +
                ((point.X - sheet.ViewBox.X) * scaleX);
            double pageY =
                sheet.SceneViewport.Y +
                ((point.Y - sheet.ViewBox.Y) * scaleY);

            return new PdfPoint(
                pageX * PointsPerMillimeter,
                (sheet.Paper.HeightMm - pageY) *
                PointsPerMillimeter);
        }

        public double MapWidth(double widthMm) =>
            widthMm * scaleX * PointsPerMillimeter;

        public double MapHeight(double heightMm) =>
            heightMm * scaleY * PointsPerMillimeter;

        public PdfRect MapRect(MmRect rectangle)
        {
            PdfPoint topLeft = MapPoint(
                new MmPoint(
                    rectangle.X,
                    rectangle.Y));
            double width = MapWidth(rectangle.Width);
            double height = MapHeight(rectangle.Height);

            return new PdfRect(
                topLeft.X,
                topLeft.Y - height,
                width,
                height);
        }
    }
}
