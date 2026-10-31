using System.Windows;
using Microsoft.Win32;
using RenombradorDocumental.App.Services;
using RenombradorDocumental.App.ViewModels;

namespace RenombradorDocumental.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private void SeleccionarCarpeta_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Seleccione la carpeta de documentos" };
        if (dlg.ShowDialog() == true && Vm != null)
        {
            Vm.Carpeta = dlg.FolderName;
            Vm.EscanearCarpeta();
        }
    }

    private void EditarPerfiles_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        var dialog = new ProfileEditorDialog(Vm) { Owner = this };
        dialog.ShowDialog();
    }

    private void Exportar_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        var filas = Vm.FilasExportacion().ToList();
        if (filas.Count == 0)
        {
            MessageBox.Show("No hay historial de renombrado en esta sesión para exportar.",
                "Exportar historial", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new SaveFileDialog
        {
            Title = "Exportar historial",
            Filter = "Archivo CSV (*.csv)|*.csv",
            FileName = $"historial_renombrado_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                CsvExporter.Exportar(dlg.FileName, filas, DateTime.Now);
                MessageBox.Show("Historial exportado correctamente.", "Exportar historial",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo exportar el historial: {ex.Message}",
                    "Exportar historial", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
