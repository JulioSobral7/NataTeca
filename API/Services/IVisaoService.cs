namespace API.Services;

public record LombadaLida(int Posicao, string Texto, int Confianca);

/// <summary>Segmenta e lê as lombadas de uma foto de prateleira (implementado pelo microsserviço Python).</summary>
public interface IVisaoService
{
    Task<IReadOnlyList<LombadaLida>> ReconhecerAsync(Stream foto, string nomeArquivo, CancellationToken ct = default);
}

public class VisaoIndisponivelException(string message, Exception? inner = null) : Exception(message, inner);
