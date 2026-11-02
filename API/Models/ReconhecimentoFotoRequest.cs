using System.ComponentModel.DataAnnotations;

namespace API.Models;

public class ReconhecimentoFotoRequest
{
    [Required]
    public IFormFile Foto { get; set; } = null!;

    [Required]
    public int EstanteId { get; set; }

    [Range(0, int.MaxValue)]
    public int Prateleira { get; set; }
}
