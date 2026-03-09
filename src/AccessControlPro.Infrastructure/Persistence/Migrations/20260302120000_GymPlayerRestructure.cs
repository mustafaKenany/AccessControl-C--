using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControlPro.Infrastructure.Persistence.Migrations;

public partial class GymPlayerRestructure : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Drop old indexes
        migrationBuilder.DropIndex(name: "IX_Employees_EmployeeCode", table: "Employees");
        migrationBuilder.DropIndex(name: "IX_Employees_Department", table: "Employees");
        migrationBuilder.DropIndex(name: "IX_Employees_NationalId", table: "Employees");

        // Rename columns
        migrationBuilder.RenameColumn(name: "EmployeeCode", table: "Employees", newName: "CardNo");
        migrationBuilder.RenameColumn(name: "Department", table: "Employees", newName: "SubscriptionType");

        // Drop old columns
        migrationBuilder.DropColumn(name: "NationalId", table: "Employees");
        migrationBuilder.DropColumn(name: "Salary", table: "Employees");
        migrationBuilder.DropColumn(name: "PhotoPath", table: "Employees");

        // Add new columns
        migrationBuilder.AddColumn<byte[]>(
            name: "PhotoData",
            table: "Employees",
            type: "varbinary(max)",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "SubscriptionFee",
            table: "Employees",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "AmountPaid",
            table: "Employees",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<DateTime>(
            name: "StartDate",
            table: "Employees",
            type: "datetime2",
            nullable: false,
            defaultValue: new DateTime(2026, 1, 1));

        migrationBuilder.AddColumn<DateTime>(
            name: "EndDate",
            table: "Employees",
            type: "datetime2",
            nullable: false,
            defaultValue: new DateTime(2026, 1, 1));

        // Create new indexes
        migrationBuilder.CreateIndex(
            name: "IX_Employees_CardNo",
            table: "Employees",
            column: "CardNo",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Employees_SubscriptionType",
            table: "Employees",
            column: "SubscriptionType");

        migrationBuilder.CreateIndex(
            name: "IX_Employees_EndDate",
            table: "Employees",
            column: "EndDate");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Employees_CardNo", table: "Employees");
        migrationBuilder.DropIndex(name: "IX_Employees_SubscriptionType", table: "Employees");
        migrationBuilder.DropIndex(name: "IX_Employees_EndDate", table: "Employees");

        migrationBuilder.DropColumn(name: "PhotoData", table: "Employees");
        migrationBuilder.DropColumn(name: "SubscriptionFee", table: "Employees");
        migrationBuilder.DropColumn(name: "AmountPaid", table: "Employees");
        migrationBuilder.DropColumn(name: "StartDate", table: "Employees");
        migrationBuilder.DropColumn(name: "EndDate", table: "Employees");

        migrationBuilder.RenameColumn(name: "CardNo", table: "Employees", newName: "EmployeeCode");
        migrationBuilder.RenameColumn(name: "SubscriptionType", table: "Employees", newName: "Department");

        migrationBuilder.AddColumn<string>(
            name: "NationalId",
            table: "Employees",
            type: "nvarchar(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<decimal>(
            name: "Salary",
            table: "Employees",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<string>(
            name: "PhotoPath",
            table: "Employees",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(name: "IX_Employees_EmployeeCode", table: "Employees", column: "EmployeeCode", unique: true);
        migrationBuilder.CreateIndex(name: "IX_Employees_Department", table: "Employees", column: "Department");
        migrationBuilder.CreateIndex(name: "IX_Employees_NationalId", table: "Employees", column: "NationalId");
    }
}
