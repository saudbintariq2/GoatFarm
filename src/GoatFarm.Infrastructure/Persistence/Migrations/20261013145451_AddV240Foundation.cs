using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GoatFarm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddV240Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BreedingStage",
                table: "Goats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DamId",
                table: "Goats",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DamTagText",
                table: "Goats",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HealthState",
                table: "Goats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsTemporaryTag",
                table: "Goats",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "KiddingRecordId",
                table: "Goats",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Owner",
                table: "Goats",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SireId",
                table: "Goats",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SireTagText",
                table: "Goats",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BreedingEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DoeId = table.Column<int>(type: "integer", nullable: false),
                    BuckId = table.Column<int>(type: "integer", nullable: true),
                    BuckTagText = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MatedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ScanDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ScanKids = table.Column<int>(type: "integer", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    OutcomeDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BreedingEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BreedingEvents_Goats_BuckId",
                        column: x => x.BuckId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BreedingEvents_Goats_DoeId",
                        column: x => x.DoeId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FarmTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AssigneeUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    GoatId = table.Column<int>(type: "integer", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarmTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FarmTasks_AspNetUsers_AssigneeUserId",
                        column: x => x.AssigneeUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FarmTasks_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FarmTasks_Goats_GoatId",
                        column: x => x.GoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "KiddingRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DoeId = table.Column<int>(type: "integer", nullable: false),
                    BreedingEventId = table.Column<int>(type: "integer", nullable: true),
                    KiddingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LiveKids = table.Column<int>(type: "integer", nullable: false),
                    Stillborn = table.Column<int>(type: "integer", nullable: false),
                    Ease = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KiddingRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KiddingRecords_BreedingEvents_BreedingEventId",
                        column: x => x.BreedingEventId,
                        principalTable: "BreedingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KiddingRecords_Goats_DoeId",
                        column: x => x.DoeId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Goats_DamId",
                table: "Goats",
                column: "DamId");

            migrationBuilder.CreateIndex(
                name: "IX_Goats_KiddingRecordId",
                table: "Goats",
                column: "KiddingRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_Goats_SireId",
                table: "Goats",
                column: "SireId");

            migrationBuilder.CreateIndex(
                name: "IX_BreedingEvents_BuckId",
                table: "BreedingEvents",
                column: "BuckId");

            migrationBuilder.CreateIndex(
                name: "IX_BreedingEvents_DoeId_Outcome",
                table: "BreedingEvents",
                columns: new[] { "DoeId", "Outcome" });

            migrationBuilder.CreateIndex(
                name: "IX_BreedingEvents_MatedDate",
                table: "BreedingEvents",
                column: "MatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_FarmTasks_AssigneeUserId",
                table: "FarmTasks",
                column: "AssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FarmTasks_CreatedByUserId",
                table: "FarmTasks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FarmTasks_GoatId",
                table: "FarmTasks",
                column: "GoatId");

            migrationBuilder.CreateIndex(
                name: "IX_FarmTasks_Status_DueDate",
                table: "FarmTasks",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_KiddingRecords_BreedingEventId",
                table: "KiddingRecords",
                column: "BreedingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_KiddingRecords_ClientRequestId",
                table: "KiddingRecords",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KiddingRecords_DoeId",
                table: "KiddingRecords",
                column: "DoeId");

            migrationBuilder.CreateIndex(
                name: "IX_KiddingRecords_KiddingDate",
                table: "KiddingRecords",
                column: "KiddingDate");

            migrationBuilder.AddForeignKey(
                name: "FK_Goats_Goats_DamId",
                table: "Goats",
                column: "DamId",
                principalTable: "Goats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Goats_Goats_SireId",
                table: "Goats",
                column: "SireId",
                principalTable: "Goats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Goats_KiddingRecords_KiddingRecordId",
                table: "Goats",
                column: "KiddingRecordId",
                principalTable: "KiddingRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // BreedingStage: 0 None, 1 Ready, 2 ScanPending, 3 ConfirmedPregnant.
            migrationBuilder.Sql("""
                UPDATE "Goats" SET "BreedingStage" = CASE
                    WHEN "MatedDate" IS NOT NULL AND "KidsCount" > 0 THEN 3
                    WHEN "MatedDate" IS NOT NULL THEN 2
                    WHEN "PrepCrossDate" IS NOT NULL THEN 1
                    ELSE 0 END;
                """);

            // One open history row per doe currently crossed. Outcome: 0 Open, 1 ConfirmedPregnant. Gender 1 = Male.
            migrationBuilder.Sql("""
                INSERT INTO "BreedingEvents"
                    ("DoeId", "BuckId", "BuckTagText", "MatedDate", "ScanDate", "ScanKids", "Outcome", "OutcomeDate", "Notes", "CreatedDate", "UpdatedDate")
                SELECT g."Id",
                       (SELECT b."Id" FROM "Goats" b
                         WHERE g."BuckTag" IS NOT NULL AND lower(b."Tag") = lower(g."BuckTag") AND b."Gender" = 1
                         ORDER BY b."IsArchived", b."Id" LIMIT 1),
                       g."BuckTag",
                       g."MatedDate",
                       CASE WHEN g."KidsCount" > 0 THEN g."UltrasoundDate" END,
                       CASE WHEN g."KidsCount" > 0 THEN g."KidsCount" END,
                       CASE WHEN g."KidsCount" > 0 THEN 1 ELSE 0 END,
                       NULL,
                       'migrated from current cycle',
                       now(), now()
                FROM "Goats" g
                WHERE g."MatedDate" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM "BreedingEvents" e WHERE e."DoeId" = g."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Goats_Goats_DamId",
                table: "Goats");

            migrationBuilder.DropForeignKey(
                name: "FK_Goats_Goats_SireId",
                table: "Goats");

            migrationBuilder.DropForeignKey(
                name: "FK_Goats_KiddingRecords_KiddingRecordId",
                table: "Goats");

            migrationBuilder.DropTable(
                name: "FarmTasks");

            migrationBuilder.DropTable(
                name: "KiddingRecords");

            migrationBuilder.DropTable(
                name: "BreedingEvents");

            migrationBuilder.DropIndex(
                name: "IX_Goats_DamId",
                table: "Goats");

            migrationBuilder.DropIndex(
                name: "IX_Goats_KiddingRecordId",
                table: "Goats");

            migrationBuilder.DropIndex(
                name: "IX_Goats_SireId",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "BreedingStage",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "DamId",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "DamTagText",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "HealthState",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "IsTemporaryTag",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "KiddingRecordId",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "Owner",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "SireId",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "SireTagText",
                table: "Goats");
        }
    }
}
