using System.Globalization;
using Microsoft.Extensions.Localization;

namespace ProjectManagmentFlow;

public static class DisplayTime
{
    private static readonly TimeZoneInfo Riyadh = ResolveRiyadh();

    public static string Local(this DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Riyadh)
                    .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    public static string Local(this DateOnly date)
    {
        var culture = (CultureInfo)CultureInfo.CurrentUICulture.Clone();
        culture.DateTimeFormat.Calendar = new GregorianCalendar();
        return date.ToDateTime(TimeOnly.MinValue).ToString("d MMMM yyyy", culture);
    }

    /// <summary>صيغة حقول HTML الزمنية، دائماً بالتقويم الميلادي والأرقام اللاتينية.</summary>
    public static string ToIso(this DateOnly? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>«الآن» بتوقيت الرياض لا UTC: ليلة رأس السنة تفترق بينهما ثلاث ساعات.</summary>
    public static DateTime RiyadhNow() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Riyadh);

    public static string Relative(this DateTime utc, IStringLocalizer text)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc), Riyadh);
        var elapsed = RiyadhNow() - local;

        if (elapsed < TimeSpan.FromMinutes(1)) return text["Time_Now"];
        if (elapsed < TimeSpan.FromHours(1))
            return text["Time_MinutesAgo", Math.Max(1, (int)elapsed.TotalMinutes)];
        if (elapsed < TimeSpan.FromHours(24))
            return text["Time_HoursAgo", Math.Max(1, (int)elapsed.TotalHours)];
        if (elapsed < TimeSpan.FromHours(48))
            return text["Time_YesterdayAt", local.ToString("t", CultureInfo.CurrentCulture)];

        return DateOnly.FromDateTime(local).Local();
    }

    /// <summary>صياغة محايدة للمدة المتبقية حتى تاريخ الاستحقاق بتوقيت الرياض.</summary>
    public static string Remaining(this DateOnly due, IStringLocalizer text)
    {
        var remaining = due.DayNumber - DateOnly.FromDateTime(RiyadhNow()).DayNumber;
        return text["Time_Remaining", Math.Max(0, remaining)];
    }

    private static TimeZoneInfo ResolveRiyadh()
    {
        foreach (var id in new[] { "Asia/Riyadh", "Arab Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.CreateCustomTimeZone("Riyadh", TimeSpan.FromHours(3), "Riyadh", "Riyadh");
    }
}
