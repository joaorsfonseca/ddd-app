using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyApp.Infrastructure.Identity;
using MyApp.Infrastructure.Persistence;

namespace MyApp.Infrastructure.Auth;

public sealed class PermissionAuthorizationHandler(UserManager<ApplicationUser> userManager, AppDbContext db)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        if (context.User.Claims.Any(c => c.Type == "permission" && string.Equals(c.Value, requirement.Permission, StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
            return;
        }
        var userIdStr = userManager.GetUserId(context.User);
        if (!int.TryParse(userIdStr, out var userId)) return;
        var has = await db.UserPermissions.AnyAsync(up => up.UserId == userId && up.Permission.Name == requirement.Permission);
        if (has) { context.Succeed(requirement); return; }
        var roles = await (from ur in db.UserRoles where ur.UserId==userId
                           join rp in db.RolePermissions on ur.RoleId equals rp.RoleId
                           join p in db.Permissions on rp.PermissionId equals p.Id
                           where p.Name == requirement.Permission select p.Id).AnyAsync();
        if (roles) context.Succeed(requirement);
    }
}
