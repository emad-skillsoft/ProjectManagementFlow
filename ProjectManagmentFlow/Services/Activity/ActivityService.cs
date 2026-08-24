using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Activity;

public sealed class ActivityService(AppDbContext context) : IActivityService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public void Add(
        Guid? organizationId,
        Guid? projectId,
        Guid? actorId,
        string entityType,
        Guid entityId,
        string action,
        object? payload = null)
    {
        context.ActivityLog.Add(new ActivityLog
        {
            OrganizationId = organizationId,
            ProjectId = projectId,
            ActorId = actorId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Payload = payload is null ? null : JsonSerializer.Serialize(payload, Json),
            CreatedAt = DateTime.UtcNow
        });
    }

    public Task<List<ProjectActivityRecord>> ForProjectAsync(
        Guid projectId,
        int take = 50,
        CancellationToken cancellationToken = default) =>
        (from activity in context.ActivityLog.AsNoTracking()
         join user in context.Users.AsNoTracking()
             on activity.ActorId equals (Guid?)user.Id into actors
         from actor in actors.DefaultIfEmpty()
         where activity.ProjectId == projectId
         orderby activity.CreatedAt descending, activity.Id descending
         select new ProjectActivityRecord(
             activity.ActorId,
             actor == null ? null : actor.FullName ?? actor.Email,
             activity.EntityType,
             activity.Action,
             activity.Payload,
             activity.CreatedAt))
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);
}
