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

        if (!RequiresPermissionCheck(path))
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

        if (dynamicRole?.PermissionIds == null || !dynamicRole.PermissionIds.Any())
        {
            context.Response.Redirect("/Account/AccessDenied");
            return;
        }

        var permissionIds = dynamicRole.PermissionIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var permissions = await dbContext.Permissions
            .Find(permission => permissionIds.Contains(permission.Id))
            .ToListAsync();

        var normalizedPath = NormalizePath(path);
        var hasAccess = permissions.Any(permission =>
            PermissionMatches(permission, normalizedPath, method));

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

        if (lowerPath.StartsWith("/account")
            || lowerPath.StartsWith("/home")
            || lowerPath == "/favicon.ico")
        {
            return true;
        }

        if (lowerPath.StartsWith("/css")
            || lowerPath.StartsWith("/js")
            || lowerPath.StartsWith("/lib")
            || lowerPath.StartsWith("/images")
            || lowerPath.StartsWith("/img")
            || lowerPath.StartsWith("/assets")
            || lowerPath.StartsWith("/fonts")
            || lowerPath.StartsWith("/admin-assets"))
        {
            return true;
        }

        return false;
    }

    private static bool RequiresPermissionCheck(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return false;
        }

        string[] protectedPrefixes =
        {
            "booking",
            "dashboard",
            "admin",
            "buses",
            "busclasses",
            "branches",
            "busoperators",
            "dynamicroles",
            "permissions"
        };

        return protectedPrefixes.Any(prefix =>
            normalizedPath == prefix || normalizedPath.StartsWith(prefix + "/", StringComparison.Ordinal));
    }

    private static bool PermissionMatches(Permission permission, string requestPath, string requestMethod)
    {
        if (!MethodMatches(permission.Method, requestMethod))
        {
            return false;
        }

        return PathMatches(permission.Link, permission.Name, requestPath, requestMethod);
    }

    private static bool PathMatches(string? permissionLink, string? permissionName, string requestPath, string requestMethod)
    {
        var link = NormalizePath(permissionLink);
        if (string.IsNullOrWhiteSpace(link))
        {
            return false;
        }

        if (link == requestPath)
        {
            return true;
        }

        if (IsViewPermission(permissionName) && requestMethod == "GET")
        {
            if (requestPath == link)
            {
                return true;
            }

            if (requestPath.StartsWith(link + "/", StringComparison.Ordinal))
            {
                return true;
            }

            var linkController = link.Split('/')[0];
            if (requestPath == linkController || requestPath.StartsWith(linkController + "/", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsViewPermission(string? permissionName)
    {
        return !string.IsNullOrWhiteSpace(permissionName)
               && permissionName.StartsWith("View.", StringComparison.OrdinalIgnoreCase);
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
}
