using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using ClinicScheduling.Application.DTOs.Common;

namespace ClinicScheduling.Infrastructure.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred. TraceId: {TraceId}", context.TraceIdentifier);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        var response = new ApiResponse<object>
        {
            Success = false,
            TraceId = context.TraceIdentifier
        };

        switch (exception)
        {
            case ValidationException validationEx:
                response.ErrorCode = "VALIDATION_ERROR";
                response.Message = "Validation failed";
                response.Errors = validationEx.Errors;
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                break;

            case NotFoundException notFoundEx:
                response.ErrorCode = "NOT_FOUND";
                response.Message = notFoundEx.Message;
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                break;

            case BusinessRuleException businessEx:
                response.ErrorCode = "BUSINESS_RULE_VIOLATION";
                response.Message = businessEx.Message;
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                break;

            case ConcurrencyException concurrencyEx:
                response.ErrorCode = "CONCURRENCY_CONFLICT";
                response.Message = "The record has been modified by another user. Please refresh and try again.";
                context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                break;

            case UnauthorizedAccessException:
                response.ErrorCode = "UNAUTHORIZED";
                response.Message = "You are not authorized to perform this action";
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                break;

            case TimeoutException:
                response.ErrorCode = "TIMEOUT";
                response.Message = "The operation timed out. Please try again.";
                context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                break;

            case InvalidOperationException invalidOpEx when invalidOpEx.Message.Contains("database"):
                response.ErrorCode = "DATABASE_ERROR";
                response.Message = "A database error occurred. Please try again later.";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                break;

            default:
                response.ErrorCode = "INTERNAL_SERVER_ERROR";
                response.Message = "An internal server error occurred";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                break;
        }

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}

// Custom exception types for better error handling
public class ValidationException : Exception
{
    public Dictionary<string, string[]> Errors { get; }

    public ValidationException(Dictionary<string, string[]> errors) 
        : base("One or more validation errors occurred")
    {
        Errors = errors;
    }

    public ValidationException(string field, string error) 
        : base("Validation error occurred")
    {
        Errors = new Dictionary<string, string[]>
        {
            { field, new[] { error } }
        };
    }
}

public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key) 
        : base($"{entityName} with key '{key}' was not found")
    {
    }

    public NotFoundException(string message) : base(message)
    {
    }
}

public class BusinessRuleException : Exception
{
    public string RuleCode { get; }

    public BusinessRuleException(string ruleCode, string message) : base(message)
    {
        RuleCode = ruleCode;
    }
}

public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message)
    {
    }

    public ConcurrencyException() : base("The record has been modified by another user")
    {
    }
}

// Extension method to register the middleware
public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
