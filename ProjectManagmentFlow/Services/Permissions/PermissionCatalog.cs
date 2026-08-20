using Microsoft.EntityFrameworkCore;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Permissions;

public class PermissionCatalog : IPermissionCatalog
{
    private readonly AppDbContext _context;

    public PermissionCatalog(AppDbContext context) => _context = context;

    public async Task<List<Permission>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
}
