using System.IO;
using RenombradorDocumental.App.Models;
using RenombradorDocumental.App.Services;
using Xunit;

namespace RenombradorDocumental.Tests;

public class RenameServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _logs;

    public RenameServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ren_" + Guid.NewGuid().ToString("N"));
        _logs = Path.Combine(_dir, "logs");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private string CrearArchivo(string nombre)
    {
        var path = Path.Combine(_dir, nombre);
        File.WriteAllText(path, "contenido");
        return path;
    }

    [Fact]
    public async Task ColisionesInternasRecibenSufijosIncrementales()
    {
        var a = CrearArchivo("a.txt");
        var b = CrearArchivo("b.txt");
        var svc = new RenameService(_logs);
        var items = new List<DocumentItem>
        {
            new() { OriginalPath = a, ProposedName = "20240101 FAC Luz Iberdrola.txt" },
            new() { OriginalPath = b, ProposedName = "20240101 FAC Luz Iberdrola.txt" },
        };
        var batch = await svc.ApplyAsync(items, CancellationToken.None);

        Assert.Equal(2, batch.Entradas.Count);
        Assert.True(File.Exists(Path.Combine(_dir, "20240101 FAC Luz Iberdrola.txt")));
        Assert.True(File.Exists(Path.Combine(_dir, "20240101 FAC Luz Iberdrola _01.txt")));
    }

    [Fact]
    public async Task ColisionConArchivoExistenteAgregaSufijo()
    {
        CrearArchivo("20240101 FAC Luz Iberdrola.txt");
        var b = CrearArchivo("b.txt");
        var svc = new RenameService(_logs);
        var items = new List<DocumentItem>
        {
            new() { OriginalPath = b, ProposedName = "20240101 FAC Luz Iberdrola.txt" },
        };
        await svc.ApplyAsync(items, CancellationToken.None);
        Assert.True(File.Exists(Path.Combine(_dir, "20240101 FAC Luz Iberdrola _01.txt")));
        Assert.True(File.Exists(Path.Combine(_dir, "20240101 FAC Luz Iberdrola.txt")));
    }

    [Fact]
    public async Task NoRenombraASiMismo()
    {
        var a = CrearArchivo("mismo.txt");
        var svc = new RenameService(_logs);
        var items = new List<DocumentItem> { new() { OriginalPath = a, ProposedName = "mismo.txt" } };
        var batch = await svc.ApplyAsync(items, CancellationToken.None);
        Assert.Empty(batch.Entradas);
        Assert.Equal(AnalysisStatus.Advertencia, items[0].Status);
        Assert.True(File.Exists(a));
    }

    [Fact]
    public async Task RollbackRevierteEnOrdenInversoSinSobreescribir()
    {
        var a = CrearArchivo("a.txt");
        var b = CrearArchivo("b.txt");
        var svc = new RenameService(_logs);
        var items = new List<DocumentItem>
        {
            new() { OriginalPath = a, ProposedName = "n1.txt" },
            new() { OriginalPath = b, ProposedName = "n2.txt" },
        };
        var batch = await svc.ApplyAsync(items, CancellationToken.None);
        Assert.Equal(2, batch.Entradas.Count);

        File.WriteAllText(Path.Combine(_dir, "b.txt"), "ocupado");

        var r = await svc.RollbackLastAsync(CancellationToken.None);
        Assert.Equal(1, r.Revertidos);
        Assert.Single(r.Omitidos);
        Assert.True(File.Exists(a));
        Assert.Equal("ocupado", File.ReadAllText(Path.Combine(_dir, "b.txt")));
        Assert.True(File.Exists(Path.Combine(_dir, "n2.txt")));

        var log = Assert.Single(Directory.EnumerateFiles(_logs, "lote_*.json"));

        File.Delete(Path.Combine(_dir, "b.txt"));
        var r2 = await svc.RollbackLastAsync(CancellationToken.None);
        Assert.Equal(1, r2.Revertidos);
        Assert.True(File.Exists(Path.Combine(_dir, "b.txt")));
        Assert.Empty(Directory.EnumerateFiles(_logs, "lote_*.json"));
    }

    [Fact]
    public async Task RechazaPropuestasInvalidasSinMoverNada()
    {
        var a = CrearArchivo("a.txt");
        var b = CrearArchivo("b.txt");
        var c = CrearArchivo("c.txt");
        var d = CrearArchivo("d.txt");
        var svc = new RenameService(_logs);
        var items = new List<DocumentItem>
        {
            new() { OriginalPath = a, ProposedName = "..\\fuera.txt" },
            new() { OriginalPath = b, ProposedName = "C:\\abs\\ruta.txt" },
            new() { OriginalPath = c, ProposedName = "con<invalido>.txt" },
            new() { OriginalPath = d, ProposedName = "cambiado.pdf" },
        };
        var batch = await svc.ApplyAsync(items, CancellationToken.None);

        Assert.Empty(batch.Entradas);
        Assert.All(items, i => Assert.Equal(AnalysisStatus.Advertencia, i.Status));
        foreach (var f in new[] { a, b, c, d })
            Assert.True(File.Exists(f));
        Assert.Equal(4, Directory.EnumerateFiles(_dir).Count());
    }

    [Fact]
    public async Task RollbackConservaEntradasOmitidasPorNewPathInexistente()
    {
        var a = CrearArchivo("a.txt");
        var svc = new RenameService(_logs);
        var items = new List<DocumentItem> { new() { OriginalPath = a, ProposedName = "n1.txt" } };
        var batch = await svc.ApplyAsync(items, CancellationToken.None);
        var renombrado = batch.Entradas[0].NewPath;

        var escondido = Path.Combine(_dir, "escondido.txt");
        File.Move(renombrado, escondido);

        var r = await svc.RollbackLastAsync(CancellationToken.None);
        Assert.Equal(0, r.Revertidos);
        Assert.Single(r.Omitidos);
        Assert.Single(Directory.EnumerateFiles(_logs, "lote_*.json"));

        File.Move(escondido, renombrado);
        var r2 = await svc.RollbackLastAsync(CancellationToken.None);
        Assert.Equal(1, r2.Revertidos);
        Assert.True(File.Exists(a));
        Assert.Empty(Directory.EnumerateFiles(_logs, "lote_*.json"));
    }

    [Fact]
    public async Task RollbackSinLotesReportaError()
    {
        var svc = new RenameService(_logs);
        var r = await svc.RollbackLastAsync(CancellationToken.None);
        Assert.NotEmpty(r.Errores);
    }
}
