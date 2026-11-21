# Sytrohs

A Sytrohs touch-panel interface (dark wallpaper, bold titles, outlined icon tiles, MENU pull-up dock) built once and shipped to two places:

- **Crestron** TSW-1070 / TSW-1080 panels (1280×800) and Web XPanel, as a CH5 archive.
- **Home Assistant**, as a custom panel.

The house layout is never hard-coded. Floors, rooms, devices and the list of services come from the system at runtime, so the dock and pages adapt to each house. A house without a pool simply has no Pool tile.

Design reference: the approved mockups live on the "Zenith-style TSW-1060 UI" canvas.

## How it fits together

```
          +-----------------+
          |  UI (Lit pages) |   reads HouseConfig + states, sends Commands
          +--------+--------+
                   |
             HouseStore (src/model)
                   |
   +---------------+----------------+-------------------+
   | MockAdapter   | CrestronContractAdapter | HomeAssistantAdapter |
   | sample house  | Sytrohs CH5 contract   | HA floors/areas/entities |
   +---------------+-------------------------+--------------------------+
```

- `src/model/types.ts` — the shared house model and command list.
- `src/model/store.ts` — state store and the `Adapter` interface.
- `src/adapters/wire.ts` — decoder for the framed JSON already used by Control4_Crestron (`w:1`, ≤ 255-byte frames).
- `src/adapters/panel-router.ts` — **Crestron adapter in use.** Talks to the existing Panel Room Router (serial 101–108, digital 7). See [docs/PANEL_ROUTER_ADAPTER.md](docs/PANEL_ROUTER_ADAPTER.md).
- `src/adapters/crestron.ts` — **draft** adapter for the phase 2 Sytrohs contract in [docs/CONTRACT.md](docs/CONTRACT.md) (`?adapter=contract`).
- `src/adapters/home-assistant.ts` — builds rooms from Home Assistant's floor and area registries.
- `src/ui/` — chrome (top bar, MENU, dynamic dock) and pages.

## Place in this package

This UI lives in `ui/sytrohs` of the Control4_Crestron package. It was imported from [`jobu109/Crestron_Control4UI`](https://github.com/jobu109/Crestron_Control4UI) at commit `95a7428`, and development continues here. It is the Crestron GUI going forward. `ui/room-media` (FloydControl4) stays as the rollback until Sytrohs passes hardware acceptance. The Home Assistant build comes later.

## Status

Crestron here means the Panel Room Router adapter. Local build and tests pass. RMC4 and panel runtime are not verified yet.

| Page | Mock | Home Assistant | Crestron |
|---|---|---|---|
| Home, Rooms, Room (lighting and shade sheets) | ✓ | ✓ | ✓ rooms load when opened |
| Lighting, Shades overviews | ✓ | ✓ | hidden (needs whole-house data) |
| Media (Video / Audio tabs, service presets) | ✓ | ✓ | ✓ |
| Multi Audio | ✓ | ✓ | ✓ Whole House Audio sessions |
| Scenes | ✓ | ✓ | ✓ discovered scenes and macros |
| Climate | ✓ | ✓ | hidden |
| Settings | ✓ | ✓ | ✓ |
| Comfort, Security, Cameras, Pool, Irrigation, Energy | designed only | hidden | hidden |

Pages for services the system does not report are hidden from the dock.

## Develop

```bash
npm install
npm run dev          # http://127.0.0.1:5173 with the sample house
                     # add ?adapter=sim for the Panel Room Router adapter on a simulated router
npm test             # adapter and wire-format tests
npm run typecheck
```

Pages are routed by hash: `#/home`, `#/rooms`, `#/room/<id>`, `#/lighting`, `#/shades`, `#/media`, `#/audio`, `#/climate`, `#/scenes`, `#/settings`.

## Build for Crestron

```bash
npm run archive      # dist/crestron -> archives/Sytrohs.ch5z
```

The archive uses the Panel Room Router adapter by default. For Web XPanel, open the page with `?host=<processor>&ipId=0x03`. Add `?adapter=mock` to preview without a processor, or `?adapter=contract` for the draft phase 2 contract. Deployment steps and SIMPL wiring: [docs/PANEL_ROUTER_ADAPTER.md](docs/PANEL_ROUTER_ADAPTER.md).

## Install in Home Assistant

```bash
npm run build:ha     # dist/ha/sytrohs-panel.js
```

Copy `dist/ha/sytrohs-panel.js` to `/config/www/sytrohs/` and add to `configuration.yaml`:

```yaml
panel_custom:
  - name: sytrohs-panel
    sidebar_title: Sytrohs
    sidebar_icon: mdi:tablet-dashboard
    url_path: sytrohs
    module_url: /local/sytrohs/sytrohs-panel.js
```

Restart Home Assistant. Rooms come from your HA areas, grouped by HA floors (areas without a floor go under "Other"). Lights, covers, media players, thermostats and scenes are picked up automatically.

## Known gaps

- Montserrat is bundled in the CH5 archive (`@fontsource/montserrat`, OFL-1.1, Latin subset). The Home Assistant panel does not bundle it yet and falls back to system fonts.
- Wallpapers: set `wallpapers` in the house config (`{ "home": "<url>", "default": "<url>" }`). No images ship yet.
