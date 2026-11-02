using System.ComponentModel.DataAnnotations;

namespace API.Models;

public class LivroCreateDto
{
    [Required, MaxLength(200)]
    public string Titulo { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Autor { get; set; } = string.Empty;

    public int? Ano { get; set; }

    [MaxLength(80)]
    public string? Genero { get; set; }

    public string? CapaUrl { get; set; }

    public StatusLeitura Status { get; set; } = StatusLeitura.QueroLer;

    [Required]
    public int EstanteId { get; set; }

    [Range(0, int.MaxValue)]
    public int Prateleira { get; set; }

    [Range(0, int.MaxValue)]
    public int Posicao { get; set; }
}

public class LivroUpdateDto : LivroCreateDto
{
}

public class LivroPosicaoDto
{
    [Range(0, int.MaxValue)]
    public int Prateleira { get; set; }

    [Range(0, int.MaxValue)]
    public int Posicao { get; set; }
}
