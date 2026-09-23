using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfilesAndKitchenEquipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "Equipment",
                table: "Households",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY['Hob','Microwave']::text[]");

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CookingTime = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Budget = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Diet = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Exclusions = table.Column<string[]>(type: "text[]", nullable: false),
                    Allergens = table.Column<string[]>(type: "text[]", nullable: false),
                    HealthDataConsentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Goal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DefaultServings = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.UserId);
                    table.CheckConstraint("CK_UserProfiles_DefaultServings", "\"DefaultServings\" BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_UserProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "Equipment",
                table: "Households");
        }
    }
}
