using Kelvinvale.Application.Interfaces;
using Kelvinvale.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;
using System.Text.Json;

namespace Kelvinvale.Api.Filters
{
    public class AuditLogActionFilter : IAsyncActionFilter
    {
        private readonly IAuditRepository _auditRepo;
        private readonly ILogger<AuditLogActionFilter> _logger;

        public AuditLogActionFilter(IAuditRepository auditRepo, ILogger<AuditLogActionFilter> logger)
        {
            _auditRepo = auditRepo;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var stopwatch = Stopwatch.StartNew();

            // 1. Resolve caller GUID from header
            var callerIdHeader = context.HttpContext.Request.Headers["X-Caller-Id"].FirstOrDefault();
            var hasValidCallerId = Guid.TryParse(callerIdHeader, out var callerId);

            // 2. Fetch role through repository abstraction
            var callerRole = "Anonymous";
            if (hasValidCallerId)
            {
                var role = await _auditRepo.GetUserRoleNameByIdAsync(callerId);
                if (!string.IsNullOrEmpty(role))
                {
                    callerRole = role;
                }
            }

            // 3. Execute the controller endpoint
            var executedContext = await next();
            stopwatch.Stop();

            var httpContext = context.HttpContext;
            var statusCode = httpContext.Response.StatusCode;

            // 4. Resolve customer ID if available from route parameters
            Guid? customerId = null;
            if (context.RouteData.Values.TryGetValue("customerId", out var routeCustomerVal) &&
                Guid.TryParse(routeCustomerVal?.ToString(), out var parsedCustomerId))
            {
                customerId = parsedCustomerId;
            }

            // 5. Resolve EntityId (from created/returned response object or from route customerId)
            Guid entityId = customerId ?? Guid.Empty;
            if (executedContext.Result is ObjectResult objectResult && objectResult.Value != null)
            {
                var idProperty = objectResult.Value.GetType().GetProperty("Id")
                                 ?? objectResult.Value.GetType().GetProperty("id");

                if (idProperty != null && Guid.TryParse(idProperty.GetValue(objectResult.Value)?.ToString(), out var extractedId))
                {
                    entityId = extractedId;
                }
            }

            // 6. Build execution details
            var actionName = $"{httpContext.Request.Method} {httpContext.Request.Path}";
            var entityName = context.Controller.GetType().Name.Replace("Controller", string.Empty);
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            var timestampUtc = DateTime.UtcNow;

            var payloadDetails = JsonSerializer.Serialize(new
            {
                RouteValues = context.RouteData.Values,
                ActionArguments = context.ActionArguments,
                StatusCode = statusCode,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                Exception = executedContext.Exception?.Message
            });

            // 7. Structured Console / Observability Logging
            _logger.LogInformation(
                "COMPLIANCE_AUDIT: CallerId={CallerId}, CallerRole={CallerRole}, Action={Action}, Entity={EntityName}, EntityId={EntityId}, CustomerId={CustomerId}, StatusCode={StatusCode}, ElapsedMs={ElapsedMs}",
                callerId, callerRole, actionName, entityName, entityId, customerId, statusCode, stopwatch.ElapsedMilliseconds);

            // 8. Persist entry via repository
            var auditEntry = new AuditLog
            {
                Id = Guid.NewGuid(),
                CallerId = callerId,
                CallerRole = callerRole,
                Action = actionName,
                EntityName = entityName,
                EntityId = entityId,
                CustomerId = customerId,
                Details = payloadDetails.Length > 4000 ? payloadDetails[..4000] : payloadDetails,
                Timestamp = timestampUtc,
                IpAddress = ipAddress
            };

            try
            {
                await _auditRepo.InsertAuditLogAsync(auditEntry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist compliance audit log for request {TraceIdentifier}", httpContext.TraceIdentifier);
            }
        }
    }
}
