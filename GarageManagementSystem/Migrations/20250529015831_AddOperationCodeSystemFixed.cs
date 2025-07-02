using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GarageManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationCodeSystemFixed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperationCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PartName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PartDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceParts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationCodeParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationCodeId = table.Column<int>(type: "int", nullable: false),
                    ServicePartId = table.Column<int>(type: "int", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    AssignedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OperationCodeId1 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationCodeParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationCodeParts_OperationCodes_OperationCodeId",
                        column: x => x.OperationCodeId,
                        principalTable: "OperationCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperationCodeParts_OperationCodes_OperationCodeId1",
                        column: x => x.OperationCodeId1,
                        principalTable: "OperationCodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OperationCodeParts_ServiceParts_ServicePartId",
                        column: x => x.ServicePartId,
                        principalTable: "ServiceParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "OperationCodes",
                columns: new[] { "Id", "Code", "CreatedDate", "Description", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, "FLRS10", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Standard oil change and filter replacement service", true, "Oil Change Service" },
                    { 2, "PART1", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "General automotive parts and accessories", true, "General Parts" },
                    { 3, "BRKS20", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Brake system maintenance and parts", true, "Brake Service" },
                    { 4, "TIRE30", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Tire installation, balancing, and wheel services", true, "Tire Service" },
                    { 5, "BATRY40", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Battery replacement and electrical system parts", true, "Battery Service" }
                });

            migrationBuilder.InsertData(
                table: "ServiceParts",
                columns: new[] { "Id", "CreatedDate", "IsAvailable", "LastUpdated", "PartDescription", "PartName", "PartNumber", "Price" },
                values: new object[,]
                {
                    { 1, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, "Standard oil filter element", "ELEMENT S/A OIL FILTER", "15601-P2A12", 11.90m },
                    { 2, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, "Full synthetic motor oil 3 liters", "PEO FULL-SYN 0W-20 API SN -3L", "70010105", 140.50m },
                    { 3, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, "Oil drain plug gasket", "GASKET", "90044-30281", 3.80m },
                    { 4, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, "Small battery terminal protector spray", "BATTERY TERMINAL PROTECTOR (SMALL)", "999-40011-00000", 3.80m },
                    { 5, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, "Windshield washer fluid concentrate", "WINDSHIELD WASHER (30ML)", "999-50001-00000", 1.70m },
                    { 6, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, "Fuel injector cleaning solution", "INJECTOR CLEANER 95ML", "999-53100-00001", 24.00m },
                    { 7, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, "Front brake pad set - ceramic", "BRAKE PAD SET FRONT", "43022-S5A-000", 85.00m },
                    { 8, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, "Rear brake pad set - ceramic", "BRAKE PAD SET REAR", "43022-S5A-013", 65.00m },
                    { 9, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, "All-season tire 205/55R16", "TIRE 205/55R16", "TYR-205-55-16", 180.00m },
                    { 10, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, "Performance tire 225/45R17", "TIRE 225/45R17", "TYR-225-45-17", 220.00m },
                    { 11, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, "12V automotive battery 55Ah", "BATTERY 55D23L 12V", "BAT-55D23L", 180.00m },
                    { 12, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, "12V automotive battery 75Ah", "BATTERY 75D23L 12V", "BAT-75D23L", 220.00m }
                });

            migrationBuilder.InsertData(
                table: "OperationCodeParts",
                columns: new[] { "Id", "AssignedDate", "IsDefault", "OperationCodeId", "OperationCodeId1", "ServicePartId" },
                values: new object[,]
                {
                    { 1, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, 1, null, 1 },
                    { 2, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, 1, null, 2 },
                    { 3, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, 1, null, 3 },
                    { 4, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, 2, null, 4 },
                    { 5, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, 2, null, 5 },
                    { 6, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, 2, null, 6 },
                    { 7, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, 3, null, 7 },
                    { 8, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, 3, null, 8 },
                    { 9, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, 4, null, 9 },
                    { 10, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, 4, null, 10 },
                    { 11, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, 5, null, 11 },
                    { 12, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, 5, null, 12 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationCodeParts_OperationCodeId_ServicePartId",
                table: "OperationCodeParts",
                columns: new[] { "OperationCodeId", "ServicePartId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationCodeParts_OperationCodeId1",
                table: "OperationCodeParts",
                column: "OperationCodeId1");

            migrationBuilder.CreateIndex(
                name: "IX_OperationCodeParts_ServicePartId",
                table: "OperationCodeParts",
                column: "ServicePartId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationCodes_Code",
                table: "OperationCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceParts_PartNumber",
                table: "ServiceParts",
                column: "PartNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationCodeParts");

            migrationBuilder.DropTable(
                name: "OperationCodes");

            migrationBuilder.DropTable(
                name: "ServiceParts");
        }
    }
}
