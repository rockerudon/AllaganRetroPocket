<p align="center">
  <img src="images/icon.png" width="160" alt="Allagan Retro Pocket icon">
</p>

# Allagan Retro Pocket

Allagan Retro Pocket is a multi-system Libretro frontend that runs inside FINAL FANTASY XIV through Dalamud. It keeps a local game library and provides controller support, per-system settings, save states, memory-card saves, firmware management and quick gameplay controls.

## Installation

Open Dalamud Settings with `/xlsettings`, select **Experimental** and add this address under **Custom Plugin Repositories**:

```text
https://raw.githubusercontent.com/rockerudon/AllaganRetroPocket/main/repo.json
```

Enable the repository, save the settings, open `/xlplugins` and search for **Allagan Retro Pocket**.

## Command

```text
/retro
```

This is the only chat command registered by the plugin. The main window can also be opened from Dalamud's plugin installer.

## Library and settings

Games can be added individually or by scanning a folder. Removing a game from the library does not delete the original file.

Settings are stored per system. Available options depend on the selected Libretro core and include input bindings, video, audio, speed, save data, firmware and media controls.

## Building

The project targets .NET 10 and Dalamud API 15.

```powershell
dotnet restore .\AllaganPocket.csproj --locked-mode
dotnet build .\AllaganPocket.csproj -c Release --no-restore
```

Libretro core binaries are not committed to this repository. Put compatible `*_libretro.dll` files in `Cores`, required support files in `Cores/SystemFiles`, and keep the corresponding source and license notices beside the cores you distribute.

Do not commit ROMs, protected BIOS files or proprietary firmware.

## License

Allagan Retro Pocket is distributed under the GNU Affero General Public License version 3 or later. See `LICENSE.md`, `NOTICE` and `THIRD-PARTY-NOTICES.md`.

Support: [Buy Me a Coffee](https://buymeacoffee.com/rockmizx)
