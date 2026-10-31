using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Arconte.App.Models;
using Arconte.App.Services;

namespace Arconte.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IDocumentTextExtractor _extractor;
    private readonly ILocalDocumentAnalyzer _analyzer;
    private readonly IRenameService _rename;
    private readonly ProfileService _profiles;
    private CancellationTokenSource? _cts;
    private RenameBatch? _ultimoLote;
    private readonly List<RenameBatch> _historialSesion = new();

    private string _carpeta = "";
    private string _estado = "Listo";
    private int _progreso;
    private int _total;
    private bool _analizando;
    private Profile? _perfilSeleccionado;
    private TemaApp _tema = TemaApp.Sistema;

    public ObservableCollection<DocumentItem> Documentos { get; } = new();
    public ObservableCollection<CatalogoEntry> CatalogoActivo { get; } = new();
    public List<Profile> Perfiles => _profiles.Perfiles;
    public string[] Temas { get; } = { "Sistema", "Claro", "Oscuro" };

    public string Carpeta { get => _carpeta; set { _carpeta = value; OnChanged(); } }
    public string Estado { get => _estado; set { _estado = value; OnChanged(); } }
    public int Progreso { get => _progreso; set { _progreso = value; OnChanged(); OnChanged(nameof(TextoProgreso)); } }
    public int Total { get => _total; set { _total = value; OnChanged(); OnChanged(nameof(TextoProgreso)); } }
    public string TextoProgreso => $"{Progreso} / {Total}";
    public bool Analizando
    {
        get => _analizando;
        set
        {
            _analizando = value;
            OnChanged();
            OnChanged(nameof(MostrarCancelar));
            _analizarCommand?.RaiseCanExecuteChanged();
            _cancelarCommand?.RaiseCanExecuteChanged();
            _aplicarCommand?.RaiseCanExecuteChanged();
            (DeshacerCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }
    public bool MostrarCancelar => Analizando;

    public Profile? PerfilSeleccionado
    {
        get => _perfilSeleccionado;
        set
        {
            _perfilSeleccionado = value;
            OnChanged();
            if (value != null)
            {
                if (!_profiles.GuardarPerfilActivo(value.Id) && _profiles.UltimoError != null)
                    Estado = _profiles.UltimoError;
                RefrescarCatalogo();
                RecalcularPropuestas();
            }
        }
    }

    public string TemaSeleccionado
    {
        get => _tema.ToString();
        set
        {
            _tema = Enum.TryParse<TemaApp>(value, out var t) ? t : TemaApp.Sistema;
            OnChanged();
            ThemeService.Aplicar(_tema);
        }
    }

    private readonly RelayCommand _analizarCommand;
    private readonly RelayCommand _cancelarCommand;
    private readonly RelayCommand _aplicarCommand;

    public ICommand AnalizarCommand => _analizarCommand;
    public ICommand CancelarCommand => _cancelarCommand;
    public ICommand AplicarCommand => _aplicarCommand;
    public ICommand DeshacerCommand { get; }

    public MainViewModel(IDocumentTextExtractor extractor, ILocalDocumentAnalyzer analyzer,
        IRenameService rename, ProfileService profiles)
    {
        _extractor = extractor;
        _analyzer = analyzer;
        _rename = rename;
        _profiles = profiles;

        _perfilSeleccionado = profiles.PerfilActivo;
        RefrescarCatalogo();

        _analizarCommand = new RelayCommand(_ => AnalizarAsync(), _ => !Analizando && Documentos.Count > 0);
        _cancelarCommand = new RelayCommand(_ => _cts?.Cancel(), _ => Analizando);
        _aplicarCommand = new RelayCommand(_ => AplicarAsync(), _ => !Analizando && Documentos.Any(d => d.IsSelected));
        DeshacerCommand = new RelayCommand(_ => DeshacerAsync(), _ => !Analizando);
    }

    public string ObtenerConfiguracionPerfiles() => _profiles.LeerConfiguracion();

    public bool GuardarConfiguracionPerfiles(string json, out string error)
    {
        if (!_profiles.GuardarConfiguracion(json, out error))
            return false;
        AplicarPerfilesActualizados();
        return true;
    }

    public List<Profile> CrearCopiaEditablePerfiles() => _profiles.CrearCopiaEditable();

    public string PerfilActivoId => _profiles.PerfilActivoId;

    public string CrearVistaJsonPerfiles(IEnumerable<Profile> perfiles, string perfilActivoId)
        => ProfileService.SerializarConfiguracion(perfiles, perfilActivoId);

    public bool GuardarPerfilesVisuales(IEnumerable<Profile> perfiles, string perfilActivoId, out string error)
    {
        if (!_profiles.GuardarPerfiles(perfiles, perfilActivoId, out error))
            return false;
        AplicarPerfilesActualizados();
        return true;
    }

    private void AplicarPerfilesActualizados()
    {
        _perfilSeleccionado = _profiles.PerfilActivo;
        OnChanged(nameof(Perfiles));
        OnChanged(nameof(PerfilSeleccionado));
        RefrescarCatalogo();
        RecalcularPropuestas();
        Estado = "Perfiles actualizados";
    }

    public void EscanearCarpeta()
    {
        Documentos.Clear();
        if (!Directory.Exists(Carpeta))
        {
            Estado = "Seleccione una carpeta válida";
            _analizarCommand.RaiseCanExecuteChanged();
            _aplicarCommand.RaiseCanExecuteChanged();
            return;
        }
        foreach (var path in DocumentScanner.Scan(Carpeta))
        {
            var item = new DocumentItem { OriginalPath = path, IsSelected = true };
            item.PropertyChanged += Item_PropertyChanged;
            Documentos.Add(item);
        }
        Estado = $"{Documentos.Count} documentos encontrados y seleccionados. Pulse «Analizar todos».";
        _analizarCommand?.RaiseCanExecuteChanged();
        _aplicarCommand?.RaiseCanExecuteChanged();
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not DocumentItem item) return;
        var perfil = _profiles.PerfilActivo;
        if (perfil == null) return;
        switch (e.PropertyName)
        {
            case nameof(DocumentItem.Fecha):
            case nameof(DocumentItem.Codigo):
            case nameof(DocumentItem.Materia):
            case nameof(DocumentItem.Identificador):
                item.SetGeneratedProposedName(NameBuilder.BuildName(item.Fecha, item.Codigo, item.Materia,
                    item.Identificador, Path.GetExtension(item.OriginalPath), perfil));
                break;
            case nameof(DocumentItem.ProposedName):
                if (!item.ActualizandoPropuesta)
                    item.PropuestaManual = true;
                break;
            case nameof(DocumentItem.IsSelected):
                _aplicarCommand?.RaiseCanExecuteChanged();
                break;
        }
    }

    public void RecalcularPropuestas()
    {
        var perfil = _profiles.PerfilActivo;
        if (perfil == null) return;
        foreach (var item in Documentos)
        {
            if (item.PropuestaManual) continue;
            item.SetGeneratedProposedName(NameBuilder.BuildName(item.Fecha, item.Codigo, item.Materia,
                item.Identificador, Path.GetExtension(item.OriginalPath), perfil));
        }
    }

    private void RefrescarCatalogo()
    {
        CatalogoActivo.Clear();
        foreach (var e in _profiles.PerfilActivo?.Catalogo ?? Enumerable.Empty<CatalogoEntry>())
            CatalogoActivo.Add(e);
    }

    public async Task AnalizarAsync()
    {
        if (Analizando) return;
        var perfil = _profiles.PerfilActivo;
        if (perfil == null) { Estado = "No hay perfil activo"; return; }
        _cts = new CancellationTokenSource();
        Analizando = true;
        Progreso = 0;
        Total = Documentos.Count;
        Estado = $"Analizando {Total} documentos...";
        try
        {
            foreach (var item in Documentos)
            {
                if (_cts.IsCancellationRequested) break;
                item.Status = AnalysisStatus.Analizando;
                item.StatusMessage = "Extrayendo texto...";
                try
                {
                    var ext = await _extractor.ExtractAsync(item.OriginalPath, _cts.Token);
                    if (ext.Advertencia != null && ext.Texto.Length == 0)
                    {
                        item.Status = AnalysisStatus.Advertencia;
                        item.StatusMessage = ext.Advertencia;
                        Progreso++;
                        continue;
                    }

                    item.StatusMessage = "Analizando con IA...";
                    var propuesta = await _analyzer.AnalyzeAsync(ext.Texto, item.FolderName, item.CurrentName, perfil, _cts.Token);

                    item.Fecha = propuesta.Fecha;
                    if (item.Fecha.Length == 0)
                        item.Fecha = File.GetLastWriteTime(item.OriginalPath).ToString("yyyyMMdd");
                    item.Codigo = propuesta.Codigo;
                    item.Materia = propuesta.Materia;
                    item.Identificador = propuesta.Identificador;
                    item.SetGeneratedProposedName(NameBuilder.BuildName(item.Fecha, item.Codigo, item.Materia,
                        item.Identificador, Path.GetExtension(item.OriginalPath), perfil));
                    item.Status = item.ProposedName.Length > 0 ? AnalysisStatus.Propuesto : AnalysisStatus.Advertencia;
                    item.StatusMessage = ext.Advertencia
                        ?? (item.ProposedName.Length > 0 ? $"Propuesta generada ({ext.Metodo})" : "Datos insuficientes para proponer nombre");
                }
                catch (OperationCanceledException) { item.Status = AnalysisStatus.Pendiente; item.StatusMessage = "Cancelado"; }
                catch (Exception ex)
                {
                    item.Status = AnalysisStatus.Error;
                    item.StatusMessage = $"Error: {ex.Message}. Compruebe que Ollama está en ejecución (http://127.0.0.1:11434).";
                }
                Progreso++;
            }
            Estado = _cts.IsCancellationRequested
                ? "Análisis cancelado por el usuario"
                : $"Análisis completado: {Documentos.Count(d => d.Status == AnalysisStatus.Propuesto)} propuestas generadas";
        }
        finally
        {
            Analizando = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    public async Task AplicarAsync()
    {
        if (Analizando) return;
        try
        {
            Estado = "Aplicando renombrado...";
            var batch = await _rename.ApplyAsync(Documentos.Where(d => d.IsSelected).ToList(), CancellationToken.None);
            _ultimoLote = batch;
            if (batch.Entradas.Count > 0) _historialSesion.Add(batch);
            Estado = $"Renombrados {batch.Entradas.Count} archivos";
        }
        catch (Exception ex)
        {
            Estado = "No se pudo aplicar el renombrado: " + ex.Message;
        }
    }

    public async Task DeshacerAsync()
    {
        if (Analizando) return;
        try
        {
            Estado = "Deshaciendo último lote...";
            var r = await _rename.RollbackLastAsync(CancellationToken.None);
            Estado = r.Errores.Count > 0
                ? "Error al deshacer: " + string.Join("; ", r.Errores)
                : $"Revertidos {r.Revertidos} archivos" + (r.Omitidos.Count > 0 ? $"; omitidos {r.Omitidos.Count}" : "");
        }
        catch (Exception ex)
        {
            Estado = "No se pudo deshacer el lote: " + ex.Message;
        }
    }

    public IEnumerable<CsvExporter.Fila> FilasExportacion()
    {
        IEnumerable<RenameBatch> lotes = _historialSesion.Count > 0
            ? _historialSesion
            : (_ultimoLote != null ? new[] { _ultimoLote } : Array.Empty<RenameBatch>());
        foreach (var lote in lotes)
            foreach (var e in lote.Entradas)
                yield return new CsvExporter.Fila(
                    Path.GetFileName(e.OldPath), e.OldPath,
                    Path.GetFileName(e.NewPath), e.NewPath, "Renombrado");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

