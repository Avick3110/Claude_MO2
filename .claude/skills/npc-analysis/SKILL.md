---
description: Router skill for investigating any NPC. Kicks off the parallel first-step queries (NPC_ record + conflict chain, QUST records containing the NPC's name, OTFT records containing the NPC's name) and flags management quests won by unexpected plugins. Use before analyzing any NPC — the Jarl won't accept my quest, why does this follower look wrong, who's patching this character, what plugin is controlling this NPC's behavior. Delegates outfit-specific questions to the npc-outfit-investigation skill.
---

# NPC Investigation Router

## First Step (Always)

Query these **in parallel** on the first call:
- NPC_ record + conflict chain
- QUST records by EditorID containing the NPC's name or prefix
- OTFT records by EditorID containing the NPC's name

On the QUST results, flag any quest **won by an unexpected plugin** (not the origin mod, not USSEP, not a known overhaul). That plugin is likely patching behavior — investigate it first.

## Sub-Topic Routing

| Topic | Skill | Load When |
|---|---|---|
| Outfit / appearance / what the NPC wears | `npc-outfit-investigation` | Outfit consistency checks, armor conflicts, visual overhaul analysis |
