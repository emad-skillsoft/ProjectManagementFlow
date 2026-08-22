using Microsoft.EntityFrameworkCore;
using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrgMember> OrgMembers => Set<OrgMember>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectUpdate> ProjectUpdates => Set<ProjectUpdate>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<ProjectTask> Tasks => Set<ProjectTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Email).HasMaxLength(256);
            entity.Property(u => u.FullName).HasMaxLength(200);
            entity.Property(u => u.PasswordHash).HasMaxLength(512);
            entity.Property(u => u.AvatarUrl).HasMaxLength(512);
            entity.Property(u => u.SecurityStamp).HasMaxLength(64).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.Property(r => r.Name).HasMaxLength(128).IsRequired();
            entity.Property(r => r.Description).HasMaxLength(512);
            entity.Property(r => r.NameEn).HasMaxLength(128);
            entity.Property(r => r.DescriptionEn).HasMaxLength(512);
            entity.HasIndex(r => r.Name).IsUnique();
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.Property(p => p.Name).HasMaxLength(128).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(512);
            entity.HasIndex(p => p.Name).IsUnique();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(ur => new { ur.UserId, ur.RoleId });

            entity.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });

            entity.HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.Property(o => o.Name).HasMaxLength(200).IsRequired();
            entity.Property(o => o.Description).HasMaxLength(1000);

            entity.Property(o => o.Path).HasMaxLength(Organization.PathLength).IsRequired();

           
            entity.HasIndex(o => o.Path);
            entity.HasIndex(o => new { o.RootId, o.Depth });
            entity.HasIndex(o => o.ParentId);

            entity.HasOne(o => o.Parent)
                .WithMany(o => o.Children)
                .HasForeignKey(o => o.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Organization_Depth",
                    $"[Depth] BETWEEN 0 AND {Organization.MaxDepth}");

                t.HasCheckConstraint("CK_Organization_Root",
                    "([ParentId] IS NULL AND [RootId] = [Id] AND [Depth] = 0) OR ([ParentId] IS NOT NULL AND [Depth] > 0)");
            });
        });

        modelBuilder.Entity<OrgMember>(entity =>
        {
            entity.Property(m => m.Role).HasMaxLength(32).IsRequired();
            entity.Property(m => m.Status).HasMaxLength(32).IsRequired();

            entity.HasIndex(m => new { m.OrganizationId, m.UserId }).IsUnique();
            entity.HasIndex(m => new { m.UserId, m.Status });

            entity.HasOne(m => m.Organization)
                .WithMany(o => o.Members)
                .HasForeignKey(m => m.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_OrgMember_Status", "[Status] IN ('pending', 'active')");
                t.HasCheckConstraint("CK_OrgMember_Role", "[Role] IN ('owner', 'admin', 'member')");
            });
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.Property(p => p.Code).HasMaxLength(64);
            entity.HasIndex(p => p.Code).IsUnique();
        });

        modelBuilder.Entity<ProjectTask>(entity =>
        {
            entity.Property(t => t.EstimateHours).HasPrecision(9, 2);
            entity.Property(t => t.Position).HasPrecision(18, 6);

            entity.HasOne(t => t.ParentTask)
                .WithMany(t => t.Subtasks)
                .HasForeignKey(t => t.ParentTaskId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
