using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GarageManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class FixRelationshipIssuesClean : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Database was updated manually via SQL script
            // - Removed problematic UsersId columns from Appointments and CarMechanicAssignments
            // - Added new receipt functionality columns to MechanicReports and MechanicReportParts
            // - Created MechanicReportLabours and ServiceInspectionItems tables
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Manual rollback would be required if needed
            // This migration represents changes that were applied via SQL script
        }
    }
}