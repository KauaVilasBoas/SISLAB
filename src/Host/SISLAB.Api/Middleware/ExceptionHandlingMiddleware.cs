using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Http;

namespace SISLAB.Api.Middleware;

/// <summary>
/// Translates unhandled exceptions into the uniform <see cref="ApiResult"/> error envelope.
///
/// <para>Mapping:</para>
/// <list type="bullet">
///   <item><see cref="BusinessException"/> → 422 Unprocessable Entity</item>
///   <item><see cref="NotFoundException"/> → 404 Not Found</item>
///   <item>any other exception → 500 Internal Server Error (message hidden, full error logged)</item>
/// </list>
///
/// Controllers therefore never map errors to HTTP themselves: they map only the happy path
/// and let domain/application exceptions bubble up to this single, uniform boundary.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BusinessException ex)
        {
            await WriteAsync(context, StatusCodes.Status422UnprocessableEntity, ex.Message);
        }
        catch (NotFoundException ex)
        {
            await WriteAsync(context, StatusCodes.Status404NotFound, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}.",
                context.Request.Method, context.Request.Path);

            await WriteAsync(context, StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.");
        }
    }

    private static Task WriteAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(new ApiResult(false, message));
    }
}
