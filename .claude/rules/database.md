---
paths:
  - "**/backend/app/db/migrations/**"
  - "**/migrations/**"
  - "**/migrate/**"
  - "**/db/migrate/**"
---

# Database Migrations — PT Manager

- **Never modify an existing SQL migration.** Create a new numbered file (`NNN_description.sql`) for changes. Existing migrations may have already run in production.
- Migrations run via `python -m app.db.migrate_runner` (standalone, not during HTTP requests).
- Table `schema_migrations` tracks applied migrations — do not bypass.
- Test migrations locally before committing.
- Never seed production data in migration files. Use `backend/app/db/seeds/`.
- Never drop columns or tables without confirming data is no longer needed.
- Add indexes in the same migration that introduces the column when queries will filter on it.
