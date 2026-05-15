using BudgetAnalyzer.Api.Controllers.Requests;
using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Application.Import;
using BudgetAnalyzer.Application.Import.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetAnalyzer.Api.Controllers;

[ApiController]
[Route("api/import")]
[Authorize]
public class ImportController : ControllerBase
{
    private readonly IImportParseService _parseService;
    private readonly IImportPreviewService _previewService;
    private readonly IImportExecuteService _executeService;
    private readonly ICurrentUser _currentUser;

    public ImportController(
        IImportParseService parseService,
        IImportPreviewService previewService,
        IImportExecuteService executeService,
        ICurrentUser currentUser)
    {
        _parseService   = parseService;
        _previewService = previewService;
        _executeService = executeService;
        _currentUser    = currentUser;
    }

    /// <summary>Upload an xlsx file. Returns detected non-empty columns + a fileId for subsequent calls.</summary>
    [HttpPost("parse")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ParseResultDto>> Parse(
        IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file received.");

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .xlsx files are supported.");

        await using var stream = file.OpenReadStream();
        var result = await _parseService.ParseAsync(stream, ct);
        return Ok(result);
    }

    /// <summary>Apply column mapping to the uploaded file and return the first 10 rows for review.</summary>
    [HttpPost("preview")]
    public async Task<ActionResult<PreviewResultDto>> Preview(
        [FromBody] ColumnMappingRequest req, CancellationToken ct)
    {
        var result = await _previewService.PreviewAsync(ToDto(req), ct);
        return Ok(result);
    }

    /// <summary>Execute the import — upserts expenses and incomes into the database.</summary>
    [HttpPost("execute")]
    public async Task<ActionResult<ImportResultDto>> Execute(
        [FromBody] ColumnMappingRequest req, CancellationToken ct)
    {
        var result = await _executeService.ExecuteAsync(_currentUser.UserId, ToDto(req), ct);
        return Ok(result);
    }

    private static ColumnMappingDto ToDto(ColumnMappingRequest r) =>
        new(r.FileId, r.DateColumnIndex, r.CategoryColumnIndexes,
            r.IncomeColumnIndex, r.ScaleFactor, r.InvertSign);
}
