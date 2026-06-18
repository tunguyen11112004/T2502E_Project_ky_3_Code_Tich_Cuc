using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using Bus_ticket.Data;
using Bus_ticket.Models;

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
        string currentLink = context.Request.Path.Value.TrimStart('/');
        string currentMethod = context.Request.Method.ToUpper();

        if (currentLink.StartsWith("api/auth"))
        {
            await _next(context);
            return;
        }

        var user = context.User;
        if (user == null || !user.Identity.IsAuthenticated)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var roleId = user.Claims.FirstOrDefault(c => c.Type == "RoleId")?.Value;
        if (string.IsNullOrEmpty(roleId))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var role = await dbContext.DynamicRoles.Find(r => r.Id == roleId).FirstOrDefaultAsync();
        if (role == null || role.PermissionIds == null || !role.PermissionIds.Any())
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var hasAccess = await dbContext.Permissions.Find(p =>
            role.PermissionIds.Contains(p.Id) &&
            p.Link.ToLower() == currentLink.ToLower() &&
            p.Method.ToUpper() == currentMethod
        ).AnyAsync();

        if (!hasAccess)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await _next(context);
    }
}