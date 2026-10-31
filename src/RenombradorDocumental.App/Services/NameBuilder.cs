using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using RenombradorDocumental.App.Models;

namespace RenombradorDocumental.App.Services;

public static class NameBuilder
{
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();
    private static readonly Regex FechaRegex = new(@"^\d{8}$", RegexOptions.Compiled);

    public static string SanitizeComponent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsControl(c)) continue;
            if (Array.IndexOf(InvalidChars, c) >= 0) continue;
            sb.Append(c);
        }
        var limpio = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        limpio = limpio.TrimEnd('.', ' ');
        return limpio;
    }

    public static string CapitalizeFirst(string value)
    {
        if (value.Length == 0) return value;
        return char.ToUpper(value[0], CultureInfo.InvariantCulture) + value[1..];
    }

    public static string NormalizeFecha(string? fecha)
    {
        var f = fecha?.Trim() ?? "";
        return FechaRegex.IsMatch(f) ? f : "";
    }

    public static string NormalizeCodigo(string? codigo, Profile profile)
    {
        var c = SanitizeComponent(codigo).ToUpperInvariant();
        if (c.Length != profile.LongitudCodigo || !c.All(char.IsLetter))
            return "";
        if (profile.Catalogo.Count > 0 &&
            !profile.Catalogo.Any(e => string.Equals(e.Codigo, c, StringComparison.OrdinalIgnoreCase)))
            return "";
        return c;
    }

    public static string NormalizeMateria(string? materia) => CapitalizeFirst(SanitizeComponent(materia));

    public static string NormalizeIdentificador(string? identificador) => CapitalizeFirst(SanitizeComponent(identificador));

    public static string BuildName(string fecha, string codigo, string materia, string identificador, string extension, Profile profile)
    {
        var valores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FECHA"] = NormalizeFecha(fecha),
            ["CODIGO"] = NormalizeCodigo(codigo, profile),
            ["MATERIA"] = NormalizeMateria(materia),
            ["IDENTIFICADOR"] = NormalizeIdentificador(identificador),
        };

        var partes = new List<string>();
        foreach (var clave in profile.OrdenEstructura)
        {
            if (!valores.TryGetValue(clave, out var v) || v.Length == 0)
                return "";
            partes.Add(v);
        }
        if (partes.Count == 0) return "";

        var ext = extension.StartsWith('.') ? extension : "." + extension;
        ext = profile.ExtensionMayusculas ? ext.ToUpperInvariant() : ext.ToLowerInvariant();
        return string.Join(profile.Separador, partes) + ext;
    }

    private static readonly HashSet<string> DispositivosReservados = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static bool TryValidateFileName(string proposedName, string originalExtension, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(proposedName))
        {
            error = "El nombre propuesto está vacío";
            return false;
        }
        if (proposedName.Length > 255)
        {
            error = "El nombre propuesto supera los 255 caracteres";
            return false;
        }
        if (proposedName.IndexOfAny(new[] { '/', '\\' }) >= 0)
        {
            error = "El nombre propuesto no puede contener separadores de ruta (\\ o /)";
            return false;
        }
        if (!string.Equals(proposedName, Path.GetFileName(proposedName), StringComparison.Ordinal))
        {
            error = "El nombre propuesto no es un nombre de archivo válido";
            return false;
        }
        if (proposedName.Any(c => char.IsControl(c) || Array.IndexOf(InvalidChars, c) >= 0))
        {
            error = "El nombre propuesto contiene caracteres no válidos en Windows";
            return false;
        }
        if (proposedName.EndsWith('.') || proposedName.EndsWith(' '))
        {
            error = "El nombre propuesto no puede terminar en punto ni en espacio";
            return false;
        }
        var ext = Path.GetExtension(proposedName);
        var extOriginal = originalExtension.StartsWith('.') ? originalExtension : "." + originalExtension;
        if (!string.Equals(ext, extOriginal, StringComparison.OrdinalIgnoreCase))
        {
            error = $"La extensión debe conservarse como '{extOriginal}'";
            return false;
        }
        var stem = proposedName[..^ext.Length];
        if (stem.Length == 0)
        {
            error = "El nombre propuesto no tiene contenido antes de la extensión";
            return false;
        }
        if (stem.EndsWith('.') || stem.EndsWith(' '))
        {
            error = "El nombre propuesto no puede terminar en punto ni en espacio";
            return false;
        }
        var dispositivo = stem.Split('.')[0].TrimEnd(' ');
        if (DispositivosReservados.Contains(dispositivo))
        {
            error = "El nombre propuesto usa un nombre reservado del sistema (CON, PRN, AUX, NUL, COM1-9, LPT1-9)";
            return false;
        }
        return true;
    }

    public static string SufijoColision(string baseName)
    {
        var ext = Path.GetExtension(baseName);
        var sinExt = baseName[..^ext.Length];
        var m = Regex.Match(sinExt, @"^(.+) _(\d{2})$");
        int n = m.Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) + 1 : 1;
        var raiz = m.Success ? m.Groups[1].Value : sinExt;
        return $"{raiz} _{n:00}{ext}";
    }
}
