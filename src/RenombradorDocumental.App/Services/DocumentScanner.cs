using System.IO;

namespace RenombradorDocumental.App.Services;

public static class DocumentScanner
{
    public static readonly HashSet<string> ExtensionesSoportadas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".odt", ".xls", ".xlsx", ".ods",
        ".txt", ".jpg", ".jpeg", ".png", ".tif", ".tiff"
    };

    public static bool ShouldInclude(string fileName, FileAttributes attributes)
    {
        if ((attributes & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.Temporary)) != 0)
            return false;
        if (fileName.StartsWith("~$", StringComparison.Ordinal))
            return false;
        if (string.Equals(fileName, "Thumbs.db", StringComparison.OrdinalIgnoreCase))
            return false;
        var ext = Path.GetExtension(fileName);
        if (string.Equals(ext, ".tmp", StringComparison.OrdinalIgnoreCase))
            return false;
        return ExtensionesSoportadas.Contains(ext);
    }

    public static IReadOnlyList<string> Scan(string folder)
    {
        var resultado = new List<string>();
        var pendientes = new Stack<string>();
        pendientes.Push(folder);
        while (pendientes.Count > 0)
        {
            var dir = pendientes.Pop();
            IEnumerable<string> archivos;
            try { archivos = Directory.EnumerateFiles(dir).ToList(); }
            catch { continue; }
            foreach (var path in archivos)
            {
                FileAttributes attrs;
                try { attrs = File.GetAttributes(path); }
                catch { continue; }
                if (ShouldInclude(Path.GetFileName(path), attrs))
                    resultado.Add(path);
            }
            IEnumerable<string> subdirs;
            try { subdirs = Directory.EnumerateDirectories(dir).ToList(); }
            catch { continue; }
            foreach (var sub in subdirs)
            {
                FileAttributes attrs;
                try { attrs = File.GetAttributes(sub); }
                catch { continue; }
                if ((attrs & FileAttributes.ReparsePoint) != 0) continue;
                pendientes.Push(sub);
            }
        }
        resultado.Sort(StringComparer.OrdinalIgnoreCase);
        return resultado;
    }
}
