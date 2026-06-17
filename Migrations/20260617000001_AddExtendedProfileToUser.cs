using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniRentBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddExtendedProfileToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NationalId",
                table: "User",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "User",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccount",
                table: "User",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountHolder",
                table: "User",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupAddress",
                table: "User",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ProfileCompleted",
                table: "User",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NationalId",
                table: "User");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "User");

            migrationBuilder.DropColumn(
                name: "BankAccount",
                table: "User");

            migrationBuilder.DropColumn(
                name: "BankAccountHolder",
                table: "User");

            migrationBuilder.DropColumn(
                name: "PickupAddress",
                table: "User");

            migrationBuilder.DropColumn(
                name: "ProfileCompleted",
                table: "User");
        }
    }
}
