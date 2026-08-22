using System.Text.Json;

using CommunityToolkit.Mvvm.ComponentModel;

using MediaVault.LinkHub.Application.Models.Scraping;

namespace MediaVault.LinkHub.App.ViewModels;

/// <summary>
/// Formulario UI de <see cref="VideoScrapeHints"/>. Serializa a JSON al guardar.
/// </summary>
public partial class ScrapeHintsFormViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    [ObservableProperty]
    private string _listItemSelector = string.Empty;

    [ObservableProperty]
    private string _titleSelector = string.Empty;

    [ObservableProperty]
    private string _urlSelector = string.Empty;

    [ObservableProperty]
    private string _urlAttribute = "href";

    [ObservableProperty]
    private string _thumbnailSelector = string.Empty;

    [ObservableProperty]
    private string _thumbnailAttribute = "src";

    [ObservableProperty]
    private string _previewSelector = string.Empty;

    [ObservableProperty]
    private string _previewAttribute = string.Empty;

    [ObservableProperty]
    private string _previewHoverSelector = string.Empty;

    [ObservableProperty]
    private int _previewHoverWaitMs = 900;

    [ObservableProperty]
    private string _codeSelector = string.Empty;

    [ObservableProperty]
    private string _dateSelector = string.Empty;

    [ObservableProperty]
    private string _dateFormat = string.Empty;

    [ObservableProperty]
    private string _durationSelector = string.Empty;

    [ObservableProperty]
    private string _nextPageSelector = string.Empty;

    [ObservableProperty]
    private int _maxPages = 1;

    [ObservableProperty]
    private int? _waitMs;

    [ObservableProperty]
    private string _userAgent = string.Empty;

    [ObservableProperty]
    private string _baseUrl = string.Empty;

    /// <summary>
    /// Cabeceras extra en texto plano: una por línea <c>Nombre: Valor</c>.
    /// </summary>
    [ObservableProperty]
    private string _extraHeadersText = string.Empty;

    public void LoadTemplate() => LoadFrom(VideoScrapeHints.CreateListTemplate());

    public void LoadFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            LoadTemplate();
            return;
        }

        try
        {
            var hints = JsonSerializer.Deserialize<VideoScrapeHints>(json, JsonOptions);
            if (hints is null)
            {
                LoadTemplate();
                return;
            }

            LoadFrom(hints);
        }
        catch (JsonException)
        {
            LoadTemplate();
        }
    }

    public void LoadFrom(VideoScrapeHints hints)
    {
        ListItemSelector = hints.ListItemSelector ?? string.Empty;
        TitleSelector = hints.TitleSelector ?? string.Empty;
        UrlSelector = hints.UrlSelector ?? string.Empty;
        UrlAttribute = string.IsNullOrWhiteSpace(hints.UrlAttribute) ? "href" : hints.UrlAttribute;
        ThumbnailSelector = hints.ThumbnailSelector ?? string.Empty;
        ThumbnailAttribute = string.IsNullOrWhiteSpace(hints.ThumbnailAttribute) ? "src" : hints.ThumbnailAttribute;
        PreviewSelector = hints.PreviewSelector ?? string.Empty;
        PreviewAttribute = hints.PreviewAttribute ?? string.Empty;
        PreviewHoverSelector = hints.PreviewHoverSelector ?? string.Empty;
        PreviewHoverWaitMs = hints.PreviewHoverWaitMs <= 0 ? 900 : hints.PreviewHoverWaitMs;
        CodeSelector = hints.CodeSelector ?? string.Empty;
        DateSelector = hints.DateSelector ?? string.Empty;
        DateFormat = hints.DateFormat ?? string.Empty;
        DurationSelector = hints.DurationSelector ?? string.Empty;
        NextPageSelector = hints.NextPageSelector ?? string.Empty;
        MaxPages = hints.MaxPages <= 0 ? 1 : hints.MaxPages;
        WaitMs = hints.WaitMs;
        UserAgent = hints.UserAgent ?? string.Empty;
        BaseUrl = hints.BaseUrl ?? string.Empty;
        ExtraHeadersText = FormatExtraHeaders(hints.ExtraHeaders);
    }

    public string ToJson()
    {
        var hints = ToModel();
        return JsonSerializer.Serialize(hints, JsonOptions);
    }

    public VideoScrapeHints ToModel() =>
        new()
        {
            ListItemSelector = NullIfWhiteSpace(ListItemSelector),
            TitleSelector = NullIfWhiteSpace(TitleSelector),
            UrlSelector = NullIfWhiteSpace(UrlSelector),
            UrlAttribute = string.IsNullOrWhiteSpace(UrlAttribute) ? "href" : UrlAttribute.Trim(),
            ThumbnailSelector = NullIfWhiteSpace(ThumbnailSelector),
            ThumbnailAttribute = string.IsNullOrWhiteSpace(ThumbnailAttribute) ? "src" : ThumbnailAttribute.Trim(),
            PreviewSelector = NullIfWhiteSpace(PreviewSelector),
            PreviewAttribute = NullIfWhiteSpace(PreviewAttribute),
            PreviewHoverSelector = NullIfWhiteSpace(PreviewHoverSelector),
            PreviewHoverWaitMs = PreviewHoverWaitMs <= 0 ? 900 : PreviewHoverWaitMs,
            CodeSelector = NullIfWhiteSpace(CodeSelector),
            DateSelector = NullIfWhiteSpace(DateSelector),
            DateFormat = NullIfWhiteSpace(DateFormat),
            DurationSelector = NullIfWhiteSpace(DurationSelector),
            NextPageSelector = NullIfWhiteSpace(NextPageSelector),
            MaxPages = MaxPages <= 0 ? 1 : MaxPages,
            WaitMs = WaitMs is > 0 ? WaitMs : null,
            UserAgent = NullIfWhiteSpace(UserAgent),
            BaseUrl = NullIfWhiteSpace(BaseUrl),
            ExtraHeaders = ParseExtraHeaders(ExtraHeadersText)
        };

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatExtraHeaders(Dictionary<string, string>? headers)
    {
        if (headers is null || headers.Count == 0)
            return string.Empty;

        return string.Join(
            Environment.NewLine,
            headers.Select(pair => $"{pair.Key}: {pair.Value}"));
    }

    private static Dictionary<string, string>? ParseExtraHeaders(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = rawLine.IndexOf(':');
            if (separator <= 0)
                continue;

            var name = rawLine[..separator].Trim();
            var value = rawLine[(separator + 1)..].Trim();
            if (name.Length == 0)
                continue;

            map[name] = value;
        }

        return map.Count == 0 ? null : map;
    }
}
