START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014084854_AddGoatDateOfBirth') THEN
    ALTER TABLE "Goats" ADD "DateOfBirth" date;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014084854_AddGoatDateOfBirth') THEN
    ALTER TABLE "Goats" ADD "DateOfBirthIsEstimated" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014084854_AddGoatDateOfBirth') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261014084854_AddGoatDateOfBirth', '9.0.20');
    END IF;
END $EF$;
COMMIT;

