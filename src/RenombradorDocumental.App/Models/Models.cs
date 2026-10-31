using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace RenombradorDocumental.App.Models;

public sealed record DocumentProposal(string Fecha, string Codigo, string Materia, string Identificador);

public enum AnalysisStatus { Pendiente, Analizando, Propuesto, Advertencia, Error, Renombrado }

public enum ExtractionMethod { Directo, OCR, Metadatos }

public sealed record ExtractionResult(string Texto, ExtractionMethod Metodo, string? Advertencia = null);

public sealed class CatalogoEntry
{
    public CatalogoEntry() { }
    public CatalogoEntry(string codigo, string categoria, string descripcion)
    { Codigo = codigo; Categoria = categoria; Descripcion = descripcion; }
    public string Codigo { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string Descripcion { get; set; } = "";
}

public sealed class Profile
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string FormatoFecha { get; set; } = "YYYYMMDD";
    public string Separador { get; set; } = " ";
    public int LongitudCodigo { get; set; } = 3;
    public string ReglaCaja { get; set; } = "mayusculas_codigo_resto_capitalizado";
    public bool ExtensionMayusculas { get; set; }
    public List<string> OrdenEstructura { get; set; } = new() { "FECHA", "CODIGO", "MATERIA", "IDENTIFICADOR" };
    public List<CatalogoEntry> Catalogo { get; set; } = new();
}

public sealed record RenameEntry(string OldPath, string NewPath);

public sealed class RenameBatch
{
    public DateTime FechaHora { get; set; } = DateTime.Now;
    public List<RenameEntry> Entradas { get; set; } = new();
    public string? ArchivoLog { get; set; }
}

public sealed class RollbackResult
{
    public int Revertidos { get; set; }
    public List<string> Omitidos { get; set; } = new();
    public List<string> Errores { get; set; } = new();
}

public sealed class DocumentItem : INotifyPropertyChanged
{
    private string _fecha = "";
    private string _codigo = "";
    private string _materia = "";
    private string _identificador = "";
    private string _proposedName = "";
    private AnalysisStatus _status = AnalysisStatus.Pendiente;
    private string _statusMessage = "";
    private bool _isSelected = true;
    private bool _propuestaManual;

    public required string OriginalPath { get; init; }
    public string CurrentName => Path.GetFileName(OriginalPath);
    public string FolderName => Path.GetFileName(Path.GetDirectoryName(OriginalPath) ?? "") ?? "";

    public string Fecha { get => _fecha; set { _fecha = value; OnChanged(); } }
    public string Codigo { get => _codigo; set { _codigo = value; OnChanged(); } }
    public string Materia { get => _materia; set { _materia = value; OnChanged(); } }
    public string Identificador { get => _identificador; set { _identificador = value; OnChanged(); } }
    public string ProposedName { get => _proposedName; set { _proposedName = value; OnChanged(); } }
    public AnalysisStatus Status { get => _status; set { _status = value; OnChanged(); } }
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnChanged(); } }
    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnChanged(); } }

    public bool PropuestaManual { get => _propuestaManual; set { _propuestaManual = value; OnChanged(); } }

    private bool _actualizandoPropuesta;
    public bool ActualizandoPropuesta => _actualizandoPropuesta;
    public void SetGeneratedProposedName(string value)
    {
        _actualizandoPropuesta = true;
        ProposedName = value;
        _actualizandoPropuesta = false;
        PropuestaManual = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
