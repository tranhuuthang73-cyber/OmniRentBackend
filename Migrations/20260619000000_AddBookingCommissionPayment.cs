using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OmniRentBackend.Data;

#nullable disable

namespace OmniRentBackend.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(OmniRentDbContext))]
    [Migration("20260619000000_AddBookingCommissionPayment")]
    public partial class AddBookingCommissionPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CommissionPaid",
                table: "Booking",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "CommissionPaidAt",
                table: "Booking",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommissionPaid",
                table: "Booking");

            migrationBuilder.DropColumn(
                name: "CommissionPaidAt",
                table: "Booking");
        }
    }
}
