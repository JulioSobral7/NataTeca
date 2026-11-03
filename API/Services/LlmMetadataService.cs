using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace API.Services;

public class LlmOptions
{
    /// <summary>Endereço do Ollama.</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434/";

    /// <summary>Nome do modelo no Ollama (o criado com <c>ollama create</c> ou baixado com <c>ollama pull</c>).</summary>
    public string Modelo { get; set; } = "natateca-llm";

    /// <summary>Modelo com visão (GGUF + mmproj) usado pela fonte "VisaoLlm".</summary>
    public string ModeloVisao { get; set; } = "natateca-vl";

    /// <summary>A primeira chamada carrega o modelo na GPU e pode demorar.</summary>
    public int TimeoutSegundos { get; set; } = 180;

    /// <summary>
    /// Lombadas por chamada. Lotes grandes pioram um modelo pequeno: quando a maioria é lixo de OCR
    /// ele devolve null para tudo, e às vezes entra em repetição. 5 foi o melhor nos testes.
    /// </summary>
    public int TamanhoLote { get; set; } = 5;
}

/// <summary>
/// Separa título, autor e ano do texto OCR das lombadas usando um LLM local servido pelo Ollama.
/// As lombadas vão em lotes pequenos (com os vizinhos como contexto), com a saída presa a um JSON Schema.
/// O que o modelo devolve é conferido contra o texto lido, para descartar dados inventados.
/// </summary>
public class LlmMetadataService(HttpClient http, IOptions<LlmOptions> opcoes, ILogger<LlmMetadataService> logger) : IBookMetadataService
{
    /// <summary>Fração mínima dos bigramas do título/autor que precisa estar no texto OCR.</summary>
    private const double ContencaoMinima = 0.5;

    private const string PromptSistema = """
        Você recebe o texto lido por OCR das lombadas de livros de uma prateleira, em ordem.
        Para cada lombada, separe o título, o autor e o ano de publicação.
        Avalie cada lombada sozinha: na mesma prateleira, algumas são legíveis e outras não.

        Regras:
        - Use apenas o que está escrito no texto daquela lombada. Não complete com seu conhecimento nem com as lombadas vizinhas: não invente título, autor nem ano.
        - Muitos textos são lixo de OCR (letras soltas, sílabas sem sentido, texto espelhado). Nesses casos use null. Nunca copie texto sem sentido para o título ou o autor.
        - Mantenha o idioma do texto: não traduza títulos.
        - Corrija só erros evidentes de OCR (0 no lugar de O, uma letra trocada, palavras coladas ou partidas).
        - Escreva títulos e nomes com capitalização normal, mesmo que o texto esteja todo em maiúsculas.
        - Ignore nome de editora, coleção, logotipos e códigos.
        - Preencha o ano só se ele estiver escrito no texto (4 dígitos). Caso contrário, use null.
        - Devolva exatamente uma entrada por lombada recebida, com a mesma "posicao".

        Exemplos:
        "DOM CASMURR0 MACHAD0 DE ASSIS ATICA" → título "Dom Casmurro", autor "Machado de Assis", ano null
        "HARRYPOTTER E A PEDRA FILOSOFAL J.K.ROWLING ROCCO" → título "Harry Potter e a Pedra Filosofal", autor "J.K. Rowling", ano null
        "O CORTICO Aluisio Azevedo 1890" → título "O Cortiço", autor "Aluísio Azevedo", ano 1890
        "CLARICE LISPECTOR" → título null, autor "Clarice Lispector", ano null
        "rq Te sanM -lo Ba oe" → título null, autor null, ano null
        "oinavaL odarG adiV" → título null, autor null, ano null
        "TR K" → título null, autor null, ano null
        """;

    private static readonly JsonNode Esquema = JsonNode.Parse("""
        {
          "type": "object",
          "properties": {
            "livros": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "posicao": { "type": "integer" },
                  "titulo": { "type": ["string", "null"] },
                  "autor": { "type": ["string", "null"] },
                  "ano": { "type": ["integer", "null"] }
                },
                "required": ["posicao", "titulo", "autor", "ano"]
              }
            }
          },
          "required": ["livros"]
        }
        """)!;

    private static readonly JsonSerializerOptions JsonEntrada = new()
    {
        // Mantém acentos legíveis para o modelo (sem ã).
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions JsonSaida = new(JsonSerializerDefaults.Web);

    public string Fonte => $"Llm:{opcoes.Value.Modelo}";

    public async Task<IReadOnlyList<MetadadosLivro?>> BuscarAsync(IReadOnlyList<LombadaLida> lombadas, CancellationToken ct = default)
    {
        var entradas = lombadas
            .Where(l => !string.IsNullOrWhiteSpace(l.Texto))
            .Select(l => new { posicao = l.Posicao, texto = l.Texto })
            .ToList();

        var porPosicao = new Dictionary<int, LivroLlm>();
        foreach (var lote in entradas.Chunk(Math.Max(1, opcoes.Value.TamanhoLote)))
        {
            try
            {
                var resposta = await ChamarModeloAsync(FormatarEntrada(lote.Select(e => (e.posicao, e.texto))), lote.Length, ct);
                var posicoesDoLote = lote.Select(e => e.posicao).ToHashSet();
                foreach (var item in resposta?.Livros ?? [])
                {
                    // Ignora posições que não estavam no lote (o modelo pode inventar).
                    if (posicoesDoLote.Contains(item.Posicao))
                        porPosicao.TryAdd(item.Posicao, item);
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                // Resposta ruim (ex.: JSON cortado por repetição): perde só este lote.
                logger.LogWarning(ex, "Resposta inválida do LLM para as lombadas {Posicoes}",
                    string.Join(",", lote.Select(e => e.posicao)));
            }
            catch (Exception ex) when (ex is HttpRequestException
                                           || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                // Ollama fora do ar ou travado: não adianta tentar os outros lotes.
                logger.LogError(ex, "Falha ao consultar o LLM ({Modelo} em {Url})", opcoes.Value.Modelo, http.BaseAddress);
                break;
            }
        }

        return lombadas
            .Select(l => porPosicao.TryGetValue(l.Posicao, out var item) ? Validar(item, l.Texto) : null)
            .ToList();
    }

    /// <summary>
    /// JSON com espaço depois de ':' e ',', como <c>[{"posicao": 0, "texto": "..."}, ...]</c>.
    /// Não use o JSON compacto do serializador: nos testes, com ele o modelo devolveu null para tudo.
    /// </summary>
    private static string FormatarEntrada(IEnumerable<(int Posicao, string Texto)> lote) =>
        "[" + string.Join(", ", lote.Select(e =>
            $"{{\"posicao\": {e.Posicao}, \"texto\": {JsonSerializer.Serialize(e.Texto, JsonEntrada)}}}")) + "]";

    private async Task<RespostaLlm?> ChamarModeloAsync(string entradasJson, int quantidade, CancellationToken ct)
    {
        var corpo = new
        {
            model = opcoes.Value.Modelo,
            stream = false,
            think = false,
            format = Esquema,
            // num_predict corta respostas que entram em repetição (~30 tokens por lombada bastam).
            options = new { temperature = 0, num_predict = 60 + 60 * quantidade },
            messages = new object[]
            {
                new { role = "system", content = PromptSistema },
                new { role = "user", content = entradasJson }
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

        // Por segurança, pega só o objeto JSON (caso algum texto venha antes ou depois).
        var inicio = conteudo.IndexOf('{');
        var fim = conteudo.LastIndexOf('}');
        if (inicio < 0 || fim <= inicio)
            throw new InvalidOperationException($"Resposta do LLM não é JSON: {conteudo}");

        return JsonSerializer.Deserialize<RespostaLlm>(conteudo[inicio..(fim + 1)], JsonSaida);
    }

    /// <summary>Descarta o que o modelo não tirou do texto lido.</summary>
    private MetadadosLivro? Validar(LivroLlm item, string textoOcr)
    {
        var textoNormalizado = TextoUtil.Normalizar(textoOcr);

        var titulo = AceitarSeEstaNoTexto(item.Titulo, textoNormalizado, "título", textoOcr);
        var autor = AceitarSeEstaNoTexto(item.Autor, textoNormalizado, "autor", textoOcr);

        int? ano = null;
        if (item.Ano is { } a)
        {
            if (a >= 1450 && a <= DateTime.UtcNow.Year + 1 && textoOcr.Contains(a.ToString()))
                ano = a;
            else
                logger.LogInformation("Ano {Ano} descartado: não aparece no texto '{Texto}'", a, textoOcr);
        }

        return titulo is null && autor is null && ano is null ? null : new MetadadosLivro(titulo, autor, ano, null);
    }

    private string? AceitarSeEstaNoTexto(string? valor, string textoNormalizado, string campo, string textoOcr)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        valor = valor.Trim();
        var contencao = TextoUtil.Contencao(TextoUtil.Normalizar(valor), textoNormalizado);
        if (contencao >= ContencaoMinima)
            return valor;

        logger.LogInformation("{Campo} '{Valor}' descartado (contenção {Contencao:F2}): não saiu do texto '{Texto}'",
            campo, valor, contencao, textoOcr);
        return null;
    }

    private record RespostaChat(MensagemChat? Message);
    private record MensagemChat(string? Content);
    private record RespostaLlm(List<LivroLlm>? Livros);
    private record LivroLlm(int Posicao, string? Titulo, string? Autor, int? Ano);
}
