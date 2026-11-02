using System.Text.Json.Serialization;

namespace API.Models;

public enum StatusLeitura
{
    QueroLer,
    Lendo,
    Lido
}

public class Livro
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Autor { get; set; } = string.Empty;
    public int? Ano { get; set; }
    public string? Genero { get; set; }
    public string? CapaUrl { get; set; }
    public StatusLeitura Status { get; set; } = StatusLeitura.QueroLer;
    public DateTime AdicionadoEm { get; set; } = DateTime.UtcNow;

    public int EstanteId { get; set; }

    [JsonIgnore]
    public Estante? Estante { get; set; }
    public int Prateleira { get; set; }
    public int Posicao { get; set; }
}
