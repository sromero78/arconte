using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using RenombradorDocumental.App.Models;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace RenombradorDocumental.App.Services;

public sealed class DocumentTextExtractor : IDocumentTextExtractor
{
    static DocumentTextExtractor()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public Task<ExtractionResult> ExtractAsync(string path, CancellationToken ct)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        try
        {
            return ext switch
            {
                ".txt" => Task.FromResult(ExtraerTxt(path)),
                ".pdf" => ExtraerPdf(path, ct),
                ".docx" => Task.FromResult(ExtraerDocx(path)),
                ".xlsx" => Task.FromResult(ExtraerXlsx(path)),
                ".doc" => Task.FromResult(ExtraerDoc(path)),
                ".xls" => Task.FromResult(ExtraerXls(path)),
                ".odt" or ".ods" => Task.FromResult(ExtraerOdf(path)),
                ".jpg" or ".jpeg" or ".png" or ".tif" or ".tiff" => ExtraerImagen(path, ct),
                _ => Task.FromResult(new ExtractionResult("", ExtractionMethod.Metadatos, $"Extensión no soportada: {ext}")),
            };
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ExtractionResult("", ExtractionMethod.Metadatos, $"Error al extraer texto: {ex.Message}"));
        }
    }

    private static ExtractionResult ExtraerTxt(string path)
    {
        var bytes = File.ReadAllBytes(path);
        Encoding enc;
        int inicio = 0;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        { enc = Encoding.UTF8; inicio = 3; }
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        { enc = Encoding.Unicode; inicio = 2; }
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        { enc = Encoding.BigEndianUnicode; inicio = 2; }
        else
        {
            try
            {
                var utf8 = new UTF8Encoding(false, true);
                return new ExtractionResult(utf8.GetString(bytes), ExtractionMethod.Directo);
            }
            catch (DecoderFallbackException)
            {
                return new ExtractionResult(Encoding.GetEncoding(1252).GetString(bytes), ExtractionMethod.Directo);
            }
        }
        return new ExtractionResult(enc.GetString(bytes, inicio, bytes.Length - inicio), ExtractionMethod.Directo);
    }

    private async Task<ExtractionResult> ExtraerPdf(string path, CancellationToken ct)
    {
        string texto = "";
        string? advertencia = null;
        try
        {
            var sb = new StringBuilder();
            using (var pdf = UglyToad.PdfPig.PdfDocument.Open(path))
            {
                foreach (var pagina in pdf.GetPages())
                    sb.AppendLine(ContentOrderTextExtractor.GetText(pagina));
            }
            texto = sb.ToString().Trim();
        }
        catch (Exception ex)
        {
            advertencia = $"PDF no legible directamente: {ex.Message}";
        }

        if (texto.Length >= 40)
            return new ExtractionResult(texto, ExtractionMethod.Directo, advertencia);

        var ocr = await OcrPdfAsync(path, ct).ConfigureAwait(false);
        if (ocr != null)
            return ocr;

        var msg = "PDF sin texto suficiente y OCR no disponible (faltan pdftoppm/tesseract)";
        if (advertencia != null) msg = advertencia + "; " + msg;
        return new ExtractionResult(texto, ExtractionMethod.Metadatos, msg);
    }

    private static async Task<ExtractionResult?> OcrPdfAsync(string path, CancellationToken ct)
    {
        var pdftoppm = ToolLocator.FindPdftoppm();
        var tesseract = ToolLocator.FindTesseract();
        if (pdftoppm == null || tesseract == null) return null;

        var tempDir = Path.Combine(Path.GetTempPath(), "renombrador_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var prefijo = Path.Combine(tempDir, "pagina");
            await RunProcessAsync(pdftoppm, new[] { "-png", "-r", "200", path, prefijo }, ct).ConfigureAwait(false);
            var sb = new StringBuilder();
            foreach (var img in Directory.EnumerateFiles(tempDir, "pagina-*.png").Take(10))
            {
                var salida = await RunProcessCaptureAsync(tesseract, new[] { img, "stdout", "-l", "spa" }, ct).ConfigureAwait(false);
                sb.AppendLine(salida);
            }
            return new ExtractionResult(sb.ToString().Trim(), ExtractionMethod.OCR);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static async Task<ExtractionResult> ExtraerImagen(string path, CancellationToken ct)
    {
        var tesseract = ToolLocator.FindTesseract();
        if (tesseract == null)
            return new ExtractionResult("", ExtractionMethod.Metadatos, "Tesseract no encontrado; instálelo o agréguelo al PATH para OCR de imágenes");
        var salida = await RunProcessCaptureAsync(tesseract, new[] { path, "stdout", "-l", "spa" }, ct).ConfigureAwait(false);
        return new ExtractionResult(salida.Trim(), ExtractionMethod.OCR);
    }

    private static ExtractionResult ExtraerDocx(string path)
    {
        using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(path, false);
        var cuerpo = doc.MainDocumentPart?.Document?.Body;
        var texto = cuerpo?.InnerText ?? "";
        return new ExtractionResult(texto, ExtractionMethod.Directo,
            texto.Length == 0 ? "Documento sin texto extraíble" : null);
    }

    private static ExtractionResult ExtraerXlsx(string path)
    {
        var sb = new StringBuilder();
        using var doc = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(path, false);
        var wbPart = doc.WorkbookPart;
        if (wbPart != null)
        {
            var tabla = wbPart.SharedStringTablePart?.SharedStringTable;
            foreach (var hoja in wbPart.WorksheetParts)
            {
                foreach (var celda in hoja.Worksheet.Descendants<DocumentFormat.OpenXml.Spreadsheet.Cell>())
                {
                    var valor = celda.CellValue?.Text;
                    if (valor == null) continue;
                    if (celda.DataType?.Value == DocumentFormat.OpenXml.Spreadsheet.CellValues.SharedString
                        && tabla != null && int.TryParse(valor, out var idx) && idx < tabla.ChildElements.Count)
                        sb.AppendLine(tabla.ChildElements[idx].InnerText);
                    else
                        sb.AppendLine(valor);
                }
            }
        }
        return new ExtractionResult(sb.ToString(), ExtractionMethod.Directo);
    }

    private static ExtractionResult ExtraerXls(string path)
    {
        var sb = new StringBuilder();
        using var fs = File.OpenRead(path);
        var wb = new NPOI.HSSF.UserModel.HSSFWorkbook(fs);
        for (int i = 0; i < wb.NumberOfSheets; i++)
        {
            var hoja = wb.GetSheetAt(i);
            if (hoja == null) continue;
            for (int f = hoja.FirstRowNum; f <= hoja.LastRowNum; f++)
            {
                var fila = hoja.GetRow(f);
                if (fila == null) continue;
                foreach (var celda in fila.Cells)
                    sb.Append(celda.ToString()).Append(' ');
                sb.AppendLine();
            }
        }
        return new ExtractionResult(sb.ToString(), ExtractionMethod.Directo);
    }

    private static ExtractionResult ExtraerDoc(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var sb = new StringBuilder();

        var run = new StringBuilder();
        for (int i = 0; i + 1 < bytes.Length; i += 2)
        {
            var c = (char)(bytes[i] | (bytes[i + 1] << 8));
            if (c >= 0x20 && c != 0x7F && (char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c)))
                run.Append(c);
            else
            {
                if (run.Length >= 8) sb.AppendLine(run.ToString());
                run.Clear();
            }
        }
        if (run.Length >= 8) sb.AppendLine(run.ToString());

        run.Clear();
        foreach (var b in bytes)
        {
            if (b >= 0x20 && b < 0x7F) run.Append((char)b);
            else
            {
                if (run.Length >= 12) sb.AppendLine(run.ToString());
                run.Clear();
            }
        }
        if (run.Length >= 12) sb.AppendLine(run.ToString());

        var texto = sb.ToString();
        return new ExtractionResult(texto, ExtractionMethod.Directo,
            texto.Length < 40 ? "Extracción parcial de .doc binario" : null);
    }

    private static ExtractionResult ExtraerOdf(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var entrada = zip.GetEntry("content.xml");
        if (entrada == null)
            return new ExtractionResult("", ExtractionMethod.Metadatos, "content.xml no encontrado en el paquete ODF");
        var sb = new StringBuilder();
        using var stream = entrada.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { IgnoreWhitespace = false });
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Text)
                sb.Append(reader.Value);
            else if (reader.NodeType == XmlNodeType.Element &&
                     (reader.LocalName is "p" or "h" or "line-break" or "table-row"))
                sb.AppendLine();
        }
        return new ExtractionResult(sb.ToString(), ExtractionMethod.Directo);
    }

    private static ProcessStartInfo CrearStartInfo(string exe, IEnumerable<string> args)
    {
        var si = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) si.ArgumentList.Add(a);
        return si;
    }

    private static async Task<int> RunProcessAsync(string exe, IEnumerable<string> args, CancellationToken ct)
    {
        var si = CrearStartInfo(exe, args);
        si.RedirectStandardOutput = true;
        si.RedirectStandardError = true;
        using var p = new Process { StartInfo = si };
        p.Start();
        var tOut = p.StandardOutput.ReadToEndAsync(ct);
        var tErr = p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct).ConfigureAwait(false);
        var salida = await tOut.ConfigureAwait(false);
        var error = await tErr.ConfigureAwait(false);
        if (p.ExitCode != 0 && salida.Length == 0)
            throw new InvalidOperationException(
                $"{Path.GetFileName(exe)} falló (código {p.ExitCode}): {Recortar(error)}");
        return p.ExitCode;
    }

    private static async Task<string> RunProcessCaptureAsync(string exe, IEnumerable<string> args, CancellationToken ct)
    {
        var si = CrearStartInfo(exe, args);
        si.RedirectStandardOutput = true;
        si.RedirectStandardError = true;
        si.StandardOutputEncoding = Encoding.UTF8;
        using var p = new Process { StartInfo = si };
        p.Start();
        var tOut = p.StandardOutput.ReadToEndAsync(ct);
        var tErr = p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct).ConfigureAwait(false);
        var salida = await tOut.ConfigureAwait(false);
        var error = await tErr.ConfigureAwait(false);
        if (p.ExitCode != 0 && salida.Length == 0)
            throw new InvalidOperationException(
                $"{Path.GetFileName(exe)} falló (código {p.ExitCode}): {Recortar(error)}");
        return salida;
    }

    private static string Recortar(string s)
        => s.Length > 500 ? s[..500] : s;
}
