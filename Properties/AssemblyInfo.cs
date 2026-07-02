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

[assembly: AssemblyVersion("1.8.0.0")]
[assembly: AssemblyFileVersion("1.8.0.0")]

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
[assembly: AssemblyMetadata("LongDescription", @"Dynamic Cooling sets the camera's cooling target from the ambient temperature each night instead of using a fixed setpoint. It is meant for observatories where the temperature changes enough from night to night that one fixed setpoint either wastes cooling headroom or stalls the sequence when the cooler cannot reach it.

You set everything once on the Plugins ▸ Dynamic Cooling options page:

Temperature source: read ambient from the weather device (with the focuser probe as a fallback), or from the focuser probe only.
Camera cooling power: how far below ambient your camera can hold, for example 35°C.
Cooling timeout: how long to wait for the camera to reach the target.
Dark library temperatures: tick the sensor temperatures you keep dark frames for.

Add one Dynamic Cool Camera instruction to your sequence. It has no per-step settings. It cools to the coldest ticked temperature the camera can reach for the current ambient, so every light frame matches a dark you already have. Put it at the start of the night for the first cool-down, and in After Each Target to step colder as the night cools. It will not warm the camera back up during a session, and it backs off if the cooler is already maxed out.

For the camera's anti-dew window heater, prefer your camera firmware's anti-dew cooler linkage (ZWO): the heater follows the TEC inside the camera itself, and the camera then revokes external heater control entirely. Without linkage, use NINA's own dew-heater camera setting with the native driver. Either way, leave the heater on whenever the sensor is cooled. Earlier versions shipped a Dew Heater Control trigger that switched the heater from the ambient-to-dew-point spread; 1.8.0 removed it because that models the wrong surface. The window fronts a chamber cooled far below ambient, so it can sit under the dew point while the ambient spread still looks safe, and on humid nights the trigger was permanently on anyway. Always-on while cooling is simpler and correct.

Changelog 1.8.0: removed the Dew Heater Control trigger and its options; leave the heater on while cooling via NINA's camera settings instead.
Changelog 1.7.1: the dew heater margin now defaults to 5°C, and the plugin description was rewritten.
Changelog 1.7.0: added the Dew Heater Control trigger for automatic anti-dew heater management based on how close the ambient temperature is to the dew point.
Changelog 1.6.0: simplified the options to temperature source, cooling power, cooling timeout, and the dark library grid. The enabled temperatures now define the cold limit.
Changelog 1.5.0: moved all configuration to the plugin options page with a 5°C checkbox grid.
Changelog 1.3.0: merged Dynamic Cool Readjust into the single Dynamic Cool Camera instruction.")]
