# Changelog

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
