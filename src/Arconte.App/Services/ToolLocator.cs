using System.IO;

namespace Arconte.App.Services;

public static class ToolLocator
{
    public static string? Find(string exeName, params string[] extraDirs)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidato = Path.Combine(dir.Trim(), exeName);
            if (File.Exists(candidato)) return candidato;
        }
        foreach (var dir in extraDirs)
        {
            if (string.IsNullOrEmpty(dir)) continue;
            var candidato = Path.Combine(dir, exeName);
            if (File.Exists(candidato)) return candidato;
        }
        return null;
    }

    public static string? FindTesseract() => Find("tesseract.exe",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tesseract-OCR"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Tesseract-OCR"));

    public static string? FindPdftoppm() => Find("pdftoppm.exe",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "poppler", "bin"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "poppler", "bin"),
        Path.Combine(AppContext.BaseDirectory, "poppler"));
}

