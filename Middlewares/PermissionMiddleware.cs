using Bus_ticket.Data;
using Bus_ticket.Models;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using System.Security.Claims;

namespace Bus_ticket.Middlewares;

public class PermissionMiddleware
{
    private readonly RequestDelegate _next;

    public PermissionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method.ToUpperInvariant();

        if (ShouldSkip(path))
        {
            await _next(context);
            return;
        }

        var requirement = GetPermissionRequirement(path, method);

        if (requirement == null)
        {
            await _next(context);
            return;
        }

        var user = context.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        if (user.IsInRole("Admin"))
        {
            await _next(context);
            return;
        }

        if (!user.IsInRole("Employee"))
        {
            context.Response.Redirect("/Account/AccessDenied");
            return;
        }

        var roleId = user.FindFirst("RoleId")?.Value;

        if (string.IsNullOrWhiteSpace(roleId))
        {
            context.Response.Redirect("/Account/AccessDenied");
            return;
        }

        var dynamicRole = await dbContext.DynamicRoles
            .Find(role => role.Id == roleId)
            .FirstOrDefaultAsync();

        if (dynamicRole == null || dynamicRole.PermissionIds == null || !dynamicRole.PermissionIds.Any())
        {
            context.Response.Redirect("/Account/AccessDenied");
            return;
        }

        var permissionIds = dynamicRole.PermissionIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();

        if (!permissionIds.Any())
        {
            context.Response.Redirect("/Account/AccessDenied");
            return;
        }

        var permissions = await dbContext.Permissions
            .Find(permission => permissionIds.Contains(permission.Id))
            .ToListAsync();

        var hasAccess = permissions.Any(permission =>
            PermissionMatches(permission, requirement.Value, method)
        );

        if (!hasAccess)
        {
            context.Response.Redirect("/Account/AccessDenied");
            return;
        }

        await _next(context);
    }

    private static bool ShouldSkip(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        var lowerPath = path.ToLowerInvariant();

        if (lowerPath == "/")
        {
            return true;
        }

        if (lowerPath.StartsWith("/account"))
        {
            return true;
        }

        if (lowerPath.StartsWith("/home"))
        {
            return true;
        }

        if (lowerPath.StartsWith("/css")
            || lowerPath.StartsWith("/js")
            || lowerPath.StartsWith("/lib")
            || lowerPath.StartsWith("/images")
            || lowerPath.StartsWith("/img")
            || lowerPath.StartsWith("/assets")
            || lowerPath.StartsWith("/fonts"))
        {
            return true;
        }

        if (lowerPath == "/favicon.ico")
        {
            return true;
        }

        return false;
    }

    private static PermissionRequirement? GetPermissionRequirement(string path, string method)
    {
        var currentPath = NormalizePath(path);

        if (currentPath == "booking" || currentPath.StartsWith("booking/"))
        {
            return new PermissionRequirement(
                ModuleKeywords: new[] { "booking", "book", "ticket" },
                ActionKeywords: GetActionKeywords(currentPath, method)
            );
        }

        if (currentPath == "employee" || currentPath.StartsWith("employee/"))
        {
            return new PermissionRequirement(
                ModuleKeywords: new[] { "employee", "counter", "booking", "book", "ticket", "trip", "route", "price", "fare", "policy" },
                ActionKeywords: GetActionKeywords(currentPath, method)
            );
        }

        if (currentPath == "admin/users" || currentPath.StartsWith("admin/users/"))
        {
            return new PermissionRequirement(
                ModuleKeywords: new[] { "user", "users", "employee", "employees" },
                ActionKeywords: GetActionKeywords(currentPath, method)
            );
        }
        if (currentPath == "branches" || currentPath.StartsWith("branches/"))
        {
            return new PermissionRequirement(
                ModuleKeywords: new[] { "branch", "branches" },
                ActionKeywords: GetActionKeywords(currentPath, method)
            );
        }

        if (currentPath == "busclasses" || currentPath.StartsWith("busclasses/"))
        {
            return new PermissionRequirement(
                ModuleKeywords: new[] { "busclass", "busclasses", "bus class", "bus classes", "seat", "layout" },
                ActionKeywords: GetActionKeywords(currentPath, method)
            );
        }

        if (currentPath == "dynamicroles" || currentPath.StartsWith("dynamicroles/"))
        {
            return new PermissionRequirement(
                ModuleKeywords: new[] { "dynamicrole", "dynamicroles", "role", "roles" },
                ActionKeywords: GetActionKeywords(currentPath, method)
            );
        }

        if (currentPath == "permissions" || currentPath.StartsWith("permissions/"))
        {
            return new PermissionRequirement(
                ModuleKeywords: new[] { "permission", "permissions" },
                ActionKeywords: GetActionKeywords(currentPath, method)
            );
        }

        if (currentPath == "admin/buses" || currentPath.StartsWith("admin/buses/"))
        {
            return new PermissionRequirement(
                ModuleKeywords: new[] { "bus", "buses" },
                ActionKeywords: GetActionKeywords(currentPath, method)
            );
        }

        if (currentPath == "admin/routes" || currentPath.StartsWith("admin/routes/"))
        {
            return new PermissionRequirement(
                ModuleKeywords: new[] { "route", "routes", "busroute" },
                ActionKeywords: GetActionKeywords(currentPath, method)
            );
        }

        if (currentPath == "admin/prices"
            || currentPath.StartsWith("admin/prices/")
            || currentPath == "admin/priceconfig"
            || currentPath.StartsWith("admin/priceconfig/"))
        {
            return new PermissionRequirement(
                ModuleKeywords: new[] { "price", "prices", "pricelist", "fare" },
                ActionKeywords: GetActionKeywords(currentPath, method)
            );
        }

        return null;
    }

    private static string[] GetActionKeywords(string currentPath, string method)
    {
        if (currentPath.Contains("delete", StringComparison.OrdinalIgnoreCase)
            || currentPath.Contains("deactivate", StringComparison.OrdinalIgnoreCase)
            || currentPath.Contains("remove", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "delete", "remove", "deactivate" };
        }

        if (currentPath.Contains("cancel", StringComparison.OrdinalIgnoreCase)
            || currentPath.Contains("refund", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "cancel", "refund", "delete", "update" };
        }

        if (currentPath.Contains("changerole", StringComparison.OrdinalIgnoreCase)
            || currentPath.Contains("edit", StringComparison.OrdinalIgnoreCase)
            || currentPath.Contains("update", StringComparison.OrdinalIgnoreCase)
            || currentPath.Contains("matrix", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "update", "edit", "change", "modify" };
        }

        if (currentPath.Contains("create", StringComparison.OrdinalIgnoreCase)
            || currentPath.Contains("add", StringComparison.OrdinalIgnoreCase)
            || currentPath.Contains("new", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "create", "add", "new" };
        }

        if (currentPath.Contains("search", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "view", "read", "list", "search" };
        }

        if (method == "GET")
        {
            return new[] { "view", "read", "list", "index", "search" };
        }

        if (method == "POST")
        {
            return new[] { "create", "update", "delete", "add", "edit", "change", "cancel", "refund", "book" };
        }

        return new[] { "view", "read", "list" };
    }

    private static bool PermissionMatches(Permission permission, PermissionRequirement requirement, string currentMethod)
    {
        var permissionText = BuildPermissionText(permission);

        var moduleMatches = requirement.ModuleKeywords.Any(keyword =>
            ContainsKeyword(permissionText, keyword)
        );

        if (!moduleMatches)
        {
            return false;
        }

        var actionMatches = requirement.ActionKeywords.Any(keyword =>
            ContainsKeyword(permissionText, keyword)
        );

        var methodMatches = MethodMatches(permission.Method, currentMethod);

        return actionMatches || methodMatches;
    }

    private static string BuildPermissionText(Permission permission)
    {
        return $"{permission.Name} {permission.Link} {permission.Method}"
            .Trim()
            .ToLowerInvariant();
    }

    private static bool ContainsKeyword(string source, string keyword)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        var normalizedSource = NormalizeForKeyword(source);
        var normalizedKeyword = NormalizeForKeyword(keyword);

        if (normalizedSource.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalizedKeyword.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            var singular = normalizedKeyword[..^1];

            if (normalizedSource.Contains(singular, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        else
        {
            var plural = normalizedKeyword + "s";

            if (normalizedSource.Contains(plural, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeForKeyword(string value)
    {
        return value
            .Trim()
            .Trim('/')
            .Replace("/", "")
            .Replace("\\", "")
            .Replace(".", "")
            .Replace("-", "")
            .Replace("_", "")
            .Replace(" ", "")
            .ToLowerInvariant();
    }

    private static string NormalizePath(string? path)
    {
        return (path ?? string.Empty)
            .Trim()
            .Trim('/')
            .ToLowerInvariant();
    }

    private static bool MethodMatches(string? permissionMethod, string currentMethod)
    {
        if (string.IsNullOrWhiteSpace(permissionMethod))
        {
            return false;
        }

        var method = permissionMethod.Trim().ToUpperInvariant();

        return method == "ALL" || method == currentMethod;
    }

    private readonly record struct PermissionRequirement(
        string[] ModuleKeywords,
        string[] ActionKeywords
    );
}

