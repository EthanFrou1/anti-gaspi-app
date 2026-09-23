using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DefaultShelfLifeDays = table.Column<int>(type: "integer", nullable: false),
                    ExpiryKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCategories", x => x.Id);
                    table.CheckConstraint("CK_ProductCategories_DefaultShelfLifeDays", "\"DefaultShelfLifeDays\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "OffCategoryMappings",
                columns: table => new
                {
                    OffTag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OffCategoryMappings", x => x.OffTag);
                    table.ForeignKey(
                        name: "FK_OffCategoryMappings_ProductCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ProductCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ProductCategories",
                columns: new[] { "Id", "Code", "DefaultShelfLifeDays", "ExpiryKind", "Name" },
                values: new object[,]
                {
                    { 1, "ground-meat", 1, "UseBy", "Viande hachée" },
                    { 2, "fresh-meat", 3, "UseBy", "Viande fraîche" },
                    { 3, "poultry", 2, "UseBy", "Volaille" },
                    { 4, "fish-seafood", 2, "UseBy", "Poisson, fruits de mer" },
                    { 5, "cold-cuts", 7, "UseBy", "Charcuterie" },
                    { 6, "eggs", 21, "BestBefore", "Œufs" },
                    { 7, "fresh-milk", 7, "UseBy", "Lait frais" },
                    { 8, "uht-milk", 90, "BestBefore", "Lait UHT" },
                    { 9, "yogurts", 21, "UseBy", "Yaourts, desserts lactés" },
                    { 10, "fresh-cheese", 7, "UseBy", "Fromage frais" },
                    { 11, "soft-cheese", 14, "UseBy", "Fromage à pâte molle" },
                    { 12, "hard-cheese", 30, "BestBefore", "Fromage à pâte dure" },
                    { 13, "butter", 60, "BestBefore", "Beurre" },
                    { 14, "cream", 14, "UseBy", "Crème" },
                    { 15, "fruits", 7, "BestBefore", "Fruits" },
                    { 16, "vegetables", 7, "BestBefore", "Légumes" },
                    { 17, "leafy-greens", 4, "BestBefore", "Salade, herbes" },
                    { 18, "bread", 3, "BestBefore", "Pain" },
                    { 19, "ready-meals", 3, "UseBy", "Traiteur, plats préparés" },
                    { 20, "leftovers", 3, "UseBy", "Restes faits maison" },
                    { 21, "frozen", 180, "BestBefore", "Surgelés" },
                    { 22, "canned", 730, "BestBefore", "Conserves" },
                    { 23, "dry-goods", 365, "BestBefore", "Épicerie sèche" },
                    { 24, "condiments", 90, "BestBefore", "Sauces, condiments" },
                    { 25, "drinks", 30, "BestBefore", "Boissons" },
                    { 26, "other", 7, "BestBefore", "Autre" }
                });

            migrationBuilder.InsertData(
                table: "OffCategoryMappings",
                columns: new[] { "OffTag", "CategoryId", "Priority" },
                values: new object[,]
                {
                    { "en:aromatic-plants", 17, 0 },
                    { "en:bacon", 5, 0 },
                    { "en:beaufort", 12, 0 },
                    { "en:beef", 2, 0 },
                    { "en:beef-steaks", 2, 0 },
                    { "en:beverages", 25, 0 },
                    { "en:biscuits", 23, 0 },
                    { "en:blue-veined-cheeses", 11, 0 },
                    { "en:breads", 18, 0 },
                    { "en:breakfast-cereals", 23, 0 },
                    { "en:bries", 11, 0 },
                    { "en:butters", 13, 0 },
                    { "en:camemberts", 11, 0 },
                    { "en:canned-fishes", 22, 10 },
                    { "en:canned-foods", 22, 10 },
                    { "en:canned-vegetables", 22, 10 },
                    { "en:cantal", 12, 0 },
                    { "en:cereals-and-their-products", 23, 0 },
                    { "en:cheddar-cheese", 12, 0 },
                    { "en:cheeses", 11, 0 },
                    { "en:chicken-breasts", 3, 0 },
                    { "en:chicken-eggs", 6, 0 },
                    { "en:chickens", 3, 0 },
                    { "en:chocolates", 23, 0 },
                    { "en:comte", 12, 0 },
                    { "en:condiments", 24, 0 },
                    { "en:cream-cheeses", 10, 0 },
                    { "en:creams", 14, 0 },
                    { "en:crustaceans", 4, 0 },
                    { "en:dairy-desserts", 9, 0 },
                    { "en:dried-products", 23, 10 },
                    { "en:dry-sausages", 5, 0 },
                    { "en:eggs", 6, 0 },
                    { "en:emmentaler", 12, 0 },
                    { "en:fermented-milk-products", 9, 0 },
                    { "en:fishes", 4, 0 },
                    { "en:flours", 23, 0 },
                    { "en:fresh-cheeses", 10, 0 },
                    { "en:fresh-fruits", 15, 0 },
                    { "en:fresh-meals", 19, 0 },
                    { "en:fresh-milks", 7, 0 },
                    { "en:fresh-vegetables", 16, 0 },
                    { "en:frozen-foods", 21, 10 },
                    { "en:frozen-vegetables", 21, 10 },
                    { "en:fruit-juices", 25, 0 },
                    { "en:fruits", 15, 0 },
                    { "en:goat-cheeses", 11, 0 },
                    { "en:gouda", 12, 0 },
                    { "en:grana-padano", 12, 0 },
                    { "en:grated-cheese", 12, 0 },
                    { "en:ground-meats", 1, 0 },
                    { "en:hams", 5, 0 },
                    { "en:hard-cheeses", 12, 0 },
                    { "en:honeys", 24, 0 },
                    { "en:ice-creams", 21, 10 },
                    { "en:jams", 24, 0 },
                    { "en:ketchup", 24, 0 },
                    { "en:lamb-meat", 2, 0 },
                    { "en:leaf-vegetables", 17, 0 },
                    { "en:legumes", 23, 0 },
                    { "en:lettuces", 17, 0 },
                    { "en:meals", 19, 0 },
                    { "en:meats", 2, 0 },
                    { "en:milks", 7, 0 },
                    { "en:morbier", 12, 0 },
                    { "en:mozzarella", 10, 0 },
                    { "en:munster", 11, 0 },
                    { "en:mustards", 24, 0 },
                    { "en:parmigiano-reggiano", 12, 0 },
                    { "en:pastas", 23, 0 },
                    { "en:pizzas", 19, 0 },
                    { "en:pork", 2, 0 },
                    { "en:poultries", 3, 0 },
                    { "en:prepared-meats", 5, 0 },
                    { "en:pulses", 23, 0 },
                    { "en:raclette", 12, 0 },
                    { "en:reblochon", 11, 0 },
                    { "en:rices", 23, 0 },
                    { "en:roquefort-cheeses", 11, 0 },
                    { "en:saint-nectaire", 11, 0 },
                    { "en:salad-dressings", 24, 0 },
                    { "en:salted-butters", 13, 0 },
                    { "en:sandwiches", 19, 0 },
                    { "en:sauces", 24, 0 },
                    { "en:sausages", 5, 0 },
                    { "en:seafood", 4, 0 },
                    { "en:sliced-breads", 18, 0 },
                    { "en:smoked-salmons", 4, 0 },
                    { "en:snacks", 23, 0 },
                    { "en:sodas", 25, 0 },
                    { "en:soft-cheeses", 11, 0 },
                    { "en:sour-creams", 14, 0 },
                    { "en:spreads", 24, 0 },
                    { "en:turkeys", 3, 0 },
                    { "en:uht-milks", 8, 10 },
                    { "en:uncooked-pressed-cheeses", 12, 0 },
                    { "en:veal-meat", 2, 0 },
                    { "en:vegetable-oils", 24, 0 },
                    { "en:vegetables", 16, 0 },
                    { "en:viennoiseries", 18, 0 },
                    { "en:waters", 25, 0 },
                    { "en:yogurts", 9, 0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_OffCategoryMappings_CategoryId",
                table: "OffCategoryMappings",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_Code",
                table: "ProductCategories",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OffCategoryMappings");

            migrationBuilder.DropTable(
                name: "ProductCategories");
        }
    }
}
