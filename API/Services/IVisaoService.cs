namespace API.Services;

/// <summary>Imagens JPEG em base64 de uma lombada, já ampliadas: na vertical e giradas 90° nos dois sentidos.</summary>
public record RecortesLombada(string Vertical, IReadOnlyList<string> Girados);

/// <param name="Recortes">Só vem quando pedido e quando o recorte tem contraste (pode ter texto).</param>
public record LombadaLida(int Posicao, string Texto, int Confianca, RecortesLombada? Recortes = null);

/// <summary>Segmenta e lê as lombadas de uma foto de prateleira (implementado pelo microsserviço Python).</summary>
public interface IVisaoService
{
    Task<IReadOnlyList<LombadaLida>> ReconhecerAsync(Stream foto, string nomeArquivo, bool incluirRecortes = false, CancellationToken ct = default);
}

public class VisaoIndisponivelException(string message, Exception? inner = null) : Exception(message, inner);
