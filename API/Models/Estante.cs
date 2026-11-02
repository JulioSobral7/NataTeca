using System.Text.Json.Serialization;

namespace API.Models;

public class Estante
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int QuantidadePrateleiras { get; set; }
    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public List<Livro> Livros { get; set; } = [];
}
