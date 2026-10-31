using System.IO;
using System.Text.Json;
using Arconte.App.Models;

namespace Arconte.App.Services;

public sealed class RenameService : IRenameService
{
    private readonly string _logDir;

    public RenameService(string? logDir = null)
    {
        _logDir = logDir ?? Path.Combine(AppContext.BaseDirectory, "logs");
    }

    public Task<RenameBatch> ApplyAsync(IReadOnlyList<DocumentItem> items, CancellationToken ct)
    {
        var batch = new RenameBatch { FechaHora = DateTime.Now };
        var nombresEnLote = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var logVerificado = false;
        batch.ArchivoLog = Path.Combine(_logDir, $"lote_{batch.FechaHora:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.json");

        foreach (var item in items.Where(i => i.IsSelected))
        {
            if (ct.IsCancellationRequested) break;
            var dir = Path.GetDirectoryName(item.OriginalPath) ?? "";
            var nombreDeseado = item.ProposedName;
            if (!NameBuilder.TryValidateFileName(nombreDeseado ?? "", Path.GetExtension(item.OriginalPath), out var errorValidacion))
            {
                item.Status = AnalysisStatus.Advertencia;
                item.StatusMessage = errorValidacion;
                continue;
            }

            var destino = Path.Combine(dir, nombreDeseado!);
            if (string.Equals(Path.GetFullPath(destino), Path.GetFullPath(item.OriginalPath), StringComparison.OrdinalIgnoreCase))
            {
                item.Status = AnalysisStatus.Advertencia;
                item.StatusMessage = "El nombre propuesto coincide con el actual";
                continue;
            }

            var candidato = nombreDeseado!;
            while (nombresEnLote.Contains(Path.Combine(dir, candidato)) || File.Exists(Path.Combine(dir, candidato)))
            {
                if (string.Equals(Path.Combine(dir, candidato), item.OriginalPath, StringComparison.OrdinalIgnoreCase)
                    && !nombresEnLote.Contains(Path.Combine(dir, candidato)))
                    break;
                candidato = NameBuilder.SufijoColision(candidato);
            }

            var rutaDestino = Path.Combine(dir, candidato);
            if (string.Equals(Path.GetFullPath(rutaDestino), Path.GetFullPath(item.OriginalPath), StringComparison.OrdinalIgnoreCase))
            {
                item.Status = AnalysisStatus.Advertencia;
                item.StatusMessage = "El nombre propuesto coincide con el actual";
                continue;
            }
            if (!logVerificado)
            {
                VerificarLogEscribible();
                logVerificado = true;
            }
            try
            {
                File.Move(item.OriginalPath, rutaDestino);
                nombresEnLote.Add(rutaDestino);
                batch.Entradas.Add(new RenameEntry(item.OriginalPath, rutaDestino));
                PersistirBatch(batch);
                item.Status = AnalysisStatus.Renombrado;
                item.StatusMessage = "Renombrado a " + candidato;
                item.ProposedName = candidato;
            }
            catch (Exception ex)
            {
                item.Status = AnalysisStatus.Error;
                item.StatusMessage = "Error de renombrado o registro: " + ex.Message;
                if (batch.Entradas.Any(e => e.OldPath == item.OriginalPath)) throw;
            }
        }

        if (batch.Entradas.Count > 0)
            PersistirBatch(batch);
        return Task.FromResult(batch);
    }

    public Task<RollbackResult> RollbackLastAsync(CancellationToken ct)
    {
        var resultado = new RollbackResult();
        var archivo = UltimoBatch();
        if (archivo == null)
        {
            resultado.Errores.Add("No hay lotes de renombrado registrados");
            return Task.FromResult(resultado);
        }

        RenameBatch? batch;
        try
        {
            batch = JsonSerializer.Deserialize<RenameBatch>(File.ReadAllText(archivo));
        }
        catch (Exception ex)
        {
            resultado.Errores.Add("No se pudo leer el historial: " + ex.Message);
            return Task.FromResult(resultado);
        }
        if (batch == null) return Task.FromResult(resultado);

        var pendientes = new List<RenameEntry>();
        foreach (var entrada in batch.Entradas.AsEnumerable().Reverse())
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (!File.Exists(entrada.NewPath))
                {
                    resultado.Omitidos.Add($"No existe: {entrada.NewPath}");
                    pendientes.Add(entrada);
                    continue;
                }
                if (File.Exists(entrada.OldPath))
                {
                    resultado.Omitidos.Add($"Destino original ocupado, no se sobreescribe: {entrada.OldPath}");
                    pendientes.Add(entrada);
                    continue;
                }
                File.Move(entrada.NewPath, entrada.OldPath);
                resultado.Revertidos++;
            }
            catch (Exception ex)
            {
                resultado.Errores.Add($"{entrada.NewPath}: {ex.Message}");
                pendientes.Add(entrada);
            }
        }

        try
        {
            if (pendientes.Count == 0)
            {
                File.Delete(archivo);
            }
            else
            {
                pendientes.Reverse();
                var restante = new RenameBatch { FechaHora = batch.FechaHora, Entradas = pendientes };
                var tmp = archivo + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(restante));
                File.Move(tmp, archivo, true);
            }
        }
        catch (Exception ex)
        {
            resultado.Errores.Add("No se pudo actualizar el historial: " + ex.Message);
        }
        return Task.FromResult(resultado);
    }

    private void VerificarLogEscribible()
    {
        try
        {
            Directory.CreateDirectory(_logDir);
            var sonda = Path.Combine(_logDir, Guid.NewGuid().ToString("N") + ".probe");
            File.WriteAllText(sonda, "");
            File.Delete(sonda);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "No se puede crear el registro de rollback en la carpeta de logs; compruebe permisos de escritura", ex);
        }
    }

    private void PersistirBatch(RenameBatch batch)
    {
        Directory.CreateDirectory(_logDir);
        var archivo = batch.ArchivoLog!;
        var tmp = archivo + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(batch, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, archivo, true);
        batch.ArchivoLog = archivo;
    }

    private string? UltimoBatch()
    {
        if (!Directory.Exists(_logDir)) return null;
        return Directory.EnumerateFiles(_logDir, "lote_*.json")
            .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }
}

