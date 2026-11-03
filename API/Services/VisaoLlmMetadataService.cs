using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace API.Services;

/// <summary>
/// Lê título e autor direto da imagem de cada lombada, com um LLM de visão local (Ollama, GGUF + mmproj).
///
/// Cada lombada é lida duas vezes: (1) o recorte na vertical e (2) o recorte girado nos dois sentidos.
/// Só é aceito o que as duas leituras confirmam. Nos testes, quando a lombada está ilegível o modelo
/// inventa um livro famoso (Harry Potter, 1984...), mas quase nunca o mesmo nas duas leituras.
/// </summary>
public class VisaoLlmMetadataService(HttpClient http, IOptions<LlmOptions> opcoes, ILogger<VisaoLlmMetadataService> logger) : IBookMetadataService
{
    /// <summary>Similaridade mínima (Dice sobre bigramas) entre as duas leituras para aceitar.</summary>
    private const double ConcordanciaMinima = 0.6;

    private const string Regras =
        "Use só o que estiver escrito e visível na imagem. Não complete com seu conhecimento e não traduza. " +
        "Se não der para ler o título ou o autor, use null. Ignore editora e logotipos.";

    private const string PromptVertical =
        "Esta imagem é a lombada de um livro; o texto está na vertical. Leia o título e o autor. " + Regras;

    private const string PromptGirado =
        "As duas imagens são a mesma lombada de livro, girada nos dois sentidos; em uma delas o texto fica na posição normal. " +
        "Leia o título e o autor. " + Regras;

    private static readonly JsonNode Esquema = JsonNode.Parse("""
        {
          "type": "object",
          "properties": {
            "titulo": { "type": ["string", "null"] },
            "autor": { "type": ["string", "null"] }
          },
          "required": ["titulo", "autor"]
        }
        """)!;

    private static readonly JsonSerializerOptions JsonSaida = new(JsonSerializerDefaults.Web);

    public string Fonte => $"VisaoLlm:{opcoes.Value.ModeloVisao}";

    public bool PrecisaDeRecortes => true;

    public async Task<IReadOnlyList<MetadadosLivro?>> BuscarAsync(IReadOnlyList<LombadaLida> lombadas, CancellationToken ct = default)
    {
        var resultado = lombadas.Select(_ => (MetadadosLivro?)null).ToList();

        for (var i = 0; i < lombadas.Count; i++)
        {
            if (lombadas[i].Recortes is not { } recortes)
                continue;

            try
            {
                var vertical = await LerAsync(PromptVertical, [recortes.Vertical], ct);
                var girado = await LerAsync(PromptGirado, recortes.Girados, ct);
                resultado[i] = Combinar(vertical, girado, lombadas[i].Posicao);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                logger.LogWarning(ex, "Resposta inválida do LLM de visão para a lombada {Posicao}", lombadas[i].Posicao);
            }
            catch (Exception ex) when (ex is HttpRequestException
                                           || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                // Ollama fora do ar ou travado: não adianta tentar as outras lombadas.
                logger.LogError(ex, "Falha ao consultar o LLM de visão ({Modelo} em {Url})", opcoes.Value.ModeloVisao, http.BaseAddress);
                break;
            }
        }

        return resultado;
    }

    private MetadadosLivro? Combinar(Leitura a, Leitura b, int posicao)
    {
        // O modelo às vezes põe o título no campo do autor numa das leituras (ex.: "MISERY").
        var titulo = Concordar(a.Titulo, b.Titulo) ?? Concordar(a.Autor, b.Titulo) ?? Concordar(a.Titulo, b.Autor);
        var autor = Concordar(a.Autor, b.Autor);
        if (titulo is not null && autor is not null && TextoUtil.Normalizar(titulo) == TextoUtil.Normalizar(autor))
            autor = null;

        if (titulo is null && autor is null)
        {
            if ((Limpar(a.Titulo) ?? Limpar(a.Autor) ?? Limpar(b.Titulo) ?? Limpar(b.Autor)) is not null)
                logger.LogInformation("Lombada {Posicao} descartada, leituras não concordam: '{T1} / {A1}' x '{T2} / {A2}'",
                    posicao, a.Titulo, a.Autor, b.Titulo, b.Autor);
            return null;
        }

        return new MetadadosLivro(
            titulo is null ? null : TextoUtil.CapitalizarSeMaiusculas(titulo),
            autor is null ? null : TextoUtil.CapitalizarSeMaiusculas(autor),
            null, null);
    }

    /// <summary>Devolve a leitura mais completa (mais longa) se as duas forem parecidas; senão null.</summary>
    private static string? Concordar(string? a, string? b)
    {
        a = Limpar(a);
        b = Limpar(b);
        if (a is null || b is null)
            return null;

        var na = TextoUtil.Normalizar(a);
        var nb = TextoUtil.Normalizar(b);
        var parecidas = na.Length > 0 && (na == nb || TextoUtil.Similaridade(na, nb) >= ConcordanciaMinima);
        return parecidas ? (a.Length >= b.Length ? a : b) : null;
    }

    /// <summary>O modelo às vezes escreve a palavra "null" em vez do valor null.</summary>
    private static string? Limpar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;
        valor = valor.Trim();
        return valor.Equals("null", StringComparison.OrdinalIgnoreCase) || valor.Equals("none", StringComparison.OrdinalIgnoreCase)
            ? null
            : valor;
    }

    private async Task<Leitura> LerAsync(string prompt, IReadOnlyList<string> imagens, CancellationToken ct)
    {
        var corpo = new
        {
            model = opcoes.Value.ModeloVisao,
            stream = false,
            think = false,
            format = Esquema,
            options = new { temperature = 0, num_predict = 200 },
            messages = new object[]
            {
                new { role = "user", content = prompt, images = imagens }
            }
        };

        using var respostaHttp = await http.PostAsJsonAsync("api/chat", corpo, ct);
        if (!respostaHttp.IsSuccessStatusCode)
        {
            var erro = await respostaHttp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Ollama respondeu {(int)respostaHttp.StatusCode}: {erro}");
        }

        var chat = await respostaHttp.Content.ReadFromJsonAsync<RespostaChat>(JsonSaida, ct);
        var conteudo = chat?.Message?.Content ?? throw new InvalidOperationException("Resposta do Ollama sem conteúdo.");
        return JsonSerializer.Deserialize<Leitura>(conteudo, JsonSaida)
               ?? throw new InvalidOperationException($"Resposta do LLM não é JSON: {conteudo}");
    }

    private record RespostaChat(MensagemChat? Message);
    private record MensagemChat(string? Content);
    private record Leitura(string? Titulo, string? Autor);
}
