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

[assembly: AssemblyVersion("1.6.0.0")]
[assembly: AssemblyFileVersion("1.6.0.0")]

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
[assembly: AssemblyMetadata("LongDescription", @"Dynamic Cooling picks the camera's cooling target from the ambient temperature each night, instead of a fixed setpoint. It is a drop-in replacement for the built-in Cool Camera instruction for observatories where the temperature varies night to night.

Set everything once on the Plugins ▸ Dynamic Cooling options page:

- **Temperature source** — read ambient from the weather device (falls back to the focuser probe) or the focuser probe.
- **Camera cooling power** — how far below ambient your camera can cool (e.g. 35°C).
- **Cooling timeout** — how long to wait for the camera to reach the target.
- **Dark library temperatures** — tick every sensor temperature you keep dark frames for.

A single **Dynamic Cool Camera** instruction (no per-step options) then cools to the COLDEST ticked temperature the camera can actually reach for the current ambient — so every frame matches a dark library you already have. Drop it at the start of a night for a full cool-down, and in After Each Target to step colder as the night cools (it never warms back up mid-session, and backs off if the cooler is already maxed).

Changelog 1.6.0: simplified the options to just temperature source, cooling power (delta), cooling timeout, and the dark-library temperature grid. Removed the separate fallback and minimum-target settings — the enabled temperatures define the cold limit. Clearer wording and a bordered temperature grid.
Changelog 1.5.0: moved all configuration to the plugin options page with a 5°C checkbox grid.
Changelog 1.3.0: merged the former Dynamic Cool Readjust into a single context-aware Dynamic Cool Camera instruction.")]
