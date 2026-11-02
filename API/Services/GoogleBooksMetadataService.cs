using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace API.Services;

public class GoogleBooksMetadataService(HttpClient httpClient, IConfiguration config, ILogger<GoogleBooksMetadataService> logger) : IBookMetadataService
{
    private const int MaxPalavrasNaQuery = 8;
    private const int MaxCandidatos = 10;

    /// <summary>Similaridade mínima (Dice sobre bigramas) para aceitar um candidato.</summary>
    private const double SimilaridadeMinima = 0.35;

    public async Task<MetadadosLivro?> BuscarPorTextoAsync(string textoOcr)
    {
        if (string.IsNullOrWhiteSpace(textoOcr))
            return null;

        var query = string.Join(' ', textoOcr.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(MaxPalavrasNaQuery));
        if (query.Length == 0)
            return null;

        try
        {
            var url = $"https://www.googleapis.com/books/v1/volumes?q={Uri.EscapeDataString(query)}&maxResults={MaxCandidatos}";
            var chave = config["GoogleBooks:ApiKey"];
            if (!string.IsNullOrWhiteSpace(chave))
                url += $"&key={Uri.EscapeDataString(chave)}";
            var resposta = await httpClient.GetFromJsonAsync<GoogleBooksResponse>(url);

            var textoNormalizado = Normalizar(textoOcr);

            // Escolhe o candidato mais parecido com o texto lido (corrige ruído do OCR),
            // em vez de confiar cegamente no primeiro resultado.
            var melhor = (resposta?.Items ?? [])
                .Select(i => i.VolumeInfo)
                .Where(v => v is not null && !string.IsNullOrWhiteSpace(v.Title))
                .Select(v => (Info: v!, Score: Similaridade(textoNormalizado, Normalizar($"{v!.Title} {string.Join(' ', v.Authors ?? [])}"))))
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            if (melhor.Info is null || melhor.Score < SimilaridadeMinima)
                return null;

            var info = melhor.Info;
            var autor = info.Authors is { Length: > 0 } ? string.Join(", ", info.Authors) : "Desconhecido";
            return new MetadadosLivro(info.Title!, autor, info.ImageLinks?.Thumbnail);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao consultar Google Books para o texto '{Texto}'", query);
            return null;
        }
    }

    /// <summary>Minúsculas, sem acentos e sem espaços/pontuação (tolera palavras coladas pelo OCR).</summary>
    internal static string Normalizar(string texto)
    {
        var sb = new StringBuilder();
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    /// <summary>Coeficiente de Dice sobre bigramas de caracteres (0 a 1).</summary>
    internal static double Similaridade(string a, string b)
    {
        if (a.Length < 2 || b.Length < 2) return 0;

        var bigramasB = new Dictionary<string, int>();
        for (var i = 0; i < b.Length - 1; i++)
        {
            var bg = b.Substring(i, 2);
            bigramasB[bg] = bigramasB.GetValueOrDefault(bg) + 1;
        }

        var comuns = 0;
        for (var i = 0; i < a.Length - 1; i++)
        {
            var bg = a.Substring(i, 2);
            if (bigramasB.TryGetValue(bg, out var n) && n > 0)
            {
                comuns++;
                bigramasB[bg] = n - 1;
            }
        }

        return 2.0 * comuns / ((a.Length - 1) + (b.Length - 1));
    }

    private class GoogleBooksResponse
    {
        [JsonPropertyName("items")]
        public GoogleBooksItem[]? Items { get; set; }
    }

    private class GoogleBooksItem
    {
        [JsonPropertyName("volumeInfo")]
        public GoogleBooksVolumeInfo? VolumeInfo { get; set; }
    }

    private class GoogleBooksVolumeInfo
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("authors")]
        public string[]? Authors { get; set; }

        [JsonPropertyName("imageLinks")]
        public GoogleBooksImageLinks? ImageLinks { get; set; }
    }

    private class GoogleBooksImageLinks
    {
        [JsonPropertyName("thumbnail")]
        public string? Thumbnail { get; set; }
    }
}
