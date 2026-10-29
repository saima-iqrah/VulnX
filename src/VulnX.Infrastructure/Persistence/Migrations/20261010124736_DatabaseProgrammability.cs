using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VulnX.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Database programmability for PostgreSQL: functions, triggers and stored procedures.
    /// Written by hand (SQL only), so there is no model change and no designer file.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261010124736_DatabaseProgrammability")]
    public partial class DatabaseProgrammability : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------------- FUNCTIONS (return a value, usable inside SELECT) ----------------
            migrationBuilder.Sql("""
CREATE OR REPLACE FUNCTION vulnx_score_from_counts(p_critical integer, p_high integer, p_medium integer, p_low integer)
RETURNS integer
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT GREATEST(0, 100 - 25 * p_critical - 15 * p_high - 7 * p_medium - 3 * p_low);
$$;
""");

            migrationBuilder.Sql("""
CREATE OR REPLACE FUNCTION vulnx_grade(p_score integer)
RETURNS text
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT CASE
        WHEN p_score >= 90 THEN 'A'
        WHEN p_score >= 75 THEN 'B'
        WHEN p_score >= 60 THEN 'C'
        WHEN p_score >= 40 THEN 'D'
        ELSE 'F'
    END;
$$;
""");

            // Same rule as the application: every distinct finding type counts once, with its worst severity.
            migrationBuilder.Sql("""
CREATE OR REPLACE FUNCTION vulnx_scan_score(p_scan_id uuid)
RETURNS integer
LANGUAGE sql
STABLE
AS $$
    SELECT vulnx_score_from_counts(
        (COUNT(*) FILTER (WHERE w.worst = 4))::integer,
        (COUNT(*) FILTER (WHERE w.worst = 3))::integer,
        (COUNT(*) FILTER (WHERE w.worst = 2))::integer,
        (COUNT(*) FILTER (WHERE w.worst = 1))::integer)
    FROM (
        SELECT d."Code",
               MAX(CASE r."Severity" WHEN 'Critical' THEN 4 WHEN 'High' THEN 3 WHEN 'Medium' THEN 2 WHEN 'Low' THEN 1 ELSE 0 END) AS worst
        FROM "ScanResults" r
        JOIN "VulnerabilityDefinitions" d ON d."Id" = r."VulnerabilityDefinitionId"
        WHERE r."ScanId" = p_scan_id
        GROUP BY d."Code"
    ) w;
$$;
""");

            // ---------------- TRIGGERS (run automatically on a table event) ----------------
            migrationBuilder.Sql("""
CREATE OR REPLACE FUNCTION vulnx_trg_scan_status_audit()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO "AuditLogs" ("Id", "UserId", "Action", "EntityType", "EntityId", "SafeMetadataJson", "CreatedAtUtc")
    VALUES (gen_random_uuid(), NEW."UserId", 'db.scan_status_changed', 'Scan', NEW."Id"::text,
            jsonb_build_object('from', OLD."Status", 'to', NEW."Status"), now());
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_scan_status_audit ON "Scans";
CREATE TRIGGER trg_scan_status_audit
AFTER UPDATE OF "Status" ON "Scans"
FOR EACH ROW
WHEN (OLD."Status" IS DISTINCT FROM NEW."Status")
EXECUTE FUNCTION vulnx_trg_scan_status_audit();
""");

            migrationBuilder.Sql("""
CREATE OR REPLACE FUNCTION vulnx_trg_audit_immutable()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'AuditLogs rows are append-only and cannot be modified.' USING ERRCODE = 'restrict_violation';
END;
$$;

DROP TRIGGER IF EXISTS trg_auditlogs_no_update ON "AuditLogs";
CREATE TRIGGER trg_auditlogs_no_update
BEFORE UPDATE ON "AuditLogs"
FOR EACH ROW
EXECUTE FUNCTION vulnx_trg_audit_immutable();
""");

            // ---------------- STORED PROCEDURES (called explicitly with CALL) ----------------
            migrationBuilder.Sql("""
CREATE OR REPLACE PROCEDURE vulnx_recalculate_scores()
LANGUAGE plpgsql
AS $$
DECLARE
    v_updated integer;
BEGIN
    UPDATE "Scans" s
    SET "Score" = vulnx_scan_score(s."Id")
    WHERE s."Status" = 'Completed'
      AND s."Score" IS DISTINCT FROM vulnx_scan_score(s."Id");
    GET DIAGNOSTICS v_updated = ROW_COUNT;

    INSERT INTO "AuditLogs" ("Id", "UserId", "Action", "EntityType", "EntityId", "SafeMetadataJson", "CreatedAtUtc")
    VALUES (gen_random_uuid(), NULL, 'db.scores_recalculated', 'System', NULL,
            jsonb_build_object('updated', v_updated), now());
END;
$$;
""");

            migrationBuilder.Sql("""
CREATE OR REPLACE PROCEDURE vulnx_cleanup_old_data(p_audit_days integer DEFAULT 365, p_notification_days integer DEFAULT 60)
LANGUAGE plpgsql
AS $$
DECLARE
    v_tokens integer;
    v_audits integer;
    v_notes integer;
BEGIN
    DELETE FROM "RefreshTokens"
    WHERE "ExpiresAtUtc" < now() - interval '7 days'
       OR ("RevokedAtUtc" IS NOT NULL AND "RevokedAtUtc" < now() - interval '30 days');
    GET DIAGNOSTICS v_tokens = ROW_COUNT;

    DELETE FROM "AuditLogs" WHERE "CreatedAtUtc" < now() - make_interval(days => p_audit_days);
    GET DIAGNOSTICS v_audits = ROW_COUNT;

    DELETE FROM "Notifications"
    WHERE ("ReadAtUtc" IS NOT NULL AND "ReadAtUtc" < now() - make_interval(days => p_notification_days))
       OR "CreatedAtUtc" < now() - interval '180 days';
    GET DIAGNOSTICS v_notes = ROW_COUNT;

    INSERT INTO "AuditLogs" ("Id", "UserId", "Action", "EntityType", "EntityId", "SafeMetadataJson", "CreatedAtUtc")
    VALUES (gen_random_uuid(), NULL, 'db.cleanup', 'System', NULL,
            jsonb_build_object('refreshTokens', v_tokens, 'auditLogs', v_audits, 'notifications', v_notes), now());
END;
$$;
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DROP TRIGGER IF EXISTS trg_scan_status_audit ON "Scans";
DROP TRIGGER IF EXISTS trg_auditlogs_no_update ON "AuditLogs";
DROP FUNCTION IF EXISTS vulnx_trg_scan_status_audit();
DROP FUNCTION IF EXISTS vulnx_trg_audit_immutable();
DROP PROCEDURE IF EXISTS vulnx_cleanup_old_data(integer, integer);
DROP PROCEDURE IF EXISTS vulnx_recalculate_scores();
DROP FUNCTION IF EXISTS vulnx_scan_score(uuid);
DROP FUNCTION IF EXISTS vulnx_grade(integer);
DROP FUNCTION IF EXISTS vulnx_score_from_counts(integer, integer, integer, integer);
""");
        }
    }
}
