using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Migrations
{
    /// <inheritdoc />
    public partial class AddEstantesEPosicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstanteId",
                table: "Livros",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Posicao",
                table: "Livros",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Prateleira",
                table: "Livros",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Estantes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    QuantidadePrateleiras = table.Column<int>(type: "INTEGER", nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Estantes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Livros_EstanteId",
                table: "Livros",
                column: "EstanteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Livros_Estantes_EstanteId",
                table: "Livros",
                column: "EstanteId",
                principalTable: "Estantes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Livros_Estantes_EstanteId",
                table: "Livros");

            migrationBuilder.DropTable(
                name: "Estantes");

            migrationBuilder.DropIndex(
                name: "IX_Livros_EstanteId",
                table: "Livros");

            migrationBuilder.DropColumn(
                name: "EstanteId",
                table: "Livros");

            migrationBuilder.DropColumn(
                name: "Posicao",
                table: "Livros");

            migrationBuilder.DropColumn(
                name: "Prateleira",
                table: "Livros");
        }
    }
}
