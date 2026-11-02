using System.ComponentModel.DataAnnotations;

namespace API.Models;

public class EstanteCreateDto
{
    [Required, MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    [Range(1, 50)]
    public int QuantidadePrateleiras { get; set; }
}

public class EstanteUpdateDto : EstanteCreateDto
{
}
