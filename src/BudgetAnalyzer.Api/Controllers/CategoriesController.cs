using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Application.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetAnalyzer.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly CategoryService _categoryService;
    private readonly ICurrentUser _currentUser;

    public CategoriesController(CategoryService categoryService, ICurrentUser currentUser)
    {
        _categoryService = categoryService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<List<CategoryResponse>>> List(
        [FromQuery] bool includeArchived = false,
        CancellationToken ct = default)
    {
        var categories = await _categoryService.ListAsync(_currentUser.UserId, includeArchived, ct);
        return Ok(categories);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(
        [FromBody] CreateCategoryRequest request,
        CancellationToken ct)
    {
        var category = await _categoryService.CreateAsync(_currentUser.UserId, request.Name!, ct);
        return CreatedAtAction(nameof(List), new { }, category);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> Rename(
        Guid id,
        [FromBody] RenameCategoryRequest request,
        CancellationToken ct)
    {
        var category = await _categoryService.RenameAsync(_currentUser.UserId, id, request.Name!, ct);
        return Ok(category);
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await _categoryService.ArchiveAsync(_currentUser.UserId, id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/unarchive")]
    public async Task<IActionResult> Unarchive(Guid id, CancellationToken ct)
    {
        await _categoryService.UnarchiveAsync(_currentUser.UserId, id, ct);
        return NoContent();
    }
}
