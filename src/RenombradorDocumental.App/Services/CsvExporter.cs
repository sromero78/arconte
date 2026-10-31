using System.IO;
using System.Text;
using RenombradorDocumental.App.Models;

namespace RenombradorDocumental.App.Services;

public static class CsvExporter
{
    public sealed record Fila(string NombreAntiguo, string RutaAntigua, string NombreNuevo, string RutaNueva, string Estado);

    public static void Exportar(string ruta, IEnumerable<Fila> filas, DateTime fecha)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Nombre antiguo,Ruta antigua,Nombre nuevo,Ruta nueva,Estado,Fecha");
        foreach (var f in filas)
        {
            sb.Append(Escapar(f.NombreAntiguo)).Append(',')
              .Append(Escapar(f.RutaAntigua)).Append(',')
              .Append(Escapar(f.NombreNuevo)).Append(',')
              .Append(Escapar(f.RutaNueva)).Append(',')
              .Append(Escapar(f.Estado)).Append(',')
              .Append(fecha.ToString("yyyy-MM-dd HH:mm:ss")).AppendLine();
        }
        File.WriteAllText(ruta, sb.ToString(), new UTF8Encoding(true));
    }

    private static string Escapar(string valor)
    {
        if (valor.Contains('"') || valor.Contains(',') || valor.Contains('\n') || valor.Contains('\r'))
            return "\"" + valor.Replace("\"", "\"\"") + "\"";
        return valor;
    }
}
