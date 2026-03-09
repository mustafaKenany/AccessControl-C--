using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControlPro.Infrastructure.Persistence.Migrations;

public partial class AddPhoneUniqueIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Employees_Phone",
            table: "Employees",
            column: "Phone",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Employees_Phone",
            table: "Employees");
    }
}
