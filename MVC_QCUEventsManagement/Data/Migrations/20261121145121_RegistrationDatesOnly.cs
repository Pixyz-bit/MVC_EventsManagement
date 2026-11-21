using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MVC_QCUEventsManagement.Data.Migrations;

/// <inheritdoc />
public partial class _20261121145121_RegistrationDatesOnly : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_EventsTable_Schedule",
            schema: "dbo",
            table: "EventsTable");

        migrationBuilder.AlterColumn<DateOnly>(
            name: "RegStart",
            schema: "dbo",
            table: "EventsTable",
            type: "date",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "datetime");

        migrationBuilder.AlterColumn<DateOnly>(
            name: "RegEnd",
            schema: "dbo",
            table: "EventsTable",
            type: "date",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "datetime");

        migrationBuilder.AddCheckConstraint(
            name: "CK_EventsTable_Schedule",
            schema: "dbo",
            table: "EventsTable",
            sql: "[EventStart] < [EventEnd] AND [RegStart] <= [RegEnd] AND [RegStart] < CONVERT(date, [EventStart]) AND [RegEnd] < CONVERT(date, [EventStart])");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_EventsTable_Schedule",
            schema: "dbo",
            table: "EventsTable");

        migrationBuilder.AlterColumn<DateTime>(
            name: "RegStart",
            schema: "dbo",
            table: "EventsTable",
            type: "datetime",
            nullable: false,
            oldClrType: typeof(DateOnly),
            oldType: "date");

        migrationBuilder.AlterColumn<DateTime>(
            name: "RegEnd",
            schema: "dbo",
            table: "EventsTable",
            type: "datetime",
            nullable: false,
            oldClrType: typeof(DateOnly),
            oldType: "date");

        migrationBuilder.AddCheckConstraint(
            name: "CK_EventsTable_Schedule",
            schema: "dbo",
            table: "EventsTable",
            sql: "[EventStart] < [EventEnd] AND [RegStart] < [RegEnd] AND CONVERT(date, [RegStart]) < CONVERT(date, [EventStart]) AND CONVERT(date, [RegEnd]) < CONVERT(date, [EventStart])");
    }
}
