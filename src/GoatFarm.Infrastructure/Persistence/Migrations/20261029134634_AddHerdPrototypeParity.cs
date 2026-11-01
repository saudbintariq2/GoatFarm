using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatFarm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHerdPrototypeParity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: dev DBs may already have these columns from a partial apply or an removed migration.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = current_schema() AND table_name = 'Goats' AND column_name = 'BirthBatchName') THEN
                        ALTER TABLE "Goats" ADD "BirthBatchName" character varying(100) NULL;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = current_schema() AND table_name = 'Goats' AND column_name = 'CustomFeedingGroupId') THEN
                        ALTER TABLE "Goats" ADD "CustomFeedingGroupId" integer NULL;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = current_schema() AND table_name = 'Goats' AND column_name = 'NextCrossDate') THEN
                        ALTER TABLE "Goats" ADD "NextCrossDate" date NULL;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = current_schema() AND table_name = 'Goats' AND column_name = 'WeanDate') THEN
                        ALTER TABLE "Goats" ADD "WeanDate" date NULL;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = current_schema() AND table_name = 'Goats' AND column_name = 'YoungClass') THEN
                        ALTER TABLE "Goats" ADD "YoungClass" integer NOT NULL DEFAULT 0;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = current_schema() AND table_name = 'Goats' AND column_name = 'YoungClassDate') THEN
                        ALTER TABLE "Goats" ADD "YoungClassDate" date NULL;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Goats_BirthBatchName" ON "Goats" ("BirthBatchName");
                CREATE INDEX IF NOT EXISTS "IX_Goats_CustomFeedingGroupId" ON "Goats" ("CustomFeedingGroupId");
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'FK_Goats_CustomFeedingGroups_CustomFeedingGroupId') THEN
                        ALTER TABLE "Goats"
                            ADD CONSTRAINT "FK_Goats_CustomFeedingGroups_CustomFeedingGroupId"
                            FOREIGN KEY ("CustomFeedingGroupId") REFERENCES "CustomFeedingGroups" ("Id") ON DELETE SET NULL;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Goats_CustomFeedingGroups_CustomFeedingGroupId",
                table: "Goats");

            migrationBuilder.DropIndex(
                name: "IX_Goats_BirthBatchName",
                table: "Goats");

            migrationBuilder.DropIndex(
                name: "IX_Goats_CustomFeedingGroupId",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "BirthBatchName",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "CustomFeedingGroupId",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "NextCrossDate",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "WeanDate",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "YoungClass",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "YoungClassDate",
                table: "Goats");
        }
    }
}
