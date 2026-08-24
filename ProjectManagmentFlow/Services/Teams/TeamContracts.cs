namespace ProjectManagmentFlow.Services.Teams;

public sealed record TeamMemberCard(
    Guid UserId,
    string Name,
    string Email,
    string Role,
    string DepartmentName,
    int OpenTasks,
    bool IsProjectOwner);
