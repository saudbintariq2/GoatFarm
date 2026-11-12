using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatFarm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKidBreedingParityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = current_schema()
                          AND table_name = 'Goats'
                          AND column_name = 'KidHealthTag') THEN
                        ALTER TABLE "Goats" ADD COLUMN "KidHealthTag" integer NOT NULL DEFAULT 0;
                    END IF;
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = current_schema()
                          AND table_name = 'Goats'
                          AND column_name = 'KidMilkType') THEN
                        ALTER TABLE "Goats" ADD COLUMN "KidMilkType" integer NOT NULL DEFAULT 0;
                    END IF;
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = current_schema()
                          AND table_name = 'Goats'
                          AND column_name = 'WeanMethod') THEN
                        ALTER TABLE "Goats" ADD COLUMN "WeanMethod" character varying(20) NULL;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "KidHealthTag", table: "Goats");
            migrationBuilder.DropColumn(name: "KidMilkType", table: "Goats");
            migrationBuilder.DropColumn(name: "WeanMethod", table: "Goats");
        }
    }
}
