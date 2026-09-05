# Character Select prototype

React + Tailwind + Framer Motion prototype of the Elementals Fight character-select screen, built for landscape phones. It is a UI/UX reference for the Unity `CharacterSelect` scene, not shipped game code.

## Run
```
npm install
npm run dev        # local dev server
npm run build      # single self-contained dist/index.html (open it on a phone)
npm run shots      # Playwright screenshots at 844x390, 932x430, 1180x820
```

## Add or edit a fighter
Edit `src/data/characters.js` only. Append one object to `CHARACTERS` with `id`, `name`, `pantheon`, `domain`, `title`, `lore`, `playstyle`, `element`, `colors`, `stats`, `glyph`, `unityIndex`. Set `portrait` / `icon` to an image URL or data URI when renders exist; while they are `null` the UI draws a stylised glyph placeholder. Mark stand-ins with `placeholder: true`.

The Unity mirror of this file lives in `Assets/Scripts/Roster/` (`CharacterId` enum, `CharacterDefinition` and `CharacterRoster` ScriptableObjects). Keep `id` equal to the enum member name (lower-case) and `unityIndex` equal to the fighter's slot in the roster list.

## Where things are
- `src/CharacterSelect.jsx` - the whole screen (backdrop, lore panel, deity stage, radar chart, elastic carousel, confirm).
- `src/data/characters.js` - roster data. The only file that changes when the cast changes.
- `shot.mjs` - screenshot harness used for visual checks.
