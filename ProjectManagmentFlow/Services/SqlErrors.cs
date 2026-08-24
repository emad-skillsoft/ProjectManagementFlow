using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ProjectManagmentFlow.Services;

/// <summary>أخطاء التفريد التي يحكم بها SQL Server بعد سباق قراءة/كتابة.</summary>
public static class SqlErrors
{
    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
