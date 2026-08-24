using System.Text.Json;

namespace MediaVault.LinkHub.Application.Models.Scraping;

/// <summary>
/// Serialización y validación compartida de <see cref="VideoScrapeHints"/>.
/// </summary>
public static class VideoScrapeHintsJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
    /// Normaliza JSON de hints. Si <paramref name="requireSelectors"/> es true, exige ListItem o Title.
    /// </summary>
    public static string? Normalize(string? scrapeHintsJson, bool requireSelectors, string paramName = "scrapeHintsJson")
    {
        if (string.IsNullOrWhiteSpace(scrapeHintsJson))
        {
            if (requireSelectors)
            {
                throw new ArgumentException(
                    "Configure los selectores CSS de scraping (ListItemSelector o TitleSelector).",
                    paramName);
            }

            return null;
        }

        try
        {
            var hints = JsonSerializer.Deserialize<VideoScrapeHints>(scrapeHintsJson, Options)
                ?? throw new ArgumentException("ScrapeHintsJson inválido.", paramName);

            if (requireSelectors
                && string.IsNullOrWhiteSpace(hints.ListItemSelector)
                && string.IsNullOrWhiteSpace(hints.TitleSelector))
            {
                throw new ArgumentException(
                    "Indique al menos ListItemSelector o TitleSelector en los hints de scraping.",
                    paramName);
            }

            return JsonSerializer.Serialize(hints, Options);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("ScrapeHintsJson no es JSON válido.", paramName, ex);
        }
    }

    public static VideoScrapeHints DeserializeRequired(
        string? json,
        string missingMessage = "No hay ScrapeHintsJson configurado en el sitio (Link Manager).")
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException(missingMessage);

        try
        {
            return JsonSerializer.Deserialize<VideoScrapeHints>(json, Options)
                ?? throw new InvalidOperationException("ScrapeHintsJson inválido.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("ScrapeHintsJson no es JSON válido.", ex);
        }
    }
}
