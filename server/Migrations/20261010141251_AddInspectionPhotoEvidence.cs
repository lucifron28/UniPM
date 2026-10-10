using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInspectionPhotoEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InspectionPhotoEvidence",
                columns: table => new
                {
                    InspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LengthBytes = table.Column<int>(type: "int", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionPhotoEvidence", x => x.InspectionId);
                    table.CheckConstraint("CK_InspectionPhotoEvidence_LengthBytes_Positive", "[LengthBytes] > 0");
                    table.CheckConstraint("CK_InspectionPhotoEvidence_Revision_Positive", "[Revision] > 0");
                    table.ForeignKey(
                        name: "FK_InspectionPhotoEvidence_InspectionRecords_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "InspectionRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InspectionPhotoEvidence");
        }
    }
}
