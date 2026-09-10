# MakeMeAnRTS

A real-time strategy game in C# on .NET 10, drawn with SDL3. One human player against one CPU
opponent, on a procedurally generated map with rivers, lakes, woods and hidden mineral deposits.

There are no asset files. The terrain, the units, the font and the sound effects are all generated in
code, so the whole game builds and runs from a clone with nothing else to fetch.

## Running it

```sh
dotnet run --project src/MakeMeAnRTS            # play
dotnet run --project src/MakeMeAnRTS -- help    # every command and option
```

The map seed is printed at startup. Pass it back to replay the same map:

```sh
dotnet run --project src/MakeMeAnRTS -- play --seed 1234
```

## Playing

You start with a home base, four citizens, a scout and a prospector. Your citizens begin cutting wood
on their own. Win by destroying everything the opponent has.

### Controls

| | |
| --- | --- |
| `WASD` / arrow keys | Pan the camera |
| Mouse wheel | Zoom |
| Left click | Select a unit or building |
| Left drag | Box-select your units |
| Shift + click | Add to the selection |
| Right click | Do the sensible thing to whatever you clicked |
| `L` `M` `B` `R` `H` | Place a lumberjack, mine, barracks, archery range or home base |
| `1` – `4` | Train a unit at the selected building |
| `Backspace` | Cancel the last unit queued |
| `Tab` | Cycle the mineral heat map |
| `Space` | Jump to your home base |
| `.` | Select the next idle worker |
| `F1` | Debug overlay: unit routes and order targets |
| `Esc` | Cancel placement, then clear the selection |

Right click is context-sensitive and resolves per unit, so a mixed selection sent at one tile does the
right thing for each of them: citizens cut trees, work mines and raise buildings; soldiers attack;
prospectors survey.

### Prospecting

**A mine can only be built on ground you have surveyed.** Mineral abundance is hidden until a
prospector reads it, and this is the one rule that is not standard RTS convention:

1. Right click open ground with the prospector selected. It walks there and surveys a radius of 10.
2. It plants a sign recording what it found, so you can see later where the good ground was without
   remembering it.
3. `Tab` cycles a heat map of stone, iron and gold. It draws **only what you have surveyed** - the game
   never shows you the true map.

Mines draw from the four tiles they stand on, so deposits are finite and mines run out. An exhausted
mine is drawn with a `-` and reported in the selection panel. Mines are the one building that can be
placed on bare rock, which is where the richest seams tend to be.

### Economy

Workers gather and then haul: each load goes to the nearest drop-off, so a lumberjack near the woods
pays for itself by shortening the walk. Wood goes to a lumberjack or the home base, ore goes to the
home base. Whoever builds a mine starts working it.

Woods slow movement rather than blocking it, and have to be cleared before you can build on them.

## Tools

The simulation has no dependency on SDL, and the renderer can draw to an offscreen surface, so the game
can be inspected from a terminal with no display attached. These are the tools used to build it.

```sh
# Render any map seed to an image, with terrain, wood and mineral statistics.
dotnet run --project src/MakeMeAnRTS -- map-preview --seed 7 --overlay iron

# Zoom in to check how something is drawn at the size a player sees it.
dotnet run --project src/MakeMeAnRTS -- map-preview --seed 7 --tiles 40 --scale 18

# Run a whole match with no window and print what each side owns as time passes.
dotnet run --project src/MakeMeAnRTS -- simulate --seed 7 --seconds 900 --cpu-vs-cpu

# Fast-forward a match and save a picture of the real game view.
dotnet run --project src/MakeMeAnRTS -- screenshot --seed 7 --seconds 600 --debug

# SDL can only write BMP without extra native dependencies.
tools/bmp2png.py screenshots/game-7.bmp
```

`simulate --cpu-vs-cpu` puts the CPU on both sides. It is the fastest way to find out whether a change
to the economy or the opponent actually works: a match that would take an hour to play runs in seconds
and prints its result as numbers.

## Layout

Code is grouped by feature before technical layer, so everything one part of the game needs sits
together - including how it draws itself.

```
src/MakeMeAnRTS/
  Core/            Grids, vectors, seeded noise and RNG
  Engine/          SDL3 platform, primitive renderer, in-code font, audio, screenshots
  Features/
    World/         Terrain, tiles, minerals, and the map generator
    Pathfinding/   Grid A*
    Match/         Match state, players, resources, and the command API
    Units/         Unit kinds, orders and behaviour
    Buildings/     Building kinds, construction and training
    Prospecting/   Surveys, per-player mineral knowledge, signs
    Vision/        Fog of war
    Camera/        World-to-screen transform, panning and zooming
    PlayerControl/ Selection, orders and building placement
    Ai/            The CPU opponent
    Hud/           Interface layout and drawing
    Audio/         Synthesised sound effects
    Debug/         The F1 overlay
  Cli/             Command line entry points
  Game/            Composition: the game screen and the frame loop
tools/             Development scripts
```

Both sides play through the same `MatchCommands` API and the CPU reads only its own surveyed knowledge
and explored ground, so it cannot mine what it has not surveyed or march on a base it has not found.
There are no factions: one stat table serves both players.
