using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace proj1.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBiddingAndVisibilityControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "negotiable_price",
                table: "properties",
                type: "decimal(14,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "locality",
                table: "properties",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "location_url",
                table: "properties",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "show_photos",
                table: "properties",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "show_asking_price",
                table: "properties",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "show_negotiable_price",
                table: "properties",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "show_exact_address",
                table: "properties",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "show_description",
                table: "properties",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "show_seller_contact",
                table: "properties",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "admin_comment",
                table: "properties",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "buyer_id",
                table: "bids",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "original_amount",
                table: "bids",
                type: "decimal(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "is_modified_by_admin",
                table: "bids",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "admin_notes",
                table: "bids",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "buyer_message",
                table: "bids",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_bids_buyer_id",
                table: "bids",
                column: "buyer_id");

            migrationBuilder.AddForeignKey(
                name: "FK_bids_profiles_buyer_id",
                table: "bids",
                column: "buyer_id",
                principalTable: "profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bids_profiles_buyer_id",
                table: "bids");

            migrationBuilder.DropIndex(
                name: "IX_bids_buyer_id",
                table: "bids");

            migrationBuilder.DropColumn(
                name: "negotiable_price",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "locality",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "location_url",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "show_photos",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "show_asking_price",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "show_negotiable_price",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "show_exact_address",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "show_description",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "show_seller_contact",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "admin_comment",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "buyer_id",
                table: "bids");

            migrationBuilder.DropColumn(
                name: "original_amount",
                table: "bids");

            migrationBuilder.DropColumn(
                name: "is_modified_by_admin",
                table: "bids");

            migrationBuilder.DropColumn(
                name: "admin_notes",
                table: "bids");

            migrationBuilder.DropColumn(
                name: "buyer_message",
                table: "bids");
        }
    }
}
