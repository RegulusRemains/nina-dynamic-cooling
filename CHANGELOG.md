# Changelog

## 1.7.1.0
- The Dew Heater Control margin now defaults to 5 °C instead of 3 °C. The camera's front window runs colder than the surrounding air on clear nights, so leading the dew point by a few degrees switches the heater on before the glass itself reaches the dew point.
- Documented that the dew heater is only reachable when the camera is connected with its native driver, not ASCOM. If the camera's ASCOM settings have their own anti-dew option, turn it off so it does not fight the trigger.
- Rewrote the plugin description and README.

## 1.7.0.0
- Added a **Dew Heater Control** trigger. Drop it in your sequence and it turns the camera's anti-dew heater **on** when the ambient air gets close to the dew point and **off** again once the air dries out, with a small hysteresis band so it doesn't flap.
- New options on the Plugins ▸ Dynamic Cooling page: a master **Manage the camera's dew heater automatically** toggle and a **Turn on within … °C of the dew point** margin (default 3 °C).
- Dew control reads the dew point from a connected weather device (it computes it from temperature + humidity if the device doesn't report one directly).

## 1.6.1.0
- The **Dynamic Cool Camera** sequencer block now shows a live summary of the active settings — the enabled temperatures, cooling power, and source — instead of a static label.
- Refreshed the screenshot to the new options page.

## 1.6.0.0
- **Simplified the options** down to four settings: **Temperature source**, **Camera cooling power** (max delta below ambient), **Cooling timeout**, and the **Dark library temperatures** grid.
- Removed the separate **Minimum target** and **Fallback target** settings. The coldest enabled dark-library temperature now defines the cold limit, and when no sensor reading is available the plugin uses the warmest enabled temperature (always reachable).
- Removed the **Only step colder** toggle from the UI; between-target re-checks always step colder and never warm the camera back up.
- Clearer wording throughout, and each temperature toggle is boxed with its label so the pairing is unambiguous.

## 1.5.0.0
- **Moved all configuration to the Plugins ▸ Dynamic Cooling options page** (stored per profile). The **Dynamic Cool Camera** instruction no longer has per-step settings — it executes against the shared options wherever you place it.
- Replaced the temperature input with a **5 °C checkbox grid** (+5 down to −40): enable the temperatures you keep dark libraries for, and the plugin only ever cools to one of them.

## 1.4.0.0
- Added a configurable **dark-library temperature** picker, so cooling targets always land on a temperature you have darks for instead of a fixed 5 °C grid.

## 1.3.0.0
- **Merged the two instructions into one.** *Dynamic Cool Readjust* is gone; a single, context-aware **Dynamic Cool Camera** now does both jobs:
  - At the **start of a night** (cooler off or sensor still warm) it performs a full ramped cool-down with the warm-night fallback.
  - Placed in **After Each Target** it re-checks ambient and only nudges the setpoint colder as the night cools — never warming, and backing off if the TEC is already maxed.
- Added an **Only step colder on re-check** option on the instruction (replaces the former Readjust `OnlyColder`).
- Removed the empty plugin Options page — all parameters live on the instruction; the Plugins-tab Description covers usage.
- Migration: sequences that used *Dynamic Cool Readjust* should re-point that step to *Dynamic Cool Camera*; saved parameters (delta, min target, timeout, only-colder) carry over unchanged.

## 1.2.0.0
- First public release of **Dynamic Cooling**.

## 1.1.0.3
- UX: the temperature **Source** is now a labelled dropdown instead of a plain number field. Each entry shows the connected device and its current reading (e.g. "Weather device - PLL ObCo 3.2 (26.1°C)", "Focuser probe - ZWO Focuser (24.8°C)", or "Manual"). Existing sequences are unaffected.

## 1.1.0.2
- Fix: instruction blocks in a loaded sequence rendered without a name header. The name is now set in each instruction's constructor so it persists across save/load (NINA does not re-derive a plugin leaf instruction's name from export metadata on deserialize).
- Polish: use the standard cooling icon (`SnowflakeSVG`).

## 1.1.0.1
- Fix: assigned a unique plugin Identifier (the previous build shared a placeholder GUID with another plugin), which is what blanked the instruction names.
- Internal: clean async rewrite of both instructions; behavior unchanged.

## 1.1.0.0
- Added **Dynamic Cool Readjust** for between-target re-cooling as the night cools.

## 1.0.0.0
- Initial **Dynamic Cool Camera**: ambient-relative cooling with 5 °C step rounding and sustainable-target fallback.
