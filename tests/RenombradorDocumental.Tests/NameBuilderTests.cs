using RenombradorDocumental.App.Models;
using RenombradorDocumental.App.Services;
using Xunit;

namespace RenombradorDocumental.Tests;

public class NameBuilderTests
{
    private static Profile Perfil() => new()
    {
        Id = "p",
        Catalogo = new() { new CatalogoEntry("FAC", "Eco", "Factura"), new CatalogoEntry("CON", "Tram", "Contrato") },
        Separador = " ",
        LongitudCodigo = 3,
        ExtensionMayusculas = false,
        OrdenEstructura = new() { "FECHA", "CODIGO", "MATERIA", "IDENTIFICADOR" },
    };

    [Theory]
    [InlineData("20240115", "20240115")]
    [InlineData("15/01/2024", "")]
    [InlineData("2024-01-15", "")]
    [InlineData(" 20240115 ", "20240115")]
    [InlineData("2024", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizaFechaADigitos8(string? entrada, string esperado)
        => Assert.Equal(esperado, NameBuilder.NormalizeFecha(entrada));

    [Fact]
    public void NormalizaCodigoAMayusculasYValidaCatalogo()
    {
        var p = Perfil();
        Assert.Equal("FAC", NameBuilder.NormalizeCodigo("fac", p));
        Assert.Equal("", NameBuilder.NormalizeCodigo("XXZ", p));
        Assert.Equal("", NameBuilder.NormalizeCodigo("FA", p));
        Assert.Equal("", NameBuilder.NormalizeCodigo("F1C", p));
    }

    [Fact]
    public void EliminaCaracteresInvalidosYColapsaEspacios()
    {
        Assert.Equal("factura luz", NameBuilder.SanitizeComponent("  fa:ctu*ra   \"luz\"?  "));
        Assert.Equal("fin", NameBuilder.SanitizeComponent("fin.  "));
        Assert.Equal("Factura", NameBuilder.NormalizeMateria("factura"));
        Assert.Equal("Banco123", NameBuilder.NormalizeIdentificador("banco<123>"));
    }

    [Fact]
    public void GeneraNombreSegunOrdenSeparadorYExtensionMinusculas()
    {
        var p = Perfil();
        var nombre = NameBuilder.BuildName("20240315", "fac", "factura luz", "iberdrola", ".PDF", p);
        Assert.Equal("20240315 FAC Factura luz Iberdrola.pdf", nombre);
    }

    [Fact]
    public void RequiereTodosLosComponentesDelOrden()
    {
        var p = Perfil();
        Assert.Equal("", NameBuilder.BuildName("", "fac", "", "recibo", ".txt", p));
        Assert.Equal("", NameBuilder.BuildName("20240101", "fac", "materia", "", ".txt", p));
    }

    [Theory]
    [InlineData("nombre valido.txt", ".txt", true)]
    [InlineData("..\\fuera.txt", ".txt", false)]
    [InlineData("C:\\abs\\ruta.txt", ".txt", false)]
    [InlineData("con<invalido>.txt", ".txt", false)]
    [InlineData("cambiado.pdf", ".txt", false)]
    [InlineData("termina en punto..txt", ".txt", false)]
    [InlineData("final.txt.", ".txt", false)]
    [InlineData("final .txt", ".txt", false)]
    [InlineData("CON.txt", ".txt", false)]
    [InlineData("COM3.txt", ".txt", false)]
    [InlineData("CON Contrato.txt", ".txt", true)]
    [InlineData("CONTRATO CON.txt", ".txt", true)]
    [InlineData("", ".txt", false)]
    public void ValidaNombresPropuestos(string nombre, string ext, bool esperado)
    {
        Assert.Equal(esperado, NameBuilder.TryValidateFileName(nombre, ext, out var err));
        if (!esperado) Assert.False(string.IsNullOrEmpty(err));
    }

    [Fact]
    public void RespetaOrdenEstructuraAlternativo()
    {
        var p = Perfil();
        p.OrdenEstructura = new() { "CODIGO", "FECHA", "MATERIA", "IDENTIFICADOR" };
        p.Separador = "_";
        var nombre = NameBuilder.BuildName("20240101", "con", "contrato", "alquiler", ".docx", p);
        Assert.Equal("CON_20240101_Contrato_Alquiler.docx", nombre);
    }
}
