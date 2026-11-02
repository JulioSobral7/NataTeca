using API.Data;
using API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LivrosController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Livro>>> GetLivros([FromQuery] StatusLeitura? status)
    {
        var query = db.Livros.AsQueryable();
        if (status is not null)
        {
            query = query.Where(l => l.Status == status);
        }

        return Ok(await query.OrderBy(l => l.Titulo).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Livro>> GetLivro(int id)
    {
        var livro = await db.Livros.FindAsync(id);
        return livro is null ? NotFound() : Ok(livro);
    }

    [HttpPost]
    public async Task<ActionResult<Livro>> CreateLivro(LivroCreateDto dto)
    {
        var (estante, erroEstante) = await ObterEstanteValidaAsync(dto.EstanteId, dto.Prateleira);
        if (erroEstante is not null) return BadRequest(erroEstante);

        if (await PosicaoOcupadaAsync(dto.EstanteId, dto.Prateleira, dto.Posicao, ignorarLivroId: null))
            return Conflict("Já existe um livro nessa posição da estante.");

        var livro = new Livro
        {
            Titulo = dto.Titulo,
            Autor = dto.Autor,
            Ano = dto.Ano,
            Genero = dto.Genero,
            CapaUrl = dto.CapaUrl,
            Status = dto.Status,
            EstanteId = dto.EstanteId,
            Prateleira = dto.Prateleira,
            Posicao = dto.Posicao
        };

        db.Livros.Add(livro);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetLivro), new { id = livro.Id }, livro);
    }

    [HttpPost("lote")]
    public async Task<ActionResult<IEnumerable<Livro>>> CreateLivrosEmLote(List<LivroCreateDto> dtos)
    {
        if (dtos.Count == 0)
            return BadRequest("Lista de livros vazia.");

        var erros = new Dictionary<int, string>();
        var posicoesNoLote = new HashSet<(int EstanteId, int Prateleira, int Posicao)>();

        for (var i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];

            var (_, erroEstante) = await ObterEstanteValidaAsync(dto.EstanteId, dto.Prateleira);
            if (erroEstante is not null)
            {
                erros[i] = erroEstante;
                continue;
            }

            if (!posicoesNoLote.Add((dto.EstanteId, dto.Prateleira, dto.Posicao)))
            {
                erros[i] = "Colide com outro item deste mesmo lote.";
                continue;
            }

            if (await PosicaoOcupadaAsync(dto.EstanteId, dto.Prateleira, dto.Posicao, ignorarLivroId: null))
            {
                erros[i] = "Já existe um livro nessa posição da estante.";
            }
        }

        if (erros.Count > 0)
            return BadRequest(new { erros });

        var livros = dtos.Select(dto => new Livro
        {
            Titulo = dto.Titulo,
            Autor = dto.Autor,
            Ano = dto.Ano,
            Genero = dto.Genero,
            CapaUrl = dto.CapaUrl,
            Status = dto.Status,
            EstanteId = dto.EstanteId,
            Prateleira = dto.Prateleira,
            Posicao = dto.Posicao
        }).ToList();

        db.Livros.AddRange(livros);
        await db.SaveChangesAsync();

        return Created(string.Empty, livros);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateLivro(int id, LivroUpdateDto dto)
    {
        var livro = await db.Livros.FindAsync(id);
        if (livro is null) return NotFound();

        var (_, erroEstante) = await ObterEstanteValidaAsync(dto.EstanteId, dto.Prateleira);
        if (erroEstante is not null) return BadRequest(erroEstante);

        if (await PosicaoOcupadaAsync(dto.EstanteId, dto.Prateleira, dto.Posicao, ignorarLivroId: id))
            return Conflict("Já existe um livro nessa posição da estante.");

        livro.Titulo = dto.Titulo;
        livro.Autor = dto.Autor;
        livro.Ano = dto.Ano;
        livro.Genero = dto.Genero;
        livro.CapaUrl = dto.CapaUrl;
        livro.Status = dto.Status;
        livro.EstanteId = dto.EstanteId;
        livro.Prateleira = dto.Prateleira;
        livro.Posicao = dto.Posicao;

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:int}/posicao")]
    public async Task<IActionResult> MoverLivro(int id, LivroPosicaoDto dto)
    {
        var livro = await db.Livros.FindAsync(id);
        if (livro is null) return NotFound();

        var (_, erroEstante) = await ObterEstanteValidaAsync(livro.EstanteId, dto.Prateleira);
        if (erroEstante is not null) return BadRequest(erroEstante);

        if (await PosicaoOcupadaAsync(livro.EstanteId, dto.Prateleira, dto.Posicao, ignorarLivroId: id))
            return Conflict("Já existe um livro nessa posição da estante.");

        livro.Prateleira = dto.Prateleira;
        livro.Posicao = dto.Posicao;

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteLivro(int id)
    {
        var livro = await db.Livros.FindAsync(id);
        if (livro is null) return NotFound();

        db.Livros.Remove(livro);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<(Estante? Estante, string? Erro)> ObterEstanteValidaAsync(int estanteId, int prateleira)
    {
        var estante = await db.Estantes.FindAsync(estanteId);
        if (estante is null)
            return (null, "Estante informada não existe.");

        if (prateleira >= estante.QuantidadePrateleiras)
            return (null, $"A estante '{estante.Nome}' só possui {estante.QuantidadePrateleiras} prateleira(s).");

        return (estante, null);
    }

    private async Task<bool> PosicaoOcupadaAsync(int estanteId, int prateleira, int posicao, int? ignorarLivroId)
    {
        return await db.Livros.AnyAsync(l =>
            (ignorarLivroId == null || l.Id != ignorarLivroId) &&
            l.EstanteId == estanteId && l.Prateleira == prateleira && l.Posicao == posicao);
    }
}
