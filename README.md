PKHeX
=====
<div>
  <span>English</span> / <a href=".github/README-es.md">Español</a> / <a href=".github/README-fr.md">Français</a> / <a href=".github/README-de.md">Deutsch</a> / <a href=".github/README-it.md">Italiano</a> / <a href=".github/README-zhHK.md">繁體中文</a> / <a href=".github/README-zh.md">简体中文</a>
</div>

![License](https://img.shields.io/badge/License-GPLv3-blue.svg)

# This is an ALL-IN-ONE version of PKHeX which includes ALM (Santacrab2's) and Manu's TeraFinder Plugins
## Visit [My Discord](https://shinypkm.com) for the most advanced raidbot and tradebot programs

## Includes the following Plugins
* PasteImporter
* ExportBoxToShowdown
* LegalizeBoxes
* LiveHeX
* LivingDex
* MGDBDownloader
* SmogonGenner
* TransferLivingDex
* URLGenning
* GPSSPlugin
* PKSMBankPlugin
* SettingsEditor
* TeraFinder Plugin
* PokeFileNamer See [instructions](https://github.com/architdate/PokeFilename/wiki/CustomNamer)

Pokémon core series save editor, programmed in [C#](https://en.wikipedia.org/wiki/C_Sharp_%28programming_language%29).


# All of my Projects

## Showdown Alternative Website
- https://genpkm.com - An online alternative to Showdown that has legality checks and batch trade codes built in to make genning pokemon a breeze.

## Scarlet/Violet RaidBot

- [NotRaidBot](https://github.com/bdawg1989/NotPaldeaNET) - The most advanced RaidBot for Scarlet/Violet available, period.
  
## PKHeX - AIO (All-In-One)

- [PKHeX-AIO](https://github.com/bdawg1989/PKHeX-ALL-IN-ONE) - A single .exe with ALM, TeraFinder, and PokeNamer plugins included.  No extra folders and plugin.dll's to keep up with.

## MergeBot - The Ultimate TradeBot

- [Source Code](https://github.com/bdawg1989/MergeBot)

## Grand Oak - SysBot Helper
- A discord bot that helps with legality issues if someone submits a wrong showdown format.  [Join My Discord To Learn More](https://discord.gg/GtUu9BmCzy)
  
![image](https://github.com/bdawg1989/MergeBot/assets/80122551/0842b48e-1b4d-4621-b321-89f478db508b)


Supports the following files:
* Save files ("main", \*.sav, \*.dsv, \*.dat, \*.gci, \*.bin)
* GameCube Memory Card files (\*.raw, \*.bin) containing GC Pokémon savegames.
* Individual Pokémon entity files (.pk\*, \*.ck3, \*.xk3, \*.pb7, \*.sk2, \*.bk4, \*.rk4)
* Mystery Gift files (\*.pgt, \*.pcd, \*.pgf, .wc\*) including conversion to .pk\*
* Importing GO Park entities (\*.gp1) including conversion to .pb7
* Importing teams from Decrypted 3DS Battle Videos
* Transferring from one generation to another, converting formats along the way.

Data is displayed in a view which can be edited and saved.
The interface can be translated with resource/external text files so that different languages can be supported.

Pokémon Showdown sets and QR codes can be imported/exported to assist in sharing.

PKHeX expects save files that are not encrypted with console-specific keys. Use a savedata manager to import and export savedata from the console ([Checkpoint](https://github.com/FlagBrew/Checkpoint), save_manager, [JKSM](https://github.com/J-D-K/JKSM), or SaveDataFiler).

**We do not support or condone cheating at the expense of others. Do not use significantly hacked Pokémon in battle or in trades with those who are unaware hacked Pokémon are in use.**

## Screenshots

![Main Window](https://i.imgur.com/HZs37cM.png)

## Building

PKHeX is a Windows Forms application which requires [.NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0).

The executable can be built with any compiler that supports C# 12.

### Build Configurations

Use the Debug or Release build configurations when building. There isn't any platform specific code to worry about!

## Dependencies

PKHeX's QR code generation code is taken from [QRCoder](https://github.com/codebude/QRCoder), which is licensed under [the MIT license](https://github.com/codebude/QRCoder/blob/master/LICENSE.txt).

PKHeX's shiny sprite collection is taken from [pokesprite](https://github.com/msikma/pokesprite), which is licensed under [the MIT license](https://github.com/msikma/pokesprite/blob/master/LICENSE).

PKHeX's Pokémon Legends: Arceus sprite collection is taken from the [National Pokédex - Icon Dex](https://www.deviantart.com/pikafan2000/art/National-Pokedex-Version-Delta-Icon-Dex-824897934) project and its abundance of collaborators and contributors.

### IDE

PKHeX can be opened with IDEs such as [Visual Studio](https://visualstudio.microsoft.com/downloads/) by opening the .sln or .csproj file.
