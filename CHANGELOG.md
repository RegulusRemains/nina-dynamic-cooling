# Changelog

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
