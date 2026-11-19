For the current multi-page Construct version, see [Shared Construct room pages](../../docs/UI_CONSTRUCT_SHARED_PAGES.md) and [the browser preview](construct-pages/index.html).

# Room media UI

Open [index.html](index.html) directly for the offline interactive mockup. No credentials or system commands are used by the preview. Station names come from exported fixtures; metadata is a captured Pandora sample.

- [Construct instructions](../../docs/UI_BUILD_CONSTRUCT.md)
- [Vanilla HTML5 instructions](../../docs/UI_BUILD_VANILLA.md)
- [Shared SIMPL wiring](../../docs/UI_JOIN_MAP.md)
- [Status and validation](../../docs/UI_BUILD_START_HERE.md)

`npm ci`, `npm test`, `npm run test:preview`, `npm run build`, `npm run build:construct`, `npm run archive`.

Live configuration is deliberately disabled in join-map.js. Example joins must be assigned in SIMPL before enabling. dist/index.html is the live entry; the source index.html is the offline mockup. Never substitute preview-data.js for processor feedback.
