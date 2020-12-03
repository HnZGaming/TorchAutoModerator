# TorchShittyShitShitter

Auto moderation with shit load of configs.

## Dependencies

* Profiler plugin

## Configs (Torch UI)

* `Enable broadcasting` -- Enables GPS broadcasting of laggy grids.
* `Broadcast to Admins only` -- Broadcasts GPS to Moderators and above only.
* `First idle seconds` -- First N seconds of the session (warm-up period) to not scan grids.
* `Threshold ms/f per online member` -- Threshold "lagginess" to start broadcasting grids of a laggy faction. See below for details.
* `MAX GPS count` -- Maximum number of GPS entities to show up at once on players' HUD.
* `Window time (seconds)` -- N seconds to wait until a laggy faction's grids get broadcasted.
* `GPS lifespan (seconds)` -- N seconds to keep showing GPS entities after the faction is no longer laggy.
* `Threshold sim speed` -- Maximum sim speed to enable GPS broadcasting.
* `Muted players` -- Steam ID of players who have opted out using the `!lg mute` command.

## Commands

* `!lg on` -- Enable GPS broadcasting. Equivalent to `Enable broadcasting` confing.
* `!lg off` -- Disable GPS broadcasting. Equivalent to `Enable broadcasting` confing.
* `!lg mspf` -- Get or set the current ms/f threshold per online member. Equivalent to `Threshold ms/f per online member` config.
* `!lg ss` -- Get or set the current sim speed thershold. Equivalent to `Threshold sim speed` config.
* `!lg clear` -- Clear all GPS entities populated by this plugin from all players' HUD.
* `!lg show` -- Show the list of GPS entities populated by this plugin.
* `!lg mute` -- (For players) Unsubscribe from GPS broadcasting.
* `!lg unmute` -- (For players) Subscribe to GPS broadcasting.
* `!lg unmute_all` -- Force every player to subscribe to GPS broadcasting.

## Calculate Threshold ms/f Per Online Member

`ms/f` stands for `milliseconds per frame`; a unit that represents how "laggy" given game entity is.
This plugin generally tries to measure ms/f of each player in a "fair" way, that is, divide a faction's total ms/f by its current online member count.
For "single" players, we consider them as a one-man faction (divide the sum of all his/her grids by 1).
For "unowned" grids, we consider them as of one "token" player's one-man faction (divide the sum of all unowned grids by 1).
For any factions or grids all of whose members are offline, divide by 1.
This plugin will then broacast the laggiest grid of each laggy faction.

## Guideline

1. Tick on `Broadcast to Admins only` first so that your configuration will not broadcast every grid to every player. This option is ticked on by default.
2. Try to begin with a generous configuration. Set `Threshold sim speed` less than `0.7` so that broadcasting will only kick in when the server is really under pressure.
3. Measure how long your server takes to "warm up" on a startup. For a medium-to-large server, the warm-up usually takes 1-3 minutes. `First idle seconds` should be set according to your server's warm-up time (plus some dozen seconds) otherwise every grid can appear laggy to the profiler.
4. `MAX GPS count` shouldn't exceed `5` otherwise they can clutter up the HUD.

## Forking & Extending

Core logic of "scanning" (interpreting profiler result) is defined in separate classes in `TorchShittyShitShitter.Core.Scanners.*` namespace. 
To add new "scanners", implement an interface `ILagScanner` and register the instance to `ShittyShitShitterPlugin`.
