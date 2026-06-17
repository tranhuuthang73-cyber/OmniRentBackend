using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniRentBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingDepositFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CompletedAt",
                table: "Booking",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DepositAmount",
                table: "Booking",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<long>(
                name: "DepositPaidAt",
                table: "Booking",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "RemainingAmount",
                table: "Booking",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<bool>(
                name: "RemainingPaid",
                table: "Booking",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TransferContent",
                table: "Booking",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Booking");

            migrationBuilder.DropColumn(
                name: "DepositAmount",
                table: "Booking");

            migrationBuilder.DropColumn(
                name: "DepositPaidAt",
                table: "Booking");

            migrationBuilder.DropColumn(
                name: "RemainingAmount",
                table: "Booking");

            migrationBuilder.DropColumn(
                name: "RemainingPaid",
                table: "Booking");

            migrationBuilder.DropColumn(
                name: "TransferContent",
                table: "Booking");
        }
    }
}
