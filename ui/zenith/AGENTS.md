# Working rules

- The package root AGENTS.md also applies here (4-Series only, SIMPL+ rules, account-only auth, runtime discovery, join numbering).
- The UI talks to systems only through `src/model` (HouseConfig, states, Commands). Pages never call an adapter directly.
- The Crestron build uses `src/adapters/panel-router.ts` over the existing Panel Room Router joins. Keep it in step with `Control4Gateway/Devices/PanelRoomModule.cs` and docs/PANEL_ROUTER_ADAPTER.md.
- Keep the draft contract adapter in step with docs/CONTRACT.md. Change both together and bump the draft version.
- Do not invent state. Unknown values stay absent, and actions the system cannot carry out are disabled or hidden.
- Every serial command must stay at or under 255 bytes.
- Design target is 1280×800 (TSW-1070/1080). The app scales to fit other screens.
- Run `npm run typecheck`, `npm test` and `npm run build` before committing.
