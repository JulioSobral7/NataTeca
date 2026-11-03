using API.Data;
using API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=natateca.db"));

// Quem identifica título/autor das lombadas:
//   "VisaoLlm"    — LLM de visão local lê a imagem de cada lombada (padrão; melhor nos testes)
//   "Llm"         — LLM de texto local separa o texto do OCR
//   "GoogleBooks" — busca o texto do OCR no Google Books
var fonteMetadados = builder.Configuration["Metadados:Fonte"] ?? "VisaoLlm";
builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection("Llm"));
void ConfigurarOllama(IServiceProvider sp, HttpClient client)
{
    var llm = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
    client.BaseAddress = new Uri(llm.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(llm.TimeoutSegundos);
}

switch (fonteMetadados.ToLowerInvariant())
{
    case "visaollm":
        builder.Services.AddHttpClient<IBookMetadataService, VisaoLlmMetadataService>(ConfigurarOllama);
        break;
    case "llm":
        builder.Services.AddHttpClient<IBookMetadataService, LlmMetadataService>(ConfigurarOllama);
        break;
    case "googlebooks":
        builder.Services.AddHttpClient<IBookMetadataService, GoogleBooksMetadataService>();
        break;
    default:
        throw new InvalidOperationException(
            $"Metadados:Fonte inválida: '{fonteMetadados}'. Use 'VisaoLlm', 'Llm' ou 'GoogleBooks'.");
}

builder.Services.AddScoped<LivroService>();
builder.Services.AddSingleton<ArquivoReconhecimentoService>();

builder.Services.AddHttpClient<IVisaoService, PythonVisaoService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["VisionService:BaseUrl"] ?? "http://localhost:8000/");
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders(API.Controllers.ReconhecimentoController.CabecalhoArquivo);
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCors("Frontend");

app.UseAuthorization();

app.MapControllers();

app.Run();
