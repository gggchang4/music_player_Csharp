# MusicPlayerApp

[简体中文](README.zh-CN.md) | English

A local music player client built with `C#`, `WPF`, and `.NET Framework 4.8` for Windows desktop environments.

## Repository

- GitHub: [gggchang4/music_player_Csharp](https://github.com/gggchang4/music_player_Csharp.git)
- Author: `GGG Chang`
- GitHub username: `gggchang4`

## Introduction

MusicPlayerApp is a desktop application focused on local music playback and library management. It includes audio import, playback controls, library browsing, playlists, favorites, search, lyric display, user profile management, and application settings. The project follows an `MVVM`-style structure and uses `SQLite` for local persistence.

## Features

- Import and play local audio files
- Supported audio formats: `mp3`, `wav`, `flac`, `ogg`, `m4a`, `wma`
- Library views for all songs, artists, albums, and favorite albums
- Search across songs, artists, and albums
- Favorite songs and favorite albums
- Create custom playlists and add songs to playlists
- Built-in `Liked Songs` playlist synced with favorite songs
- Core playback controls: play / pause, previous / next, seek, volume, mute, shuffle, repeat
- Local lyric display with priority for `.lrc` files stored alongside audio files
- User profile, theme settings, playback settings, and cache cleanup
- Equalizer settings window with preset persistence

## Tech Stack

- `C#`
- `WPF`
- `.NET Framework 4.8`
- `MVVM Light`
- `NAudio`
- `Entity Framework Core 2.2`
- `SQLite`
- `TagLibSharp`
- `MaterialDesignThemes`
- `Newtonsoft.Json`
- `NLog`

## Project Structure

```text
MusicPlayer/
├─ Audio/           Audio-related processing
├─ Commands/        Command implementations
├─ Controls/        Custom controls
├─ Converters/      Value converters
├─ Data/            DbContext and initialization
├─ Helpers/         Utility helpers
├─ Models/          Domain models
├─ Services/        Player, library, lyric, and user services
├─ UserControls/    Reusable UI controls
├─ ViewModels/      View models
├─ Views/           Pages and windows
└─ MusicPlayerApp.sln
```

## Requirements

- Windows 10 / 11
- Visual Studio 2019 or 2022
- `.NET desktop development` workload installed
- `.NET Framework 4.8 SDK / Targeting Pack`
- NuGet package restore support

## Getting Started

1. Clone the repository

```bash
git clone https://github.com/gggchang4/music_player_Csharp.git
cd MusicPlayer
```

2. Open `MusicPlayerApp.sln` in Visual Studio
3. Restore NuGet packages
4. Set `MusicPlayerApp` as the startup project
5. Build and run with the `Debug` or `Release` configuration

## Usage Notes

- On first launch, the app initializes the local database and default seed data automatically
- Default database location: `%AppData%\MusicPlayerApp\musicplayer.db`
- Log directory: `%AppData%\MusicPlayerApp\Logs`
- Cover cache directory: `%AppData%\MusicPlayerApp\Covers`
- Local cache directory: `%LocalAppData%\MusicPlayerApp\Cache`
- A default user named `DefaultUser` is created automatically
- A default `Liked Songs` playlist is also created automatically
- Use the `Import Music` action in the left sidebar to add local audio files
- If a matching `.lrc` file exists next to a song file, the app will load it as local lyrics

## Screenshots

The screenshot structure is prepared for you. You can place real screenshots under `docs/images/` and replace this section with actual images later.

Recommended filenames:

- `docs/images/main-window.png`
- `docs/images/library-view.png`
- `docs/images/playlist-view.png`
- `docs/images/equalizer-window.png`

Example:

```md
![Main Window](docs/images/main-window.png)
![Library View](docs/images/library-view.png)
```

## Current Notes

- This project is currently designed for local desktop use
- The current user system is suitable for local development/demo scenarios, not production-grade authentication
- Lyrics currently rely mainly on local `.lrc` files, while online lyric lookup can be extended later
- The equalizer currently provides UI configuration and preset persistence; real DSP processing can be improved further

## Roadmap

- Add folder-based library scanning and import workflows
- Improve real-time equalizer audio processing
- Extend lyric and metadata acquisition
- Improve error handling, tests, and release workflow

## Pre-release Checklist

- Add real project screenshots
- Review `.gitignore` to avoid pushing `bin`, `obj`, local databases, or IDE cache files
- Check the codebase and logs for personal paths, credentials, or temporary test data
- Confirm NuGet packages can be restored successfully
- Verify first-run behavior, including database initialization, default user creation, and music import
- Create a GitHub Release with installation notes or packaged binaries if you plan to distribute builds

## Contributing

Issues and pull requests are welcome.

## License

This project is licensed under the [MIT License](LICENSE).
