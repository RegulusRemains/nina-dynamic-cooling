# Dynamic Cooling for N.I.N.A.

Dynamic camera cooling for [N.I.N.A.](https://nighttime-imaging.eu/) (Nighttime Imaging 'N' Astronomy). Instead of a fixed cooling setpoint, Dynamic Cooling sets the target from the ambient temperature each night and snaps it to a temperature you actually keep dark frames for. Your lights always match a dark library, and the sequence does not stall because the cooler cannot reach an over-ambitious setpoint.

It adds an Advanced Sequencer instruction, **Dynamic Cool Camera**. Place it at the start of a night for a full cool-down, and in **After Each Target** to keep stepping colder as the night cools; it adjusts what it does based on where you place it. The sequencer item can override the cooling time for an individual step, while its other configuration comes from the plugin's options page.

![Dynamic Cooling options page](assets/options.jpg)

## Setup: Plugins ▸ Dynamic Cooling

Open **Options → Plugins → Dynamic Cooling** and set these once (they apply to every *Dynamic Cool Camera* step, and are stored per profile):

| Setting | Default | Meaning |
|---------|---------|---------|
| **Temperature source** | Weather device | Where to read ambient temperature. *Weather device (or focuser)* uses the weather device and falls back to the focuser's probe if it's unavailable; *Focuser probe* uses the focuser only. |
| **Camera cooling power** | 30 °C | How far below ambient your camera's cooler can pull. Most cooled CMOS cameras sustain about 30 °C in the field; raise it only if yours reliably holds more. |
| **Cooling timeout** | 5 min | How long to wait for the camera to reach the target before the sequence continues. |
| **Dark library temperatures** | 0, −5, −10, −15, −20 °C | Tick every sensor temperature you keep dark frames for (a 5 °C grid from +5 down to −40 °C). The plugin only ever cools to one of these. |

## How it works

**Dynamic Cool Camera** lives under the **Dynamic Cooling** category in the Advanced Sequencer's instruction list. Its optional **Cooling time** field overrides the plugin-wide cooling timeout for that sequence step, in minutes; leave it empty to use the value from **Options → Plugins → Dynamic Cooling**. Place it wherever you want cooling managed:

- **Start of a night** (cooler off, or the sensor is still well above target) → full cool-down. It reads ambient, computes `ambient − cooling power`, and snaps to the **coldest enabled dark-library temperature the camera can actually reach** (rounding toward warmer so the target is achievable). If the TEC still can't hold it on a warm night, it steps back to a sustainable enabled temperature so the sequence proceeds instead of hanging.
- **After Each Target** (cooler already on and cold) → it re-reads ambient and steps the camera **colder** as the night cools, moving between enabled temperatures only. It never warms the camera back up mid-session, and it backs off when the TEC is already straining (> 90 % power).

If no temperature reading is available at all, it falls back to the **warmest enabled** temperature (always reachable). If you enable *no* temperatures, it reverts to the legacy behavior of snapping to a uniform 5 °C grid.

## What about the camera's dew heater?

Versions 1.7.x shipped a **Dew Heater Control** trigger that switched the camera's anti-dew window heater based on the ambient − dew point spread. **It was removed in 1.8.0** because it modeled the wrong surface: the window sits on a chamber cooled 20–35 °C below ambient, so its temperature tracks the *sensor*, not the air. The window can be below the dew point while the ambient spread still looks safe (a false OFF on dry nights), and on humid nights the spread test kept the heater on permanently anyway — the ambient model was either wrong or redundant. As a trigger it also stopped evaluating the moment the sequence ended, stranding the heater in its last state (issues [#3](https://github.com/RegulusRemains/nina-dynamic-cooling/issues/3), [#4](https://github.com/RegulusRemains/nina-dynamic-cooling/issues/4)).

**Do this instead — in order of preference:**

1. **Camera firmware "anti-dew cooler linkage"** (ZWO cameras, in the driver/firmware settings): the heater follows the TEC — on whenever the sensor is cooling, off when it stops. This is the on-while-cooling policy enforced inside the camera itself: it survives software crashes and unsafe shutdowns, self-cancels at warm-up, and cannot be stranded on. Note that once linkage is enabled the camera revokes external control of the heater — the SDK stops reporting the anti-dew control as writable, so NINA's dew-heater toggle disappears from the camera panel (`HasDewHeater` goes false). That is by design: one authority. It also means any software heater control — including the removed 1.7.x trigger — would silently no-op with linkage on.
2. **NINA's own dew-heater camera setting** (native driver only — the ASCOM camera interface has no dew-heater member): applied at connect, effectively always-on while connected. Use this if your camera has no cooler linkage.

Either way the principle is the same: leave the window heater on whenever the sensor is cooled. It is the physically correct policy, costs a couple of watts, and needs no automation.

## Why dark-library temperatures?

Only cooling to temperatures you already have darks for keeps every light frame calibratable. No surprise setpoints, and no rebuilding your dark library because a warm night forced an odd temperature. Tick the steps you maintain (commonly 0, -5, -10, -15, -20 °C) and the plugin always lands on one of them.

## Installation

No compiling required. A prebuilt DLL is provided with every release.

**From inside N.I.N.A.** (once it's in the plugin catalogue): *Options → Plugins → Available*, find **Dynamic Cooling**, and click install.

**Manual install (works today):**
1. Download `NINA.Plugin.DynamicCooling.dll` from the [latest release](https://github.com/RegulusRemains/nina-dynamic-cooling/releases/latest).
2. Put it in `%LOCALAPPDATA%\NINA\Plugins\3.0.0\Dynamic Cooling\` (create the `Dynamic Cooling` folder if it doesn't exist).
3. Restart N.I.N.A.

## Building

Requires the .NET 8 SDK (with the Windows Desktop workload). Place the N.I.N.A. reference assemblies in a sibling `..\refs\` folder (copy them from your N.I.N.A. install directory), then:

```
dotnet build -c Release -p:Platform=x64
```

Output: `bin\x64\Release\NINA.Plugin.DynamicCooling.dll`.

## License

[MPL-2.0](LICENSE).

## Tests

Unit tests for the sequencer logic live in `Tests/` (NUnit + Moq). They run against the
real NINA assemblies supplied in `..\refs` (the same set the plugin builds against), on a
Windows machine with the .NET 8 SDK:

```
dotnet test Tests
```
