using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CapasController(IWebHostEnvironment env) : ControllerBase
{
    private static readonly HashSet<string> ExtensoesPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private const long TamanhoMaximoBytes = 5 * 1024 * 1024;

    [HttpPost]
    [RequestSizeLimit(TamanhoMaximoBytes)]
    public async Task<IActionResult> UploadCapa(IFormFile arquivo)
    {
        if (arquivo.Length == 0)
            return BadRequest("Arquivo vazio.");

        if (arquivo.Length > TamanhoMaximoBytes)
            return BadRequest("Arquivo excede o tamanho máximo de 5MB.");

        var extensao = Path.GetExtension(arquivo.FileName);
        if (!ExtensoesPermitidas.Contains(extensao))
            return BadRequest("Formato de imagem não suportado. Use jpg, png ou webp.");

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var pastaCapas = Path.Combine(webRoot, "capas");
        Directory.CreateDirectory(pastaCapas);

        var nomeArquivo = $"{Guid.NewGuid()}{extensao}";
        var caminhoCompleto = Path.Combine(pastaCapas, nomeArquivo);

        await using (var stream = System.IO.File.Create(caminhoCompleto))
        {
            await arquivo.CopyToAsync(stream);
        }

        return Ok(new { url = $"/capas/{nomeArquivo}" });
    }
}
