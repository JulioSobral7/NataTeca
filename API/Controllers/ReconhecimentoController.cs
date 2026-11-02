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
    ILogger<ReconhecimentoController> logger) : ControllerBase
{
    private const long TamanhoMaximoBytes = 15 * 1024 * 1024;

    [HttpPost("foto")]
    [RequestSizeLimit(TamanhoMaximoBytes)]
    public async Task<ActionResult<List<LivroSugerido>>> ReconhecerFoto([FromForm] ReconhecimentoFotoRequest request)
    {
        var estante = await db.Estantes.FindAsync(request.EstanteId);
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
            lidas = await visao.ReconhecerAsync(stream, request.Foto.FileName);
        }
        catch (VisaoIndisponivelException ex)
        {
            logger.LogError(ex, "Serviço de visão indisponível");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "Serviço de visão indisponível. " + ex.Message);
        }

        var sugestoes = new List<LivroSugerido>();
        foreach (var lida in lidas)
        {
            var meta = await metadados.BuscarPorTextoAsync(lida.Texto);

            sugestoes.Add(new LivroSugerido
            {
                Posicao = lida.Posicao,
                TituloSugerido = meta?.Titulo,
                AutorSugerido = meta?.Autor,
                CapaUrlSugerida = meta?.CapaUrl,
                TextoOcrBruto = lida.Texto,
                Confianca = lida.Confianca
            });
        }

        return Ok(sugestoes);
    }
}
