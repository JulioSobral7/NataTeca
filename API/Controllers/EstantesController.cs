using API.Data;
using API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EstantesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Estante>>> GetEstantes()
    {
        return Ok(await db.Estantes.OrderBy(e => e.Nome).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Estante>> GetEstante(int id)
    {
        var estante = await db.Estantes.FindAsync(id);
        return estante is null ? NotFound() : Ok(estante);
    }

    [HttpGet("{estanteId:int}/livros")]
    public async Task<ActionResult<IEnumerable<Livro>>> GetLivrosDaEstante(int estanteId)
    {
        if (!await db.Estantes.AnyAsync(e => e.Id == estanteId))
            return NotFound();

        var livros = await db.Livros
            .Where(l => l.EstanteId == estanteId)
            .OrderBy(l => l.Prateleira)
            .ThenBy(l => l.Posicao)
            .ToListAsync();

        return Ok(livros);
    }

    [HttpPost]
    public async Task<ActionResult<Estante>> CreateEstante(EstanteCreateDto dto)
    {
        var estante = new Estante
        {
            Nome = dto.Nome,
            QuantidadePrateleiras = dto.QuantidadePrateleiras
        };

        db.Estantes.Add(estante);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetEstante), new { id = estante.Id }, estante);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateEstante(int id, EstanteUpdateDto dto)
    {
        var estante = await db.Estantes.FindAsync(id);
        if (estante is null) return NotFound();

        if (dto.QuantidadePrateleiras < estante.QuantidadePrateleiras)
        {
            var possuiLivroForaDoLimite = await db.Livros
                .AnyAsync(l => l.EstanteId == id && l.Prateleira >= dto.QuantidadePrateleiras);
            if (possuiLivroForaDoLimite)
                return BadRequest("Existem livros em prateleiras que seriam removidas. Mova-os antes de reduzir a estante.");
        }

        estante.Nome = dto.Nome;
        estante.QuantidadePrateleiras = dto.QuantidadePrateleiras;

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteEstante(int id)
    {
        var estante = await db.Estantes.FindAsync(id);
        if (estante is null) return NotFound();

        var possuiLivros = await db.Livros.AnyAsync(l => l.EstanteId == id);
        if (possuiLivros)
            return BadRequest("Não é possível excluir uma estante que ainda possui livros. Mova ou remova os livros primeiro.");

        db.Estantes.Remove(estante);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
