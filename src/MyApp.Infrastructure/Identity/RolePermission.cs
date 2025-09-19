namespace MyApp.Infrastructure.Identity;

public class RolePermission
{
    public int RoleId { get; set; }
    public ApplicationRole Role { get; set; } = default!; public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = default!;
}