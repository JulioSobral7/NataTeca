namespace API.Services;

public record MetadadosLivro(string Titulo, string Autor, string? CapaUrl);

public interface IBookMetadataService
{
    Task<MetadadosLivro?> BuscarPorTextoAsync(string textoOcr);
}
