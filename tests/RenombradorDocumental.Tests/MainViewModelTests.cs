using System.IO;
using RenombradorDocumental.App.Models;
using RenombradorDocumental.App.Services;
using RenombradorDocumental.App.ViewModels;
using Xunit;

namespace RenombradorDocumental.Tests;

public class MainViewModelTests : IDisposable
{
    private readonly string _dir;

    public MainViewModelTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "vm_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private sealed class ExtractorFalso : IDocumentTextExtractor
    {
        public Task<ExtractionResult> ExtractAsync(string path, CancellationToken ct)
            => Task.FromResult(new ExtractionResult("texto", ExtractionMethod.Directo));
    }

    private sealed class AnalizadorFalso : ILocalDocumentAnalyzer
    {
        public Task<DocumentProposal> AnalyzeAsync(string t, string f, string n, Profile p, CancellationToken ct)
            => Task.FromResult(new DocumentProposal("20240101", "FAC", "factura", "proveedor"));
    }

    private sealed class AnalizadorCapturador : ILocalDocumentAnalyzer
    {
        public string? Texto;
        public string? Carpeta;
        public string? Nombre;
        public Task<DocumentProposal> AnalyzeAsync(string t, string f, string n, Profile p, CancellationToken ct)
        {
            Texto = t; Carpeta = f; Nombre = n;
            return Task.FromResult(new DocumentProposal("20240101", "FAC", "factura", "proveedor"));
        }
    }

    private MainViewModel CrearVm(ILocalDocumentAnalyzer? analyzer = null, IDocumentTextExtractor? extractor = null)
    {
        var rutaPerfiles = Path.Combine(_dir, "perfiles.json");
        File.WriteAllText(rutaPerfiles, """
        {
          "perfil_activo": "p",
          "perfiles": {
            "p": {
              "nombre_perfil": "Prueba",
              "formato_fecha": "YYYYMMDD",
              "separador": " ",
              "longitud_codigo": 3,
              "regla_caja": "mayusculas_codigo_resto_capitalizado",
              "extension_mayusculas": false,
              "orden_estructura": ["FECHA", "CODIGO", "MATERIA", "IDENTIFICADOR"],
              "catalogo_codigos": [
                { "codigo": "FAC", "familia": "Eco", "concepto": "Factura" }
              ]
            }
          }
        }
        """);
        var ps = new ProfileService(rutaPerfiles);
        ps.Cargar();
        return new MainViewModel(extractor ?? new ExtractorFalso(), analyzer ?? new AnalizadorFalso(),
            new RenameService(Path.Combine(_dir, "logs")), ps);
    }

    [Fact]
    public void EdicionesConsecutivasDeComponentesRecalculanYEdicionManualMarcaFlag()
    {
        File.WriteAllText(Path.Combine(_dir, "doc.pdf"), "x");
        var vm = CrearVm();
        vm.Carpeta = _dir;
        vm.EscanearCarpeta();
        var item = Assert.Single(vm.Documentos);

        item.Fecha = "20240101";
        item.Codigo = "FAC";
        item.Materia = "factura";
        item.Identificador = "uno";
        Assert.Equal("20240101 FAC Factura Uno.pdf", item.ProposedName);

        item.Identificador = "dos";
        Assert.Equal("20240101 FAC Factura Dos.pdf", item.ProposedName);
        Assert.False(item.PropuestaManual);

        item.ProposedName = "nombre manual.pdf";
        Assert.True(item.PropuestaManual);

        item.Identificador = "tres";
        Assert.False(item.PropuestaManual);
        Assert.Equal("20240101 FAC Factura Tres.pdf", item.ProposedName);
    }

    [Fact]
    public void TrasEscaneoTodosSeleccionadosYAnalizarHabilitado()
    {
        File.WriteAllText(Path.Combine(_dir, "a.pdf"), "x");
        File.WriteAllText(Path.Combine(_dir, "b.txt"), "x");
        var vm = CrearVm();
        vm.Carpeta = _dir;
        vm.EscanearCarpeta();

        Assert.Equal(2, vm.Documentos.Count);
        Assert.All(vm.Documentos, d => Assert.True(d.IsSelected));
        Assert.True(vm.AnalizarCommand.CanExecute(null));
        Assert.True(vm.AplicarCommand.CanExecute(null));
    }

    [Fact]
    public async Task AnalizarPasaTextoCarpetaYNombreYGeneraPropuesta()
    {
        var sub = Path.Combine(_dir, "MiCarpeta");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "original.pdf"), "x");
        var capturador = new AnalizadorCapturador();
        var extractor = new ExtractorFalso();
        var vm = CrearVm(capturador, extractor);
        vm.Carpeta = _dir;
        vm.EscanearCarpeta();

        await vm.AnalizarAsync();

        Assert.Equal("texto", capturador.Texto);
        Assert.Equal("MiCarpeta", capturador.Carpeta);
        Assert.Equal("original.pdf", capturador.Nombre);
        var item = Assert.Single(vm.Documentos);
        Assert.Equal("20240101", item.Fecha);
        Assert.Equal("FAC", item.Codigo);
        Assert.Equal("factura", item.Materia);
        Assert.Equal("proveedor", item.Identificador);
        Assert.Equal("20240101 FAC Factura Proveedor.pdf", item.ProposedName);
        Assert.Equal(AnalysisStatus.Propuesto, item.Status);
    }

    [Fact]
    public void AplicarSeDeshabilitaSinSeleccionYSeRehabilita()
    {
        File.WriteAllText(Path.Combine(_dir, "a.pdf"), "x");
        var vm = CrearVm();
        vm.Carpeta = _dir;
        vm.EscanearCarpeta();
        var item = vm.Documentos[0];

        item.IsSelected = false;
        Assert.False(vm.AplicarCommand.CanExecute(null));
        item.IsSelected = true;
        Assert.True(vm.AplicarCommand.CanExecute(null));
    }

    [Fact]
    public void RescaneoConRutaInvalidaDeshabilitaComandos()
    {
        File.WriteAllText(Path.Combine(_dir, "a.pdf"), "x");
        var vm = CrearVm();
        vm.Carpeta = _dir;
        vm.EscanearCarpeta();
        Assert.True(vm.AnalizarCommand.CanExecute(null));
        Assert.True(vm.AplicarCommand.CanExecute(null));

        vm.Carpeta = Path.Combine(_dir, "no_existe_esta_carpeta");
        vm.EscanearCarpeta();

        Assert.Empty(vm.Documentos);
        Assert.False(vm.AnalizarCommand.CanExecute(null));
        Assert.False(vm.AplicarCommand.CanExecute(null));
    }

    [Fact]
    public void SetGeneratedProposedNameNoMarcaEdicionManual()
    {
        var item = new DocumentItem { OriginalPath = Path.Combine(_dir, "x.txt") };
        item.SetGeneratedProposedName("generado.txt");
        Assert.False(item.PropuestaManual);
        Assert.Equal("generado.txt", item.ProposedName);
    }
}
