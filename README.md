
# ![AnoMech](images/icon.png) AnoMech

*Another FFXIV mechanics simulator*

---
 
Simulate FFXIV raid mechanics client-side for solo practice. Go to any Inn, open the plugin with `/anomech` and start practicing!


Thanks to improvemnts by [WorstAquaPlayer](https://github.com/WorstAquaPlayer) plugin is quite stable now! No more 
crashes after training session.

**WARNING!!!**

**You are cut off from server traffic while in the sim zone.** To keep the
fake zone stable, the plugin firewalls incoming packets from the server.  
While simulating:
  * Players joining or leaving your party will not appear in the party list
  until you leave the sim zone.
  * Ready checks will not pop.
 

### Beta: Advanced Simulation Resolution

The simulator now includes a beta feature that properly resolves most skills, triggers, and gauges during simulation. 
As this feature is still in beta, some edge cases and less common interactions may not yet resolve correctly.


### Splatoon presets and scripts

With [Splatoon](https://github.com/PunishXIV/Splatoon) installed, its layouts (presets) and scripts
for the simulated fight work inside a scenario. Toggle it under `/anomech config` →
**Splatoon compatibility** (on by default).

Boss casts, action effects, head-marker and other actor VFX, tethers and statuses already
reach Splatoon, because the sim plays them through the same game functions server packets use.
While a scenario runs, AnoMech also sets the encounter state the firewall would otherwise
block:

* **Zone.** Dalamud only learns the zone from the server, so it would keep reporting the inn.
  AnoMech sets it to the simulated fight's territory on entry and back to the inn on leave, so
  zone-locked layouts and scripts load.
* **In combat.** Enables combat-only layouts and `Controller.CombatSeconds`, and resets
  Splatoon's scripts between pulls. A restart drops combat for a few frames so Splatoon sees
  the new pull.
* **In duty.** Enables duty-only layouts.
* **Party.** The bots are already in the game's party list. While you're in a cross-world
  party, though, Splatoon reads the server's cross-world roster instead, so AnoMech hides
  the cross-world flag during a run. Priority lists and scripts then see the bots, named
  after their job (`Warrior`, `White Mage`, ...) on your world. Role-mode lists with
  **Fill automatically** work best, because which job the bots get depends on your own.
* **Phase scene.** Sets the scene that scripts check with `Controller.Scene` and that layout
  scene locks use: TOP P5 = 6, TOP P6 = 7, Dancing Mad P2 = 7, Dancing Mad P3 = 8. Other
  phases keep the zone's own value. Use **Scene override** to force a scene for them.
* **Map effects.** Arena map effects are sent through the game function itself, so
  Splatoon's `OnMapEffect` fires no matter which plugin loaded first.

Known gaps:
* The scene is only known for the phases above.
* Scripts that read `OnActorControl` head-marker packets or NPC chat lines (`OnMessage` with
  boss dialogue) don't get those events. The sim spawns head markers as VFX, so
  `OnVFXSpawn`-based scripts work. `OnDirectorUpdate` only sees the director updates the sim
  replays itself, such as Commence at zone load. It never sees a wipe, but the combat reset
  between pulls covers that.
* Scenarios start mid-fight, so layouts timed from the start of combat run from the start of
  the scenario.

## Installation

See: https://github.com/anomek/MyDalamudPlugins

## Currently implemented:
- Dancing Mad (Ultimate)
    - P2 Forsaken
      - NA
        - [Kroxy-Rinon 341 (Center/N Stacks) melee adjust](https://raidplan.io/plan/UATE__aDcw1-bgVv)
        - [South Adjust 341](https://raidplan.io/plan/uq7zdjvuu7uuw8fj)
        - diamond markers or week one positions
      - EU _by [Wydox](https://github.com/Wydox)_
        - [\[LPDU\] Buddies](https://raidplan.io/plan/142oXOZpPc_jh3dd)
        - [\[Old\] p3Z Buddy Meow](https://raidplan.io/plan/lZWqxfxvyhF9sp3Z)
        - [\[Old\] zP6 South adjust](https://raidplan.io/plan/rtc1FcuZFMuyBzP6)
    - P3 Black Hole _old bh (DSA, single tethers, n/s stomps)_
    - P4 Kefka Says _kefkabin_
    - P5 Exaflares _by [Wydox](https://github.com/Wydox)_
    - P5 Celestriad _by [RoarkGit](https://github.com/RoarkGit)_
    - P5 Forsaken Null _no ai or damage_
- The Omega Protocol (Ultimate): _NA pf strats_
    - P2 Party Synergy
    - P5 Delta
    - P5 Sigma
    - P5 Omega
    - P6 Exasquares / Wave Cannon 2
- The Weapon's Refrain (Ultimate) _by [WorstAquaPlayer](https://github.com/WorstAquaPlayer)_
    - Ultimate Predaction
    - Ultimate Suppression
- The Unending Coil of Bahamut (Ultimate) _by [RoarkGit](https://github.com/RoarkGit)_
    - Exaflares

## Details

* Spawns fake party members and boss NPCs into the live game client
* Drives their positions, cast bars, tethers, and VFX so mechanics play out visually
* Your fake party members are full fledged bots that will do mechanics.  
  Some scanarios also have solo mode where you can practice without disctractions.


## How to help
1. Please provide feedback and report any issues in scenarios: bad timing, damage, config not working at it supposed
2. Bot AI currently only covers strategies from my region. Adding strategies for other regions requires little coding.
   Feel free to create pull request or contact me.
3. Adding new scenario is more involved. `tools/parser.py` generates a baseline scenario from a log, which still
   needs randomization, mechanic-failure logic and bot AI added by hand.
4. Plugin-development or reverse-engineering help, and improvement ideas, are also welcome.


## Known issues
* Minor visual and timing issues may occur
* In scenarios for Top Omega Protocol (Ultimate):
  * Tether distance threshold are very rough estimations
  * Line AOE from Optiocal Unit (eye) doesn't render
* Not all skills will resolve properly

#  Acknowledgments

Thanks for contributors:
* [WorstAquaPlayer](https://github.com/WorstAquaPlayer) - rewriting core & fixing crashes, scenarios for uwu
* [Wydox](https://github.com/Wydox) - EU strats for Forsaken, UMAD Exaflares, core improvements
* [RoarkGit](https://github.com/RoarkGit) - UMAD Celestriad, UCOB exas, win streaks

AnoMech leans heavily on the work of other Dalamud plugins. Huge thanks to their authors!  
Without them, the following would not be possible:

* **Hyperborea** — solo duty arena loading.
* **FFXIV-RaidsRewritten** — stunning the player on death and playing raid VFX.
* **bossmod** — mechanics timings and positions.
