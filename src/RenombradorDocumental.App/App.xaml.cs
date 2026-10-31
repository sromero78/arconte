using System.IO;
using System.Windows;
using RenombradorDocumental.App.Services;
using RenombradorDocumental.App.ViewModels;

namespace RenombradorDocumental.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        ThemeService.Aplicar(TemaApp.Sistema);

        var profiles = new ProfileService();
        try
        {
            profiles.Cargar();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo cargar perfiles.json: {ex.Message}", "Renombrador Documental",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        if (!LegalAceptado())
        {
            var legal = new LegalDialog();
            if (legal.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
            try
            {
                RegistrarAceptacion();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo registrar la aceptación del aviso legal: {ex.Message}",
                    "Renombrador Documental", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }
        }

        var extractor = new DocumentTextExtractor();
        ILocalDocumentAnalyzer analyzer;
        try
        {
            analyzer = new OllamaAnalyzer();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Configuración de Ollama", MessageBoxButton.OK, MessageBoxImage.Warning);
            analyzer = new OllamaAnalyzer("http://127.0.0.1:11434");
        }
        var rename = new RenameService();
        var vm = new MainViewModel(extractor, analyzer, rename, profiles);

        var ventana = new MainWindow { DataContext = vm };
        MainWindow = ventana;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        ventana.Show();
    }

    private static string RutaAceptacion =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RenombradorDocumental", "legal_aceptado.txt");

    private static bool LegalAceptado() => File.Exists(RutaAceptacion);

    private static void RegistrarAceptacion()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaAceptacion)!);
        File.WriteAllText(RutaAceptacion, DateTime.Now.ToString("o"));
    }
}
