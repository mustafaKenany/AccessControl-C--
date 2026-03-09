using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControlPro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletedEmployeeFreezeHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add IsFrozen and FreezeStartDate to Employees
            migrationBuilder.AddColumn<bool>(
                name: "IsFrozen",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FreezeStartDate",
                table: "Employees",
                type: "datetime2",
                nullable: true);

            // Create DeletedEmployees table
            migrationBuilder.CreateTable(
                name: "DeletedEmployees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OriginalId = table.Column<int>(type: "int", nullable: false),
                    FullNameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FullNameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CardNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubscriptionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PhotoData = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    Height = table.Column<double>(type: "float", nullable: false),
                    Weight = table.Column<double>(type: "float", nullable: false),
                    SubscriptionFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DeleteReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OriginalCreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletedEmployees", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeletedEmployees_OriginalId",
                table: "DeletedEmployees",
                column: "OriginalId");

            migrationBuilder.CreateIndex(
                name: "IX_DeletedEmployees_DeletedAt",
                table: "DeletedEmployees",
                column: "DeletedAt");

            // Create FreezeHistories table
            migrationBuilder.CreateTable(
                name: "FreezeHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    FreezeStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FreezeEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FreezeDays = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FreezeHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FreezeHistories_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FreezeHistories_EmployeeId",
                table: "FreezeHistories",
                column: "EmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FreezeHistories");
            migrationBuilder.DropTable(name: "DeletedEmployees");
            migrationBuilder.DropColumn(name: "FreezeStartDate", table: "Employees");
            migrationBuilder.DropColumn(name: "IsFrozen", table: "Employees");
        }
    }
}
