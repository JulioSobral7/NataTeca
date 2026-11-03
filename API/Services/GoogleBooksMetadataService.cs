using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace API.Services;

public class GoogleBooksMetadataService(HttpClient httpClient, IConfiguration config, ILogger<GoogleBooksMetadataService> logger) : IBookMetadataService
{
    private const int MaxPalavrasNaQuery = 8;
    private const int MaxCandidatos = 10;

    /// <summary>Similaridade mínima (Dice sobre bigramas) para aceitar um candidato.</summary>
    private const double SimilaridadeMinima = 0.35;

    public string Fonte => "GoogleBooks";

    public async Task<IReadOnlyList<MetadadosLivro?>> BuscarAsync(IReadOnlyList<LombadaLida> lombadas, CancellationToken ct = default)
    {
        var resultado = new List<MetadadosLivro?>(lombadas.Count);
        foreach (var lombada in lombadas)
            resultado.Add(await BuscarPorTextoAsync(lombada.Texto, ct));
        return resultado;
    }

    private async Task<MetadadosLivro?> BuscarPorTextoAsync(string textoOcr, CancellationToken ct)
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
            var resposta = await httpClient.GetFromJsonAsync<GoogleBooksResponse>(url, ct);

            var textoNormalizado = TextoUtil.Normalizar(textoOcr);

            // Escolhe o candidato mais parecido com o texto lido (corrige ruído do OCR),
            // em vez de confiar cegamente no primeiro resultado.
            var melhor = (resposta?.Items ?? [])
                .Select(i => i.VolumeInfo)
                .Where(v => v is not null && !string.IsNullOrWhiteSpace(v.Title))
                .Select(v => (Info: v!, Score: TextoUtil.Similaridade(textoNormalizado, TextoUtil.Normalizar($"{v!.Title} {string.Join(' ', v.Authors ?? [])}"))))
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            if (melhor.Info is null || melhor.Score < SimilaridadeMinima)
                return null;

            var info = melhor.Info;
            var autor = info.Authors is { Length: > 0 } ? string.Join(", ", info.Authors) : "Desconhecido";
            int? ano = info.PublishedDate is { Length: >= 4 } data && int.TryParse(data[..4], out var a) ? a : null;
            return new MetadadosLivro(info.Title!, autor, ano, info.ImageLinks?.Thumbnail);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Falha ao consultar Google Books para o texto '{Texto}'", query);
            return null;
        }
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

        [JsonPropertyName("publishedDate")]
        public string? PublishedDate { get; set; }

        [JsonPropertyName("imageLinks")]
        public GoogleBooksImageLinks? ImageLinks { get; set; }
    }

    private class GoogleBooksImageLinks
    {
        [JsonPropertyName("thumbnail")]
        public string? Thumbnail { get; set; }
    }
}
