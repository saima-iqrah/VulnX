-- Run once as the PostgreSQL superuser (pgAdmin Query Tool or: psql -U postgres -f docs/setup-database.sql)
-- To use another database name, change vulnx_db here AND in the connection string (Database=...).
CREATE USER vulnx_app WITH PASSWORD 'YOUR_DB_PASSWORD';
CREATE DATABASE vulnx_db OWNER vulnx_app;
