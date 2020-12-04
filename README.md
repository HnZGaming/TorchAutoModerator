# TorchShittyShitShitter

Auto moderation with a shit ton of configs.

Broadcasts GPS of top grids based on the number of online faction members.
Helps moderate a server with a big number of grids.
Intended to be used as a supplement to LagGridBroadcaster plugin.

## Dependencies

* [Profiler plugin](https://torchapi.net/plugins/item/da82de0f-9d2f-4571-af1c-88c7921bc063)

## Configs (Torch UI)

* `Enable broadcasting` -- Enables GPS broadcasting of top grids.
* `Broadcast to Admins only` -- Broadcasts GPS to Moderators and above only.
* `First idle seconds` -- First N seconds of the session (warm-up period) to not scan grids.
* `Threshold ms/f per online member` -- Threshold "lagginess" to start broadcasting top grids (see below).
* `Threshold sim speed` -- Maximum sim speed to enable GPS broadcasting.
* `MAX GPS count` -- Maximum number of GPS entities to show up at once on players' HUD.
* `Window time (seconds)` -- N seconds to wait until top grids get broadcasted.
* `GPS lifespan (seconds)` -- N seconds to keep showing GPS entities after the faction is no longer laggy.
* `Exempt NPC factions` -- Ignore NPC factions even if they may be laggy.
* `Exempt faction tags` -- Name tags of factions whose grids will not be broadcasted.
* `Muted players` -- Steam ID of players who have opted out using the `!lg mute` command.

## Commands

* `!lg on` -- Tick on `Enable broadcasting`.
* `!lg off` -- Tick off `Enable broadcasting`.
* `!lg mspf` -- Get or set `Threshold ms/f per online member`.
* `!lg ss` -- Get or set `Threshold sim speed`.
* `!lg admins-only` -- Get or set `Broadcast to Admins only`.
* `!lg clear` -- Clear all GPS entities populated by this plugin from all players' HUD.
* `!lg show` -- Show the list of GPS entities populated by this plugin.
* `!lg scan -time=5 -buffered -broadcast` -- Manually invoke scanning with options.
* `!lg profile -time=5 -top=10` -- Profile factions and their per-online-player ms/f.
* `!lg mute` -- (For players) Unsubscribe from GPS broadcasting.
* `!lg unmute` -- (For players) Subscribe to GPS broadcasting.
* `!lg unmute_all` -- Force every player to subscribe to GPS broadcasting.

## "ms/f Per Online Member" And Top Grids

`ms/f` stands for `milliseconds per frame`; a unit that represents how "laggy" given game entity is.
This plugin generally tries to measure ms/f of each player in a "fair" way, that is, divide a faction's total ms/f by its current online member count.

Corer cases: for "single" players, we consider them as a one-man faction (divide the sum of all his/her grids by 1).
For "unowned" grids, we consider them as of one "token" player's one-man faction (divide the sum of all unowned grids by 1).
For any "completely offline" factions, divide by 1.

Each ms/f of online members will be sorted and the first N factions will enter into the broadcast queue (where N is set by `MAX GPS count`).
A broadcasted grid ("top grid") is the first laggiest grid that's possessed by each laggy faction.

## Guideline

1. Tick on `Broadcast to Admins only` first to work on your configuration safely. This option is ticked on by default.
1. Set `Exempt NPC factions` to your need. Should be ticked on for most cases.
1. Register admin factions to `Exempt faction tags`.
1. Measure how long your server takes to "warm up". Every grid appears laggy to the profiler when the server is starting up (for several reasons). Set `First idle seconds` to `180` (3 minutes) to be sure.
1. Try to begin with a generous configuration. Set `Threshold ms/f per online member` to `3.0` and slowly bring it down. Set `Threshold sim speed` less than `0.7` so that broadcasting will only kick in when the server is under a considerable pressure.
1. Set `MAX GPS count` lower than `5` otherwise they can clutter up the HUD of all players.
1. Run `!lg scan` to manually scan top grids. Set `-buffered` to simulate the production result. Set `-broadcast` to broadcast resulting GPS entities (you should set `Broadcast to Admins only`).
1. Run `!lg profile` to profile ms/f of factions per their online member count.
1. Read debug logs by adding NLog rule `name=TorchShittyShitShitter.*` with `minlevel=Debug`.

## Window Time And GPS Lifespan

`Window time` option defines how much time the plugin should wait until a top grid is broadcasted. 
If the top grid's faction "stops being laggy" before the window time, the grid will not be broadcasted.

## Forking & Extending

Core logic of "scanning" (interpreting profiler result) is defined in classes under `TorchShittyShitShitter.Core.Scanners.*` namespace. 
To add new "scanners", implement an interface `ILagScanner` and register the instance to a list of scanners defined in `ShittyShitShitterPlugin.Init()`.
To remove a scanner, comment it out of the list.
