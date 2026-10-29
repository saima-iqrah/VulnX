-- =====================================================================
-- Part 1: the objects used inside VulnX (run against the "vulnx_db" database)
-- =====================================================================

-- FUNCTION: the database calculates the score and grade of every completed scan
SELECT s."Id",
       s."Score"                                   AS stored_score,
       vulnx_scan_score(s."Id")                    AS calculated_score,
       vulnx_grade(vulnx_scan_score(s."Id"))       AS grade
FROM "Scans" s
WHERE s."Status" = 'Completed';

-- TRIGGER: every status change of a scan was written to the audit log automatically
SELECT "CreatedAtUtc", "EntityId", "SafeMetadataJson"
FROM "AuditLogs"
WHERE "Action" = 'db.scan_status_changed'
ORDER BY "CreatedAtUtc" DESC
LIMIT 20;

-- TRIGGER: audit logs are append-only. This statement fails with "AuditLogs rows are append-only...":
-- UPDATE "AuditLogs" SET "Action" = 'x';

-- STORED PROCEDURES: batch operations started explicitly
CALL vulnx_recalculate_scores();
CALL vulnx_cleanup_old_data(365, 60);

-- Everything installed (triggers, functions, procedures)
SELECT 'Trigger' AS kind, t.tgname::text AS name, 'on ' || c.relname::text AS detail
FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid
WHERE NOT t.tgisinternal AND t.tgname LIKE 'trg\_%'
UNION ALL
SELECT CASE p.prokind WHEN 'p' THEN 'Procedure' ELSE 'Function' END, p.proname::text, pg_get_function_arguments(p.oid)
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname = 'public' AND p.proname LIKE 'vulnx\_%'
ORDER BY 1, 2;

-- =====================================================================
-- Part 2: the classic employees example from the course PDF, in PostgreSQL syntax
-- (kept in its own schema "demo" so it never touches the VulnX tables)
-- =====================================================================
CREATE SCHEMA IF NOT EXISTS demo;

CREATE TABLE IF NOT EXISTS demo.employees (
    emp_id        serial PRIMARY KEY,
    name          text NOT NULL,
    salary        numeric(10,2) NOT NULL,
    department_id integer NOT NULL
);

CREATE TABLE IF NOT EXISTS demo.salary_log (
    log_id     serial PRIMARY KEY,
    emp_id     integer NOT NULL,
    old_salary numeric(10,2),
    new_salary numeric(10,2),
    changed_on timestamptz NOT NULL DEFAULT now()
);

-- Trigger: log every salary change (PostgreSQL needs a trigger function plus CREATE TRIGGER)
CREATE OR REPLACE FUNCTION demo.trg_salary_update_fn()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO demo.salary_log (emp_id, old_salary, new_salary)
    VALUES (OLD.emp_id, OLD.salary, NEW.salary);
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_salary_update ON demo.employees;
CREATE TRIGGER trg_salary_update
AFTER UPDATE OF salary ON demo.employees
FOR EACH ROW
WHEN (OLD.salary IS DISTINCT FROM NEW.salary)
EXECUTE FUNCTION demo.trg_salary_update_fn();

-- Function: annual salary
CREATE OR REPLACE FUNCTION demo.annual_salary(monthly numeric)
RETURNS numeric
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT monthly * 12;
$$;

-- Stored procedure: give a raise to a department
CREATE OR REPLACE PROCEDURE demo.give_raise(IN p_dept_id integer, IN p_percent numeric)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE demo.employees
    SET salary = salary + (salary * p_percent / 100)
    WHERE department_id = p_dept_id;
END;
$$;

-- Try it
INSERT INTO demo.employees (name, salary, department_id)
VALUES ('Rahim', 1000, 3), ('Karim', 2000, 3), ('Salma', 1500, 4);

SELECT name, demo.annual_salary(salary) AS yearly FROM demo.employees;   -- function inside SELECT
CALL demo.give_raise(3, 10);                                             -- stored procedure
SELECT * FROM demo.salary_log;                                           -- rows added by the trigger
