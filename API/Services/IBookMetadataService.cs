namespace API.Services;

public record MetadadosLivro(string? Titulo, string? Autor, int? Ano, string? CapaUrl);

/// <summary>Transforma o texto lido das lombadas em título, autor e ano.</summary>
public interface IBookMetadataService
{
    /// <summary>Nome da fonte, gravado no arquivo de reconhecimento (ex.: "GoogleBooks", "Llm:natateca-llm").</summary>
    string Fonte { get; }

    /// <summary>Se precisa das imagens das lombadas (leitura por modelo de visão), além do texto do OCR.</summary>
    bool PrecisaDeRecortes => false;

    /// <summary>
    /// Recebe todas as lombadas de uma foto e devolve uma lista do mesmo tamanho, na mesma ordem;
    /// <c>null</c> quando a lombada não foi identificada.
    /// </summary>
    Task<IReadOnlyList<MetadadosLivro?>> BuscarAsync(IReadOnlyList<LombadaLida> lombadas, CancellationToken ct = default);
}
