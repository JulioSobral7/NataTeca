using API.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Livro> Livros => Set<Livro>();
    public DbSet<Estante> Estantes => Set<Estante>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Livro>()
            .HasOne(l => l.Estante)
            .WithMany(e => e.Livros)
            .HasForeignKey(l => l.EstanteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
