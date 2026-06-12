using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniRentBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddRentalAddressToBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RentalAddress",
                table: "Booking",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RentalAddress",
                table: "Booking");
        }
    }
}
