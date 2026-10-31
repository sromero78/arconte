using RenombradorDocumental.App.Models;
using RenombradorDocumental.App.Services;
using Xunit;

namespace RenombradorDocumental.Tests;

public class OllamaAnalyzerTests
{
    [Theory]
    [InlineData("http://127.0.0.1:11434")]
    [InlineData("http://localhost:11434")]
    [InlineData("http://[::1]:11434")]
    public void AceptaEndpointsLoopback(string ep) => Assert.True(OllamaAnalyzer.IsLoopbackEndpoint(ep));

    [Theory]
    [InlineData("http://api.ejemplo.com:11434")]
    [InlineData("http://192.168.1.10:11434")]
    [InlineData("ftp://127.0.0.1")]
    [InlineData("http://usuario@localhost:11434")]
    [InlineData("http://usuario:clave@127.0.0.1:11434")]
    [InlineData("no-es-una-url")]
    public void RechazaEndpointsNoLoopback(string ep) => Assert.False(OllamaAnalyzer.IsLoopbackEndpoint(ep));

    [Fact]
    public void ConstructorRechazaHostExterno()
        => Assert.Throws<ArgumentException>(() => new OllamaAnalyzer("http://8.8.8.8:11434"));

    [Fact]
    public void ParseaJsonConBloqueMarkdownYValidaEstricto()
    {
        var perfil = new Profile
        {
            Catalogo = new() { new CatalogoEntry("FAC", "Eco", "Factura") },
        };
        var p = OllamaAnalyzer.ParsearPropuesta(
            "```json\n{\"fecha\":\"20240115\",\"codigo\":\"fac\",\"materia\":\"factura:luz\",\"identificador\":\"iberdrola\"}\n```",
            perfil);
        Assert.Equal("20240115", p.Fecha);
        Assert.Equal("FAC", p.Codigo);
        Assert.Equal("Facturaluz", p.Materia);
        Assert.Equal("Iberdrola", p.Identificador);
    }

    [Fact]
    public void RechazaFechaInventadaOCodigoFueraDeCatalogo()
    {
        var perfil = new Profile
        {
            Catalogo = new() { new CatalogoEntry("FAC", "Eco", "Factura") },
        };
        var p = OllamaAnalyzer.ParsearPropuesta(
            "{\"fecha\":\"ayer\",\"codigo\":\"XYZ\",\"materia\":\"m\",\"identificador\":\"i\"}", perfil);
        Assert.Equal("", p.Fecha);
        Assert.Equal("", p.Codigo);
    }
}
