using ProjectManagmentFlow.Services.Organizations;
using ProjectManagmentFlow.Services.Projects;

namespace ProjectManagmentFlow.ViewModels;

/// <summary>مدخل صفحة «الهيكل» — وحدة واحدة، شجرتها، ولوحاتها. يبني المتحكّم مرّة واحدة.</summary>
public sealed class StructureViewModel
{
    public required StructureUnitPanelViewModel Panel { get; init; }
    public required StructureTreePanelViewModel Tree { get; init; }
    public required StructureChartPanelViewModel Chart { get; init; }
    public required StructureViewSwitchViewModel View { get; init; }
    public bool IsChartView { get; init; }
    public string? Status { get; init; }
    public string? Error { get; init; }
}

/// <summary>لوحة الوحدة: الترويسة، التبويبات الثلاثة، ونوافذ الإنشاء والنقل والحذف.</summary>
public sealed class StructureUnitPanelViewModel
{
    public required Guid UnitId { get; init; }
    public required string Name { get; init; }
    public UnitTypeBadgeViewModel Type { get; init; } = new();
    public string? Code { get; init; }
    public short Depth { get; init; }
    public int MaxDepth { get; init; }
    /// <summary>المستوى المعروض «٥ من ٨» — الرتبة (لا العمق).</summary>
    public required string LevelLabel { get; init; }
    public required int DirectChildren { get; init; }
    public bool MayManage { get; init; }

    /// <summary>تبويبٌ محمّل — العرض يُبنى عليه ولا يُعاد استعلام.</summary>
    public List<StructureTabViewModel> Tabs { get; init; } = [];

    // تبويب «نظرة عامّة»
    public List<StructureStatViewModel> Stats { get; init; } = [];
    public List<StructureDirectUnitViewModel> DirectUnits { get; init; } = [];

    // تبويب «الأعضاء»
    public List<StructureMemberViewModel> Members { get; init; } = [];
    public List<StructureMemberViewModel> InheritedMembers { get; init; } = [];
    public string? Error { get; init; }
    public string? Status { get; init; }

    // تبويب «المشاريع» — بطاقات جاهزة للعرض كما في صفحة المشاريع.
    public List<ProjectCardViewModel> Projects { get; init; } = [];
    public bool IncludeDescendants { get; init; }

    // نوافذ — تُبنى دائماً لكن تُعرض لمن يدير الوحدة فقط.
    public StructureCreateDialogViewModel Create { get; init; } = new()
    {
        Label = string.Empty, NameLabel = string.Empty, CodeLabel = string.Empty,
        TypeLabel = string.Empty, SubmitLabel = string.Empty, ParentId = Guid.Empty.ToString()
    };
    public StructureMoveDialogViewModel Move { get; init; } = new()
    {
        UnitId = Guid.Empty,
        Label = string.Empty, Summary = string.Empty, SubmitLabel = string.Empty
    };
    public StructureDeleteDialogViewModel DeleteDialog { get; init; } = new()
    {
        UnitId = Guid.Empty,
        Label = string.Empty, ConfirmLabel = string.Empty, UnitName = string.Empty
    };
}

/// <summary>خانة في التبويبات الثلاثة — تُبنى مرة واحدة.</summary>
public sealed class StructureTabViewModel
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required string Href { get; init; }
    public bool IsCurrent { get; init; }
}

/// <summary>بطاقة مؤشّر في «نظرة عامّة».</summary>
public sealed class StructureStatViewModel
{
    public required string Value { get; init; }
    public required string Label { get; init; }
    public string CssClass { get; init; } = string.Empty;
}

/// <summary>وحدةٌ مباشرةٌ في «نظرة عامّة» — اسم، شارة، بعدّاد مشاريعها المباشرة.</summary>
public sealed class StructureDirectUnitViewModel
{
    public required Guid UnitId { get; init; }
    public required string Name { get; init; }
    public UnitTypeBadgeViewModel Type { get; init; } = new();
    public string? Code { get; init; }
    public int DirectProjects { get; init; }
    public required string Href { get; init; }
}

/// <summary>صفُّ عضو في تبويب «الأعضاء» — مباشرٌ أو موروَث.</summary>
public sealed class StructureMemberViewModel
{
    public required Guid UserId { get; init; }
    public required string Name { get; init; }
    public string Initials { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public required string Role { get; init; }
    public required string RoleLabel { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public bool IsInherited { get; init; }
    public string? InheritedFromName { get; init; }
    public bool MayChangeRole { get; init; }
}

/// <summary>نواة الشجرة الجانبيّة — عرفت الجذر والأبناء يُبنون ذاتيّاً.</summary>
public sealed class StructureTreePanelViewModel
{
    public required string Label { get; init; }
    public required string UnitCountLabel { get; init; }
    public required string SearchLabel { get; init; }
    public required string ExpandAllLabel { get; init; }
    public required string CollapseAllLabel { get; init; }
    public required string ToggleLabel { get; init; }
    public required Guid SelectedUnitId { get; init; }
    public string Search { get; init; } = string.Empty;
    public bool AllExpanded { get; init; }
    /// <summary>جذر شجرةٍ واحدة — منظّمةٌ لا أكثر من جذرٍ في نطاقٍ واحد.</summary>
    public StructureTreeNodeViewModel? Root { get; init; }
}

/// <summary>عقدةٌ واحدة في الشجرة الجانبيّة — تبنِي نفسها من أبنائها.</summary>
public sealed class StructureTreeNodeViewModel
{
    public required Guid UnitId { get; init; }
    public required string Name { get; init; }
    public UnitTypeBadgeViewModel Type { get; init; } = new();
    public string? Code { get; init; }
    public int DirectProjects { get; init; }
    public int Depth { get; init; }
    public bool IsExpanded { get; init; }
    public List<StructureTreeNodeViewModel> Children { get; init; } = [];
    public bool IsCurrent { get; init; }
    public string Href { get; init; } = string.Empty;

    /// <summary>الأب — تقرأه الشجرة المسطّحة لتطوي ذرّيّة عقدةٍ بعينها.</summary>
    public Guid? ParentId { get; init; }

    public bool HasChildren => Children.Count > 0;

    /// <summary>
    /// الشجرة تُرسم مسطّحةً بترتيبٍ سابق، والتداخل يُعبَّر عنه بـ--ds-unit-depth.
    /// هكذا يعرّفها الثيم: ds-unit-tree__item صفٌّ مرنٌ واحد، لا وعاءٌ يتداخل.
    /// </summary>
    public IEnumerable<StructureTreeNodeViewModel> Flatten()
    {
        yield return this;
        foreach (var descendant in Children.SelectMany(child => child.Flatten()))
        {
            yield return descendant;
        }
    }
}

/// <summary>نواة مخطّط الشجرة — تقاربها JS ويطيّها، و«ملاءمة العرض» تسوّيها مع عرض الشاشة.</summary>
public sealed class StructureChartToolbarViewModel
{
    public required string SearchLabel { get; init; }
    public required string ExpandAllLabel { get; init; }
    public required string CollapseAllLabel { get; init; }
    public required string ZoomInLabel { get; init; }
    public required string ZoomOutLabel { get; init; }
    public required string FitLabel { get; init; }
    public int ZoomPercent { get; init; } = 100;
}

/// <summary>مخطّط الشجرة — جذر واحد وحيد، والأبناء متسلسلون.</summary>
public sealed class StructureChartPanelViewModel
{
    public required string Label { get; init; }
    public required StructureChartToolbarViewModel Toolbar { get; init; }
    public StructureChartNodeViewModel? Root { get; init; }
    public string Hint { get; init; } = string.Empty;
}

/// <summary>عقدةٌ واحدة في المخطّط — تبني أبناءها.</summary>
public sealed class StructureChartNodeViewModel
{
    /// <summary>يحمل وضع العرض الحاليّ كي لا يُخرجك الضغط من المخطّط.</summary>
    public string Href { get; init; } = string.Empty;

    public required Guid UnitId { get; init; }
    public required string Name { get; init; }
    public UnitTypeBadgeViewModel Type { get; init; } = new();
    public string? Code { get; init; }
    public bool HasChildren { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsExpanded { get; init; }
    public List<StructureChartNodeViewModel> Children { get; init; } = [];
}

/// <summary>مبدّل «قائمة/مخطّط» — حالة العرض حاليّة، وحالة «لا أدير شيئاً» لا تظهر أصلًا هنا.</summary>
public sealed class StructureViewSwitchViewModel
{
    public required string Label { get; init; }
    public required string Meta { get; init; }
    public List<StructureViewOptionViewModel> Options { get; init; } = [];
}

public sealed class StructureViewOptionViewModel
{
    public required string Label { get; init; }
    public required string Value { get; init; }
    public bool IsSelected { get; init; }
}

/// <summary>نافذة «وحدة فرعيّة جديدة» — الأنواع المسموحة محسوبة من رتبة الأب.</summary>
public sealed class StructureCreateDialogViewModel
{
    public required string Label { get; init; }
    public required string NameLabel { get; init; }
    public required string CodeLabel { get; init; }
    public required string TypeLabel { get; init; }
    public required string SubmitLabel { get; init; }
    public required string ParentId { get; init; }
    public List<StructureTypeOptionViewModel> AllowedTypes { get; init; } = [];
}

public sealed class StructureTypeOptionViewModel
{
    public required string Value { get; init; }
    public required string Label { get; init; }
    public bool IsSelected { get; init; }
}

/// <summary>نافذة «نقل الوحدة» — كلّ وجهة مع سببِها إن مُنعت، ثمّ «أثر النقل» قبل التأكيد.</summary>
public sealed class StructureMoveDialogViewModel
{
    public required Guid UnitId { get; init; }
    public required string Label { get; init; }
    public required string Summary { get; init; }
    public List<StructureMoveOptionViewModel> Targets { get; init; } = [];
    public string? SelectedTargetId { get; init; }
    public required string SubmitLabel { get; init; }
    public string ImpactLabel { get; init; } = string.Empty;
}

public sealed class StructureMoveOptionViewModel
{
    public Guid? TargetId { get; init; }
    public required string Name { get; init; }
    public bool IsAllowed { get; init; }
    public string? Reason { get; init; }
}

/// <summary>نافذة «الحذف» — المانع ظاهرة، والزرّ معلّق ما دام المانع قائماً.</summary>
public sealed class StructureDeleteDialogViewModel
{
    public required Guid UnitId { get; init; }
    public required string Label { get; init; }
    public required string ConfirmLabel { get; init; }
    public required string UnitName { get; init; }
    public string? Blocker { get; init; }
    public bool IsBlocked => Blocker is not null;
}
