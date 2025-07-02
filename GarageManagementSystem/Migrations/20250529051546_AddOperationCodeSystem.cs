using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GarageManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationCodeSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OperationCodeParts_OperationCodes_OperationCodeId1",
                table: "OperationCodeParts");

            migrationBuilder.DropIndex(
                name: "IX_OperationCodeParts_OperationCodeId1",
                table: "OperationCodeParts");

            migrationBuilder.DropColumn(
                name: "OperationCodeId1",
                table: "OperationCodeParts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OperationCodeId1",
                table: "OperationCodeParts",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "OperationCodeParts",
                keyColumn: "Id",
                keyValue: 1,
                column: "OperationCodeId1",
                value: null);

            migrationBuilder.UpdateData(
                table: "OperationCodeParts",
                keyColumn: "Id",
                keyValue: 2,
                column: "OperationCodeId1",
                value: null);

            migrationBuilder.UpdateData(
                table: "OperationCodeParts",
                keyColumn: "Id",
                keyValue: 3,
                column: "OperationCodeId1",
                value: null);

            migrationBuilder.UpdateData(
                table: "OperationCodeParts",
                keyColumn: "Id",
                keyValue: 4,
                column: "OperationCodeId1",
                value: null);

            migrationBuilder.UpdateData(
                table: "OperationCodeParts",
                keyColumn: "Id",
                keyValue: 5,
                column: "OperationCodeId1",
                value: null);

            migrationBuilder.UpdateData(
                table: "OperationCodeParts",
                keyColumn: "Id",
                keyValue: 6,
                column: "OperationCodeId1",
                value: null);

            migrationBuilder.UpdateData(
                table: "OperationCodeParts",
                keyColumn: "Id",
                keyValue: 7,
                column: "OperationCodeId1",
                value: null);

            migrationBuilder.UpdateData(
                table: "OperationCodeParts",
                keyColumn: "Id",
                keyValue: 8,
                column: "OperationCodeId1",
                value: null);

            migrationBuilder.UpdateData(
                table: "OperationCodeParts",
                keyColumn: "Id",
                keyValue: 9,
                column: "OperationCodeId1",
                value: null);

            migrationBuilder.UpdateData(
                table: "OperationCodeParts",
                keyColumn: "Id",
                keyValue: 10,
                column: "OperationCodeId1",
                value: null);

            migrationBuilder.UpdateData(
                table: "OperationCodeParts",
                keyColumn: "Id",
                keyValue: 11,
                column: "OperationCodeId1",
                value: null);

            migrationBuilder.UpdateData(
                table: "OperationCodeParts",
                keyColumn: "Id",
                keyValue: 12,
                column: "OperationCodeId1",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_OperationCodeParts_OperationCodeId1",
                table: "OperationCodeParts",
                column: "OperationCodeId1");

            migrationBuilder.AddForeignKey(
                name: "FK_OperationCodeParts_OperationCodes_OperationCodeId1",
                table: "OperationCodeParts",
                column: "OperationCodeId1",
                principalTable: "OperationCodes",
                principalColumn: "Id");
        }
    }
}
