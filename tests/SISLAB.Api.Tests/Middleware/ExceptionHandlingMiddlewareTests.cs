using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SISLAB.Api.Middleware;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Observability;

namespace SISLAB.Api.Tests.Middleware;

/// <summary>
/// Tests for <see cref="ExceptionHandlingMiddleware"/> after its migration to RFC 7807
/// <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> (card [E9] #59): status/type mapping per exception,
/// the validation <c>errors</c> map, the <c>traceId</c>/<c>instance</c> members, and production detail hiding.
/// </summary>
public sealed class ExceptionHandlingMiddlewareTests
{
    private const string CorrelationId = "corr-123";

    [Fact]
    public async Task BusinessException_maps_to_422_business_rule_violation()
    {
        JsonElement problem = await InvokeAsync(
            _ => throw new BusinessException("Balance would go negative."),
            path: "/api/inventory/stock/consume");

        Assert.Equal(422, problem.GetProperty("status").GetInt32());
        Assert.Equal("https://sislab.app/errors/business-rule-violation", problem.GetProperty("type").GetString());
        Assert.Equal("Balance would go negative.", problem.GetProperty("detail").GetString());
        Assert.Equal(CorrelationId, problem.GetProperty("traceId").GetString());
        Assert.Equal("/api/inventory/stock/consume", problem.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task NotFoundException_maps_to_404_not_found()
    {
        JsonElement problem = await InvokeAsync(_ => throw new NotFoundException("StockItem", Guid.Empty));

        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        Assert.Equal("https://sislab.app/errors/not-found", problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task ConflictException_maps_to_409_conflict()
    {
        JsonElement problem = await InvokeAsync(_ => throw new ConflictException("Asset tag already in use."));

        Assert.Equal(409, problem.GetProperty("status").GetInt32());
        Assert.Equal("https://sislab.app/errors/conflict", problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task ForbiddenException_maps_to_403_forbidden()
    {
        JsonElement problem = await InvokeAsync(_ => throw new ForbiddenException());

        Assert.Equal(403, problem.GetProperty("status").GetInt32());
        Assert.Equal("https://sislab.app/errors/forbidden", problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task ValidationException_maps_to_400_with_field_errors()
    {
        var failures = new[]
        {
            new ValidationFailure("Quantity", "Quantity must be greater than 0."),
            new ValidationFailure("Unit", "Unit is required.")
        };

        JsonElement problem = await InvokeAsync(_ => throw new ValidationException(failures));

        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal("https://sislab.app/errors/validation-error", problem.GetProperty("type").GetString());

        JsonElement errors = problem.GetProperty("errors");
        Assert.Equal("Quantity must be greater than 0.", errors.GetProperty("Quantity")[0].GetString());
        Assert.Equal("Unit is required.", errors.GetProperty("Unit")[0].GetString());
    }

    [Fact]
    public async Task UnhandledException_maps_to_500_and_hides_detail_in_production()
    {
        JsonElement problem = await InvokeAsync(
            _ => throw new InvalidOperationException("Secret internal detail."),
            isDevelopment: false);

        Assert.Equal(500, problem.GetProperty("status").GetInt32());
        Assert.Equal("https://sislab.app/errors/internal-error", problem.GetProperty("type").GetString());
        Assert.Equal("An unexpected error occurred.", problem.GetProperty("detail").GetString());
        Assert.DoesNotContain("Secret internal detail.", problem.GetRawText());
    }

    [Fact]
    public async Task UnhandledException_exposes_detail_in_development()
    {
        JsonElement problem = await InvokeAsync(
            _ => throw new InvalidOperationException("Diagnostic detail."),
            isDevelopment: true);

        Assert.Equal("Diagnostic detail.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Response_uses_the_problem_json_content_type()
    {
        var context = BuildContext("/api/x");
        await RunAsync(context, _ => throw new BusinessException("x"), isDevelopment: true);

        Assert.StartsWith("application/problem+json", context.Response.ContentType);
    }

    private static async Task<JsonElement> InvokeAsync(
        RequestDelegate throwingNext,
        string path = "/api/x",
        bool isDevelopment = true)
    {
        var context = BuildContext(path);
        await RunAsync(context, throwingNext, isDevelopment);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        string json = await reader.ReadToEndAsync();

        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private static Task RunAsync(HttpContext context, RequestDelegate throwingNext, bool isDevelopment)
    {
        var middleware = new ExceptionHandlingMiddleware(
            throwingNext,
            NullLogger<ExceptionHandlingMiddleware>.Instance,
            new StubHostEnvironment(isDevelopment));

        return middleware.InvokeAsync(context, new StubCorrelationIdAccessor(CorrelationId));
    }

    private static DefaultHttpContext BuildContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class StubCorrelationIdAccessor : ICorrelationIdAccessor
    {
        public StubCorrelationIdAccessor(string correlationId) => CorrelationId = correlationId;

        public string CorrelationId { get; }
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public StubHostEnvironment(bool isDevelopment) =>
            EnvironmentName = isDevelopment ? Environments.Development : Environments.Production;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "SISLAB.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
