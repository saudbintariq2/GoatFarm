# Herd & Breeding — Sandeel fix list (B1–B6, H2–H5)

| ID | Item | Status |
|----|------|--------|
| B1 | Single pregnancy rule (cross + positive scan); reconcile tool | Done — `PregnancyRules`, `ReconcilePregnancy` |
| B2 | Inbreeding warnings + confirm resend | Done — server + `CheckCross` UI on breeding add-doe |
| B5 | Season new batch reloads correct batch | Done — `UpdateSeason` returns `newBatchId` |
| B6 | Group/bulk cross requires buck | Done — server + breeding/ herd JS |
| H2 | Tag + date required on add goat | Done — client + server |
| H3 | Bulk birth batch + feeding group | Done — `bat:`, `fg:`, `+ New birth batch…` |
| H4 | Scan tag → full profile | Done — header search + herd scan bar → `/Search` |
| H5 | Age/kid alert filters | Done — herd filter chips + age filters |
| — | Hard delete guard (X9) | Done — delete blocked when history exists |
| — | Cross does not set pregnant before scan | Done — `BreedingCycleWriter.ApplyCross` |

**Ops:** Run **Sync pregnancy status** on staging/production after deploy if herd counts still disagree with breeding lists.
