using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace proj1.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPropertyFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerProperties",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    property_type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    listing_type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    locality = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    city = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    pincode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    location_url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    asking_price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    negotiable_price = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    locality_average_price = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    price_per_sq_ft = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    area_sq_ft = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    bedrooms = table.Column<int>(type: "int", nullable: true),
                    bathrooms = table.Column<int>(type: "int", nullable: true),
                    floor = table.Column<int>(type: "int", nullable: true),
                    parking = table.Column<bool>(type: "bit", nullable: false),
                    furnished = table.Column<bool>(type: "bit", nullable: false),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    approved_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    approved_by = table.Column<int>(type: "int", nullable: true),
                    admin_comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    show_photos = table.Column<bool>(type: "bit", nullable: false),
                    show_asking_price = table.Column<bool>(type: "bit", nullable: false),
                    show_negotiable_price = table.Column<bool>(type: "bit", nullable: false),
                    show_locality_average_price = table.Column<bool>(type: "bit", nullable: false),
                    show_location = table.Column<bool>(type: "bit", nullable: false),
                    show_owner_name = table.Column<bool>(type: "bit", nullable: false),
                    show_owner_contact = table.Column<bool>(type: "bit", nullable: false),
                    show_description = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerProperties", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPropertyImages",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_property_id = table.Column<int>(type: "int", nullable: false),
                    image_url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    display_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPropertyImages", x => x.id);
                    table.ForeignKey(
                        name: "FK_CustomerPropertyImages_CustomerProperties_customer_property_id",
                        column: x => x.customer_property_id,
                        principalTable: "CustomerProperties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPropertyImages_customer_property_id",
                table: "CustomerPropertyImages",
                column: "customer_property_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerPropertyImages");

            migrationBuilder.DropTable(
                name: "CustomerProperties");
        }
    }
}
