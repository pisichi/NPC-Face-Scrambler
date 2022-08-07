
# NPC-Face-Scrambler

Scrambles the NPC Appearances!!

Synthesis patch to randomly copy NPCs appearance from the selected mods and assigned its face and default skin to NPCs of the target mods.

However, It'll not be completely random, the NPC that is copied must have the same race(currently only support vanilla humanoid race) and same sex.

It'll try to use NPC with the same weight first for maximum compatibility. 

If something went wrong (No matching weight, not detected face nif, source NPC contains blacklisted face parts) it'll try again up to 10 times, if it's still got an error, it'll skip that NPC.
  
## Settings

- Facegen Output Directory: Facegen, tint file output folder.

- Source Mods: The NPC appearances will be copied from these Mod(s). 
  The plugin must contain the base NPC, Follower mods are ideal. If want to use a vanilla NPC overhaul mod like Bijin, you must select the base game plugins (Skyrim.esm, Dragonborn.esm, etc.)

- Target Mods: The NPC appearances will be applied to these Mod(s).

- Same Name: Npc with the same name, race, and weight will have the same face.

- Patch Female: Patch female NPCs unselect this if you want to patch only male NPCs.

- Patch Male: Patchmale NPCs unselect this if you want to patch only female NPCs.

- Only Important Npc: Only copy appearance from Protected, Essential NPC.

- Only Vanilla Race: Only patch NPC that uses Humanoid Vanilla race.

- Block Head parts: Blacklist head parts, skip NPC that contain these head part.
  sometimes some head parts did not play well with some NPC or your loadorder and leading to ctd.
  You can check a crash log, see what caused the crash, and blacklist it by adding the EditorID there.

  
## Related

This project is based on / inspried from

[NPC-Appearance-Copier](https://github.com/Piranha91/NPC-Appearance-Copier)

[facefixer](https://github.com/Synthesis-Collective/facefixer)
  
