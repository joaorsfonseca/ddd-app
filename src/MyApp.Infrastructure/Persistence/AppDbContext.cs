using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Identity;

namespace MyApp.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options): base(options){}
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Product>(cfg=>{
            cfg.HasKey(p=>p.Id);
            cfg.Property(p=>p.Name).HasMaxLength(200).IsRequired();
            cfg.Property(p=>p.Price).HasColumnType("decimal(18,2)");
            cfg.HasIndex(p=>p.Name).IsUnique();
        });
        b.Entity<Permission>(cfg=>{
            cfg.HasKey(p=>p.Id);
            cfg.Property(p=>p.Name).HasMaxLength(200).IsRequired();
            cfg.HasIndex(p=>p.Name).IsUnique();
        });
        b.Entity<RolePermission>(cfg=>{
            cfg.HasKey(x=> new {x.RoleId, x.PermissionId});
            cfg.HasOne(x=>x.Role).WithMany().HasForeignKey(x=>x.RoleId);
            cfg.HasOne(x=>x.Permission).WithMany(x=>x.RolePermissions).HasForeignKey(x=>x.PermissionId);
        });
        b.Entity<UserPermission>(cfg=>{
            cfg.HasKey(x=> new {x.UserId, x.PermissionId});
            cfg.HasOne(x=>x.User).WithMany().HasForeignKey(x=>x.UserId);
            cfg.HasOne(x=>x.Permission).WithMany(x=>x.UserPermissions).HasForeignKey(x=>x.PermissionId);
        });
    }
}
