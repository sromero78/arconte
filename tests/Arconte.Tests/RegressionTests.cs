using System.IO;
using System.IO.Compression;
using System.Text;
using Arconte.App.Models;
using Arconte.App.Services;
using Xunit;

namespace Arconte.Tests;

public sealed class RegressionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "arconte_" + Guid.NewGuid().ToString("N"));
    public RegressionTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, true);

    [Theory]
    [InlineData("20240229", "20240229")]
    [InlineData("20230229", "")]
    [InlineData("20241301", "")]
    [InlineData("00000000", "")]
    public void ValidaCalendario(string input, string expected)
        => Assert.Equal(expected, NameBuilder.NormalizeFecha(input));

    [Fact]
    public void SufijosSuperanCien()
        => Assert.Equal("doc _101.txt", NameBuilder.SufijoColision("doc _100.txt"));

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("42")]
    public void JsonNoObjetoNoRompeAnalisis(string json)
        => Assert.Equal(new DocumentProposal("", "", "", ""), OllamaAnalyzer.ParsearPropuesta(json, new Profile()));

    [Fact]
    public void CsvEscapaFormulasYComillas()
    {
        var path = Path.Combine(_dir, "out.csv");
        CsvExporter.Exportar(path, new[] { new CsvExporter.Fila("=1+1", "ruta,uno", "@formula", "ruta\"dos", "OK") }, DateTime.Today);
        var csv = File.ReadAllText(path);
        Assert.Contains("'=1+1", csv);
        Assert.Contains("'@formula", csv);
        Assert.Contains("\"ruta,uno\"", csv);
        Assert.Contains("\"ruta\"\"dos\"", csv);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExtraeTxtSinModificarOriginal(bool utf16)
    {
        var path = Path.Combine(_dir, "texto.txt");
        File.WriteAllText(path, "Factura sintética: año 2024, importe 123", utf16 ? Encoding.Unicode : new UTF8Encoding(false));
        var before = File.ReadAllBytes(path);
        var result = await new DocumentTextExtractor().ExtractAsync(path, CancellationToken.None);
        Assert.Equal(ExtractionMethod.Directo, result.Metodo);
        Assert.Contains("Factura sintética", result.Texto);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(".odt")]
    [InlineData(".ods")]
    public async Task ExtraeOdfSinteticoSinModificarOriginal(string extension)
    {
        var path = Path.Combine(_dir, "documento" + extension);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(zip.CreateEntry("content.xml").Open()))
            writer.Write("<document><p>Contenido sintético</p></document>");
        var before = File.ReadAllBytes(path);
        var result = await new DocumentTextExtractor().ExtractAsync(path, CancellationToken.None);
        Assert.Contains("Contenido sintético", result.Texto);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task ExtraeXlsSinteticoSinModificarOriginal()
    {
        var path = Path.Combine(_dir, "libro.xls");
        using (var book = new NPOI.HSSF.UserModel.HSSFWorkbook())
        {
            book.CreateSheet("Prueba").CreateRow(0).CreateCell(0).SetCellValue("Factura sintética");
            using var stream = File.Create(path);
            book.Write(stream);
        }
        var before = File.ReadAllBytes(path);
        var result = await new DocumentTextExtractor().ExtractAsync(path, CancellationToken.None);
        Assert.Equal(ExtractionMethod.Directo, result.Metodo);
        Assert.Contains("Factura sintética", result.Texto);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task LotesConsecutivosConservanContenidoYRegistros()
    {
        var service = new RenameService(Path.Combine(_dir, "logs"));
        for (int n = 0; n < 2; n++)
        {
            var path = Path.Combine(_dir, $"original{n}.txt");
            File.WriteAllText(path, $"contenido sintético {n}");
            var batch = await service.ApplyAsync(new[] { new DocumentItem { OriginalPath = path, ProposedName = $"nuevo{n}.txt" } }, CancellationToken.None);
            Assert.Equal($"contenido sintético {n}", File.ReadAllText(Assert.Single(batch.Entradas).NewPath));
        }
        Assert.Equal(2, Directory.GetFiles(Path.Combine(_dir, "logs"), "lote_*.json").Length);
        Assert.Equal(1, (await service.RollbackLastAsync(CancellationToken.None)).Revertidos);
        Assert.Equal(1, (await service.RollbackLastAsync(CancellationToken.None)).Revertidos);
        Assert.True(File.Exists(Path.Combine(_dir, "original0.txt")));
        Assert.True(File.Exists(Path.Combine(_dir, "original1.txt")));
    }
}
