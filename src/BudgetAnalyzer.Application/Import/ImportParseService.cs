using BudgetAnalyzer.Application.Import.Dtos;

namespace BudgetAnalyzer.Application.Import;

public class ImportParseService : IImportParseService
{
    private readonly ITempFileStore _store;
    private readonly IXlsxParser _parser;

    public ImportParseService(ITempFileStore store, IXlsxParser parser)
    {
        _store = store;
        _parser = parser;
    }

    public async Task<ParseResultDto> ParseAsync(Stream fileStream, CancellationToken ct = default)
    {
        var fileId = await _store.SaveAsync(fileStream, ct);
        var path = _store.GetFilePath(fileId);
        var columns = _parser.DetectColumns(path);
        return new ParseResultDto(fileId, columns);
    }
}
