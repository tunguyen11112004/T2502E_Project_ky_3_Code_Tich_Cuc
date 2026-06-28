using Bus_ticket.Data;
using Bus_ticket.Models;
using MongoDB.Driver;
using System.Security.Claims;

namespace Bus_ticket.Services;

public class SidebarPermissionService
{
    private readonly ApplicationDbContext _context;

    public SidebarPermissionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<string>> GetCurrentUserPermissionNamesAsync(ClaimsPrincipal user)
    {
        if (user.IsInRole("Admin"))
        {
            return new List<string> { "*" };
        }

        if (!user.IsInRole("Employee"))
        {
            return new List<string>();
        }

        var roleId = user.FindFirst("RoleId")?.Value;

        if (string.IsNullOrWhiteSpace(roleId))
        {
            return new List<string>();
        }

        var dynamicRole = await _context.DynamicRoles
            .Find(role => role.Id == roleId)
            .FirstOrDefaultAsync();

        if (dynamicRole == null || dynamicRole.PermissionIds == null || !dynamicRole.PermissionIds.Any())
        {
            return new List<string>();
        }

        var permissionIds = dynamicRole.PermissionIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();

        if (!permissionIds.Any())
        {
            return new List<string>();
        }

        var permissionFilter = Builders<Permission>.Filter.In(
            permission => permission.Id,
            permissionIds
        );

        var permissions = await _context.Permissions
            .Find(permissionFilter)
            .ToListAsync();

        var permissionKeys = new List<string>();

        foreach (var permission in permissions)
        {
            if (!string.IsNullOrWhiteSpace(permission.Name))
            {
                permissionKeys.Add(permission.Name);
            }

            if (!string.IsNullOrWhiteSpace(permission.Link))
            {
                permissionKeys.Add(permission.Link);
            }

            if (!string.IsNullOrWhiteSpace(permission.Method))
            {
                permissionKeys.Add(permission.Method);
            }
        }

        return permissionKeys
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool HasPermission(List<string> permissionNames, params string[] keywords)
    {
        if (permissionNames.Contains("*"))
        {
            return true;
        }

        return permissionNames.Any(permissionName =>
            keywords.Any(keyword =>
                permissionName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            )
        );
    }
}

