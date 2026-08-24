namespace ProjectManagmentFlow.Models;

public sealed class ActivityLog
{
    public long Id { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? ActorId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class ActivityEntities
{
    public const string Project = "Project";
    public const string Task = "Task";
    public const string Team = "Team";
    public const string Member = "Member";
}

public static class ActivityActions
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string StatusChanged = "StatusChanged";
    public const string Assigned = "Assigned";
    public const string Added = "Added";
    public const string Removed = "Removed";
    public const string Archived = "Archived";
    public const string Restored = "Restored";
}
