using System.Windows;
using Microsoft.Win32;

namespace Arconte.App.Services;

public enum TemaApp { Sistema, Claro, Oscuro }

public static class ThemeService
{
    public static void Aplicar(TemaApp tema)
    {
        var oscuro = tema switch
        {
            TemaApp.Claro => false,
            TemaApp.Oscuro => true,
            _ => SistemaEsOscuro(),
        };
        CargarDiccionario(oscuro ? "Themes/Dark.xaml" : "Themes/Light.xaml");
    }

    public static bool SistemaEsOscuro()
    {
        try
        {
            var v = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return v is int i && i == 0;
        }
        catch { return false; }
    }

    private static void CargarDiccionario(string ruta)
    {
        var dic = new ResourceDictionary { Source = new Uri(ruta, UriKind.Relative) };
        var app = Application.Current;
        if (app == null) return;
        var previo = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source?.OriginalString.StartsWith("Themes/", StringComparison.OrdinalIgnoreCase) == true);
        if (previo != null) app.Resources.MergedDictionaries.Remove(previo);
        app.Resources.MergedDictionaries.Add(dic);
    }
}

