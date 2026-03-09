using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControlPro.Infrastructure.Persistence.Migrations
{
    public partial class EnhanceEmployeeAndCards : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Employee new fields
            migrationBuilder.AddColumn<string>("Phone", "Employees", type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>("NationalId", "Employees", type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>("PhotoPath", "Employees", type: "nvarchar(500)", maxLength: 500, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<double>("Height", "Employees", type: "float", nullable: false, defaultValue: 0.0);
            migrationBuilder.AddColumn<double>("Weight", "Employees", type: "float", nullable: false, defaultValue: 0.0);
            migrationBuilder.AddColumn<decimal>("Salary", "Employees", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<string>("Notes", "Employees", type: "nvarchar(1000)", maxLength: 1000, nullable: false, defaultValue: "");

            // Employee indexes for fast search
            migrationBuilder.AlterColumn<string>("FullNameEn", "Employees", type: "nvarchar(200)", maxLength: 200, nullable: false, oldClrType: typeof(string), oldType: "nvarchar(max)");
            migrationBuilder.AlterColumn<string>("FullNameAr", "Employees", type: "nvarchar(200)", maxLength: 200, nullable: false, oldClrType: typeof(string), oldType: "nvarchar(max)");
            migrationBuilder.AlterColumn<string>("EmployeeCode", "Employees", type: "nvarchar(50)", maxLength: 50, nullable: false, oldClrType: typeof(string), oldType: "nvarchar(max)");
            migrationBuilder.AlterColumn<string>("Department", "Employees", type: "nvarchar(100)", maxLength: 100, nullable: false, oldClrType: typeof(string), oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex("IX_Employees_EmployeeCode", "Employees", "EmployeeCode", unique: true);
            migrationBuilder.CreateIndex("IX_Employees_FullNameEn", "Employees", "FullNameEn");
            migrationBuilder.CreateIndex("IX_Employees_FullNameAr", "Employees", "FullNameAr");
            migrationBuilder.CreateIndex("IX_Employees_Department", "Employees", "Department");
            migrationBuilder.CreateIndex("IX_Employees_NationalId", "Employees", "NationalId");

            // AccessCard new fields
            migrationBuilder.AddColumn<string>("CardPassword", "AccessCards", type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<int>("OpenMode", "AccessCards", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<string>("DoorPermissions", "AccessCards", type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "01000000");
            migrationBuilder.AddColumn<int>("EffectiveTimes", "AccessCards", type: "int", nullable: false, defaultValue: 65535);
            migrationBuilder.AddColumn<int>("TimePeriodIndex", "AccessCards", type: "int", nullable: false, defaultValue: 1);
            migrationBuilder.AddColumn<bool>("HolidayEnabled", "AccessCards", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>("IsSyncedToDevice", "AccessCards", type: "bit", nullable: false, defaultValue: false);

            // AccessCard indexes
            migrationBuilder.AlterColumn<string>("CardNumber", "AccessCards", type: "nvarchar(20)", maxLength: 20, nullable: false, oldClrType: typeof(string), oldType: "nvarchar(max)");
            migrationBuilder.AlterColumn<string>("CardType", "AccessCards", type: "nvarchar(30)", maxLength: 30, nullable: false, oldClrType: typeof(string), oldType: "nvarchar(max)");
            migrationBuilder.CreateIndex("IX_AccessCards_CardNumber", "AccessCards", "CardNumber", unique: true);

            // AuditLog table
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex("IX_AuditLogs_Timestamp", "AuditLogs", "Timestamp");
            migrationBuilder.CreateIndex("IX_AuditLogs_EntityType", "AuditLogs", "EntityType");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("AuditLogs");

            migrationBuilder.DropIndex("IX_AccessCards_CardNumber", "AccessCards");
            migrationBuilder.DropColumn("CardPassword", "AccessCards");
            migrationBuilder.DropColumn("OpenMode", "AccessCards");
            migrationBuilder.DropColumn("DoorPermissions", "AccessCards");
            migrationBuilder.DropColumn("EffectiveTimes", "AccessCards");
            migrationBuilder.DropColumn("TimePeriodIndex", "AccessCards");
            migrationBuilder.DropColumn("HolidayEnabled", "AccessCards");
            migrationBuilder.DropColumn("IsSyncedToDevice", "AccessCards");

            migrationBuilder.DropIndex("IX_Employees_EmployeeCode", "Employees");
            migrationBuilder.DropIndex("IX_Employees_FullNameEn", "Employees");
            migrationBuilder.DropIndex("IX_Employees_FullNameAr", "Employees");
            migrationBuilder.DropIndex("IX_Employees_Department", "Employees");
            migrationBuilder.DropIndex("IX_Employees_NationalId", "Employees");
            migrationBuilder.DropColumn("Phone", "Employees");
            migrationBuilder.DropColumn("NationalId", "Employees");
            migrationBuilder.DropColumn("PhotoPath", "Employees");
            migrationBuilder.DropColumn("Height", "Employees");
            migrationBuilder.DropColumn("Weight", "Employees");
            migrationBuilder.DropColumn("Salary", "Employees");
            migrationBuilder.DropColumn("Notes", "Employees");
        }
    }
}
