using System.IO;
using RenombradorDocumental.App.Services;
using Xunit;

namespace RenombradorDocumental.Tests;

public class DocumentScannerTests
{
    [Fact]
    public void ExcluyeBloqueosThumbsTmpYOcultos()
    {
        Assert.False(DocumentScanner.ShouldInclude("~$doc.docx", FileAttributes.Normal));
        Assert.False(DocumentScanner.ShouldInclude("~$cualquiera.pdf", FileAttributes.Normal));
        Assert.False(DocumentScanner.ShouldInclude("Thumbs.db", FileAttributes.Normal));
        Assert.False(DocumentScanner.ShouldInclude("thumbs.db", FileAttributes.Normal));
        Assert.False(DocumentScanner.ShouldInclude("archivo.tmp", FileAttributes.Normal));
        Assert.False(DocumentScanner.ShouldInclude("nota.txt", FileAttributes.Hidden));
        Assert.False(DocumentScanner.ShouldInclude("nota.txt", FileAttributes.System));
        Assert.False(DocumentScanner.ShouldInclude("nota.txt", FileAttributes.Temporary));
    }

    [Theory]
    [InlineData("a.pdf")] [InlineData("a.doc")] [InlineData("a.docx")] [InlineData("a.odt")]
    [InlineData("a.xls")] [InlineData("a.xlsx")] [InlineData("a.ods")] [InlineData("a.txt")]
    [InlineData("a.jpg")] [InlineData("a.jpeg")] [InlineData("a.png")] [InlineData("a.tif")] [InlineData("a.tiff")]
    [InlineData("A.PDF")] [InlineData("A.JPG")]
    public void AceptaFormatosSoportados(string nombre)
    {
        Assert.True(DocumentScanner.ShouldInclude(nombre, FileAttributes.Normal));
    }

    [Theory]
    [InlineData("a.exe")] [InlineData("a.zip")] [InlineData("sinextension")] [InlineData("a.db")]
    public void RechazaFormatosNoSoportados(string nombre)
    {
        Assert.False(DocumentScanner.ShouldInclude(nombre, FileAttributes.Normal));
    }

    [Fact]
    public void ScanRecursivoFiltraCorrectamente()
    {
        var dir = Path.Combine(Path.GetTempPath(), "scan_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "ok.pdf"), "x");
            File.WriteAllText(Path.Combine(dir, "~$ok.docx"), "x");
            File.WriteAllText(Path.Combine(dir, "Thumbs.db"), "x");
            File.WriteAllText(Path.Combine(dir, "temp.tmp"), "x");
            File.WriteAllText(Path.Combine(dir, "a.exe"), "x");
            var sub = Directory.CreateDirectory(Path.Combine(dir, "sub"));
            File.WriteAllText(Path.Combine(sub.FullName, "nota.txt"), "x");

            var res = DocumentScanner.Scan(dir);
            Assert.Equal(2, res.Count);
            Assert.Contains(res, p => p.EndsWith("ok.pdf"));
            Assert.Contains(res, p => p.EndsWith("nota.txt"));
        }
        finally { Directory.Delete(dir, true); }
    }
}
