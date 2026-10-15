# Behance reference — Dashboard Design | Dairy Farm Management

**Gallery:** [254449911](https://www.behance.net/gallery/254449911/Dashboard-Design-Dairy-Farm-Management) (Lida Bdoyan, Aug 2026)

## Inspection limitation

Automated fetch of the Behance page returns project metadata and tools (Figma, Photoshop) only — **no embedded color specs, typography names, or downloadable frames** in the HTML response. Public search did not surface frame-level assets for this gallery ID.

## Source of truth used for implementation

Implementation follows **visible patterns typical of this project type** (SaaS farm/dairy dashboard UI/UX) and adapts labels to **Goat Farm Pro**:

| Element | Implemented treatment |
|--------|------------------------|
| App shell | Fixed dark-green sidebar, light cool-gray canvas, sticky white header |
| Navigation | Grouped sections, icon + label links, white active pill on sidebar |
| Typography | Plus Jakarta Sans (web stand-in for modern dashboard sans in Behance farm UIs) |
| Cards / panels | White surfaces, 16–20px radius, soft elevation |
| KPI stats | White stat cards with subtle hover lift; dashboard keeps pastel accent variants |
| Tables / forms | Existing component classes; tokens reskinned via `body.farm-pro` CSS variables |
| Search | Header pill search (tag / RFID) |
| Login | Split hero (farm branding) + sign-in card |

## Remaining unknowns vs Behance

Exact hex values, illustration assets, chart styles, and mobile frame layouts from the Figma file **cannot be matched pixel-perfect** without the designer file or screenshot exports. Re-verify against Behance manually if pixel parity is required.

## CSS architecture

- `wwwroot/css/farm-pro.css` — design tokens, shell, auth, component reskin scoped to `body.farm-pro`
- `wwwroot/css/goat-records.css` — module layouts and behaviors (no second shell theme block)
