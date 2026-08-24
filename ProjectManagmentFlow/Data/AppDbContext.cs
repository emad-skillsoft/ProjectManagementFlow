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
    public DbSet<ActivityLog> ActivityLog => Set<ActivityLog>();

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
            entity.Property(p => p.Code).HasMaxLength(64).IsRequired();
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(2000);
            entity.Property(p => p.Status).HasMaxLength(32).IsRequired();
            entity.Property(p => p.Priority).HasMaxLength(32).IsRequired();

            entity.HasIndex(p => p.Code).IsUnique();
            entity.HasIndex(p => new { p.OrganizationId, p.Status });

            entity.HasOne(p => p.Organization)
                .WithMany(o => o.Projects)
                .HasForeignKey(p => p.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Project_Status",   "[Status] IN ('planning', 'active', 'on_hold', 'done')");
                t.HasCheckConstraint("CK_Project_Priority", "[Priority] IN ('low', 'normal', 'high', 'urgent')");
                t.HasCheckConstraint("CK_Project_Dates",
                    "[StartDate] IS NULL OR [DueDate] IS NULL OR [DueDate] >= [StartDate]");
            });
        });

        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.Property(a => a.EntityType).HasMaxLength(64).IsRequired();
            entity.Property(a => a.Action).HasMaxLength(64).IsRequired();
            entity.Property(a => a.Payload).HasMaxLength(4000);

            entity.HasIndex(a => new { a.ProjectId, a.CreatedAt })
                .IsDescending(false, true);
            entity.HasIndex(a => new { a.OrganizationId, a.CreatedAt })
                .IsDescending(false, true);

            entity.HasOne(a => a.Project)
                .WithMany(p => p.Activities)
                .HasForeignKey(a => a.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.Property(t => t.Name).HasMaxLength(200).IsRequired();

            entity.HasIndex(t => t.ProjectId)
                .IsUnique()
                .HasFilter("[ProjectId] IS NOT NULL");
        });

        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.Property(m => m.Role).HasMaxLength(32).IsRequired();

            entity.HasIndex(m => new { m.TeamId, m.UserId }).IsUnique();
            entity.HasIndex(m => m.TeamId, "UX_TeamMember_Lead")
                .IsUnique()
                .HasFilter("[Role] = 'lead'");
            entity.HasIndex(m => m.TeamId, "UX_TeamMember_Deputy")
                .IsUnique()
                .HasFilter("[Role] = 'deputy'");

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_TeamMember_Role", "[Role] IN ('lead', 'deputy', 'member')"));
        });

        modelBuilder.Entity<ProjectTask>(entity =>
        {
            entity.Property(t => t.Code).HasMaxLength(16).IsRequired();
            entity.Property(t => t.Title).HasMaxLength(200).IsRequired();
            entity.Property(t => t.Description).HasMaxLength(4000);
            entity.Property(t => t.Status).HasMaxLength(32).IsRequired();
            entity.Property(t => t.Priority).HasMaxLength(32).IsRequired();
            entity.Property(t => t.EstimateHours).HasPrecision(9, 2);
            entity.Property(t => t.Position).HasPrecision(18, 6);

            entity.HasOne(t => t.ParentTask)
                .WithMany(t => t.Subtasks)
                .HasForeignKey(t => t.ParentTaskId)
                .OnDelete(DeleteBehavior.Restrict);

            // عدّ بطاقة المشروع (جذريّة مكتملة/كلّ الجذريّة) بلا مسحٍ كامل للجدول.
            entity.HasIndex(t => new { t.ProjectId, t.ParentTaskId, t.CompletedAt });
            entity.HasIndex(t => new { t.ProjectId, t.Status });
            entity.HasIndex(t => new { t.ProjectId, t.Code }).IsUnique();
            entity.HasIndex(t => new { t.ProjectId, t.Status, t.Position });
            entity.HasIndex(t => new { t.AssigneeId, t.Status });

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Task_Status",
                    "[Status] IN ('todo', 'in_progress', 'in_review', 'done', 'cancelled')");
                t.HasCheckConstraint("CK_Task_Priority",
                    "[Priority] IN ('low', 'normal', 'high', 'urgent')");
            });
        });
    }
}
