using RenombradorDocumental.App.Models;

namespace RenombradorDocumental.App.Services;

public interface IDocumentTextExtractor
{
    Task<ExtractionResult> ExtractAsync(string path, CancellationToken ct);
}

public interface ILocalDocumentAnalyzer
{
    Task<DocumentProposal> AnalyzeAsync(string extractedText, string folderName, string originalName, Profile profile, CancellationToken ct);
}

public interface IRenameService
{
    Task<RenameBatch> ApplyAsync(IReadOnlyList<DocumentItem> items, CancellationToken ct);
    Task<RollbackResult> RollbackLastAsync(CancellationToken ct);
}
