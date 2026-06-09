using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("Dynamic Cooling")]
[assembly: AssemblyDescription("Dynamic camera cooling based on ambient temperature")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("RegulusRemains")]
[assembly: AssemblyProduct("NINA.Plugin.DynamicCooling")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]

// Unique plugin Identifier.
[assembly: Guid("25ac9c96-885e-4733-a437-a5d4863a1c7e")]

[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]

// NINA Plugin metadata
[assembly: AssemblyMetadata("Homepage", "https://github.com/RegulusRemains/nina-dynamic-cooling")]
[assembly: AssemblyMetadata("Repository", "https://github.com/RegulusRemains/nina-dynamic-cooling")]
[assembly: AssemblyMetadata("License", "MPL-2.0")]
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
[assembly: AssemblyMetadata("ChangelogURL", "https://github.com/RegulusRemains/nina-dynamic-cooling/blob/main/CHANGELOG.md")]
[assembly: AssemblyMetadata("Tags", "Camera,Cooling,Temperature,Automation")]
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.0.0.3001")]
[assembly: AssemblyMetadata("FeaturedImageURL", "")]
[assembly: AssemblyMetadata("ScreenshotURL", "")]
[assembly: AssemblyMetadata("AltScreenshotURL", "")]
[assembly: AssemblyMetadata("LongDescription", @"Dynamic Cooling dynamically picks the camera cooling target from any connected temperature sensor (weather device, focuser probe, or a manual value), instead of a fixed setpoint. It is a drop-in replacement for the built-in Cool Camera instruction for observatories where ambient temperature varies night to night.

A single **Dynamic Cool Camera** instruction handles cooling wherever you place it — it is context-aware:

- At the **start of a night** (cooler off or sensor still warm) it performs a full ramped cool-down: it targets ambient minus a configurable delta, rounded to the nearest 5°C step and clamped to a minimum target. If the TEC cannot reach the target on a warm night, it steps back to a sustainable setpoint automatically so the sequence never stalls on cooling. Manual mode pins a fixed fallback target.
- Placed in **After Each Target** it re-checks ambient and steps the camera colder as the night cools, snapping to 5°C library steps. It skips when the TEC is already straining (>90% power) and, with ‘only step colder’ enabled (default), never warms the camera back up mid-session.

All parameters — temperature source, max delta, minimum target, fallback target, cooling timeout, and ‘only step colder’ — are configured on the instruction itself in the sequencer.

Changelog 1.3.0: merged the former Dynamic Cool Readjust into a single context-aware Dynamic Cool Camera instruction.")]
