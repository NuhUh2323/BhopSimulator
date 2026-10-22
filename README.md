# Tevrix BunnyHop 3D

**A 3D bunnyhop simulator built from scratch in C# using OpenTK.**

Tevrix BunnyHop 3D is a lightweight, experimental first-person parkour game focused on bunnyhopping, movement, and speed.

Unlike many other games, Tevrix BunnyHop 3D does not use Unity, Godot, or Unreal Engine. Instead, it uses C# and OpenTK for OpenGL rendering.

## Features

- **3D BunnyHop Movement** — Jump between platforms, gain speed, and master air strafing.
- **Procedural Map Generation** — Play through automatically generated platform courses.
- **Auto-BHop Bot** — Watch an automated player attempt to complete maps.
- **Bot Racing** — Compete against an AI-controlled opponent.
- **Ghost Replay System** — Watch a translucent red ghost replay your previous attempt.
- **Knife Collection** — Earn coins, unlock knives, and inspect them in first person.
- **In-Game Shop** — Spend your earned coins on new knives.
- **Network Chat** — Communicate through a simple TCP-based chat system.
- **Progress Saving** — Keep your coins, unlocked knives, and progression.

## Controls

| Key           | Action               |
| ------------- | -------------------- |
| W / A / S / D | Movement             |
| Mouse         | Look around          |
| Space         | Jump                 |
| B             | Toggle Auto-BHop     |
| F             | Inspect / Spin Knife |
| Tab           | Open Knife Shop      |
| R             | Start Bot Race       |
| T             | Open Chat            |
| Enter         | Send Chat Message    |

## Requirements

- Windows 10 or Windows 11
- .NET 8 SDK
- OpenGL-compatible graphics hardware

## Installation

1. Download or clone the repository.

2. Open a terminal in the project directory.

3. Restore the dependencies:

   `dotnet restore`

4. Start the game:

   `dotnet run`

## Chat Server

Tevrix BunnyHop 3D includes an experimental TCP chat server.

To start the server, open a terminal and run:

`dotnet run -- --server`

The server uses port `27777` by default.

Then open another terminal and start the game:

`dotnet run`

The chat system is currently experimental and intended primarily for local testing.

## Technology

- **Language:** C#
- **Framework:** .NET 8
- **Graphics:** OpenGL via OpenTK
- **Networking:** TCP sockets
- **Game Engine:** None

## Roadmap

- [x] Basic 3D rendering
- [x] Bunnyhop movement
- [x] Procedurally generated maps
- [x] Automatic bunnyhop bot
- [x] Coin rewards
- [x] Knife shop
- [x] Knife inspection animations
- [x] Ghost replay system
- [x] Bot racing
- [x] Experimental network chat
- [ ] Main menu and settings
- [ ] Improved knife models and animations
- [ ] Better bot movement and pathfinding
- [ ] Online multiplayer racing
- [ ] Public servers and lobbies
- [ ] Leaderboards and time trials
- [ ] More map environments
- [ ] Sound effects and music

## Development Status

**Current version: 0.3 — Experimental Alpha**

Tevrix BunnyHop 3D is an independent hobby project under active development. Features may change, and bugs are expected.

Feedback, bug reports, and contributions are welcome!

## Contributing

Want to improve the project?

You can open an issue to report a bug, suggest a feature, or submit a pull request with improvements.

## License

A license has not yet been selected. Please check the repository for licensing information before redistributing or modifying the project.

---

**Made with C# and OpenTK. No game engine.**
