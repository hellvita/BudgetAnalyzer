using BudgetAnalyzer.Application.Import;

namespace BudgetAnalyzer.Infrastructure.Import;

public class TempFileStore : ITempFileStore
{
    private static readonly string BaseDir =
        Path.Combine(Path.GetTempPath(), "budget-import");

    public TempFileStore()
    {
        Directory.CreateDirectory(BaseDir);
    }

    public async Task<string> SaveAsync(Stream fileStream, CancellationToken ct = default)
    {
        var fileId = Guid.NewGuid().ToString("N");
        await using var fs = File.Create(BuildPath(fileId));
        await fileStream.CopyToAsync(fs, ct);
        return fileId;
    }

    public string GetFilePath(string fileId) => BuildPath(fileId);

    public bool Exists(string fileId) => File.Exists(BuildPath(fileId));

    public void Delete(string fileId)
    {
        var path = BuildPath(fileId);
        if (File.Exists(path))
            File.Delete(path);
    }

    public IEnumerable<(string fileId, DateTime createdAtUtc)> ListAll() =>
        Directory.EnumerateFiles(BaseDir, "*.xlsx")
            .Select(p => (
                fileId: Path.GetFileNameWithoutExtension(p),
                createdAtUtc: File.GetCreationTimeUtc(p)
            ));

    private static string BuildPath(string fileId) =>
        Path.Combine(BaseDir, $"{fileId}.xlsx");
}
