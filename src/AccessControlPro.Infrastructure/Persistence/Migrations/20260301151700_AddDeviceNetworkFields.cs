using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControlPro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceNetworkFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Gateway",
                table: "Devices",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "0.0.0.0");

            migrationBuilder.AddColumn<string>(
                name: "SubnetMask",
                table: "Devices",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "255.255.255.0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gateway",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "SubnetMask",
                table: "Devices");
        }
    }
}
