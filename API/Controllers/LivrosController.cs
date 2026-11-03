using API.Data;
using API.Models;
using API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LivrosController(AppDbContext db, LivroService livros) : ControllerBase
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
        var (_, erroEstante) = await livros.ObterEstanteValidaAsync(dto.EstanteId, dto.Prateleira);
        if (erroEstante is not null) return BadRequest(erroEstante);

        if (await livros.PosicaoOcupadaAsync(dto.EstanteId, dto.Prateleira, dto.Posicao, ignorarLivroId: null))
            return Conflict("Já existe um livro nessa posição da estante.");

        var livro = LivroService.ParaLivro(dto);

        db.Livros.Add(livro);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetLivro), new { id = livro.Id }, livro);
    }

    [HttpPost("lote")]
    public async Task<ActionResult<IEnumerable<Livro>>> CreateLivrosEmLote(List<LivroCreateDto> dtos)
    {
        if (dtos.Count == 0)
            return BadRequest("Lista de livros vazia.");

        var (criados, erros) = await livros.CriarEmLoteAsync(dtos);
        if (erros is not null)
            return BadRequest(new { erros });

        return Created(string.Empty, criados);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateLivro(int id, LivroUpdateDto dto)
    {
        var livro = await db.Livros.FindAsync(id);
        if (livro is null) return NotFound();

        var (_, erroEstante) = await livros.ObterEstanteValidaAsync(dto.EstanteId, dto.Prateleira);
        if (erroEstante is not null) return BadRequest(erroEstante);

        if (await livros.PosicaoOcupadaAsync(dto.EstanteId, dto.Prateleira, dto.Posicao, ignorarLivroId: id))
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

        var (_, erroEstante) = await livros.ObterEstanteValidaAsync(livro.EstanteId, dto.Prateleira);
        if (erroEstante is not null) return BadRequest(erroEstante);

        if (await livros.PosicaoOcupadaAsync(livro.EstanteId, dto.Prateleira, dto.Posicao, ignorarLivroId: id))
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
}
