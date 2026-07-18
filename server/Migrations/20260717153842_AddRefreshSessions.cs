using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RefreshSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TokenFamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SecurityStampHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUsedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReplacedBySessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevocationReason = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshSessions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RefreshSessions_RefreshSessions_ReplacedBySessionId",
                        column: x => x.ReplacedBySessionId,
                        principalTable: "RefreshSessions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshSessions_ReplacedBySessionId",
                table: "RefreshSessions",
                column: "ReplacedBySessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshSessions_TokenFamilyId_ExpiresAtUtc",
                table: "RefreshSessions",
                columns: new[] { "TokenFamilyId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshSessions_TokenHash",
                table: "RefreshSessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshSessions_UserId_TokenFamilyId",
                table: "RefreshSessions",
                columns: new[] { "UserId", "TokenFamilyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshSessions");
        }
    }
}
