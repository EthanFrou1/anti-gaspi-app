using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecipeGenerations_AspNetUsers_RequestedByUserId",
                table: "RecipeGenerations");

            migrationBuilder.AlterColumn<Guid>(
                name: "RequestedByUserId",
                table: "RecipeGenerations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "RecipeFavorites",
                columns: table => new
                {
                    RecipeGenerationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeFavorites", x => new { x.RecipeGenerationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_RecipeFavorites_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecipeFavorites_RecipeGenerations_RecipeGenerationId",
                        column: x => x.RecipeGenerationId,
                        principalTable: "RecipeGenerations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeFavorites_UserId",
                table: "RecipeFavorites",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeGenerations_AspNetUsers_RequestedByUserId",
                table: "RecipeGenerations",
                column: "RequestedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecipeGenerations_AspNetUsers_RequestedByUserId",
                table: "RecipeGenerations");

            migrationBuilder.DropTable(
                name: "RecipeFavorites");

            migrationBuilder.AlterColumn<Guid>(
                name: "RequestedByUserId",
                table: "RecipeGenerations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeGenerations_AspNetUsers_RequestedByUserId",
                table: "RecipeGenerations",
                column: "RequestedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
