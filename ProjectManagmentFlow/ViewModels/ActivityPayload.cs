using System.Text.Json;
using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.ViewModels;

/// <summary>
/// وصف حمولة سجلّ النشاط بلغة العرض. مكانها هنا لا في متحكّم، لأنّ السجلّ
/// يُعرض في تبويب النشاط وفي درج المهمّة معاً — وقراءته في موضعين تُنتج نصّين مختلفين.
/// </summary>
public static class ActivityPayload
{
    /// <summary>
    /// الاسم والقيمة معاً — لسجلّ المشروع، حيث لا يُعرف أيّ كيانٍ تغيّر.
    /// </summary>
    public static string Describe(IStringLocalizer text, string entityType, string? payload)
    {
        var (name, value) = Read(text, entityType, payload);

        return (name, value) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => text["Activity_NameAndValue", name, value],
            ({ Length: > 0 }, _) => name!,
            (_, { Length: > 0 }) => value!,
            _ => "—"
        };
    }

    /// <summary>
    /// القيمة وحدها — لسجلّ الكيان نفسه، حيث اسمه معروفٌ من الصفحة
    /// فذكره يُنتج «غيّر حالة المهمة إلى تجربه — القيمة: منجزة».
    /// </summary>
    public static string Value(IStringLocalizer text, string entityType, string? payload)
    {
        var (name, value) = Read(text, entityType, payload);
        return value is { Length: > 0 } ? value : name is { Length: > 0 } ? name : "—";
    }

    private static (string? Name, string? Value) Read(
        IStringLocalizer text,
        string entityType,
        string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return (null, null);

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            var name = root.TryGetProperty("Name", out var nameElement) ? nameElement.ToString() : null;
            var value = root.TryGetProperty("Value", out var valueElement)
                ? valueElement.ToString()
                : root.TryGetProperty("Status", out var statusElement)
                    ? statusElement.ToString()
                    : null;

            return (name, Label(text, entityType, value));
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    /// <summary>
    /// نوع الكيان يفصل بين المتشابهات: «done» حالةُ مشروعٍ ومهمّةٍ معاً،
    /// ونصّها يختلف — «منجز» للمشروع و«منجزة» للمهمّة.
    /// </summary>
    private static string? Label(IStringLocalizer text, string entityType, string? value)
    {
        if (value is null or "") return value;

        return entityType switch
        {
            ActivityEntities.Task when TaskState.IsKnown(value) => text[$"TaskState_{value}"],
            ActivityEntities.Project when ProjectStatus.IsKnown(value) => text[$"ProjectStatus_{value}"],
            ActivityEntities.Member or ActivityEntities.Team when TeamMemberRoles.IsKnown(value)
                => text[$"TeamRole_{value}"],
            _ => value
        };
    }
}
