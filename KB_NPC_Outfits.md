# KB_NPC_Outfits.md — NPC Outfit Investigation

## Override Priority (highest wins)

```
Script SetOutfit()  >  Quest alias outfit  >  SkyPatcher outfitDefault  >  NPC DOFT  >  WNAM
```

If an NPC has a management quest with outfit script properties, DOFT and WNAM on the NPC_ record are irrelevant.

## Steps

### 1. Check Flagged Quests from NPC Router
The parallel query in KB_NPCAnalysis.md already found QUST records. Check the winning version's VMAD script properties for:
- `*Outfit*` properties — OTFT FormID references
- `*Armor*` / equipment properties — ARMO references
- Container/FormList properties — outfit system storage

A patch plugin overriding this quest can redirect outfit properties without touching the NPC_ record.

### 2. Trace the OTFT
Follow the outfit property → OTFT conflict chain → INAM contents. The winning OTFT's items are what the NPC actually wears.

### 3. Secondary Systems (only if steps 1-2 don't explain behavior)
- Follower framework overrides (dedicated outfit management via scripts/dialogue)
- SPID `_DISTR.ini` outfit distribution
- SkyPatcher `outfitDefault` / `outfitSleep`

Do NOT chase these before exhausting quest record analysis. They are lower priority in the override chain and cost many tool calls to investigate.
