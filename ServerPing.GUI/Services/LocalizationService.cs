using System.Collections.ObjectModel;
using System.Windows;
using ServerPing.Shared.Localization;

namespace ServerPing.GUI.Services;

public sealed class LanguageOption
{
    public required string Code { get; init; }
    public required string DisplayName { get; init; }
}

public static class LocalizationService
{
    public const string DefaultLanguage = SharedLocalization.SystemLanguage;

    private static readonly Dictionary<string, string> ResourcePaths = new()
    {
        [SharedLocalization.Chinese] = "Localization/Strings.zh-CN.xaml",
        [SharedLocalization.English] = "Localization/Strings.en-US.xaml",
        [SharedLocalization.Russian] = "Localization/Strings.ru-RU.xaml"
    };

    private static ResourceDictionary? _currentDictionary;

    public static ReadOnlyCollection<LanguageOption> Languages { get; } = new List<LanguageOption>
    {
        new() { Code = SharedLocalization.SystemLanguage, DisplayName = "System default" },
        new() { Code = SharedLocalization.Chinese, DisplayName = "中文" },
        new() { Code = SharedLocalization.English, DisplayName = "English" },
        new() { Code = SharedLocalization.Russian, DisplayName = "Русский" }
    }.AsReadOnly();

    public static string CurrentLanguage { get; private set; } = DefaultLanguage;

    public static void Apply(string? language)
    {
        var code = SharedLocalization.Resolve(language);
        var dictionary = new ResourceDictionary
        {
            Source = new Uri(ResourcePaths[code], UriKind.Relative)
        };

        var resources = Application.Current.Resources.MergedDictionaries;
        if (_currentDictionary != null)
            resources.Remove(_currentDictionary);

        resources.Add(dictionary);
        _currentDictionary = dictionary;
        CurrentLanguage = SharedLocalization.Normalize(language);
    }

    public static string Normalize(string? language) => SharedLocalization.Normalize(language);

    public static string Get(string key) =>
        Application.Current.TryFindResource(key) as string ?? key;

    public static string Format(string key, params object[] args) =>
        string.Format(Get(key), args);
}
