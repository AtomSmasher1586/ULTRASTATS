# ULTRASTATS

**ULTRASTATS** is an all-in-one stat tracking, performance analysis, and data collection mod for **ULTRAKILL**.

The goal of the project is simple: make your runs useful.

Whether you speedrun, grind Cybergrind, test custom levels, or just want a better record of how you play, ULTRASTATS saves your run data in a lightweight format and gives you tools to browse it in-game.

ULTRASTATS currently supports:

- **Campaign** run logging
- **Cybergrind** run logging
- **Custom level** run logging through **Angry Level Loader**
- An in-game **main menu UI** with **INFO**, **STATS**, and **PLOTS** tabs
- A **STATS** tab that reads saved files and converts stored values into readable dates, times, and labels
- **PluginConfigurator** options for data storage, logging preferences, menu behavior, and stat sorting

---

## Current Version

This repo is currently on **v0.0.12**.

Here is an example of the new ingame stat viewer works:
![ULTRASTATS file structure](https://raw.githubusercontent.com/AtomSmasher1586/ULTRASTATS/main/images/cyberView.png)
![ULTRASTATS file structure](https://raw.githubusercontent.com/AtomSmasher1586/ULTRASTATS/main/images/campaignView.png)
![ULTRASTATS file structure](https://raw.githubusercontent.com/AtomSmasher1586/ULTRASTATS/main/images/customView.png)

---

## Features

- Logs runs to compact **`.jsonl`** files
- Stores data separately by **difficulty**
- Supports **Campaign**, **Cybergrind**, and **Custom** level runs
- Works even if **Angry Level Loader** is not installed; custom logging is simply disabled
- Lets you choose a custom ULTRASTATS data folder
- Lets you enable or disable **Campaign**, **Cybergrind**, or **Custom** logging independently
- Includes a main menu button that opens the ULTRASTATS UI
- Includes **INFO**, **STATS**, and **PLOTS** tabs in the main menu UI
- Lets you choose the main menu button corner
- Lets you choose which ULTRASTATS tab opens by default
- Lets you choose the default difficulty shown in the **STATS** tab
- Lets you sort run IDs in the **STATS** tab in ascending or descending order
- Can optionally let you discard a pending endscreen run with a configurable key
- Queues and saves runs in a way designed to reduce unnecessary endscreen overhead

---

## Requirements

Required:

- **BepInEx**
- **PluginConfigurator**

Optional:

- **Angry Level Loader** for custom level support

---

## Installation

If you already have a working ULTRAKILL mod setup, install ULTRASTATS like any other BepInEx plugin.

### Quick install

1. Install **BepInEx** for ULTRAKILL.
2. Install **PluginConfigurator**.
3. Drop the **ULTRASTATS** plugin files into:

   ```
   ULTRAKILL\BepInEx\plugins\
   ```

4. Launch **ULTRAKILL**.
5. Confirm the mod loaded by checking the BepInEx console or log.

### Optional custom level support

To enable custom level logging, also install:

- **Angry Level Loader**

If Angry Level Loader is not present, ULTRASTATS still works for Campaign and Cybergrind.

---

## What ULTRASTATS Does In-Game

ULTRASTATS adds a main menu button that opens its UI.

Right now that UI contains:

- **INFO**: project information, notes, and contact info
- **STATS**: an in-game browser for your saved ULTRASTATS files
- **PLOTS**: a placeholder tab for future graphs and visualizations

The **STATS** tab is especially useful if you do not want to read raw `.jsonl` files manually. It can already:

- browse saved runs by difficulty and mode
- display friendlier names for layers, bundles, and levels
- convert Unix timestamps into readable dates
- convert stored milliseconds into readable time values
- sort runs by ID

---

## Data Folder

By default, ULTRASTATS stores data in:

```text
%AppData%\Roaming\AtomSmasher1586\ULTRASTATS
```

You can change the parent folder through **PluginConfigurator**. ULTRASTATS will still create and use its own `ULTRASTATS` folder inside that location.

---

## File Structure Overview

ULTRASTATS stores runs by **difficulty** first, then by mode.

![ULTRASTATS file structure](https://raw.githubusercontent.com/AtomSmasher1586/ULTRASTATS/main/images/FileStructure.png)

This image is the main reference for the save layout. It shows campaign files grouped into layer folders, a separate Cybergrind file for each difficulty, and custom level files grouped under bundle keys. In the example names, the suffix before `.jsonl` such as `_4` is the difficulty number.

---

## Reading `.jsonl` Files Manually

Each line in a ULTRASTATS `.jsonl` file is one saved run.

![ULTRASTATS file structure](https://raw.githubusercontent.com/AtomSmasher1586/ULTRASTATS/main/images/ManualDataReading.png)

This image is the main reference for the compact field names used in ULTRASTATS logs. **Blue** entries are shared fields, **yellow** is Cybergrind-specific, and **cyan** is used for Campaign and Custom runs. `F` stores run flags: `1` means major assists, `2` means cheats, and `3` means both. For Cybergrind, `w` stores the wave, with the last two digits representing percent completion.

If you want readable values without decoding raw fields yourself, use the in-game **STATS** tab instead.

---

## Notes About Custom Levels

Custom level logging is designed around **Angry Level Loader**.

When custom support is available, ULTRASTATS stores custom runs inside a `custom` folder for the selected difficulty, then sorts them by bundle key and level file.

This keeps custom content separate from the base game while still keeping the layout predictable.

---

## Planned Direction

ULTRASTATS is still in active development.

The long-term direction includes better analysis and visualization tools, especially in the **PLOTS** tab. That tab is intended to eventually hold things like:

- run history plots
- performance trends
- weapon usage breakdowns
- comparison charts
- other useful visualizations

---

## Feedback

If you use ULTRASTATS, feedback helps a lot.

Please reach out if you:

- use the mod regularly
- speedrun
- play Cybergrind a lot
- play casually and want better stats
- found a bug
- have feature suggestions

**Discord:** `atomsmasher_1586`

---

## TLDR

ULTRASTATS is meant to be a practical stats backbone for ULTRAKILL:

- save your runs
- organize them cleanly
- browse them in-game
- make future analysis possible

If you care about understanding your gameplay, this is what the mod is being built for.
