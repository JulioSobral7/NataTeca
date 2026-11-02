using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace API.Services;

public class PythonVisaoService(HttpClient httpClient, ILogger<PythonVisaoService> logger) : IVisaoService
{
    public async Task<IReadOnlyList<LombadaLida>> ReconhecerAsync(Stream foto, string nomeArquivo, CancellationToken ct = default)
    {
        using var conteudo = new MultipartFormDataContent();
        conteudo.Add(new StreamContent(foto), "foto", nomeArquivo);

        try
        {
            using var resposta = await httpClient.PostAsync("reconhecer", conteudo, ct);
            if (!resposta.IsSuccessStatusCode)
            {
                var corpo = await resposta.Content.ReadAsStringAsync(ct);
                throw new VisaoIndisponivelException($"Serviço de visão respondeu {(int)resposta.StatusCode}: {corpo}");
            }

            var itens = await resposta.Content.ReadFromJsonAsync<List<ItemResposta>>(ct) ?? [];
            return itens.Select(i => new LombadaLida(i.Posicao, i.Texto, i.Confianca)).ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Serviço de visão indisponível");
            throw new VisaoIndisponivelException("Serviço de visão (Python) indisponível.", ex);
        }
    }

    private class ItemResposta
    {
        [JsonPropertyName("posicao")] public int Posicao { get; set; }
        [JsonPropertyName("texto")] public string Texto { get; set; } = string.Empty;
        [JsonPropertyName("confianca")] public int Confianca { get; set; }
    }
}
