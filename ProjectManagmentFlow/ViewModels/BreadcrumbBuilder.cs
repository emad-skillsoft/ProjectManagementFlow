using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.ViewModels;

/// <summary>
/// مسار التنقّل لصفحات المشروع. الطيّ إلى «…» قاعدةٌ واحدة هنا، لا نسخةٌ
/// في كلّ متحكّم — وقد اختلفت النسختان فعلاً: واحدةٌ تطوي والأخرى لا.
/// </summary>
public static class BreadcrumbBuilder
{
    private const int VisibleUnits = 3;

    public static AppBreadcrumbViewModel ForProject(
        IStringLocalizer text,
        IReadOnlyList<Organization> ancestors,
        string projectName,
        Func<Guid, string?> unitHref)
    {
        var items = new List<AppBreadcrumbItemViewModel>
        {
            new() { Label = text["Dashboard_Title"], Url = "/Dashboard" },
            new() { Label = text["Nav_Projects"], Url = "/Projects" }
        };

        items.AddRange(Fold(ancestors
            .Select(unit => new AppBreadcrumbItemViewModel
            {
                Label = unit.Name,
                Url = unitHref(unit.Id)
            })
            .ToList()));

        items.Add(new AppBreadcrumbItemViewModel { Label = projectName, IsCurrent = true });

        return new AppBreadcrumbViewModel { Label = projectName, Items = items };
    }

    /// <summary>
    /// مسار وحدةٍ في الهيكل: سلسلة أجدادها مطويّةً، ثمّ هي نفسها.
    /// يشترك مع مسار المشروع في قاعدة الطيّ — فلا تختلف الصفحتان.
    /// </summary>
    public static AppBreadcrumbViewModel ForUnit(
        IStringLocalizer text,
        IReadOnlyList<Organization> ancestors,
        string unitName,
        Func<Guid, string?> unitHref)
    {
        var items = Fold(ancestors
            .Select(unit => new AppBreadcrumbItemViewModel
            {
                Label = unit.Name,
                Url = unitHref(unit.Id)
            })
            .ToList());

        items.Add(new AppBreadcrumbItemViewModel { Label = unitName, IsCurrent = true });

        return new AppBreadcrumbViewModel { Label = text["Nav_Structure"], Items = items };
    }

    /// <summary>
    /// سلسلةٌ أطول من ثلاثٍ تُطوى إلى: الأولى · «…» · الأخيرة.
    /// المطويّات تبقى في العنوان والوصف المسموع كي لا يضيع السياق.
    /// </summary>
    private static List<AppBreadcrumbItemViewModel> Fold(List<AppBreadcrumbItemViewModel> units)
    {
        if (units.Count <= VisibleUnits) return units;

        var folded = units.Skip(1).Take(units.Count - 2).ToList();

        return
        [
            units[0],
            new AppBreadcrumbItemViewModel
            {
                Label = "…",
                Url = folded[^1].Url,
                Title = string.Join(" › ", folded.Select(item => item.Label)),
                AccessibleLabel = string.Join(", ", folded.Select(item => item.Label)),
                IsFolded = true
            },
            units[^1]
        ];
    }
}
