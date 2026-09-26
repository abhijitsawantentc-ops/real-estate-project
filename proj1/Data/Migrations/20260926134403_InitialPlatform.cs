using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace proj1.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    full_name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    role = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    avatar_url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    password_hash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    agency_name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    license_number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    address = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    city = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    state = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    verification_status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents", x => x.id);
                    table.ForeignKey(
                        name: "FK_agents_profiles_user_id",
                        column: x => x.user_id,
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    old_value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    new_value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_logs_profiles_user_id",
                        column: x => x.user_id,
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "properties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    agent_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    property_type = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    listing_type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    price = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    city = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    state = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    pincode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    bedrooms = table.Column<int>(type: "int", nullable: true),
                    bathrooms = table.Column<int>(type: "int", nullable: true),
                    area = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    area_unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    admin_approved = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    published_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_properties", x => x.id);
                    table.ForeignKey(
                        name: "FK_properties_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_interests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    property_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    customer_message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_interests", x => x.id);
                    table.ForeignKey(
                        name: "FK_customer_interests_profiles_customer_id",
                        column: x => x.customer_id,
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customer_interests_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    type = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    property_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    bid_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    transaction_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    is_read = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_profiles_user_id",
                        column: x => x.user_id,
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notifications_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "property_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    property_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    storage_path = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    image_url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    is_primary = table.Column<bool>(type: "bit", nullable: false),
                    display_order = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_property_images_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bids",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    property_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    agent_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    customer_interest_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_by_admin = table.Column<bool>(type: "bit", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bids", x => x.id);
                    table.ForeignKey(
                        name: "FK_bids_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bids_customer_interests_customer_interest_id",
                        column: x => x.customer_interest_id,
                        principalTable: "customer_interests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_bids_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    property_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    agent_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    interest_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    bid_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    agreed_price = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: true),
                    admin_notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    started_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    completed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_transactions_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transactions_bids_bid_id",
                        column: x => x.bid_id,
                        principalTable: "bids",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_transactions_customer_interests_interest_id",
                        column: x => x.interest_id,
                        principalTable: "customer_interests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_transactions_profiles_customer_id",
                        column: x => x.customer_id,
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transactions_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agents_user_id",
                table: "agents",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_entity_type_entity_id_created_at",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_user_id",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_bids_agent_id",
                table: "bids",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "IX_bids_customer_interest_id",
                table: "bids",
                column: "customer_interest_id");

            migrationBuilder.CreateIndex(
                name: "IX_bids_property_id",
                table: "bids",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "IX_customer_interests_customer_id_property_id",
                table: "customer_interests",
                columns: new[] { "customer_id", "property_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_interests_property_id",
                table: "customer_interests",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_property_id",
                table: "notifications",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id",
                table: "notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_profiles_email",
                table: "profiles",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_properties_admin_approved_status_created_at",
                table: "properties",
                columns: new[] { "admin_approved", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_properties_agent_id",
                table: "properties",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "IX_properties_city_listing_type_price",
                table: "properties",
                columns: new[] { "city", "listing_type", "price" });

            migrationBuilder.CreateIndex(
                name: "IX_property_images_property_id_display_order",
                table: "property_images",
                columns: new[] { "property_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_agent_id",
                table: "transactions",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_bid_id",
                table: "transactions",
                column: "bid_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_customer_id",
                table: "transactions",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_interest_id",
                table: "transactions",
                column: "interest_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_property_id",
                table: "transactions",
                column: "property_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "property_images");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "bids");

            migrationBuilder.DropTable(
                name: "customer_interests");

            migrationBuilder.DropTable(
                name: "properties");

            migrationBuilder.DropTable(
                name: "agents");

            migrationBuilder.DropTable(
                name: "profiles");
        }
    }
}
