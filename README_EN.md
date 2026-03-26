# PineA.BarricadeSaver

English documentation for the project.  
For the original Chinese documentation, see [README.md](README.md).

## Overview

`PineA.BarricadeSaver` is a **RocketMod / Unturned** plugin that saves eligible barricades to **MySQL** when they should be taken off the map, then restores them automatically later.

It is mainly designed for:

- offline barricade hosting / sleeping systems
- reducing the number of persistent barricades on the map
- restoring player or group barricades when they reconnect
- optional barricade count limits based on permissions

## Main Features

- Save eligible barricades to MySQL and remove them from the map
- Automatically restore barricades when a player reconnects
- Support both personal mode and group-based mode
- Restore barricades saved under a player's previous groups without changing the original group ownership
- Compensate farm growth while the barricade was saved offline
- Optional space collision checks before restoration
- Barricade filtering by player, item whitelist, item blacklist, and barricade type
- Permission-based barricade limit system through a separate XML file
- Automatic count correction when barricades are salvaged or destroyed
- Skip restoration for players who have been offline longer than a configured number of days

## How It Works

### When the map finishes loading

The plugin scans the whole map, saves all qualified barricades to the database, removes them from the world, and then recounts the remaining barricades that stay on the map.

### When a player connects

- The player's last online time is updated
- If `UseGroupID = false`, the plugin restores barricades by player SteamID
- If `UseGroupID = true`:
  - players without a group are restored as personal owners
  - players with a group restore the group's barricades only once when the first member comes online
- The plugin can also restore barricades that belong to the player's old groups while keeping the original group ID

### When a player disconnects

- If personal mode is used, the player's eligible barricades are saved and removed
- If group mode is used, the plugin checks whether any group member is still online
- If nobody in the group remains online, the group's barricades are saved and removed

## Filtering Rules

The restore/save decision follows this priority:

1. If the owner is listed in `NotSaveSteamID`, the barricade is ignored
2. If the barricade item ID is in `BarricadeBlacklist`, it is ignored
3. If the barricade item ID is in `BarricadeWhitelist`, it is always handled
4. Otherwise, the barricade is checked against `BarricadeType`

## Farm Growth Compensation

If a restored barricade is a farm (`EBuild.FARM`) and `GrowWhenSaved = true`, the plugin adjusts the farm growth timer based on how long the barricade stayed saved in the database.

## Space Check

If `UseSpaceCheck = true`, the plugin performs overlap checks before restoring barricades to reduce failed placements caused by collisions or overlapping objects.

Different barricade types use different checks internally, such as:

- `OverlapBox` for door-like barricades
- adjusted sphere checks for beds
- sphere checks for most other barricade types

If the position is blocked, the barricade is not restored and is counted as failed.

## Barricade Limit System

The plugin uses a separate config file:

```text
Plugins/PineA.BarricadeSaver/PineA.BarricadeLimit.xml
```

This system supports:

- per-type limits such as `DOOR`, `BED`, or `FARM`
- a global `ALL` limit
- different limits for different permission groups
- whether a type should also count toward `ALL`
- live count updates on placement, salvage, and destruction

The count system is maintained in memory and is updated when:

- a barricade is placed
- a barricade is salvaged
- a barricade is destroyed by other means such as explosions or admin removal

## Configuration

### Main config

The RocketMod main config class is `Config`.

| Setting | Description |
|---|---|
| `MySqlConnectionString` | MySQL connection string |
| `BarricadeTableName` | Table name for saved barricades |
| `GrowWhenSaved` | Enables offline farm growth compensation |
| `UseSpaceCheck` | Enables placement collision checking during restore |
| `UseGroupID` | Saves/restores by group ID instead of only by owner |
| `BarricadeType` | Allowed barricade types |
| `BarricadeWhitelist` | Always-handled barricade item IDs |
| `BarricadeBlacklist` | Never-handled barricade item IDs |
| `NotSaveSteamID` | Players whose barricades are not saved/restored |
| `MessageIconURL` | Chat message icon URL |
| `Message_RestorePartial` | Personal restore message for partial success |
| `Message_RestoreAll` | Personal restore message for full success |
| `Message_RestorePartial_Group` | Group restore message for partial success |
| `Message_RestoreAll_Group` | Group restore message for full success |
| `NotRestoreIfOfflineDays` | Do not restore if the player has been offline longer than this |

### Default main config example

```xml
<Config>
  <MySqlConnectionString>Server=127.0.0.1;Port=3306;Database=myDB;Uid=user;Pwd=pass;</MySqlConnectionString>
  <BarricadeTableName>PineA_Barricade</BarricadeTableName>

  <GrowWhenSaved>true</GrowWhenSaved>
  <UseSpaceCheck>true</UseSpaceCheck>
  <UseGroupID>false</UseGroupID>

  <BarricadeType>
    <string>DOOR</string>
    <string>BED</string>
    <string>STORAGE</string>
    <string>FARM</string>
  </BarricadeType>

  <BarricadeWhitelist>
    <ushort>288</ushort>
  </BarricadeWhitelist>

  <BarricadeBlacklist>
    <ushort>289</ushort>
  </BarricadeBlacklist>

  <NotSaveSteamID>
    <ulong>76561198020988945</ulong>
  </NotSaveSteamID>

  <NotRestoreIfOfflineDays>7</NotRestoreIfOfflineDays>
  <MessageIconURL>http://example.com</MessageIconURL>
</Config>
```

### Limit config example

```xml
<LimitConfig>
  <BasicSetting Enabled="true"
                PlacementMessage="&lt;b&gt;&lt;size=15&gt;&lt;color=#FFFACD&gt;[Barricade Limit] Your barricade usage: &lt;color=#FFFFFF&gt;{Current}&lt;/color&gt;/&lt;color=#FFFFFF&gt;{Limit}&lt;/color&gt;&lt;/color&gt;&lt;/size&gt;&lt;/b&gt;"
                PlacementIconURL="" />

  <BarricadeLimit Type="ALL" Count="400" RequiredPermission="default" CountInToALL="true" />
  <BarricadeLimit Type="ALL" Count="500" RequiredPermission="VIP" CountInToALL="true" />
</LimitConfig>
```

### Limit rule fields

Each `BarricadeLimit` entry contains:

| Field | Description |
|---|---|
| `Type` | Barricade type such as `ALL`, `DOOR`, or `FARM` |
| `Count` | Maximum allowed amount |
| `RequiredPermission` | Permission required for that limit |
| `CountInToALL` | Whether this type also counts toward `ALL` |

## Database

### Saved barricade table

The main table name is controlled by `BarricadeTableName`. The default value is:

```text
PineA_Barricade
```

Stored fields:

- `ID`
- `InstanceID`
- `BarricadeItemID`
- `Owner`
- `Group`
- `Health`
- `State`
- `PosX`
- `PosY`
- `PosZ`
- `RotX`
- `RotY`
- `RotZ`
- `SavedTime`

### Last online time table

The plugin also creates:

```text
PineA.LastOnlineTime
```

Stored fields:

- `SteamID`
- `LastOnline`

## Requirements

- Unturned server
- RocketMod
- MySQL
- .NET Framework 4.8 build environment

## Dependencies

The project references these main libraries:

- `MySql.Data`
- `HarmonyLib` / `0Harmony`
- `Rocket.API`
- `Rocket.Core`
- `Rocket.Unturned`
- `Assembly-CSharp`
- `com.rlabrecque.steamworks.net`
- `UnityEngine`

## Installation

1. Build the project and generate the plugin DLL
2. Copy the DLL to your server plugin directory
3. Make sure RocketMod is installed
4. Make sure the server can connect to MySQL
5. Start the server once so the plugin can create its config files and database tables
6. Edit the generated config files
7. Restart the server after changing the configuration

## Source Structure

Important source files include:

- `BarricadeHelper.cs`
- `BarricadeLimit.cs`
- `BarricadeSaverLogic.cs`
- `Config.cs`
- `ConfigurationHelper.cs`
- `DatabaseManager.cs`
- `LastOnlineTimeManager.cs`
- `LimitConfig.cs`
- `Main.cs`
- `Patch.cs`
- `PineA_BarricadeData.cs`
- `Tool.cs`

## Commands

This project does not currently define custom chat commands.

The plugin runs automatically through game and server events such as:

- map load
- player connect
- player disconnect
- barricade placement
- barricade salvage
- barricade destruction patch hooks

## Notes

- Storage barricades are cleared before removal to avoid unwanted item drops
- Successful restores are deleted from the database after placement
- In group mode, restoration is intentionally avoided when another member of the same group is already online
- If a player has been offline longer than `NotRestoreIfOfflineDays`, their saved barricades are skipped during restoration
