using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentService.Migrations
{
    /// <inheritdoc />
    public partial class Changes1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PaymentIdempotency",
                table: "PaymentIdempotency");

            migrationBuilder.DropIndex(
                name: "IX_PaymentIdempotency_Key",
                table: "PaymentIdempotency");

            migrationBuilder.DropColumn(
                name: "Error",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "PaymentIdempotency");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "PaymentIdempotency");

            migrationBuilder.DropColumn(
                name: "Key",
                table: "PaymentIdempotency");

            migrationBuilder.DropColumn(
                name: "ResponseCode",
                table: "PaymentIdempotency");

            migrationBuilder.RenameColumn(
                name: "ProcessedAtUtc",
                table: "Payments",
                newName: "CompletedAtUtc");

            migrationBuilder.RenameColumn(
                name: "ResponseBodyJson",
                table: "PaymentIdempotency",
                newName: "ResultError");

            migrationBuilder.AlterColumn<byte>(
                name: "Status",
                table: "Payments",
                type: "tinyint",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Payments",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "Payments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fingerprint",
                table: "Payments",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Payments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<byte>(
                name: "Status",
                table: "PaymentIdempotency",
                type: "tinyint",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<Guid>(
                name: "IdempotencyKey",
                table: "PaymentIdempotency",
                type: "uniqueidentifier",
                maxLength: 64,
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentId",
                table: "PaymentIdempotency",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "ResultStatus",
                table: "PaymentIdempotency",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PaymentIdempotency",
                table: "PaymentIdempotency",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderId",
                table: "Payments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PaymentIdempotency",
                table: "PaymentIdempotency");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Fingerprint",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "PaymentIdempotency");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                table: "PaymentIdempotency");

            migrationBuilder.DropColumn(
                name: "ResultStatus",
                table: "PaymentIdempotency");

            migrationBuilder.RenameColumn(
                name: "CompletedAtUtc",
                table: "Payments",
                newName: "ProcessedAtUtc");

            migrationBuilder.RenameColumn(
                name: "ResultError",
                table: "PaymentIdempotency",
                newName: "ResponseBodyJson");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Payments",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "tinyint");

            migrationBuilder.AddColumn<string>(
                name: "Error",
                table: "Payments",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "PaymentIdempotency",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "tinyint");

            migrationBuilder.AddColumn<long>(
                name: "Id",
                table: "PaymentIdempotency",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "PaymentIdempotency",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Key",
                table: "PaymentIdempotency",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ResponseCode",
                table: "PaymentIdempotency",
                type: "int",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PaymentIdempotency",
                table: "PaymentIdempotency",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIdempotency_Key",
                table: "PaymentIdempotency",
                column: "Key",
                unique: true);
        }
    }
}
