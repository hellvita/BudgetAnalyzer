using System.ComponentModel.DataAnnotations;

namespace BudgetAnalyzer.Application.Categories;

public record CategoryResponse(Guid Id, string Name, bool IsArchived);

public record CreateCategoryRequest([Required][StringLength(100, MinimumLength = 1)] string? Name);

public record RenameCategoryRequest([Required][StringLength(100, MinimumLength = 1)] string? Name);
