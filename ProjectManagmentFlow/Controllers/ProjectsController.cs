using System.Security.Claims;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.Authorization;
using ProjectManagmentFlow.Filters;
using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.Services.Activity;
using ProjectManagmentFlow.Services.Organizations;
using ProjectManagmentFlow.Services;
using ProjectManagmentFlow.Services.Permissions;
using ProjectManagmentFlow.Services.Projects;
using ProjectManagmentFlow.Services.Teams;
using ProjectManagmentFlow.Services.Users;
using ProjectManagmentFlow.ViewModels;

namespace ProjectManagmentFlow.Controllers;

[RequirePermission(PermissionNames.ProjectsView)]
public class ProjectsController : Controller
{
    private readonly IProjectQueryService _projects;
    private readonly IProjectCommandService _projectCommands;
    private readonly IOrganizationQueryService _organizations;
    private readonly ITeamQueryService _teams;
    private readonly ITeamCommandService _teamCommands;
    private readonly IActivityService _activity;
    private readonly IPermissionService _permissions;
    private readonly IStringLocalizer<Messages> _text;

    public ProjectsController(
        IProjectQueryService projects,
        IProjectCommandService projectCommands,
        IOrganizationQueryService organizations,
        ITeamQueryService teams,
        ITeamCommandService teamCommands,
        IActivityService activity,
        IPermissionService permissions,
        IStringLocalizer<Messages> text)
    {
        _projects = projects;
        _projectCommands = projectCommands;
        _organizations = organizations;
        _teams = teams;
        _teamCommands = teamCommands;
        _activity = activity;
        _permissions = permissions;
        _text = text;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        Guid? unit,
        bool sub = false,
        string? status = null,
        string? q = null,
        string tab = "active",
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Forbid();
        }

        var memberships = await _organizations.GetOrganizationsByUserAsync(userId, cancellationToken);
        unit ??= memberships.FirstOrDefault()?.Id;

        ViewData["Title"] = _text["Nav_Projects"].Value;

        if (unit is null)
        {
            SetBreadcrumb([]);
            return View(new ProjectsIndexViewModel
            {
                Unit = new Organization(),
                Ancestors = [],
                HasUnit = false,
                Scope = ProjectScope.Active,
                ScopeSwitch = BuildScopeSwitch(false)
            });
        }

        // معرّف الوحدة يأتي من العنوان؛ الصلاحية العامة لعرض المشاريع لا تكفي
        // كي يرى المستخدم وحدةً لا تقع تحت إحدى عضويّاته الفعّالة.
        // نطاقات المستخدم مشتقّةٌ من عضويّاته المجلوبة أعلاه: الشرط نفسه، فلا استعلام ثانٍ.
        // والأجداد تحمل مسار الوحدة، فلا استعلام ثالث لجلبه.
        var scopePaths = OrgScope.Outermost(memberships).Select(o => o.Path).ToList();
        var ancestors = await _organizations.GetAncestorsAsync(unit.Value, cancellationToken);
        var selectedUnit = ancestors.LastOrDefault(node => node.Id == unit.Value);

        if (selectedUnit is null || !OrgScope.Contains(scopePaths, selectedUnit.Path))
        {
            return Forbid();
        }

        var scope = string.Equals(tab, "archived", StringComparison.OrdinalIgnoreCase)
            ? ProjectScope.Archived
            : ProjectScope.Active;
        var selectedStatus = ProjectStatus.IsKnown(status) ? status : null;
        var search = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

        var all = await _projects.GetByOrgAsync(
            selectedUnit.Id, sub, scope, search, cancellationToken);

        // الأعداد تُحسب من القائمة الكاملة قبل التصفية كي لا تصير بقيّة الأزرار أصفاراً.
        var counts = all
            .GroupBy(card => card.Status)
            .ToDictionary(group => group.Key, group => group.Count());
        var visible = selectedStatus is null
            ? all
            : all.Where(card => card.Status == selectedStatus).ToList();

        string Href(string? filterStatus, ProjectScope targetScope) =>
            Url.Action(nameof(Index), new
            {
                unit = selectedUnit.Id,
                sub,
                status = filterStatus,
                q = search,
                tab = targetScope == ProjectScope.Archived ? "archived" : "active"
            }) ?? "/Projects";

        var filters = new List<StatusFilterViewModel>
        {
            new()
            {
                Label = _text["Common_All"],
                Count = all.Count,
                IsSelected = selectedStatus is null,
                Href = Href(null, scope)
            }
        };

        filters.AddRange(ProjectStatus.All.Select(projectStatus => new StatusFilterViewModel
        {
            Value = projectStatus,
            Label = _text[$"ProjectStatus_{projectStatus}"],
            Count = counts.GetValueOrDefault(projectStatus),
            IsSelected = selectedStatus == projectStatus,
            Href = Href(projectStatus, scope)
        }));

        var model = new ProjectsIndexViewModel
        {
            Unit = selectedUnit,
            Ancestors = ancestors,
            IncludeDescendants = sub,
            Scope = scope,
            Status = selectedStatus,
            Search = search,
            ScopeSwitch = BuildScopeSwitch(sub),
            Filters = filters,
            Cards = visible.Select(BuildCard).ToList(),
            CanCreate = _permissions.HasPermission(PermissionNames.ProjectsCreate)
        };

        ViewData["ProjectsActiveHref"] = Href(selectedStatus, ProjectScope.Active);
        ViewData["ProjectsArchivedHref"] = Href(selectedStatus, ProjectScope.Archived);
        SetBreadcrumb(ancestors);

        return View(model);
    }

    [HttpGet("/projects/{id:guid}")]
    public async Task<IActionResult> Overview(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var access = await AuthorizeAsync(id, cancellationToken);
        if (access.Denied) return access.Failure!;
        var detail = access.Project!;
        var members = await _teams.GetMembersAsync(id, cancellationToken);
        var counts = await _projects.GetTaskStatusCountsAsync(id, cancellationToken);
        var overdue = await _projects.CountOverdueTasksAsync(id, cancellationToken);
        var mayEdit = CanEditProject();
        var header = BuildHeader(detail, members, "overview", mayEdit);
        var counted = counts.Where(pair => pair.Key != TaskState.Cancelled).Sum(pair => pair.Value);
        var done = counts.GetValueOrDefault(TaskState.Done);
        var maximum = counts.Values.DefaultIfEmpty().Max();
        var owner = members.FirstOrDefault(member => member.UserId == detail.OwnerId);

        var model = new ProjectDetailViewModel
        {
            Header = header,
            Description = detail.Description,
            StatusBars = TaskState.All.Select(status => BuildStatusBar(
                status, counts.GetValueOrDefault(status), maximum)).ToList(),
            TasksSummary = _text["ProjectDetail_TasksSummary", done, counted, overdue],
            Facts =
            [
                new() { Label = _text["Project_Fact_CreatedOn"], Value = detail.CreatedAt.Local() },
                new() { Label = _text["Project_Fact_CreatedBy"], Value = detail.CreatedByName },
                new()
                {
                    Label = _text["Project_Fact_LastActivity"],
                    Value = (detail.UpdatedAt ?? detail.CreatedAt).Local()
                }
            ],
            OwnerInitials = NameInitials.Of(detail.OwnerName),
            OwnerDepartment = owner?.DepartmentName ?? "—",
            OwnerRoleDescription = _text["ProjectDetail_OwnerRoleDescription"],
            MayArchive = mayEdit && detail.ArchivedAt is null
        };

        await SetProjectBreadcrumbAsync(detail, cancellationToken);
        return View("Details", model);
    }

    [HttpGet("/projects/{id:guid}/team")]
    public async Task<IActionResult> Team(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var access = await AuthorizeAsync(id, cancellationToken);
        if (access.Denied) return access.Failure!;
        var detail = access.Project!;
        var members = await _teams.GetMembersAsync(id, cancellationToken);
        var memberIds = members.Select(member => member.UserId).ToHashSet();
        // جذر الوحدة يأتي مع سجلّ التفاصيل، فلا تُقرأ المنظّمة مرّةً أخرى.
        var candidates = await _projects.GetCandidatesAsync(
            detail.OrganizationRootId, cancellationToken);
        var mayEdit = CanEditProject();
        var model = new ProjectTeamViewModel
        {
            Header = BuildHeader(detail, members, "team", mayEdit),
            ProjectId = id,
            LeadCards = members
                .Where(member => member.Role is TeamMemberRoles.Leader or TeamMemberRoles.Deputy)
                .OrderBy(member => member.Role == TeamMemberRoles.Leader ? 0 : 1)
                .Select(member => new ProjectLeadCardViewModel
                {
                    Name = member.Name,
                    Initials = NameInitials.Of(member.Name),
                    Department = member.DepartmentName,
                    BadgeLabel = _text[$"TeamRole_{member.Role}"],
                    IsPrimary = member.Role == TeamMemberRoles.Leader
                }).ToList(),
            Members = members.Select(member => new ProjectTeamMemberViewModel
            {
                UserId = member.UserId,
                Name = member.Name,
                Email = member.Email,
                Initials = NameInitials.Of(member.Name),
                Role = member.Role,
                RoleName = _text[$"TeamRole_{member.Role}"],
                Department = member.DepartmentName,
                OpenTasks = member.OpenTasks,
                IsProjectOwner = member.IsProjectOwner
            }).ToList(),
            Roles = TeamMemberRoles.All.Select(role => new ProjectCreateOptionViewModel
            {
                Value = role,
                Label = _text[$"TeamRole_{role}"]
            }).ToList(),
            Candidates = candidates.Where(candidate => !memberIds.Contains(candidate.Id))
                .Select(candidate => new ProjectCreatePersonViewModel
                {
                    Id = candidate.Id,
                    Name = candidate.Name,
                    Initial = NameInitials.Of(candidate.Name),
                    OrganizationName = candidate.OrganizationName
                }).ToList(),
            MayEdit = mayEdit,
            Error = TempData["ProjectTeamError"] as string,
            Saved = TempData["ProjectTeamSaved"] as string
        };

        await SetProjectBreadcrumbAsync(detail, cancellationToken);
        return View(model);
    }

    [HttpGet("/projects/{id:guid}/activity")]
    public async Task<IActionResult> Activity(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var access = await AuthorizeAsync(id, cancellationToken);
        if (access.Denied) return access.Failure!;
        var detail = access.Project!;
        var members = await _teams.GetMembersAsync(id, cancellationToken);
        var rows = await _activity.ForProjectAsync(id, 50, cancellationToken);
        var mayEdit = CanEditProject();

        var model = new ProjectActivityViewModel
        {
            Header = BuildHeader(detail, members, "activity", mayEdit),
            Items = rows.Select(BuildActivity).ToList()
        };

        await SetProjectBreadcrumbAsync(detail, cancellationToken);
        return View(model);
    }

    [HttpPost("/projects/{id:guid}/archive")]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionNames.ProjectsEdit)]
    public async Task<IActionResult> Archive(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var access = await AuthorizeAsync(id, cancellationToken);
        if (access.Denied) return access.Failure!;
        if (!TryGetActorId(out var actorId)) return Forbid();

        await _projectCommands.ArchiveAsync(id, actorId, cancellationToken);
        TempData["ProjectArchived"] = _text["ProjectDetail_Archived"].Value;
        return RedirectToAction(nameof(Index), new
        {
            unit = access.Project!.OrganizationId,
            tab = "archived"
        });
    }

    [HttpPost("/projects/{id:guid}/team/members")]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionNames.ProjectsEdit)]
    public Task<IActionResult> AddTeamMember(
        Guid id,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default) =>
        TeamCommandAsync(
            id,
            "Team_MemberAdded",
            actorId => _teamCommands.AddMemberAsync(id, userId, role, actorId, cancellationToken),
            cancellationToken);

    [HttpPost("/projects/{id:guid}/team/role")]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionNames.ProjectsEdit)]
    public Task<IActionResult> SetTeamRole(
        Guid id,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default) =>
        TeamCommandAsync(
            id,
            "Team_RoleUpdated",
            actorId => _teamCommands.SetRoleAsync(id, userId, role, actorId, cancellationToken),
            cancellationToken);

    [HttpPost("/projects/{id:guid}/team/remove")]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionNames.ProjectsEdit)]
    public Task<IActionResult> RemoveTeamMember(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        TeamCommandAsync(
            id,
            "Team_MemberRemoved",
            actorId => _teamCommands.RemoveMemberAsync(id, userId, actorId, cancellationToken),
            cancellationToken);

    [HttpGet("/Projects/Create")]
    [RequirePermission(PermissionNames.ProjectsCreate)]
    public async Task<IActionResult> Create(
        Guid? unit,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActorId(out var actorId)) return Forbid();

        var allowedUnits = await GetCreatableUnitsAsync(actorId, cancellationToken);
        if (allowedUnits.Count == 0) return Forbid();

        var selectedUnit = unit is null
            ? allowedUnits[0]
            : allowedUnits.FirstOrDefault(candidate => candidate.Id == unit.Value);
        if (selectedUnit is null) return Forbid();

        var candidates = await _projects.GetCandidatesAsync(selectedUnit.RootId, cancellationToken);
        var input = new ProjectCreateInput
        {
            UnitId = selectedUnit.Id,
            OwnerId = candidates.FirstOrDefault(person => person.Id == actorId)?.Id
                      ?? candidates.FirstOrDefault()?.Id,
            Status = ProjectStatus.Planning,
            Priority = ProjectPriority.Normal
        };

        return await StepOneViewAsync(input, allowedUnits, selectedUnit, null, cancellationToken);
    }

    [HttpPost("/Projects/Create/Details")]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionNames.ProjectsCreate)]
    public async Task<IActionResult> CreateDetails(
        ProjectCreateInput input,
        CancellationToken cancellationToken = default)
    {
        var (failure, allowedUnits, selectedUnit, _) = await ResolveCreateUnitAsync(input, cancellationToken);
        if (failure is not null) return failure;

        if (!ModelState.IsValid)
        {
            return await StepOneViewAsync(
                input, allowedUnits!, selectedUnit!, CreateBindingError(), cancellationToken);
        }

        // الخطوة الأولى لا تحفظ، فتُستشار الخدمة على سبيل الاختبار قبل عرض الخطوة الثانية.
        try
        {
            await _projectCommands.ValidateDraftAsync(
                selectedUnit!.Id, input.Name, input.Description, input.Status,
                input.Priority, input.StartDate, input.DueDate, input.OwnerId, cancellationToken);
        }
        catch (DomainException exception)
        {
            return await StepOneViewAsync(
                input, allowedUnits!, selectedUnit!, Attach(exception), cancellationToken);
        }

        input.SelectedMemberIds = [input.OwnerId!.Value];
        input.MemberRoles = new Dictionary<Guid, string>
        {
            [input.OwnerId.Value] = TeamMemberRoles.Leader
        };

        return await StepTwoViewAsync(input, selectedUnit!, null, cancellationToken);
    }

    [HttpPost("/Projects/Create")]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionNames.ProjectsCreate)]
    public async Task<IActionResult> CreateProject(
        ProjectCreateInput input,
        CancellationToken cancellationToken = default)
    {
        var (failure, _, selectedUnit, actorId) = await ResolveCreateUnitAsync(input, cancellationToken);
        if (failure is not null) return failure;

        // الاختيارات تُعاد كما أدخلها المستخدم لا كما نجت من الفحص،
        // وإلّا مسح خطأٌ واحدٌ ما اختاره كلّه.
        var ownerId = input.OwnerId ?? Guid.Empty;
        input.SelectedMemberIds = input.SelectedMemberIds
            .Append(ownerId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (!ModelState.IsValid)
        {
            return await StepTwoViewAsync(input, selectedUnit!, CreateBindingError(), cancellationToken);
        }

        var assignments = input.SelectedMemberIds
            .Select(memberId => new ProjectTeamMember(
                memberId,
                memberId == ownerId
                    ? TeamMemberRoles.Leader
                    : input.MemberRoles.GetValueOrDefault(memberId) ?? string.Empty))
            .ToList();

        try
        {
            var created = await _projectCommands.CreateWithTeamAsync(
                selectedUnit!.Id,
                input.Name,
                input.Description,
                input.Status,
                input.Priority,
                input.StartDate,
                input.DueDate,
                ownerId,
                ProjectTeamName(input.Name),
                assignments,
                actorId,
                cancellationToken);

            TempData["ProjectCreated"] = _text[
                "ProjectCreate_Success",
                created.Project.Name,
                selectedUnit.Name,
                created.Project.Code,
                created.Team.Name,
                created.MemberCount].Value;

            return RedirectToAction(nameof(Index), new { unit = selectedUnit.Id });
        }
        catch (DomainException exception)
        {
            return await StepTwoViewAsync(input, selectedUnit!, Attach(exception), cancellationToken);
        }
    }

    [HttpPost("/Projects/Create/Back")]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionNames.ProjectsCreate)]
    public async Task<IActionResult> BackToCreateDetails(
        ProjectCreateInput input,
        CancellationToken cancellationToken = default)
    {
        var (failure, allowedUnits, selectedUnit, _) = await ResolveCreateUnitAsync(input, cancellationToken);
        if (failure is not null) return failure;

        return await StepOneViewAsync(input, allowedUnits!, selectedUnit!, null, cancellationToken);
    }

    /// <summary>
    /// الوحدة المختارة يجب أن تكون من وحدات الفاعل المسموحة؛ معرّفها يأتي من النموذج.
    /// الإجراءات الأربعة تبدأ بهذا الفحص، فجُمع هنا كي لا يسقط من واحدٍ منها.
    /// </summary>
    private async Task<(IActionResult? Failure, List<Organization>? Units, Organization? Selected, Guid ActorId)>
        ResolveCreateUnitAsync(ProjectCreateInput input, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return (Forbid(), null, null, Guid.Empty);

        var allowedUnits = await GetCreatableUnitsAsync(actorId, cancellationToken);
        var selectedUnit = allowedUnits.FirstOrDefault(unit => unit.Id == input.UnitId);

        return selectedUnit is null
            ? (Forbid(), null, null, Guid.Empty)
            : (null, allowedUnits, selectedUnit, actorId);
    }

    /// <summary>يعلّق خطأ المجال بحقله إن كان له حقل، ويعيد رسالته.</summary>
    private string Attach(DomainException exception)
    {
        if (exception.Field is not null)
        {
            ModelState.TryAddModelError(exception.Field, exception.Message);
        }

        return exception.Message;
    }

    private async Task<IActionResult> StepOneViewAsync(
        ProjectCreateInput input,
        IReadOnlyList<Organization> allowedUnits,
        Organization selectedUnit,
        string? error,
        CancellationToken cancellationToken)
    {
        var (candidates, pathLabel, codePreview) =
            await CreateContextAsync(selectedUnit, cancellationToken);

        SetCreateBreadcrumb();
        return View("CreateStepOne", new ProjectCreateStepOneViewModel
        {
            Input = input,
            Units = allowedUnits.Select(unit => new ProjectCreateUnitViewModel
            {
                Id = unit.Id,
                Name = unit.Name,
                TypeLabel = _text[$"OrgType_{unit.Type}"],
                Depth = unit.Depth,
                IsRoot = unit.Depth == 0,
                IsSelected = unit.Id == selectedUnit.Id,
                Href = Url.Action(nameof(Create), new { unit = unit.Id }) ?? "/Projects/Create"
            }).ToList(),
            Owners = candidates.Select(candidate => BuildPerson(candidate, input)).ToList(),
            Statuses = ProjectStatus.All.Select(status => new ProjectCreateOptionViewModel
            {
                Value = status,
                Label = _text[$"ProjectStatus_{status}"],
                IsSelected = input.Status == status
            }).ToList(),
            Priorities = ProjectPriority.All.Select(priority => new ProjectCreateOptionViewModel
            {
                Value = priority,
                Label = _text[$"ProjectPriority_{priority}"],
                IsSelected = input.Priority == priority
            }).ToList(),
            UnitPathLabel = pathLabel,
            CodePreview = codePreview,
            TeamName = ProjectTeamName(input.Name),
            FieldErrors = BuildCreateFieldErrors(),
            Error = error
        });
    }

    private async Task<IActionResult> StepTwoViewAsync(
        ProjectCreateInput input,
        Organization selectedUnit,
        string? error,
        CancellationToken cancellationToken)
    {
        var (candidates, pathLabel, codePreview) =
            await CreateContextAsync(selectedUnit, cancellationToken);

        SetCreateBreadcrumb();
        return View("CreateStepTwo", new ProjectCreateStepTwoViewModel
        {
            Input = input,
            UnitName = selectedUnit.Name,
            UnitPathLabel = pathLabel,
            CodePreview = codePreview,
            TeamName = ProjectTeamName(input.Name),
            Candidates = candidates.Select(candidate => BuildPerson(candidate, input)).ToList(),
            Roles = TeamMemberRoles.All.Select(role => new ProjectCreateOptionViewModel
            {
                Value = role,
                Label = _text[$"TeamRole_{role}"]
            }).ToList(),
            FieldErrors = BuildCreateFieldErrors(),
            Error = error
        });
    }

    private async Task<(List<ProjectPerson> Candidates, string PathLabel, string CodePreview)>
        CreateContextAsync(Organization selectedUnit, CancellationToken cancellationToken)
    {
        var ancestors = await _organizations.GetAncestorsAsync(selectedUnit.Id, cancellationToken);
        var candidates = await _projects.GetCandidatesAsync(selectedUnit.RootId, cancellationToken);
        var codePreview = await _projects.GetNextCodePreviewAsync(cancellationToken);

        return (candidates, string.Join(" › ", ancestors.Select(unit => unit.Name)), codePreview);
    }

    private ProjectCreatePersonViewModel BuildPerson(ProjectPerson candidate, ProjectCreateInput input)
    {
        var isOwner = candidate.Id == input.OwnerId;

        return new ProjectCreatePersonViewModel
        {
            Id = candidate.Id,
            Name = candidate.Name,
            Initial = NameInitials.Of(candidate.Name),
            OrganizationName = candidate.OrganizationName,
            OrganizationRoleLabel = _text[$"OrgMemberRole_{candidate.OrganizationRole}"],
            IsOwner = isOwner,
            IsSelected = isOwner || input.SelectedMemberIds.Contains(candidate.Id),
            SelectedRole = isOwner
                ? TeamMemberRoles.Leader
                : input.MemberRoles.GetValueOrDefault(candidate.Id) ?? TeamMemberRoles.Member
        };
    }

    /// <summary>
    /// وحدات الفاعل وما يتبعها . تُنزع العضويّات المتداخلة أوّلاً لأنّ شجرة الأعلى
    /// تشمل ما دونها، فيبقى استعلام شجرةٍ واحد بدل استعلامٍ لكلّ عضويّة.
    /// </summary>
    private async Task<List<Organization>> GetCreatableUnitsAsync(
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var memberships = await _organizations.GetOrganizationsByUserAsync(actorId, cancellationToken);

        var unique = new Dictionary<Guid, Organization>();
        foreach (var membership in OrgScope.Outermost(memberships))
        {
            foreach (var unit in await _organizations.GetSubtreeAsync(membership.Id, cancellationToken))
            {
                unique[unit.Id] = unit;
            }
        }

        return unique.Values.OrderBy(unit => unit.Path).ToList();
    }

    private string ProjectTeamName(string? name) =>
        _text["ProjectCreate_TeamName", string.IsNullOrWhiteSpace(name) ? "—" : name.Trim()];

    private string CreateBindingError()
    {
        var errors = BuildCreateFieldErrors();
        if (errors.Count > 0) return string.Join(" ", errors.Values.Distinct());

        var firstMessage = ModelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

        return firstMessage ?? _text["ProjectCreate_InvalidInput"].Value;
    }

    private Dictionary<string, string> BuildCreateFieldErrors()
    {
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in ModelState.Where(entry => entry.Value?.Errors.Count > 0))
        {
            var field = entry.Key.Split('.').Last();
            var modelMessage = entry.Value!.Errors
                .Select(error => error.ErrorMessage)
                .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

            errors[field] = field switch
            {
                nameof(ProjectCreateInput.StartDate)
                    when string.Equals(modelMessage, "Invalid ISO date.", StringComparison.Ordinal) =>
                    _text["ProjectCreate_InvalidDate", _text["ProjectCreate_StartDate"]],
                nameof(ProjectCreateInput.DueDate)
                    when string.Equals(modelMessage, "Invalid ISO date.", StringComparison.Ordinal) =>
                    _text["ProjectCreate_InvalidDate", _text["ProjectCreate_DueDate"]],
                _ when !string.IsNullOrWhiteSpace(modelMessage) => modelMessage,
                nameof(ProjectCreateInput.OwnerId) => _text["ProjectCreate_InvalidOwner"],
                nameof(ProjectCreateInput.SelectedMemberIds) or nameof(ProjectCreateInput.MemberRoles) =>
                    _text["ProjectCreate_InvalidMembers"],
                _ => _text["ProjectCreate_InvalidField", field]
            };
        }

        return errors;
    }

    private bool TryGetActorId(out Guid actorId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);

    private void SetCreateBreadcrumb()
    {
        ViewData["Title"] = _text["ProjectCreate_Title"].Value;
        ViewData["Breadcrumb"] = new AppBreadcrumbViewModel
        {
            Label = _text["ProjectCreate_Title"],
            Items =
            [
                new() { Label = _text["Dashboard_Title"], Url = "/Dashboard" },
                new() { Label = _text["Nav_Projects"], Url = "/Projects" },
                new() { Label = _text["Projects_New"], IsCurrent = true }
            ]
        };
    }

    /// <summary>
    /// نتيجة التفويض: إمّا مشروعٌ مقروء وإمّا ردُّ رفضٍ جاهز. الردّ يُحمل هنا
    /// لا في HttpContext.Items كي يعجز أيّ إجراءٍ عن استعمال المشروع قبل فحصه.
    /// </summary>
    private readonly record struct ProjectAccess(IActionResult? Failure, ProjectDetailRecord? Project)
    {
        public bool Denied => Failure is not null;
    }

    /// <summary>
    /// قراءة المشروع وفحص نطاقه في استعلامين: السجلّ يحمل مسار وحدته،
    /// فلا حاجة لقراءة المشروع ثمّ المنظّمة ثمّ المسار ثلاث مرّات.
    /// </summary>
    private async Task<ProjectAccess> AuthorizeAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await _projects.GetDetailAsync(projectId, cancellationToken);
        if (project is null) return new ProjectAccess(NotFound(), null);

        if (!TryGetActorId(out var actorId) || project.OrganizationId is null)
        {
            return new ProjectAccess(Forbid(), null);
        }

        var scopePaths = await _organizations.GetScopePathsByUserAsync(actorId, cancellationToken);

        return OrgScope.Contains(scopePaths, project.OrganizationPath)
            ? new ProjectAccess(null, project)
            : new ProjectAccess(Forbid(), null);
    }

    /// <summary>
    /// الأوامر الثلاثة على الفريق تشترك في كلّ شيء عدا الأمر ورسالة نجاحه؛
    /// جمعها هنا يمنع إجراءً جديداً من تخطّي التفويض سهواً.
    /// </summary>
    private async Task<IActionResult> TeamCommandAsync(
        Guid id,
        string successKey,
        Func<Guid, Task> command,
        CancellationToken cancellationToken)
    {
        var access = await AuthorizeAsync(id, cancellationToken);
        if (access.Denied) return access.Failure!;
        if (!TryGetActorId(out var actorId)) return Forbid();

        try
        {
            await command(actorId);
            TempData["ProjectTeamSaved"] = _text[successKey].Value;
        }
        catch (DomainException exception)
        {
            TempData["ProjectTeamError"] = exception.Message;
        }

        return RedirectToAction(nameof(Team), new { id });
    }

    private bool CanEditProject() => _permissions.HasPermission(PermissionNames.ProjectsEdit);

    private ProjectHeaderViewModel BuildHeader(
        ProjectDetailRecord project,
        IReadOnlyList<TeamMemberCard> members,
        string currentTab,
        bool mayEdit) =>
        ProjectHeaderBuilder.Build(
            _text,
            project,
            members,
            currentTab,
            mayEdit,
            project.OrganizationId is { } unitId
                ? Url.Action("Index", "Projects", new { unit = unitId }) ?? "/Projects"
                : "/Projects");

    private TaskStatusBarViewModel BuildStatusBar(string status, int count, int maximum)
    {
        var tone = status switch
        {
            TaskState.InProgress => "info",
            TaskState.InReview => "warning",
            TaskState.Done => "success",
            TaskState.Cancelled => "danger",
            _ => "neutral"
        };

        return new TaskStatusBarViewModel
        {
            Label = _text[$"TaskState_{status}"],
            Count = count,
            WidthPercent = maximum == 0 ? 0 : (int)Math.Round(count * 100d / maximum),
            DotClass = $"ds-status-dot ds-status-dot--{tone}",
            BarClass = $"ds-status-bar__fill ds-status-bar__fill--{tone}"
        };
    }

    private ProjectActivityItemViewModel BuildActivity(ProjectActivityRecord activity)
    {
        var actor = string.IsNullOrWhiteSpace(activity.ActorName)
            ? _text["Activity_System"].Value
            : activity.ActorName;
        var entity = _text[$"Activity_Entity_{activity.EntityType}"];
        var payloadValue = ActivityPayload.Describe(_text, activity.EntityType, activity.Payload);
        var tone = activity.Action switch
        {
            ActivityActions.Created => "success",
            ActivityActions.Assigned or ActivityActions.Updated => "info",
            ActivityActions.StatusChanged => "warning",
            ActivityActions.Removed or ActivityActions.Archived => "danger",
            _ => "neutral"
        };

        return new ProjectActivityItemViewModel
        {
            Title = _text[$"Activity_{activity.Action}", actor, entity, payloadValue],
            TimeLabel = activity.CreatedAt.Relative(_text),
            TimeIso = activity.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
            ToneClass = $"ds-timeline-dot ds-timeline-dot--{tone}"
        };
    }


    private async Task SetProjectBreadcrumbAsync(
        ProjectDetailRecord project,
        CancellationToken cancellationToken)
    {
        var ancestors = project.OrganizationId is { } unitId
            ? await _organizations.GetAncestorsAsync(unitId, cancellationToken)
            : [];

        ViewData["Title"] = project.Name;
        ViewData["Breadcrumb"] = BreadcrumbBuilder.ForProject(
            _text, ancestors, project.Name,
            unit => Url.Action("Index", "Projects", new { unit }));
    }

    private ProjectCardViewModel BuildCard(ProjectCard card)
    {
        var ownerName = string.IsNullOrWhiteSpace(card.OwnerName) ? "—" : card.OwnerName.Trim();
        var percent = card.TotalTasks == 0
            ? 0
            : (int)Math.Round(card.DoneTasks * 100d / card.TotalTasks);
        var today = DateOnly.FromDateTime(DisplayTime.RiyadhNow());

        return new ProjectCardViewModel
        {
            Id = card.Id,
            Code = card.Code,
            Name = card.Name,
            Description = card.Description,
            StatusLabel = _text[$"ProjectStatus_{card.Status}"],
            StatusClass = ProjectPresentation.StatusClass(card.Status),
            StatusBadgeClass = ProjectPresentation.StatusBadgeClass(card.Status),
            PriorityLabel = _text[$"ProjectPriority_{card.Priority}"],
            PriorityClass = $"ds-priority ds-priority--{card.Priority}",
            UnitName = card.OrganizationName ?? "—",
            UnitTypeLabel = _text[$"OrgType_{card.OrganizationType}"],
            UnitIsRoot = card.OrganizationDepth == 0 && card.OrganizationType == "organization",
            OwnerName = ownerName,
            OwnerInitial = NameInitials.Of(ownerName),
            DueLabel = card.DueDate is { } due
                ? _text["Projects_Due", due.Local()]
                : _text["Projects_NoDue"],
            IsOverdue = card.DueDate is { } dueDate
                        && dueDate < today
                        && card.Status != ProjectStatus.Done,
            ProgressLabel = $"{card.DoneTasks}/{card.TotalTasks}",
            HasTasks = card.TotalTasks > 0,
            Percent = Math.Clamp(percent, 0, 100),
            Href = $"/projects/{card.Id}"
        };
    }

    private UnitScopeSwitchViewModel BuildScopeSwitch(bool includeDescendants) => new()
    {
        Label = _text["Projects_ScopeLabel"],
        Name = "sub",
        Hint = _text["Projects_ScopeHint"],
        Options =
        [
            new()
            {
                Label = _text["Projects_ThisUnit"],
                Value = "false",
                IsSelected = !includeDescendants
            },
            new()
            {
                Label = _text["Projects_AndBelow"],
                Value = "true",
                IsSelected = includeDescendants
            }
        ]
    };

    private void SetBreadcrumb(IReadOnlyList<Organization> ancestors)
    {
        var items = new List<AppBreadcrumbItemViewModel>
        {
            new() { Label = _text["Dashboard_Title"], Url = "/Dashboard" },
            new()
            {
                Label = _text["Nav_Projects"],
                Url = ancestors.Count == 0 ? null : "/Projects",
                IsCurrent = ancestors.Count == 0
            }
        };

        var unitItems = ancestors.Select((node, index) => new AppBreadcrumbItemViewModel
        {
            Label = node.Name,
            Url = index == ancestors.Count - 1
                ? null
                : Url.Action(nameof(Index), new { unit = node.Id }),
            IsCurrent = index == ancestors.Count - 1
        }).ToList();

        if (unitItems.Count > 3)
        {
            var foldedItems = unitItems.Skip(1).Take(unitItems.Count - 2).ToList();
            items.Add(unitItems[0]);
            items.Add(new AppBreadcrumbItemViewModel
            {
                Label = "…",
                Url = foldedItems[^1].Url,
                Title = string.Join(" › ", foldedItems.Select(item => item.Label)),
                AccessibleLabel = string.Join(", ", foldedItems.Select(item => item.Label)),
                IsFolded = true
            });
            items.Add(unitItems[^1]);
        }
        else
        {
            items.AddRange(unitItems);
        }

        ViewData["Breadcrumb"] = new AppBreadcrumbViewModel
        {
            Label = _text["Nav_Projects"],
            Items = items
        };
    }
}
