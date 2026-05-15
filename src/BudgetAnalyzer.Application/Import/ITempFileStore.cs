namespace BudgetAnalyzer.Application.Import;

public interface ITempFileStore
{
    Task<string> SaveAsync(Stream fileStream, CancellationToken ct = default);
    string GetFilePath(string fileId);
    bool Exists(string fileId);
    void Delete(string fileId);
    IEnumerable<(string fileId, DateTime createdAtUtc)> ListAll();
}
