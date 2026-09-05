// ============================================================================
// Elementals Fight - Character Roster (single source of truth for the UI)
// ----------------------------------------------------------------------------
// To add a fighter: append one object to CHARACTERS. Nothing else in the
// prototype needs to change - the carousel, lore panel, radar chart, colours
// and ambient backdrop are all driven from this file.
//
// Mirror of the Unity side: Assets/Scripts/Roster/CharacterId.cs (enum) and
// CharacterDefinition.cs (ScriptableObject). Keep `id` values identical to the
// enum member names (lower-case) and keep `unityIndex` equal to the fighter's
// slot in PlayerSelection.characters[] / LoadCharacter.charPrefabs[].
//
// Stats are 0-10. The radar chart reads STAT_AXES in order, so every fighter
// must define every axis listed there.
// ============================================================================

/** Elemental affinity. Drives the neon ring colour and the ambient backdrop. */
export const Element = Object.freeze({
  WATER: "water",
  THUNDER: "thunder",
  WIND: "wind",
  SPIRIT: "spirit",
  EARTH: "earth",
  SUN: "sun",
});

/** Broad archetype label shown as the playstyle chip. */
export const Playstyle = Object.freeze({
  BRUISER: "Bruiser / Heavy",
  RUSHDOWN: "Assassin / Rushdown",
  ZONER: "Ranged / Zoning",
  MARKSMAN: "Marksman / Agile",
  GUARDIAN: "Guardian / Counter",
  TRICKSTER: "Trickster / Mix-up",
});

/** Radar axes, in drawing order (clockwise from the top). */
export const STAT_AXES = Object.freeze([
  { key: "power", label: "PWR" },
  { key: "speed", label: "SPD" },
  { key: "range", label: "RNG" },
  { key: "defense", label: "DEF" },
  { key: "mobility", label: "MOB" },
]);

export const STAT_MAX = 10;

/**
 * @typedef {Object} Character
 * @property {string} id           Stable key. Matches the C# CharacterId enum member (lower-case).
 * @property {string} name         Display name.
 * @property {string} pantheon     Origin culture / pantheon line, e.g. "Yoruba".
 * @property {string} domain       One-or-two-word domain, e.g. "Ocean Mother".
 * @property {string} title        Epithet shown under the name.
 * @property {string} lore         Short lore, ~3 lines on a phone.
 * @property {string} playstyle    One of Playstyle.
 * @property {string} element      One of Element.
 * @property {{primary:string, secondary:string, glow:string, deep:string}} colors
 *   primary   - neon ring + accent text
 *   secondary - gradient partner colour
 *   glow      - light bloom behind the deity
 *   deep      - darkest backdrop tone
 * @property {Record<string, number>} stats  0..STAT_MAX per STAT_AXES key.
 * @property {string|null} portrait  URL/data-URI of the full-body render. null = stylised placeholder.
 * @property {string|null} icon      URL/data-URI of the 2D carousel portrait. null = stylised placeholder.
 * @property {string} glyph          Single character used by the placeholder art.
 * @property {number} unityIndex     Slot in PlayerSelection.characters[] (see note at top).
 * @property {boolean} [placeholder] True while the entry is a stand-in for a real fighter.
 */

/** @type {Character[]} */
export const CHARACTERS = [
  {
    id: "yemoja",
    name: "Yemoja",
    pantheon: "Yoruba",
    domain: "Ocean Mother",
    title: "Mother of the Waters",
    lore:
      "Born where the river meets the sea, Yemoja carries the weight of every tide. Her trident calls the deep to rise; her shield turns the storm itself aside.",
    playstyle: Playstyle.GUARDIAN,
    element: Element.WATER,
    colors: {
      primary: "#38e8ff",
      secondary: "#f5f0ff",
      glow: "#22b8d8",
      deep: "#052a4a",
    },
    stats: { power: 7, speed: 5, range: 6, defense: 9, mobility: 4 },
    portrait: null,
    icon: null,
    glyph: "Y",
    unityIndex: 3,
  },
  {
    id: "shango",
    name: "Shango",
    pantheon: "Yoruba",
    domain: "Thunder King",
    title: "Lord of the Double Axe",
    lore:
      "Fourth king of Oyo, crowned in fire. Shango's oshe axe splits the sky and every strike lands with the crack of thunder. Where he walks, the drums never stop.",
    playstyle: Playstyle.BRUISER,
    element: Element.THUNDER,
    colors: {
      primary: "#ff4d3d",
      secondary: "#ffc93c",
      glow: "#ff7a1a",
      deep: "#3d0a0a",
    },
    stats: { power: 10, speed: 5, range: 4, defense: 6, mobility: 5 },
    portrait: null,
    icon: null,
    glyph: "S",
    unityIndex: 0,
    placeholder: true,
  },
  {
    id: "oya",
    name: "Oya",
    pantheon: "Yoruba",
    domain: "Storm Bringer",
    title: "Mistress of the Winds",
    lore:
      "Keeper of the gate between worlds, Oya rides the tornado and speaks in lightning. She is change itself: fast, unforgiving, and gone before the dust settles.",
    playstyle: Playstyle.MARKSMAN,
    element: Element.WIND,
    colors: {
      primary: "#c084fc",
      secondary: "#e2e8f0",
      glow: "#a855f7",
      deep: "#2a0a4a",
    },
    stats: { power: 6, speed: 9, range: 8, defense: 4, mobility: 9 },
    portrait: null,
    icon: null,
    glyph: "O",
    unityIndex: 1,
    placeholder: true,
  },
  {
    id: "anansi",
    name: "Anansi",
    pantheon: "Akan",
    domain: "Spider Trickster",
    title: "Keeper of All Stories",
    lore:
      "He bought every story in the world from the sky god and paid in cunning. Anansi fights the way he talks: sideways, in webs, and always one step ahead.",
    playstyle: Playstyle.TRICKSTER,
    element: Element.SPIRIT,
    colors: {
      primary: "#34d399",
      secondary: "#fde68a",
      glow: "#10b981",
      deep: "#06231c",
    },
    stats: { power: 5, speed: 8, range: 5, defense: 3, mobility: 10 },
    portrait: null,
    icon: null,
    glyph: "A",
    unityIndex: 2,
    placeholder: true,
  },
];

/** Lookup helper used by the UI; O(1) after first call. */
const byId = new Map(CHARACTERS.map((c) => [c.id, c]));
export const getCharacter = (id) => byId.get(id);

export default CHARACTERS;
