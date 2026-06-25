# RynthRemote — UI/UX Redesign Plan ("Bridgeport")

Deep-dive verdict (2026-06-25, 12-agent workflow: 5-lens audit → 3 design directions → 3-persona judge
panel → synthesis). **Unanimous** winner across all audits and all three judges (daily multiboxer,
product designer, build engineer): the **Hybrid** direction.

## 0. The verdict

**Bridgeport — a dark "command deck" shell with AC-parchment detail views.** One governing rule:

> **The dark shell is the remote talking; the parchment is the game talking.**

Operator tools (fleet, vitals, toggles, profile pickers, d-pad, stream controls, connection) live in a
fast, warmed-dark UI tuned for one-handed speed. Everything you *read like a player* (inventory, gear +
appraisal, chat, the run-archive ledger) renders on the warm AC parchment — turning the inventory popup
the user already loves from an "orphan from another app" into the app's signature.

**Why it won (3 points the code confirms):**
1. **The real problem is structural, not cosmetic.** Today the app stacks seven ~2000px mega-cards in a
   `.ac-grid` at a **21px root font** (`app.css:21`), so landing is "blind" and core controls sit inside
   an off-screen toggle for boxes 2-7. The fix (fleet list → drill-in deck → pinned control bar) is the
   same regardless of skin — so the tiebreaker is cohesion + reuse.
2. **Cohesion.** The code literally bolts two visual languages together: a cold indigo dark shell
   (`--accent #6366f1`) + an inline Georgia/tan parchment block (`AcClients.razor` `<style>`). Only the
   hybrid gives a *principle* for why both exist, and extends parchment to the other read-surfaces.
3. **Lowest risk.** The parchment CSS already exists (reuse ~verbatim) → lowest net-new CSS to cohesion.
   ~4-4.5 focused days, fully phaseable, **zero engine/agent/plugin changes** (the data model + the
   d-pad keepalive/HD/feed logic survive untouched). AC-Fantasy ("Scrycloth") was a 2-3 week rewrite +
   a bundled serif webfont (FOUT in WKWebView) + per-frame gem/leather paint on a tree that re-renders
   every ~1s + diegetic naming that slows 3am dead-box triage.

**Runner-up:** "Command Deck" (refined dark) — within ~1 pt on every card; **same IA as Bridgeport**, so
it's a strict subset: if parchment-everywhere ever feels heavy on a 380px screen, dial it back to
inventory-only and you've effectively landed on Command Deck with zero rework.
**Revisit later:** the full AC-Fantasy skin becomes a `:root` swap once these tokens exist (gemstone
vitals + heraldic state gems already land it ~halfway).

## 1. Design system (all tokens → `:root` in app.css; today only 12 vars exist)

- **Root font (highest-ROI fix):** `html { font-size: clamp(15px, 4.1vw, 18px); }` — kills `html{font-size:21px}`, recovers ~25% vertical height, WCAG 1.4.4 resize-safe.
- **Dark shell (cool slate, warmed):** `--bg #0d1018` `--surface #161b27` `--surface-2 #1f2735` `--surface-3 #2a3344` `--border #313c4f` `--border-soft #232c3a`. Text (all ≥4.5:1): `--text #eef2f8` `--text-2 #b9c4d6` (was #9aa9c4 — failed AA) `--text-3 #8a97ac`.
- **Accent (drop indigo → AC bronze/gold):** `--accent #d9a441` `--accent-deep #a9761f` `--accent-bright #e7c074` `--accent-ink #2a1d0e`.
- **Semantic state** (intent-mapped; each gets `-ink` + 12% `-bg`): ok `#34d399` · info `#60a5fa` · warn `#f5b14a` · bad `#f5604d` (+ `--state-bad-bg #2a1014` / `#ffd2d2` text for crit pills). **Rule that kills today's red-means-3-things ambiguity:** mode toggles = gold when ON; START = gold; STOP/danger = red; client HEALTH uses the state colors only.
- **Heraldic state gems** (color **+ sigil + label**, never color alone — WCAG 1.4.1 + delight): botting ⚔ ok · idle/running ◈ info · loading/wedged ⚠ warn · hung/dead ☠ bad. Rendered as a small faceted lozenge (gradient + 1px accent rim). Sigils = Tabler icons.
- **Vitals gemstones** (Status view only): HP ruby, Stam jade, Mana sapphire in recessed bronze channels.
- **Parchment tokens** (lift the inline hexes into `:root` so Chat + Archive reuse them): `--pch-bg-1 #e8d6ad` `--pch-bg-2 #d8c08c` `--pch-panel #f3e8cb` `--pch-ink #2a1d0e` `--pch-ink-2 #5a3d1c` (bumped from #7a5c30 — failed AA) `--pch-edge #5a3d1c` `--pch-rule #c4a772` `--pch-gold #e8c060` `--pch-accent #9a5a10`.
- **Type scale:** `--fs-cap .72` `--fs-sm .82` `--fs-base .92` `--fs-md 1` `--fs-lg 1.18` `--fs-xl 1.5` rem; weights 400/600/700; `tabular-nums` on all stats. Serif scoped to `.pch` surfaces **and character names** (the one bridge touch).
- **Spacing** (4pt grid): 4/8/12/16/24/32. **Radius:** 8/12/999. **Elevation** (3-tier): cards `0 1px 2px`, sheets `0 6px 20px`, pinned bar `0 -4px 24px`. **Focus ring:** `0 0 0 3px rgba(217,164,65,.18)`.
- **Tap targets:** 44px min on every button/pill/select/input (the naked `.ac-gear-h` chevrons get real padding).
- **Motion** (WKWebView-safe, transform/opacity only, ≤180ms, **nothing on the per-second re-render path**): deck slide-in, sheet slide-up, `:active scale(.97)`, ONE 2s opacity pulse on unhealthy rows, gilt shimmer on the in-flight control only. Respect `prefers-reduced-motion`. `navigator.vibrate` as progressive enhancement (likely no-op on iOS — never load-bearing).

## 2. Navigation / IA (replaces the single `.ac-grid` of mega-cards)

- **Top bar:** title + connection-state dot (green=WS feed live, amber=poll fallback, red=down) + overflow ⋯ (Connection, Refresh, Theme).
- **Primary tabs:** FLEET · COUNCIL · ARCHIVE.
- **Tier 1 — Fleet Overview (landing, "is anything broken?" in <1s):** health roll-up chips (`clients.GroupBy(State)`, tap to filter) + filter/search (persisted to SettingsStore) + vertical list of compact ~64px rows, **unhealthy-first** (reuses `OrderByDescending(!Healthy)`): heraldic gem+sigil, serif name+server, 3px HP sliver, kills/hr, stale dot if AgeSec≥12, Start/Stop quick cluster; unhealthy rows get a red left bar + the single 2s pulse. **All 7 boxes on one screen, zero scroll.**
- **Tier 2 — Single-Client Deck** (tap a row → slide-in): header (back, serif name, gem, "N of 7" + ‹ ›) + segmented sub-nav **STATUS · CONTROL · LIVE · INVENTORY · CHAT**. World-boundary rule: STATUS/CONTROL/LIVE = dark; INVENTORY/CHAT (+ gear appraisal, Archive) = parchment.
- **Persistent bottom control bar** (every sub-tab, `env(safe-area-inset-bottom)`): `[Start/Stop] [Cbt][Buf][Nav][Loot][Meta] [More⌄]`. The biggest IA win — profile swap drops ~6 taps → 2. "More⌄" = slide-up sheet (profiles + danger actions incl. Close-with-confirm).
- **Council:** checkbox list of all 7 + Start All / Stop All / Rebuff All / Clear Busy / Hide UI; hosts the parchment leaderboard (top killer / fewest deaths / best xp/hr; tap a name → that deck).
- **Alerts:** sticky top toast + Fleet-tab badge on any client healthy→crit transition (computed client-side from State/Healthy deltas across pushes). True iOS push is a later add.

## 3. Screen redesigns

(See the synthesis for full per-screen detail; condensed.)
- **Fleet Overview** — dark; roll-up chips, compact unhealthy-first rows, each an extracted `@key`'d `<ClientRow>` so the ~1s push re-renders only deltas.
- **Client Deck / STATUS** — gemstone vitals + a promoted 4-stat KPI strip (kills/hr · last-kill · deaths · vitae) + collapsible full-stats grid + LastIssue banner.
- **Client Deck / CONTROL** — big Start/Stop, 5 labeled toggle pills (gold=on), **optimistic flip + "applying…" shimmer** per control, full-width 44pt profile selects, danger group, real **Close modal with visible 10s countdown** (replaces the silent 4s timeout), toast feedback.
- **Client Deck / LIVE-DRIVE** — stream + sticky quality presets (data-rate shown *before* opening) + an always-present 5-key d-pad with **held-direction ring** + gold-fill/scale. ⚠ Restyle the d-pad IN PLACE — do NOT move the keepalive/movement-lock code.
- **Inventory** (parchment sheet) — the existing modal promoted to a full-screen sheet from the INVENTORY sub-tab, driven by `--pch-*` tokens, fixed 4-col grid on phone (replaces auto-fill), name-search, Esc/✕/swipe-down close + focus trap.
- **Chat** (parchment reader) — message-scroll, parchment type tabs, **auto-scroll to newest** (fixes today's reversed/hidden-newest bug), sticky send box, unread badge.
- **Archive** (parchment ledger) — date-grouped runs, live run gets a wax-seal "live" badge, Expand/Collapse-all.
- **Connection / first-run** — auto-inline form above an illustrated empty state + a **Test connection** button; afterward lives in the ⋯ overflow.
- **Council** — first-class batch screen + parchment leaderboard.

## 4. Roadmap (each phase a standalone SideStore version bump)

| Phase | Scope | Effort | Impact |
|---|---|---|---|
| **0 — Tokens & the 21px fix** | Replace `:root` with the full token set (clamp root font, dark palette, bronze accent, semantic state, parchment tokens, type/spacing/radius/elevation, 44px floor, focus ring); fix the AA contrast misses; recolor existing classes to tokens. **Pure CSS, no markup/logic change.** | ~1 day | Very high — recovers ~25% height, makes 44pt targets real, fixes contrast, de-risks everything later. Shippable day one. |
| **1 — Render isolation + Fleet Overview** | **MANDATORY first:** extract `@key`'d `<ClientRow>`/`<VitalsBar>` so the ~1s WS push re-renders only deltas (perf gate, not polish). Then roll-up strip + compact unhealthy-first rows + filter/search. | ~1.5 days | Very high — kills "refresh 5× to see 7 boxes"; whole fleet on one screen. |
| **2 — Single-Client Deck + pinned control bar** | Drill-in deck, segmented sub-nav, ‹ › paging, persistent bottom control bar + More sheet, optimistic toggles + shimmer + toast, real Close-confirm countdown, gemstone vitals + KPI strip. | ~1.5 days | High — buried controls surfaced (profile swap 6→2 taps), stop always under thumb. |
| **3 — Parchment surfaces + d-pad + chat** | Parchment → `:root`; world-boundary rule on Inventory/Chat/Archive; d-pad held-ring restyle (in place); floating presets w/ pre-shown data rates; `loading=lazy` MJPEG; first-run auto-form + Test. | ~1 day | High — resolves the "two products" feel; fixes phantom-motion + broken chat + first-run. |
| **4 — Council, leaderboard, alerts, polish** | Council batch tab; leaderboard; healthy→crit toast + Fleet badge; swipe paging (gesture-scoped to header); reduced-motion pass; on-device SideStore verification across 7 boxes. | ~0.5-1 day | Med-high — batch ops save 20+ taps/session; alerts catch backgrounded failures. |

## 5. Top risks

1. **WKWebView re-render cost** — the ~1s push re-renders the whole tree today; extract `@key`'d `<ClientRow>`/`<VitalsBar>` **before** the IA rewrite (Phase 1) or the new list/deck will jank. Nothing animated on the per-second path. *(Gating engineering task.)*
2. **D-pad regression** — the pointer/keepalive/movement-lock dead-man's-switch is load-bearing + decoupled from markup. **Restyle in place; do NOT move it into a child component.**
3. **iOS keyboard + sticky elements** — the pinned bar + sticky chat send box can be hidden/shoved by the soft keyboard; needs `env(safe-area-inset-bottom)` + `viewport-fit` + `scrollIntoView`. Only verifiable on-device.
4. **Swipe-between-clients** fights the d-pad touch handlers + iOS back-swipe — ship ‹ › buttons first; add swipe later, gesture-scoped to the header band.
5. **Beige discipline** — parchment must stay strictly on read-only surfaces, or a 380px screen becomes a beige wall.
6. **Scope creep / long-lived branch** — ship Phase 0 and Phase 1 independently (each a real version bump) rather than one big merge.

## 6. Open decisions (resolve as we hit them)

- The 4 "hottest" Status KPIs (proposed: kills/hr, last-kill, deaths, vitae).
- Compact-row layout for `Source=="heartbeat-log"` clients (no bot fields → gem + name + "basic" badge).
- Council default selection when opened from a roll-up filter (pre-check the filtered set?).
- Inventory grid columns on a 380px phone: 4 (safer ~3.5rem cells) vs 5.
- Swipe paging vs ‹ › only (ship buttons first).
- Keep AC-Fantasy as a future optional `:root` theme swap, or commit fully to hybrid for now.
- Alert transition detection stays client-side (diffing pushes) for now.
