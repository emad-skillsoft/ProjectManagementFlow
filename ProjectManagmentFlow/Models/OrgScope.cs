namespace ProjectManagmentFlow.Models;

/// <summary>
/// حساب النطاق على مسارات الشجرة. مقارنة المسارات تقع في الذاكرة دائماً،
/// فـStringComparison.Ordinal هنا صحيحة — بخلاف استعلامات EF التي لا تترجمها.
/// </summary>
public static class OrgScope
{
    /// <summary>هل يقع المسار داخل أحد النطاقات المعطاة؟</summary>
    public static bool Contains(IEnumerable<string> scopePaths, string? path) =>
        path is not null
        && scopePaths.Any(scope => path.StartsWith(scope, StringComparison.Ordinal));

    /// <summary>
    /// المسارات الخارجيّة فقط: مسارٌ يبدأ بمسارٍ آخر يقع داخله، فإبقاؤه يكرّر الشرط.
    /// </summary>
    public static List<string> Outermost(IReadOnlyCollection<string> paths) =>
        paths.Where(path => !paths.Any(other =>
                  other.Length < path.Length
                  && path.StartsWith(other, StringComparison.Ordinal)))
             .ToList();

    /// <summary>المنظّمات الخارجيّة فقط، بالقاعدة نفسها.</summary>
    public static List<Organization> Outermost(IReadOnlyCollection<Organization> organizations) =>
        organizations.Where(organization => !organizations.Any(other =>
                          other.Id != organization.Id
                          && organization.Path.StartsWith(other.Path, StringComparison.Ordinal)))
                     .ToList();
}
