namespace ProjectManagmentFlow.Services;

/// <summary>
/// خطأ قاعدةٍ من قواعد المجال، برسالةٍ مترجَمة جاهزةٍ للعرض.
/// يحمل اسم الحقل حين يكون للخطأ حقلٌ يُعلَّق به في النموذج، فيستغني
/// المتحكّم عن إعادة الفحص لمجرّد أن يعرف أين يضع الرسالة.
/// </summary>
public sealed class DomainException(string message, string? field = null) : Exception(message)
{
    public string? Field { get; } = field;
}
