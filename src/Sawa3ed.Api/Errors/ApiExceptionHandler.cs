using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sawa3ed.Application.Common;

namespace Sawa3ed.Api.Errors;

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, code, detail) = exception switch
        {
            AppException app => (app.StatusCode, app.Code, app.Message),
            DbUpdateConcurrencyException => (409, "concurrent_update", "The resource changed. Reload it and try again."),
            SqlException { Number: 1205 } => (409, "concurrent_update", "A concurrent operation conflicted. Retry the request."),
            SqliteException { SqliteErrorCode: 5 or 6 } => (409, "concurrent_update", "A concurrent operation conflicted. Retry the request."),
            DbUpdateException { InnerException: SqlException { Number: 1205 } } => (409, "concurrent_update", "A concurrent operation conflicted. Retry the request."),
            DbUpdateException { InnerException: SqliteException { SqliteErrorCode: 5 or 6 } } => (409, "concurrent_update", "A concurrent operation conflicted. Retry the request."),
            DbUpdateException { InnerException: SqliteException { SqliteErrorCode: 19 } } => (409, "conflict", "The request conflicts with existing data."),
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => (409, "conflict", "The request conflicts with existing data."),
            BadHttpRequestException bad => (bad.StatusCode, "invalid_request", "The request could not be read."),
            OperationCanceledException when context.RequestAborted.IsCancellationRequested => (499, "cancelled", "The request was cancelled."),
            _ => (500, "internal_error", "An unexpected error occurred. Use the trace ID when contacting support.")
        };
        // Do not serialize provider exceptions, connection strings, request bodies or credentials into logs.
        if (status >= 500) logger.LogError("Request failed ({ErrorType}), trace {TraceId}", exception.GetType().Name, Activity.Current?.Id ?? context.TraceIdentifier);
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Title = code, Detail = detail, Extensions = { ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier } }
        });
    }
}
