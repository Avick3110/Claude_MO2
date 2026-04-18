# KB_NPCAnalysis.md — NPC Investigation Router

Load before analyzing any NPC. Routes to sub-KBs by topic.

## First Step (Always)

Query these **in parallel** on the first call:
- NPC_ record + conflict chain
- QUST records by EditorID containing the NPC's name or prefix
- OTFT records by EditorID containing the NPC's name

On the QUST results, flag any quest **won by an unexpected plugin** (not the origin mod, not USSEP, not a known overhaul). That plugin is likely patching behavior — investigate it first.

## Sub-KB Routing

| Topic | File | Load When |
|---|---|---|
| Outfit / appearance / what the NPC wears | `KB_NPC_Outfits.md` | Outfit consistency checks, armor conflicts, visual overhaul analysis |
