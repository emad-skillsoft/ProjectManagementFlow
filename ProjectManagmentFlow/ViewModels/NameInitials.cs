namespace ProjectManagmentFlow.ViewModels;

public static class NameInitials
{
    /// <summary>
    /// حرف الأفاتار: أوّل حرف من الاسم، حرفاً واحداً لا غير.
    /// حروف العربية تتّصل، فحرفان من اسمين متجاورين يُقرآن كلمةً مبتورة —
    /// «عمر السلمي» تصير «عس».
    /// </summary>
    public static string Of(string name)
    {
        var first = (name ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Significant)
            .FirstOrDefault(part => part.Length > 0);

        return first is null ? "—" : first[..1];
    }

    // «ال» التعريف لا تميّز اسماً عن آخر: «السلمي» و«القحطاني» كلاهما يبدأ بألف.
    private static string Significant(string part) =>
        part.Length > 2 && part.StartsWith("\u0627\u0644", StringComparison.Ordinal)
            ? part[2..]
            : part;
}
