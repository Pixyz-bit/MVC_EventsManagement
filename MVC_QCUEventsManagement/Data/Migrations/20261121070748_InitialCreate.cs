using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MVC_QCUEventsManagement.Data.Migrations;

/// <inheritdoc />
public partial class _20261121070748_InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "dbo");

        migrationBuilder.CreateTable(
            name: "UserTable",
            schema: "dbo",
            columns: table => new
            {
                UserId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                PasswordHash = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: false),
                PasswordSalt = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                Role = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserTable", x => x.UserId);
                table.CheckConstraint("CK_UserTable_Role", "([Role] COLLATE Latin1_General_100_BIN2 = 'Admin' AND DATALENGTH([Role]) = 5) OR ([Role] COLLATE Latin1_General_100_BIN2 = 'Student' AND DATALENGTH([Role]) = 7)");
            });

        migrationBuilder.CreateTable(
            name: "EventsTable",
            schema: "dbo",
            columns: table => new
            {
                EventId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                VenueLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                MaxCapacity = table.Column<int>(type: "int", nullable: false),
                CurrentRegistrations = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                EventStart = table.Column<DateTime>(type: "datetime", nullable: false),
                EventEnd = table.Column<DateTime>(type: "datetime", nullable: false),
                RegStart = table.Column<DateTime>(type: "datetime", nullable: false),
                RegEnd = table.Column<DateTime>(type: "datetime", nullable: false),
                Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false, defaultValue: "Upcoming"),
                CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                TargetBranch = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                TargetDepartment = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                TargetProgram = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                TargetYearLevel = table.Column<int>(type: "int", nullable: true),
                EventPhotoPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EventsTable", x => x.EventId);
                table.CheckConstraint("CK_EventsTable_CancellationReason", "[Status] <> 'Cancelled' OR LEN(LTRIM(RTRIM([CancellationReason]))) > 0 AND [CancellationReason] IS NOT NULL");
                table.CheckConstraint("CK_EventsTable_Capacity", "[MaxCapacity] > 0 AND [CurrentRegistrations] >= 0 AND [CurrentRegistrations] <= [MaxCapacity]");
                table.CheckConstraint("CK_EventsTable_Schedule", "[EventStart] < [EventEnd] AND [RegStart] < [RegEnd] AND CONVERT(date, [RegStart]) < CONVERT(date, [EventStart]) AND CONVERT(date, [RegEnd]) < CONVERT(date, [EventStart])");
                table.CheckConstraint("CK_EventsTable_Status", "([Status] COLLATE Latin1_General_100_BIN2 = 'Upcoming' AND DATALENGTH([Status]) = 8) OR ([Status] COLLATE Latin1_General_100_BIN2 = 'Cancelled' AND DATALENGTH([Status]) = 9) OR ([Status] COLLATE Latin1_General_100_BIN2 = 'Completed' AND DATALENGTH([Status]) = 9)");
                table.CheckConstraint("CK_EventsTable_TargetYearLevel", "[TargetYearLevel] IS NULL OR [TargetYearLevel] BETWEEN 1 AND 5");
                table.ForeignKey(
                    name: "FK_EventsTable_UserTable",
                    column: x => x.CreatedByUserId,
                    principalSchema: "dbo",
                    principalTable: "UserTable",
                    principalColumn: "UserId");
            });

        migrationBuilder.CreateTable(
            name: "StudentTable",
            schema: "dbo",
            columns: table => new
            {
                StudentId = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                MiddleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Gender = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                CampusBranch = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Program = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                UserId = table.Column<int>(type: "int", nullable: false),
                BirthDate = table.Column<DateOnly>(type: "date", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StudentTable", x => x.StudentId);
                table.ForeignKey(
                    name: "FK_StudentTable_UserTable",
                    column: x => x.UserId,
                    principalSchema: "dbo",
                    principalTable: "UserTable",
                    principalColumn: "UserId");
            });

        migrationBuilder.CreateTable(
            name: "SponsorListTable",
            schema: "dbo",
            columns: table => new
            {
                SponsorEntryId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                SponsorName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false),
                EventId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SponsorListTable", x => x.SponsorEntryId);
                table.ForeignKey(
                    name: "FK_SponsorListTable_EventsTable",
                    column: x => x.EventId,
                    principalSchema: "dbo",
                    principalTable: "EventsTable",
                    principalColumn: "EventId");
            });

        migrationBuilder.CreateTable(
            name: "EventRegistrationTable",
            schema: "dbo",
            columns: table => new
            {
                EventRegistrationId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                EventId = table.Column<int>(type: "int", nullable: false),
                StudentId = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                CurrentYearLvl = table.Column<int>(type: "int", nullable: false),
                CurrentSection = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false, defaultValue: "NoShow"),
                CheckInTimestamp = table.Column<DateTime>(type: "datetime", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EventRegistrationTable", x => x.EventRegistrationId);
                table.CheckConstraint("CK_EventRegistrationTable_CheckIn", "([Status] = 'NoShow' AND [CheckInTimestamp] IS NULL) OR ([Status] = 'Present' AND [CheckInTimestamp] IS NOT NULL) OR [Status] = 'Cancelled'");
                table.CheckConstraint("CK_EventRegistrationTable_Section", "LEN(LTRIM(RTRIM([CurrentSection]))) > 0");
                table.CheckConstraint("CK_EventRegistrationTable_Status", "([Status] COLLATE Latin1_General_100_BIN2 = 'NoShow' AND DATALENGTH([Status]) = 6) OR ([Status] COLLATE Latin1_General_100_BIN2 = 'Present' AND DATALENGTH([Status]) = 7) OR ([Status] COLLATE Latin1_General_100_BIN2 = 'Cancelled' AND DATALENGTH([Status]) = 9)");
                table.CheckConstraint("CK_EventRegistrationTable_Year", "[CurrentYearLvl] BETWEEN 1 AND 5");
                table.ForeignKey(
                    name: "FK_EventRegistrationTable_EventsTable",
                    column: x => x.EventId,
                    principalSchema: "dbo",
                    principalTable: "EventsTable",
                    principalColumn: "EventId");
                table.ForeignKey(
                    name: "FK_EventRegistrationTable_StudentTable",
                    column: x => x.StudentId,
                    principalSchema: "dbo",
                    principalTable: "StudentTable",
                    principalColumn: "StudentId");
            });

        migrationBuilder.CreateIndex(
            name: "IX_EventRegistrationTable_StudentId",
            schema: "dbo",
            table: "EventRegistrationTable",
            column: "StudentId");

        migrationBuilder.CreateIndex(
            name: "UX_EventRegistrationTable_ActiveStudentEvent",
            schema: "dbo",
            table: "EventRegistrationTable",
            columns: new[] { "EventId", "StudentId" },
            unique: true,
            filter: "[Status] <> 'Cancelled'");

        migrationBuilder.CreateIndex(
            name: "IX_EventsTable_CreatedByUserId",
            schema: "dbo",
            table: "EventsTable",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_SponsorListTable_EventId",
            schema: "dbo",
            table: "SponsorListTable",
            column: "EventId");

        migrationBuilder.CreateIndex(
            name: "UX_StudentTable_UserId",
            schema: "dbo",
            table: "StudentTable",
            column: "UserId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_UserTable_Email",
            schema: "dbo",
            table: "UserTable",
            column: "Email",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EventRegistrationTable",
            schema: "dbo");

        migrationBuilder.DropTable(
            name: "SponsorListTable",
            schema: "dbo");

        migrationBuilder.DropTable(
            name: "StudentTable",
            schema: "dbo");

        migrationBuilder.DropTable(
            name: "EventsTable",
            schema: "dbo");

        migrationBuilder.DropTable(
            name: "UserTable",
            schema: "dbo");
    }
}
