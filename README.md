# Star Sentinel

[![License: MPL 2.0](https://img.shields.io/badge/License-MPL%202.0-brightgreen.svg)](https://opensource.org/licenses/MPL-2.0)

StarSentinel is a N.I.N.A. plugin that monitors the number of detected stars in saved light frames and helps protect imaging sessions from degrading sky conditions.

It provides a Loop Condition for the Advanced Sequencer. The plugin keeps a history of recent star counts, calculates the 80th-percentile value as a reference, and evaluates each new frame against both a relative threshold and an absolute star-count threshold.

A frame is considered bad when the relative star count falls below the configured percentile threshold or when the raw star count is below the configured absolute limit. After a configurable number of consecutive bad frames, the loop condition is set to false and the sequence can stop.

The analysis ignores non-light frames and maintains an in-memory registry of imaging contexts instead of discarding previous data. When the current context changes (filter, exposure, binning, gain, sensor type, or significant field-of-view shift), it switches to the matching context state while keeping the previous contexts available, so the condition can adapt across multiple targets, exposures, and multi-panel mosaics.

This plugin is intended as a lightweight safeguard based on image data rather than external sky sensors. It is heuristic by design: star count can be influenced by focus, filters, target density, and seeing conditions, so use it with appropriate thresholds for your setup.

**⚠️ Important**: StarSentinel is a heuristic and cautionary tool. Star count is not a perfect proxy for sky quality and may be affected by seeing conditions, focus accuracy, filters, or target star density. For fully unattended or critical operations, dedicated hardware solutions are still recommended.

*Special thanks to Roberto Volpini for inspiration and support in the making of this plugin.*

## Manual Installation

To install the plugin manually, download the latest release package and copy the compiled DLL into your N.I.N.A. plugins folder.

1. **Download the latest release**
   - Go to the [Releases](https://github.com/michelegz/nina.plugin.starsentinel/releases) page and download the ZIP file for the latest version.

2. **Extract the release package**
   - Extract the ZIP contents to a temporary folder.

3. **Copy the compiled DLL**
   - Copy the release DLL to:
     ```
     %LOCALAPPDATA%\NINA\Plugins\3.0.0\StarSentinel\
     ```

4. **Restart N.I.N.A.**
   - Launch N.I.N.A. and verify that **Star Sentinel** appears in the Plugins list.

## Development and Building

### Prerequisites

* Visual Studio 2022 or newer
* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* Git installed and available in the system PATH

### Building the Plugin

1. Clone the repository:
   ```sh
   git clone https://github.com/michelegz/nina.plugin.starsentinel.git
   ```
2. Open `nina.plugin.starsentinel.sln` in Visual Studio.
3. Build the solution in `Debug` or `Release`.

The project includes a `Directory.Build.targets` file that:

* generates version info from the latest Git tag
* renames the output DLL to `StarSentinel-<version>.dll`
* copies the new DLL into the local N.I.N.A. plugins folder
* cleans old versioned outputs on `Clean`

## Automatic Versioning

The plugin version is generated automatically during the build from Git tags.

* The latest tag is read from `git describe --tags --abbrev=0`.
* The commit count since that tag is appended to the version.
* The final assembly/file version format is `Major.Minor.Patch.BuildNumber`.

To create a new release, tag a commit with the desired semantic version, for example:

```sh
git tag v1.2.0
```

Then build the project again to produce the new versioned DLL.

## License

This project is licensed under **Mozilla Public License v2.0**.
