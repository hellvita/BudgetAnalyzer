using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BudgetAnalyzer.Api.Auth;
using BudgetAnalyzer.Api.BackgroundServices;
using BudgetAnalyzer.Api.Middleware;
using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Application.Auth;
using BudgetAnalyzer.Application.Budget;
using BudgetAnalyzer.Application.Categories;
using BudgetAnalyzer.Application.Expenses;
using BudgetAnalyzer.Application.Export;
using BudgetAnalyzer.Application.Import;
using BudgetAnalyzer.Application.Incomes;
using BudgetAnalyzer.Application.Limits;
using BudgetAnalyzer.Application.Summaries;
using BudgetAnalyzer.Application.Users;
using BudgetAnalyzer.Infrastructure.Auth;
using BudgetAnalyzer.Infrastructure.BackgroundServices;
using BudgetAnalyzer.Infrastructure.Export;
using BudgetAnalyzer.Infrastructure.Import;
using BudgetAnalyzer.Infrastructure.Persistence;
using BudgetAnalyzer.Infrastructure.Persistence.Repositories;
using BudgetAnalyzer.Infrastructure.Time;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();

builder.Services.Configure<SecurityOptions>(
    builder.Configuration.GetSection(SecurityOptions.SectionName));

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<BudgetService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<ExpenseService>();
builder.Services.AddScoped<IncomeService>();
builder.Services.AddScoped<LimitService>();
builder.Services.AddScoped<SummaryService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ICurrentToken, CurrentToken>();
builder.Services.AddScoped<ITokenRevocationService, EfTokenRevocationService>();
builder.Services.AddHostedService<TokenCleanupService>();

// Import
builder.Services.AddSingleton<ITempFileStore, TempFileStore>();
builder.Services.AddScoped<IXlsxParser, ClosedXmlParser>();
builder.Services.AddScoped<IImportParseService, ImportParseService>();
builder.Services.AddScoped<IImportPreviewService, ImportPreviewService>();
builder.Services.AddScoped<IImportExecuteService, ImportExecuteService>();
builder.Services.AddHostedService<TempFileCleanupService>();

// Export
builder.Services.AddScoped<IExportRenderer, ClosedXmlExportService>();
builder.Services.AddScoped<IExportService, ExportService>();

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(opt =>
{
    opt.MultipartBodyLengthLimit = 10 * 1024 * 1024;
});

var jwtKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is required.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var jti = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
                if (jti is null)
                { context.Fail("Missing jti claim."); return; }

                var revocation = context.HttpContext.RequestServices
                    .GetRequiredService<ITokenRevocationService>();

                if (await revocation.IsRevokedAsync(jti, context.HttpContext.RequestAborted))
                    context.Fail("Token has been revoked.");
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
