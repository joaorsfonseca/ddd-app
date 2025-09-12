using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MyApp.Infrastructure.Identity;
using MyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MyApp.Host.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IConfiguration cfg, AppDbContext db) : ControllerBase
{
    public record LoginRequest(string UserName, string Password);

    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<IActionResult> Token([FromBody] LoginRequest request)
    {
        var user = await userManager.FindByNameAsync(request.UserName);
        if (user == null) return Unauthorized();
        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded) return Unauthorized();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty)
        };
        var roles = await userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var perms = await (from up in db.UserPermissions where up.UserId == user.Id select up.Permission.Name).ToListAsync();
        var rolePerms = await (from ur in db.UserRoles where ur.UserId == user.Id
                               join rp in db.RolePermissions on ur.RoleId equals rp.RoleId
                               join p in db.Permissions on rp.PermissionId equals p.Id
                               select p.Name).ToListAsync();
        foreach (var p in perms.Concat(rolePerms).Distinct(StringComparer.OrdinalIgnoreCase))
            claims.Add(new Claim("permission", p));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: cfg["Jwt:Issuer"],
            audience: cfg["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);
        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return Ok(new { access_token = jwt });
    }
}
