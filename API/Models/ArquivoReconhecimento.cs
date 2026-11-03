namespace API.Models;

/// <summary>
/// Resultado do reconhecimento de uma foto, gravado em JSON. Pode ser revisado/editado à mão
/// e depois importado para o banco com <c>POST /api/reconhecimento/arquivos/{nome}/importar</c>.
/// </summary>
public class ArquivoReconhecimento
{
    public int Versao { get; set; } = 1;
    public DateTime CriadoEm { get; set; }
    public int EstanteId { get; set; }

    /// <summary>Índice da prateleira, começando em 0 (a "Prateleira 1" da interface é 0).</summary>
    public int Prateleira { get; set; }

    public string? Foto { get; set; }

    /// <summary>Quem separou título/autor/ano (ex.: "Llm:natateca-llm" ou "GoogleBooks").</summary>
    public string? Fonte { get; set; }

    public List<LivroReconhecido> Livros { get; set; } = [];
}

public class LivroReconhecido
{
    /// <summary>Ordem da lombada na foto, da esquerda para a direita.</summary>
    public int Posicao { get; set; }
    public string? Titulo { get; set; }
    public string? Autor { get; set; }
    public int? Ano { get; set; }
    public string? CapaUrl { get; set; }
    public string TextoOcr { get; set; } = string.Empty;
    public int Confianca { get; set; }
}
