using System.ComponentModel.DataAnnotations;
using API.Data;
using API.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Services;

/// <summary>Regras de cadastro de livros compartilhadas pelo CRUD e pela importação de arquivos.</summary>
public class LivroService(AppDbContext db)
{
    /// <summary>
    /// Valida e grava todos os livros de uma vez. Se algum for inválido, nada é gravado
    /// e os erros vêm indexados pela posição do item na lista recebida.
    /// </summary>
    public async Task<(List<Livro>? Livros, Dictionary<int, string>? Erros)> CriarEmLoteAsync(IReadOnlyList<LivroCreateDto> dtos)
    {
        var erros = new Dictionary<int, string>();
        var posicoesNoLote = new HashSet<(int EstanteId, int Prateleira, int Posicao)>();

        for (var i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];

            // O [ApiController] só valida o corpo da requisição; itens vindos de arquivo precisam disto.
            var resultados = new List<ValidationResult>();
            if (!Validator.TryValidateObject(dto, new ValidationContext(dto), resultados, validateAllProperties: true))
            {
                erros[i] = string.Join(" ", resultados.Select(r => r.ErrorMessage));
                continue;
            }

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
            return (null, erros);

        var livros = dtos.Select(ParaLivro).ToList();
        db.Livros.AddRange(livros);
        await db.SaveChangesAsync();
        return (livros, null);
    }

    public static Livro ParaLivro(LivroCreateDto dto) => new()
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

    public async Task<(Estante? Estante, string? Erro)> ObterEstanteValidaAsync(int estanteId, int prateleira)
    {
        var estante = await db.Estantes.FindAsync(estanteId);
        if (estante is null)
            return (null, "Estante informada não existe.");

        if (prateleira >= estante.QuantidadePrateleiras)
            return (null, $"A estante '{estante.Nome}' só possui {estante.QuantidadePrateleiras} prateleira(s).");

        return (estante, null);
    }

    public Task<bool> PosicaoOcupadaAsync(int estanteId, int prateleira, int posicao, int? ignorarLivroId)
    {
        return db.Livros.AnyAsync(l =>
            (ignorarLivroId == null || l.Id != ignorarLivroId) &&
            l.EstanteId == estanteId && l.Prateleira == prateleira && l.Posicao == posicao);
    }

    /// <summary>Primeira posição depois do último livro da prateleira.</summary>
    public async Task<int> ProximaPosicaoLivreAsync(int estanteId, int prateleira)
    {
        var ultima = await db.Livros
            .Where(l => l.EstanteId == estanteId && l.Prateleira == prateleira)
            .MaxAsync(l => (int?)l.Posicao);
        return (ultima ?? -1) + 1;
    }
}
