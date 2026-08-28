namespace ProjectManagmentFlow.Models;

/// <summary>
/// أنواع الوحدات الستّ. القاعدة الوحيدة: رتبة الابن أكبر من رتبة الأب
/// (رقم أعلى)، والجذر يجب أن يكون <c>organization</c>.
/// التسمية تُعرض من ملفّ الترجمة بمفتاح <c>OrgType_{code}</c>، وهنا
/// الرموز والرتب فقط — لا نصّ.
/// </summary>
public static class OrgUnitTypes
{
    public const string Organization = "organization";
    public const string Sector = "sector";
    public const string GeneralAdmin = "general_admin";
    public const string Admin = "admin";
    public const string Department = "department";
    public const string Division = "division";

    /// <summary>الأنواع مرتّبةً من الأعلى إلى الأدنى — مصدر قوائم الاختيار.</summary>
    public static readonly string[] All =
    [
        Organization, Sector, GeneralAdmin, Admin, Department, Division
    ];

    /// <summary>الرتب: organization أعلى (صفر) وdivision أدنى (خمسة).</summary>
    public static readonly Dictionary<string, int> Rank = new()
    {
        [Organization] = 0,
        [Sector] = 1,
        [GeneralAdmin] = 2,
        [Admin] = 3,
        [Department] = 4,
        [Division] = 5
    };

    public static bool IsKnown(string? code) => code is not null && Rank.ContainsKey(code);

    public static int GetRank(string code) => Rank[code];

    /// <summary>الأنواع المسموحة لولدٍ في ظل أبٍ من هذا النوع: الأرقام الأعلى فقط.</summary>
    public static string[] AllowedChildTypes(string parentType)
    {
        var parentRank = Rank[parentType];
        return All.Where(code => Rank[code] > parentRank).ToArray();
    }
}
