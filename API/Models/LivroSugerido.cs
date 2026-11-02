namespace API.Models;

public class LivroSugerido
{
    public int Posicao { get; set; }
    public string? TituloSugerido { get; set; }
    public string? AutorSugerido { get; set; }
    public string? CapaUrlSugerida { get; set; }
    public string TextoOcrBruto { get; set; } = string.Empty;
    public int Confianca { get; set; }
}
