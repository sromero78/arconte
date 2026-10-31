using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Arconte.App.Models;
using Arconte.App.ViewModels;

namespace Arconte.App;

public partial class ProfileEditorDialog : Window, INotifyPropertyChanged
{
    private readonly MainViewModel _viewModel;
    private Profile? _perfilSeleccionado;
    private CatalogoEntry? _codigoSeleccionado;

    public ObservableCollection<Profile> Perfiles { get; } = new();
    public string PerfilActivoId { get; }
    public string[] FormatosFecha { get; } = { "YYYYMMDD" };
    public string[] ReglasCaja { get; } = { "mayusculas_codigo_resto_capitalizado" };
    public string[] Componentes { get; } = { "FECHA", "CODIGO", "MATERIA", "IDENTIFICADOR" };

    public Profile? PerfilSeleccionado
    {
        get => _perfilSeleccionado;
        set
        {
            _perfilSeleccionado = value;
            OnChanged();
            RefrescarCatalogo();
        }
    }

    public CatalogoEntry? CodigoSeleccionado
    {
        get => _codigoSeleccionado;
        set { _codigoSeleccionado = value; OnChanged(); }
    }

    public ProfileEditorDialog(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        PerfilActivoId = viewModel.PerfilActivoId;
        try
        {
            foreach (var p in viewModel.CrearCopiaEditablePerfiles())
            {
                while (p.OrdenEstructura.Count < Componentes.Length)
                    p.OrdenEstructura.Add(Componentes[p.OrdenEstructura.Count]);
                if (p.OrdenEstructura.Count > Componentes.Length)
                    p.OrdenEstructura = p.OrdenEstructura.Take(Componentes.Length).ToList();
                Perfiles.Add(p);
            }
            PerfilSeleccionado = Perfiles.FirstOrDefault(p => p.Id == PerfilActivoId)
                ?? Perfiles.FirstOrDefault();
        }
        catch (Exception ex)
        {
            ValidationMessage.Text = "No se pudo cargar la configuración de perfiles: " + ex.Message;
        }
        DataContext = this;
    }

    private void RefrescarCatalogo()
    {
        CatalogGrid?.Items?.Refresh();
    }

    private void AgregarCodigo_Click(object sender, RoutedEventArgs e)
    {
        if (PerfilSeleccionado == null)
        {
            ValidationMessage.Text = "Seleccione un perfil antes de añadir códigos.";
            return;
        }
        PerfilSeleccionado.Catalogo.Add(new CatalogoEntry());
        RefrescarCatalogo();
    }

    private void EliminarCodigo_Click(object sender, RoutedEventArgs e)
    {
        if (PerfilSeleccionado == null || CodigoSeleccionado == null)
        {
            ValidationMessage.Text = "Seleccione una fila del catálogo para eliminarla.";
            return;
        }
        PerfilSeleccionado.Catalogo.Remove(CodigoSeleccionado);
        CodigoSeleccionado = null;
        RefrescarCatalogo();
    }

    private void ActualizarJson_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CatalogGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            CatalogGrid.CommitEdit(DataGridEditingUnit.Row, true);
            JsonPreview.Text = _viewModel.CrearVistaJsonPerfiles(Perfiles, PerfilActivoId);
        }
        catch (Exception ex)
        {
            ValidationMessage.Text = "No se pudo generar la vista JSON: " + ex.Message;
        }
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CatalogGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            CatalogGrid.CommitEdit(DataGridEditingUnit.Row, true);
            if (_viewModel.GuardarPerfilesVisuales(Perfiles, PerfilActivoId, out var error))
            {
                DialogResult = true;
                Close();
            }
            else
            {
                ValidationMessage.Text = error;
            }
        }
        catch (Exception ex)
        {
            ValidationMessage.Text = "Error inesperado al guardar: " + ex.Message;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

