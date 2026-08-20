using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Users;

public interface IUserQueryService
{
    Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
