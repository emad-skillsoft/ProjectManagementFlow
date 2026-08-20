using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace ProjectManagmentFlow.Localization;

/// <summary>
/// مُوطِّن يقرأ الترجمات من ملفات JSON Resources/{culture}.json
/// </summary>
public sealed class JsonStringLocalizerFactory(IHostEnvironment environment) : IStringLocalizerFactory
{
    private readonly JsonTranslationStore store = new(
        Path.Combine(environment.ContentRootPath, "Resources"));

    public IStringLocalizer Create(Type resourceSource) => new JsonStringLocalizer(store);

    public IStringLocalizer Create(string baseName, string location) => new JsonStringLocalizer(store);
}

public sealed class JsonStringLocalizer(JsonTranslationStore store) : IStringLocalizer
{
    public LocalizedString this[string name]
    {
        get
        {
            var value = store.Find(name, CultureInfo.CurrentUICulture);
            return new LocalizedString(name, value ?? name, resourceNotFound: value is null);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var value = store.Find(name, CultureInfo.CurrentUICulture);
            if (value is null) return new LocalizedString(name, name, resourceNotFound: true);

            var formatted = string.Format(CultureInfo.CurrentCulture, value, arguments);
            return new LocalizedString(name, formatted, resourceNotFound: false);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        store.All(CultureInfo.CurrentUICulture)
             .Select(pair => new LocalizedString(pair.Key, pair.Value, resourceNotFound: false));
}

public sealed class JsonTranslationStore(string resourcesPath)
{
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> cache = new();

    public string? Find(string key, CultureInfo culture)
    {
        // ar-SA ← ar ← en (الاحتياط الأخير حتى لا يظهر المفتاح للمستخدم)
        foreach (var candidate in Candidates(culture))
            if (Load(candidate).TryGetValue(key, out var value))
                return value;

        return null;
    }

    public IReadOnlyDictionary<string, string> All(CultureInfo culture)
    {
        // تُدمج سلسلة الاحتياط نفسها التي تسلكها Find: الأخصّ يغلب.
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var candidate in Candidates(culture))
            foreach (var (key, value) in Load(candidate))
                merged.TryAdd(key, value);

        return merged;
    }

    private static IEnumerable<string> Candidates(CultureInfo culture)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var c = culture; c is not null && !string.IsNullOrEmpty(c.Name); c = c.Parent)
            if (seen.Add(c.Name))
                yield return c.Name;

        if (seen.Add("en")) yield return "en";
    }

    private IReadOnlyDictionary<string, string> Load(string culture) =>
        cache.GetOrAdd(culture, name =>
        {
            var file = Path.Combine(resourcesPath, $"{name}.json");
            if (!File.Exists(file))
                return new Dictionary<string, string>(StringComparer.Ordinal);

            using var stream = File.OpenRead(file);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
            return parsed ?? new Dictionary<string, string>(StringComparer.Ordinal);
        });
}
