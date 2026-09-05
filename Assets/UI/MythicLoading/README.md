# Mythic Loading UI Assets

Persistent loading-screen artwork belongs here. Use `Sprites/` for authored rune
atlases, rail frames, portraits, and effects, and `Materials/` for UI materials.

The current rune atlas and prism rail are intentionally generated at runtime by
`MythicLoadingOverlay` and `PrismRailGraphic`; they do not create project assets.
This avoids resolution-specific duplicates while the visual direction is iterating.
