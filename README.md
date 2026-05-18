
# ![AnoMech](images/icon.png) AnoMech

*Another FFXIV mechanics simulator*

---
 
Simulate FFXIV raid mechanics client-side for solo practice. Go to any Inn, open the plugin with `/anomech` and start practicing!

**WARNING!!!**

1. This plugin is in beta and is unstable. You won't crash during your training sessions, but you will crash
after it. If you are doing any serious content: **RESTART YOUR GAME AFTER USING PLUGIN**.  
You **WILL CRASH** in the middle of pull.  
You don't have to disable plugin, just don't use it. I'm working on stability improvements, but it's not trivial.

2. **You are cut off from server traffic while in the sim zone.** To keep the
fake zone stable, the plugin firewalls incoming packets from the server.
While simulating:
* Players joining or leaving your party will not appear in the party list
  until you leave the sim zone.
* Ready checks will not pop — no toast, no sound, no flash.

## How to help
1. ACT logs from UMAD pulls would be useful for building scenarios for that fight.
2. Bot AI currently only covers strategies from my region. Adding strategies for other regions requires little code — pull requests welcome.
3. New scenarios are more involved. `tools/parser.py` generates a baseline scenario from a log, which still needs randomization and mechanic-failure logic added by hand.
4. Plugin-development or reverse-engineering help, and improvement ideas, are also welcome.

## Installation

See: https://github.com/anomek/MyDalamudPlugins

## Details

* Spawns fake party members and boss NPCs into the live game client
* Drives their positions, cast bars, tethers, and VFX so mechanics play out visually
* Supports client-side zone loading — practice inside the instance without a full party

Currently implemented:
 - The Omega Protocol (Ultimate) — P5 Delta
 - The Omega Protocol (Ultimate) — P5 Sigma

## Known issues
* Crashes ;(
* Delta timings might be little bit off
* Omega-F rendering in sigma
* Tether distance threshold are very rough estimations

## Acknowledgments

AnoMech leans heavily on the work of other Dalamud plugins. Huge thanks to their authors!

Without them, the following would not be possible:

* **Hyperborea** — solo duty arena loading.
* **FFXIV-RaidsRewritten** — stunning the player on death and playing raid VFX.
* **bossmod** (awgil) — mechanics timings and positions.
