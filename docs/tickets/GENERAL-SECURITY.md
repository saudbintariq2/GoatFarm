# General & security tickets (Sandeel PDF § General and security)

Track every **G\*** item from the Developer Fix List. Update **Status** when verified on staging/production.

| Ticket | PDF | Priority | Status | Implementation / notes |
|--------|-----|----------|--------|-------------------------|
| **GS-001** | G6 | High | **Code done** · **Ops required** | Dev seed copy removed from Settings/AccessDenied. Login shows dev accounts **only in Development**. `Seed:CreateDefaultAdmin` — production default `false` skips re-creating seed admin when users exist. **Ops:** Create owner account, disable/delete `admin@goatfarm.local`. |
| **GS-002** | G3 | High | **Code done** · **Config required** | Scheduled JSON backups (`ScheduledBackupHostedService`), Backup Center list, admin-only restore, pre-restore snapshot. **CSV exports** on Backup Center (herd, finance, feed, health, breeding, tasks) — `BackupCenter/ExportCsv`. **Config:** `Backup:AutomaticEnabled` (true in `appsettings.Production.json`). **Ops:** Test restore on staging. |
| **GS-003** | G1 | High | **Partial** | Default `ApplicationSettings:FarmName` = **Sandeel Farms**; Sandeel palette in `farm-pro.css` / `goat-records.css`. **Open:** Replace `goat-logo.png` with official Sandeel logo kit assets; rust/brown accents on more components if design review asks. |
| **GS-004** | G5 | Medium | **Partial** | Top bar: **Online/Offline** + `DeviceCode`. `FarmRecordNumberFormat` for day-based refs and **`SF-yyyyMM-DEV-NNNN`** product sales. **Open:** trading bill auto-number; multi-device offline sync. |
| **GS-005** | G4 | Medium | **Code done** · **Config** | `Features:AssistantEnabled` true by default; sidebar + **header “Assistant”** opens panel; mic/speak in panel; `[Authorize]` on `AssistantController`. **Ops:** Set OpenAI key in production secrets; confirm `/Assistant/Chat` rejects anonymous (401). |
| **GS-006** | G2 | Low | **Done** | Top bar: user **name**, role, **Sign out**; optional `ApplicationSettings:FarmLocation` under clock. |
| **GS-007** | G7 | Low | **Done** | Sidebar labels: **Finance**, **Health**, **Feed**, **Admin** (V242 names). |
| **GS-008** | — | Medium | **Done** | **DEF-24:** “Keep me signed in” — session cookie when unchecked; 14-day persistent only when checked (`AccountController` + lockout on failed attempts). |
| **GS-009** | A3 (related) | Low | **Done** | Admin home links to **Settings →** for users & roles (users remain in Settings; Admin is entry point). |
| **GS-010** | A5 (related) | Low | **Open** | Header bell still routes to **Tasks**, not a unified notifications list (PDF A5). |

## Verification checklist (per ticket)

- **GS-001:** Production login as owner; no dev hint; seed admin disabled in Identity.
- **GS-002:** After 7 days (or lower `IntervalHours` on staging), backup row appears; download CSV herd; non-admin cannot restore.
- **GS-003:** Browser tab title “Sandeel Farms”; sidebar brand matches config.
- **GS-004:** Toggle airplane mode → header shows Offline; device code visible.
- **GS-005:** Log out → POST to `/Assistant/Chat` returns 401; voice button opens panel when permitted.
- **GS-006:** Sign out from top bar works; name matches profile.
- **GS-007:** Sidebar shows Finance / Health / Feed / Admin.
- **GS-008:** Login without checkbox → closing browser ends session (browser-dependent); with checkbox → survives restart.

## Files (main touchpoints)

| Area | Paths |
|------|--------|
| Branding config | `appsettings.json`, `appsettings.Production.json`, `_Layout.cshtml` |
| CSV export | `FarmCsvExportService.cs`, `BackupCenterController.ExportCsv` |
| Seed admin | `DatabaseBootstrap.cs`, `Seed:CreateDefaultAdmin` |
| Login / session | `AccountController.cs`, `Program.cs` (lockout) |
| Assistant | `AssistantController.cs`, `_AssistantPanel.cshtml`, `Features:AssistantEnabled` |

## Ops-only (cannot be closed in git alone)

1. Owner account + password policy communication.
2. HTTPS / hosting (PDF Q5).
3. Disable legacy seed user on production DB.
4. Logo image files from Sandeel brand kit → `wwwroot/images/`.
