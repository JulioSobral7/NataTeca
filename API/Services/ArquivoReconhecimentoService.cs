using System.Text.Encodings.Web;
using System.Text.Json;
using API.Models;

namespace API.Services;

/// <summary>Grava e lê os arquivos JSON de reconhecimento (um por foto).</summary>
public class ArquivoReconhecimentoService(IConfiguration config, IWebHostEnvironment env)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        // Acentos legíveis no arquivo, para facilitar a edição à mão.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private string Pasta => Path.GetFullPath(Path.Combine(
        env.ContentRootPath, config["Reconhecimento:PastaArquivos"] ?? "reconhecimentos"));

    public async Task<string> SalvarAsync(ArquivoReconhecimento arquivo, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Pasta);

        var baseNome = $"{arquivo.CriadoEm.ToLocalTime():yyyyMMdd-HHmmss}_estante{arquivo.EstanteId}_prateleira{arquivo.Prateleira + 1}";
        var nome = $"{baseNome}.json";
        for (var n = 2; File.Exists(Path.Combine(Pasta, nome)); n++)
            nome = $"{baseNome}-{n}.json";

        await File.WriteAllTextAsync(Path.Combine(Pasta, nome), JsonSerializer.Serialize(arquivo, Json), ct);
        return nome;
    }

    public IReadOnlyList<string> Listar() =>
        Directory.Exists(Pasta)
            ? Directory.GetFiles(Pasta, "*.json").Select(Path.GetFileName).OfType<string>().OrderDescending().ToList()
            : [];

    /// <summary>Lê um arquivo pelo nome. Devolve null se o nome for inválido ou o arquivo não existir.</summary>
    /// <exception cref="JsonException">O conteúdo não é um arquivo de reconhecimento válido.</exception>
    public async Task<ArquivoReconhecimento?> LerAsync(string nome, CancellationToken ct = default)
    {
        var caminho = Caminho(nome);
        if (caminho is null || !File.Exists(caminho))
            return null;

        await using var stream = File.OpenRead(caminho);
        return await JsonSerializer.DeserializeAsync<ArquivoReconhecimento>(stream, Json, ct);
    }

    /// <summary>Aceita só um nome de arquivo .json simples, sem pastas (evita ler fora da pasta).</summary>
    private string? Caminho(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome) || nome != Path.GetFileName(nome)
            || !nome.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return null;
        return Path.Combine(Pasta, nome);
    }
}
