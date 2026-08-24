using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Localization;
using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.Services;
using ProjectManagmentFlow.Services.Activity;
using ProjectManagmentFlow.Services.Organizations;

namespace ProjectManagmentFlow.Services.Projects;

/// <summary>
/// أوامر المشاريع. الحرّاس تُطلق استثناءً برسالةٍ مترجَمة قبل لمس قاعدة البيانات،
/// وكلّ أمرٍ ناجح يضبط UpdatedAt.
/// </summary>
public class ProjectCommandService : IProjectCommandService
{
    private readonly AppDbContext _context;
    private readonly IStringLocalizer<Messages> _text;
    private readonly IActivityService _activity;
    private readonly IProjectQueryService _queries;
    private readonly IOrganizationQueryService _organizations;

    public ProjectCommandService(
        AppDbContext context,
        IStringLocalizer<Messages> text,
        IActivityService activity,
        IProjectQueryService queries,
        IOrganizationQueryService organizations)
    {
        _context = context;
        _text = text;
        _activity = activity;
        _queries = queries;
        _organizations = organizations;
    }

    public async Task ValidateDraftAsync(
        Guid organizationId,
        string name,
        string? description,
        string status,
        string priority,
        DateOnly? startDate,
        DateOnly? dueDate,
        Guid? ownerId,
        CancellationToken cancellationToken = default)
    {
        var organization = await LiveOrganization(organizationId, cancellationToken)
            ?? throw new DomainException(_text["Org_NotFound"]);

        RequireName(name);
        RequireDescription(description);
        RequireValidDates(startDate, dueDate);

        if (!ProjectStatus.IsKnown(status))
        {
            throw new DomainException(_text["Project_UnknownStatus"], nameof(Project.Status));
        }

        if (!ProjectPriority.IsKnown(priority))
        {
            throw new DomainException(_text["Project_UnknownPriority"], nameof(Project.Priority));
        }

        if (ownerId is null)
        {
            throw new DomainException(_text["ProjectCreate_OwnerRequired"], "OwnerId");
        }

        var candidates = await _queries.GetCandidatesAsync(organization.RootId, cancellationToken);
        if (candidates.All(candidate => candidate.Id != ownerId.Value))
        {
            throw new DomainException(_text["Project_OwnerNotMember"], "OwnerId");
        }
    }

    public async Task<Project> CreateAsync(
        Guid organizationId,
        string name,
        string? description,
        string priority,
        DateOnly? startDate,
        DateOnly? dueDate,
        Guid? ownerId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var organization = await LiveOrganization(organizationId, cancellationToken)
            ?? throw new DomainException(_text["Org_NotFound"]);

        var trimmedName = RequireName(name);
        RequireValidDates(startDate, dueDate);

        if (!ProjectPriority.IsKnown(priority))
        {
            throw new DomainException(_text["Project_UnknownPriority"], nameof(Project.Priority));
        }

        if (ownerId is not null)
        {
            await RequireActiveOwnerAsync(ownerId.Value, cancellationToken);
        }

        var project = new Project
        {
            OrganizationId = organization.Id,
            Name = trimmedName,
            Description = RequireDescription(description),
            Status = ProjectStatus.Planning,
            Priority = priority,
            StartDate = startDate,
            DueDate = dueDate,
            OwnerId = ownerId,
            CreatedById = actorId
        };

        _context.Projects.Add(project);
        _activity.Add(
            organization.Id,
            project.Id,
            actorId,
            ActivityEntities.Project,
            project.Id,
            ActivityActions.Created,
            new { project.Name, project.Status });

        // الرمز فهرسٌ فريد والقراءة‑ثمّ‑الكتابة تتصادم بين مديرَين في اللحظة نفسها؛
        // التفريد على قاعدة البيانات هو الحكم، والمحاولات الثلاث تلتقط ما سبقه.
        var year = DisplayTime.RiyadhNow().Year;
        var prefix = $"{year}-";

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var codes = await _context.Projects.AsNoTracking()
                .Where(p => p.Code.StartsWith(prefix))
                .Select(p => p.Code)
                .ToListAsync(cancellationToken);
            var sequence = codes
                .Select(code => int.TryParse(code[prefix.Length..], out var parsed) ? parsed : 0)
                .DefaultIfEmpty()
                .Max();

            project.Code = Compose(year, sequence + 1);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return project;
            }
            catch (DbUpdateException ex) when (SqlErrors.IsUniqueViolation(ex))
            {
                // أعِد التهيئة قبل المحاولة: الرفض لا يزيل الصنف Added لكنه يترك القيم القديمة.
                _context.Entry(project).State = EntityState.Added;
            }
        }

        throw new DomainException(_text["Project_CodeCollision"]);
    }

    public async Task<ProjectProvisionResult> CreateWithTeamAsync(
        Guid organizationId,
        string name,
        string? description,
        string status,
        string priority,
        DateOnly? startDate,
        DateOnly? dueDate,
        Guid ownerId,
        string teamName,
        IReadOnlyCollection<ProjectTeamMember> members,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (!ProjectStatus.IsKnown(status))
        {
            throw new DomainException(_text["Project_UnknownStatus"], nameof(Project.Status));
        }

        var organization = await LiveOrganization(organizationId, cancellationToken)
            ?? throw new DomainException(_text["Org_NotFound"]);

        // الصلاحية العامّة للإنشاء لا توسّع النطاق التنظيمي: الهدف يجب أن يساوي
        // إحدى عضويّات المنشئ الفعّالة أو يقع تحتها.
        var actorScopePaths = await _organizations.GetScopePathsByUserAsync(actorId, cancellationToken);
        if (!OrgScope.Contains(actorScopePaths, organization.Path))
        {
            throw new DomainException(_text["Project_CreateOutsideScope"]);
        }

        var candidateIds = (await _queries.GetCandidatesAsync(organization.RootId, cancellationToken))
            .Select(candidate => candidate.Id)
            .ToHashSet();

        if (!candidateIds.Contains(ownerId))
        {
            throw new DomainException(_text["Project_OwnerNotMember"], "OwnerId");
        }

        var assignments = members
            .GroupBy(member => member.UserId)
            .ToDictionary(group => group.Key, group => group.Last().Role);
        assignments[ownerId] = TeamMemberRoles.Leader;

        if (assignments.Keys.Any(userId => !candidateIds.Contains(userId)))
        {
            throw new DomainException(_text["Project_TeamMemberNotFound"], "SelectedMemberIds");
        }

        if (assignments.Values.Any(role => !TeamMemberRoles.IsKnown(role)))
        {
            throw new DomainException(_text["Project_UnknownTeamRole"], "MemberRoles");
        }

        // الفريق يملك قائداً واحداً (المالك) ونائباً واحداً كحد أقصى.
        foreach (var userId in assignments.Keys.Where(userId => userId != ownerId).ToList())
        {
            if (assignments[userId] == TeamMemberRoles.Leader)
            {
                assignments[userId] = TeamMemberRoles.Member;
            }
        }

        var deputyIds = assignments
            .Where(assignment => assignment.Value == TeamMemberRoles.Deputy)
            .Select(assignment => assignment.Key)
            .ToList();
        foreach (var duplicateDeputyId in deputyIds.Skip(1))
        {
            assignments[duplicateDeputyId] = TeamMemberRoles.Member;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var project = await CreateAsync(
            organizationId,
            name,
            description,
            priority,
            startDate,
            dueDate,
            ownerId,
            actorId,
            cancellationToken);

        project.Status = status;

        if (status != ProjectStatus.Planning)
        {
            _activity.Add(
                organization.Id,
                project.Id,
                actorId,
                ActivityEntities.Project,
                project.Id,
                ActivityActions.StatusChanged,
                new { project.Name, Value = status });
        }

        var now = DateTime.UtcNow;
        var team = Team.ForProject(project, teamName, actorId);
        team.Members = assignments.Select(assignment => new TeamMember
        {
            UserId = assignment.Key,
            Role = assignment.Value,
            AddedById = actorId,
            JoinedAt = now
        }).ToList();

        _context.Teams.Add(team);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ProjectProvisionResult(project, team, assignments.Count);
    }

    public async Task UpdateAsync(
        Guid projectId,
        string name,
        string? description,
        DateOnly? startDate,
        DateOnly? dueDate,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var project = await LiveProject(projectId, cancellationToken)
            ?? throw new DomainException(_text["Project_NotFound"]);

        var normalizedName = RequireName(name);
        var normalizedDescription = Blank(description);
        RequireValidDates(startDate, dueDate);
        project.Name = normalizedName;
        project.Description = normalizedDescription;
        project.StartDate = startDate;
        project.DueDate = dueDate;
        project.UpdatedAt = DateTime.UtcNow;

        _activity.Add(
            project.OrganizationId,
            project.Id,
            actorId,
            ActivityEntities.Project,
            project.Id,
            ActivityActions.Updated,
            new { project.Name, project.Description });

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetStatusAsync(
        Guid projectId,
        string status,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (!ProjectStatus.IsKnown(status))
        {
            throw new DomainException(_text["Project_UnknownStatus"], nameof(Project.Status));
        }

        var project = await LiveProject(projectId, cancellationToken)
            ?? throw new DomainException(_text["Project_NotFound"]);

        project.Status = status;
        project.UpdatedAt = DateTime.UtcNow;

        _activity.Add(
            project.OrganizationId,
            project.Id,
            actorId,
            ActivityEntities.Project,
            project.Id,
            ActivityActions.StatusChanged,
            new { project.Name, Value = status });

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetOwnerAsync(
        Guid projectId,
        Guid ownerId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var project = await LiveProject(projectId, cancellationToken)
            ?? throw new DomainException(_text["Project_NotFound"]);

        var ownerName = await RequireActiveOwnerAsync(ownerId, cancellationToken);

        project.OwnerId = ownerId;
        project.UpdatedAt = DateTime.UtcNow;

        _activity.Add(
            project.OrganizationId,
            project.Id,
            actorId,
            ActivityEntities.Project,
            project.Id,
            ActivityActions.Assigned,
            new { Name = project.Name, Value = ownerName });

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveAsync(
        Guid projectId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var project = await LiveProject(projectId, cancellationToken)
            ?? throw new DomainException(_text["Project_NotFound"]);

        if (project.ArchivedAt is null)
        {
            project.ArchivedAt = DateTime.UtcNow;
            project.UpdatedAt = project.ArchivedAt;
            _activity.Add(
                project.OrganizationId,
                project.Id,
                actorId,
                ActivityEntities.Project,
                project.Id,
                ActivityActions.Archived,
                new { project.Name });
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RestoreAsync(
        Guid projectId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var project = await LiveProject(projectId, cancellationToken)
            ?? throw new DomainException(_text["Project_NotFound"]);

        if (project.ArchivedAt is null)
        {
            throw new DomainException(_text["Project_NotArchived"]);
        }

        project.ArchivedAt = null;
        project.UpdatedAt = DateTime.UtcNow;

        _activity.Add(
            project.OrganizationId,
            project.Id,
            actorId,
            ActivityEntities.Project,
            project.Id,
            ActivityActions.Restored,
            new { project.Name });

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        Guid projectId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var project = await LiveProject(projectId, cancellationToken);
        if (project is null) return false;

        project.DeletedAt = DateTime.UtcNow;
        project.UpdatedAt = project.DeletedAt;

        _activity.Add(
            project.OrganizationId,
            project.Id,
            actorId,
            ActivityEntities.Project,
            project.Id,
            ActivityActions.Removed,
            new { project.Name });

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task MoveToOrgAsync(
        Guid projectId,
        Guid newOrganizationId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var project = await LiveProject(projectId, cancellationToken)
            ?? throw new DomainException(_text["Project_NotFound"]);

        var organization = await LiveOrganization(newOrganizationId, cancellationToken)
            ?? throw new DomainException(_text["Org_NotFound"]);

        project.OrganizationId = organization.Id;
        project.UpdatedAt = DateTime.UtcNow;

        _activity.Add(
            project.OrganizationId,
            project.Id,
            actorId,
            ActivityEntities.Project,
            project.Id,
            ActivityActions.Updated,
            new { Name = project.Name, Value = organization.Name });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string Compose(int year, int sequence) => $"{year}-{sequence:D3}";

    private Task<Project?> LiveProject(Guid id, CancellationToken cancellationToken) =>
        _context.Projects.FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, cancellationToken);

    private Task<Models.Organization?> LiveOrganization(Guid id, CancellationToken cancellationToken) =>
        _context.Organizations.FirstOrDefaultAsync(o => o.Id == id && o.DeletedAt == null, cancellationToken);

    private async Task<string> RequireActiveOwnerAsync(
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var ownerName = await _context.Users.AsNoTracking()
            .Where(u => u.Id == ownerId && u.IsActive)
            .Select(u => u.FullName ?? u.Email ?? "—")
            .FirstOrDefaultAsync(cancellationToken);

        if (ownerName is null)
        {
            throw new DomainException(_text["Project_OwnerNotFound"], "OwnerId");
        }

        return ownerName;
    }

    private void RequireValidDates(DateOnly? startDate, DateOnly? dueDate)
    {
        if (startDate is not null && dueDate is not null && dueDate < startDate)
        {
            throw new DomainException(_text["Project_DueBeforeStart"], nameof(Project.DueDate));
        }
    }

    /// <summary>العمود nvarchar(2000)؛ بلا هذا الحارس يخرج خطأ SQL خام لا رسالة.</summary>
    private string? RequireDescription(string? description)
    {
        if (description?.Length > 2000)
        {
            throw new DomainException(_text["Project_DescriptionTooLong"], nameof(Project.Description));
        }

        return Blank(description);
    }

    private string RequireName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            throw new DomainException(_text["Project_NameRequired"], nameof(Project.Name));
        }

        if (trimmed.Length > 200)
        {
            throw new DomainException(_text["Project_NameTooLong"], nameof(Project.Name));
        }

        return trimmed;
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
