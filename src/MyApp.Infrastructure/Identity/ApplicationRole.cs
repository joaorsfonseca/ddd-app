using Microsoft.AspNetCore.Identity;

namespace MyApp.Infrastructure.Identity;

public class ApplicationRole : IdentityRole<int>
{
    public virtual ICollection<ApplicationUserRole> Users { get; } = new List<ApplicationUserRole>();
}