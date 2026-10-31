using System.IO;
using RenombradorDocumental.App.Services;
using Xunit;

namespace RenombradorDocumental.Tests;

public class ProfileServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _ruta;

    private const string JsonValido = """
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
    """;

    public ProfileServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "prof_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _ruta = Path.Combine(_dir, "perfiles.json");
        File.WriteAllText(_ruta, JsonValido);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void GuardarConfiguracionValidaPersisteYRecarga()
    {
        var svc = new ProfileService(_ruta);
        svc.Cargar();

        var modificado = JsonValido
            .Replace("Prueba", "Prueba Editada")
            .Replace("{ \"codigo\": \"FAC\"",
                     "{ \"codigo\": \"NUE\", \"familia\": \"Otra\", \"concepto\": \"Nuevo\" }, { \"codigo\": \"FAC\"");

        Assert.True(svc.GuardarConfiguracion(modificado, out var error), error);
        Assert.Equal("Prueba Editada", svc.Perfiles[0].Nombre);
        Assert.Equal(2, svc.Perfiles[0].Catalogo.Count);
        Assert.Equal("NUE", svc.Perfiles[0].Catalogo[0].Codigo);
        Assert.Equal("Otra", svc.Perfiles[0].Catalogo[0].Categoria);

        var recargado = new ProfileService(_ruta);
        recargado.Cargar();
        Assert.Equal("Prueba Editada", recargado.Perfiles[0].Nombre);
        Assert.Contains("nombre_perfil", File.ReadAllText(_ruta));
        Assert.Contains("catalogo_codigos", File.ReadAllText(_ruta));
        Assert.Contains("familia", File.ReadAllText(_ruta));
        Assert.Contains("concepto", File.ReadAllText(_ruta));
    }

    [Fact]
    public void RechazaCodigoDuplicado()
        => Rechaza(JsonValido.Replace("\"codigo\": \"FAC\"", "\"codigo\": \"NUE\"").Replace("catalogo_codigos\": [", "catalogo_codigos\": [ { \"codigo\": \"NUE\", \"familia\": \"X\", \"concepto\": \"Y\" },"));

    [Theory]
    [InlineData("fac")]
    [InlineData("ABCD")]
    [InlineData("AB")]
    public void RechazaCodigoConFormatoInvalido(string codigoMalo)
        => Rechaza(JsonValido.Replace("\"codigo\": \"FAC\"", $"\"codigo\": \"{codigoMalo}\""));

    [Fact]
    public void RechazaExtensionMayusculasNoBooleana()
    {
        Rechaza(JsonValido.Replace("\"extension_mayusculas\": false", "\"extension_mayusculas\": \"no\""));
        Rechaza(JsonValido.Replace("      \"extension_mayusculas\": false,\n", ""));
    }

    [Fact]
    public void RechazaOrdenIncompleto()
        => Rechaza(JsonValido.Replace("[\"FECHA\", \"CODIGO\", \"MATERIA\", \"IDENTIFICADOR\"]", "[\"FECHA\", \"CODIGO\"]"));

    [Fact]
    public void RechazaPerfilActivoInexistente()
        => Rechaza(JsonValido.Replace("\"perfil_activo\": \"p\"", "\"perfil_activo\": \"otro\""));

    [Fact]
    public void CrearCopiaEditableEsDeepCopy()
    {
        var svc = new ProfileService(_ruta);
        svc.Cargar();
        var copia = svc.CrearCopiaEditable();

        copia[0].Nombre = "Modificado";
        copia[0].OrdenEstructura[0] = "CODIGO";
        copia[0].Catalogo[0].Codigo = "ZZZ";

        Assert.Equal("Prueba", svc.Perfiles[0].Nombre);
        Assert.Equal("FECHA", svc.Perfiles[0].OrdenEstructura[0]);
        Assert.Equal("FAC", svc.Perfiles[0].Catalogo[0].Codigo);
    }

    [Fact]
    public void SerializarConfiguracionGeneraClavesExactasYRoundTrip()
    {
        var svc = new ProfileService(_ruta);
        svc.Cargar();
        var copia = svc.CrearCopiaEditable();
        copia[0].Nombre = "Serializado";

        var json = ProfileService.SerializarConfiguracion(copia, "p");
        Assert.Contains("\"perfil_activo\"", json);
        Assert.Contains("\"perfiles\"", json);
        Assert.Contains("\"nombre_perfil\"", json);
        Assert.Contains("\"formato_fecha\"", json);
        Assert.Contains("\"separador\"", json);
        Assert.Contains("\"longitud_codigo\"", json);
        Assert.Contains("\"regla_caja\"", json);
        Assert.Contains("\"extension_mayusculas\"", json);
        Assert.Contains("\"orden_estructura\"", json);
        Assert.Contains("\"catalogo_codigos\"", json);
        Assert.Contains("\"familia\"", json);
        Assert.Contains("\"concepto\"", json);

        Assert.True(svc.GuardarConfiguracion(json, out var error), error);
        var recargado = new ProfileService(_ruta);
        recargado.Cargar();
        Assert.Equal("Serializado", recargado.Perfiles[0].Nombre);
        Assert.Equal("FAC", recargado.Perfiles[0].Catalogo[0].Codigo);
    }

    [Fact]
    public void GuardarPerfilesRechazaCodigoVacioODuplicado()
    {
        var svc = new ProfileService(_ruta);
        svc.Cargar();
        var contenidoAntes = File.ReadAllText(_ruta);
        var nombresAntes = svc.Perfiles.Select(p => p.Nombre).ToList();

        var copia = svc.CrearCopiaEditable();
        copia[0].Catalogo.Add(new RenombradorDocumental.App.Models.CatalogoEntry("", "X", "Y"));
        Assert.False(svc.GuardarPerfiles(copia, "p", out var e1));
        Assert.False(string.IsNullOrEmpty(e1));

        var copia2 = svc.CrearCopiaEditable();
        copia2[0].Catalogo.Add(new RenombradorDocumental.App.Models.CatalogoEntry("FAC", "X", "Y"));
        Assert.False(svc.GuardarPerfiles(copia2, "p", out var e2));
        Assert.False(string.IsNullOrEmpty(e2));

        Assert.Equal(contenidoAntes, File.ReadAllText(_ruta));
        Assert.Equal(nombresAntes, svc.Perfiles.Select(p => p.Nombre).ToList());
    }

    private void Rechaza(string jsonInvalido)
    {
        var svc = new ProfileService(_ruta);
        svc.Cargar();
        var perfilesAntes = svc.Perfiles.Select(p => p.Nombre).ToList();
        var activoAntes = svc.PerfilActivoId;
        var contenidoAntes = File.ReadAllText(_ruta);

        Assert.False(svc.GuardarConfiguracion(jsonInvalido, out var error));
        Assert.False(string.IsNullOrEmpty(error));
        Assert.Equal(contenidoAntes, File.ReadAllText(_ruta));
        Assert.Equal(perfilesAntes, svc.Perfiles.Select(p => p.Nombre).ToList());
        Assert.Equal(activoAntes, svc.PerfilActivoId);
    }
}
