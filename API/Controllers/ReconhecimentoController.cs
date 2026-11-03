using System.Text.Json;
using API.Data;
using API.Models;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReconhecimentoController(
    AppDbContext db,
    IVisaoService visao,
    IBookMetadataService metadados,
    ArquivoReconhecimentoService arquivos,
    LivroService livros,
    ILogger<ReconhecimentoController> logger) : ControllerBase
{
    private const long TamanhoMaximoBytes = 15 * 1024 * 1024;

    /// <summary>Nome do arquivo JSON gerado para a foto (também listado em GET arquivos).</summary>
    public const string CabecalhoArquivo = "X-Arquivo-Reconhecimento";

    [HttpPost("foto")]
    [RequestSizeLimit(TamanhoMaximoBytes)]
    public async Task<ActionResult<List<LivroSugerido>>> ReconhecerFoto([FromForm] ReconhecimentoFotoRequest request, CancellationToken ct)
    {
        var estante = await db.Estantes.FindAsync([request.EstanteId], ct);
        if (estante is null)
            return BadRequest("Estante informada não existe.");

        if (request.Prateleira >= estante.QuantidadePrateleiras)
            return BadRequest($"A estante '{estante.Nome}' só possui {estante.QuantidadePrateleiras} prateleira(s).");

        if (request.Foto.Length == 0)
            return BadRequest("Arquivo de foto vazio.");

        IReadOnlyList<LombadaLida> lidas;
        try
        {
            await using var stream = request.Foto.OpenReadStream();
            lidas = await visao.ReconhecerAsync(stream, request.Foto.FileName, metadados.PrecisaDeRecortes, ct);
        }
        catch (VisaoIndisponivelException ex)
        {
            logger.LogError(ex, "Serviço de visão indisponível");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "Serviço de visão indisponível. " + ex.Message);
        }

        var metas = await metadados.BuscarAsync(lidas, ct);

        var sugestoes = lidas.Select((lida, i) => new LivroSugerido
        {
            Posicao = lida.Posicao,
            TituloSugerido = metas[i]?.Titulo,
            AutorSugerido = metas[i]?.Autor,
            AnoSugerido = metas[i]?.Ano,
            CapaUrlSugerida = metas[i]?.CapaUrl,
            TextoOcrBruto = lida.Texto,
            Confianca = lida.Confianca
        }).ToList();

        await SalvarArquivoAsync(request, sugestoes, ct);

        return Ok(sugestoes);
    }

    [HttpGet("arquivos")]
    public ActionResult<IEnumerable<string>> ListarArquivos() => Ok(arquivos.Listar());

    [HttpGet("arquivos/{nome}")]
    public async Task<ActionResult<ArquivoReconhecimento>> LerArquivo(string nome, CancellationToken ct)
    {
        try
        {
            var arquivo = await arquivos.LerAsync(nome, ct);
            return arquivo is null ? NotFound() : Ok(arquivo);
        }
        catch (JsonException ex)
        {
            return BadRequest($"Arquivo inválido: {ex.Message}");
        }
    }

    /// <summary>
    /// Cadastra no banco os livros de um arquivo de reconhecimento. Itens sem título são ignorados
    /// (preencha o título no arquivo para incluí-los). Sem <paramref name="posicaoInicial"/>,
    /// os livros entram depois do último livro da prateleira.
    /// </summary>
    [HttpPost("arquivos/{nome}/importar")]
    public async Task<ActionResult<IEnumerable<Livro>>> ImportarArquivo(string nome, [FromQuery] int? posicaoInicial, CancellationToken ct)
    {
        ArquivoReconhecimento? arquivo;
        try
        {
            arquivo = await arquivos.LerAsync(nome, ct);
        }
        catch (JsonException ex)
        {
            return BadRequest($"Arquivo inválido: {ex.Message}");
        }
        if (arquivo is null)
            return NotFound();

        var comTitulo = arquivo.Livros
            .Where(l => !string.IsNullOrWhiteSpace(l.Titulo))
            .OrderBy(l => l.Posicao)
            .ToList();
        if (comTitulo.Count == 0)
            return BadRequest("O arquivo não tem nenhum livro com título.");

        var inicio = posicaoInicial ?? await livros.ProximaPosicaoLivreAsync(arquivo.EstanteId, arquivo.Prateleira);
        var dtos = comTitulo.Select((l, i) => new LivroCreateDto
        {
            Titulo = l.Titulo!.Trim(),
            Autor = string.IsNullOrWhiteSpace(l.Autor) ? "Desconhecido" : l.Autor.Trim(),
            Ano = l.Ano,
            CapaUrl = l.CapaUrl,
            EstanteId = arquivo.EstanteId,
            Prateleira = arquivo.Prateleira,
            Posicao = inicio + i
        }).ToList();

        var (criados, erros) = await livros.CriarEmLoteAsync(dtos);
        if (erros is not null)
        {
            // Reindexa os erros pela "posicao" do arquivo, que é o que a pessoa vai procurar para corrigir.
            var porPosicaoNoArquivo = erros.ToDictionary(e => comTitulo[e.Key].Posicao, e => e.Value);
            return BadRequest(new { erros = porPosicaoNoArquivo });
        }

        return Created(string.Empty, criados);
    }

    private async Task SalvarArquivoAsync(ReconhecimentoFotoRequest request, List<LivroSugerido> sugestoes, CancellationToken ct)
    {
        var arquivo = new ArquivoReconhecimento
        {
            CriadoEm = DateTime.UtcNow,
            EstanteId = request.EstanteId,
            Prateleira = request.Prateleira,
            Foto = request.Foto.FileName,
            Fonte = metadados.Fonte,
            Livros = sugestoes.Select(s => new LivroReconhecido
            {
                Posicao = s.Posicao,
                Titulo = s.TituloSugerido,
                Autor = s.AutorSugerido,
                Ano = s.AnoSugerido,
                CapaUrl = s.CapaUrlSugerida,
                TextoOcr = s.TextoOcrBruto,
                Confianca = s.Confianca
            }).ToList()
        };

        try
        {
            var nome = await arquivos.SalvarAsync(arquivo, ct);
            Response.Headers[CabecalhoArquivo] = nome;
            logger.LogInformation("Reconhecimento salvo em {Arquivo}", nome);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // O reconhecimento continua útil mesmo sem o arquivo.
            logger.LogError(ex, "Não foi possível salvar o arquivo de reconhecimento");
        }
    }
}
