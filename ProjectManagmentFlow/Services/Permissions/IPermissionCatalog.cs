using Microsoft.EntityFrameworkCore;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Permissions;

public interface IPermissionCatalog
{
    Task<List<Permission>> GetAllAsync(CancellationToken cancellationToken = default);
}


