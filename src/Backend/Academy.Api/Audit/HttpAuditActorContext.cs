using System.Security.Claims;
using Academy.Application.Abstractions;

namespace Academy.Api.Audit;

public sealed class HttpAuditActorContext :
    IAuditActorContext
{
    private static readonly HashSet<string>
        HumanRoles =
        new(
            new[]
            {
                "Owner",
                "Admin",
                "Manager"
            },
            StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string>
        MutationMethods =
        new(
            new[]
            {
                "POST",
                "PUT",
                "PATCH",
                "DELETE"
            },
            StringComparer.OrdinalIgnoreCase);

    private readonly IHttpContextAccessor
        _httpContextAccessor;

    public HttpAuditActorContext(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor =
            httpContextAccessor;
    }

    private HttpContext? Context =>
        _httpContextAccessor.HttpContext;

    public Guid UserId
    {
        get
        {
            string? raw =
                Context?.User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            return Guid.TryParse(
                raw,
                out Guid parsed)
                    ? parsed
                    : Guid.Empty;
        }
    }

    public string FullName =>
        Context?.User.FindFirstValue(
            "full_name")
        ?? Context?.User.FindFirstValue(
            ClaimTypes.Email)
        ?? "Unknown User";

    public string Role =>
        Context?.User.FindFirstValue(
            ClaimTypes.Role)
        ?? string.Empty;

    public string? RequestMethod =>
        Context?.Request.Method;

    public string? RequestPath =>
        Context?.Request.Path.Value;

    public string? RequestId =>
        Context?.TraceIdentifier;

    public string? UserAgent =>
        Context?.Request.Headers.UserAgent
            .ToString();

    public string? IpAddress
    {
        get
        {
            HttpContext? context =
                Context;

            if (context is null)
            {
                return null;
            }

            string forwarded =
                context.Request.Headers[
                    "X-Forwarded-For"
                ].ToString();

            if (!string.IsNullOrWhiteSpace(
                    forwarded))
            {
                return forwarded
                    .Split(',')[0]
                    .Trim();
            }

            return context.Connection
                .RemoteIpAddress?
                .ToString();
        }
    }

    public bool ShouldAudit
    {
        get
        {
            HttpContext? context =
                Context;

            if (
                context is null ||
                context.User.Identity?
                    .IsAuthenticated != true ||
                UserId == Guid.Empty ||
                !HumanRoles.Contains(Role)
            ) {
                return false;
            }

            if (!MutationMethods.Contains(
                    context.Request.Method))
            {
                return false;
            }

            string path =
                context.Request.Path.Value
                ?? string.Empty;

            return path.StartsWith(
                "/api/admin/",
                StringComparison.OrdinalIgnoreCase);
        }
    }
}