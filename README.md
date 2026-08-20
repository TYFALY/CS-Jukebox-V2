<div align="center">

  <img width="550" alt="CS-Jukebox V2 Banner" src="https://github.com/user-attachments/assets/539b2d20-e612-43bc-a9cc-b5609a9bedfa" />

  <h1>CS-Jukebox-V2 🎧</h1>

  <p><b>A modern, lightweight, and VAC-safe custom music kit player for Counter-Strike 2.</b><br/>
  <i>Powered by Valve's official Game State Integration (GSI) framework.</i></p>

  <p>
    <a href="https://github.com/TYFALY/CS-Jukebox-V2/releases/latest"><img src="https://img.shields.io/github/v/release/TYFALY/CS-Jukebox-V2?style=for-the-badge&color=007ACC&logo=github" alt="Release"></a>
    <a href="#prerequisites--download"><img src="https://img.shields.io/badge/Platform-Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white" alt="Platform"></a>
    <a href="https://dotnet.microsoft.com/download"><img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt="Target"></a>
    <a href="#about-the-project"><img src="https://img.shields.io/badge/VAC-100%25_Safe-2ea44f?style=for-the-badge&logo=shield" alt="VAC Safe"></a>
    <a href="https://www.paypal.com/paypalme/TYFALY"><img src="https://img.shields.io/badge/Donate-PayPal-00457C?style=for-the-badge&logo=paypal&logoColor=white" alt="Support"></a>
  </p>

  <p>
    <a href="https://github.com/TYFALY/CS-Jukebox-V2/releases/latest">📥 Download Release</a> •
    <a href="#-features">✨ Features</a> •
    <a href="#-getting-started">🚀 Quick Start</a> •
    <a href="#-custom-music-kit-guide">🎵 Music Kit Guide</a> •
    <a href="#️-building-from-source">🛠️ Build from Source</a>
  </p>

</div>

## 🎬 Showcase

https://github.com/user-attachments/assets/c1716a56-5743-4b0f-a908-a70c812e4016

## 🎵 Community Custom Kits

Download community-created kits or share your own on our official subreddit:

👉 **[r/CSJukeboxV2Kits](https://www.reddit.com/r/CSJukeboxV2Kits)**

---
## 💡 About the Project

**CS-Jukebox V2** lets you bring your favorite custom soundtracks directly into Counter-Strike 2. By leveraging Valve's official Game State Integration (GSI), the player reacts dynamically to live match events such as round start, bomb plants, clutch 10-second warnings, and MVP anthems without modifying game files or memory.

> [!IMPORTANT]
> **100% VAC & Anti-Cheat Safe**  
> CS-Jukebox operates purely as a local HTTP listener receiving JSON telemetry natively broadcasted by CS2. It does **not** hook processes, inject DLLs, read/write game memory, or modify CS2 executable binaries.

---

## ✨ Features

*  **Real-Time Match Synchronization:** Instantly reacts to freeze time, round start, bomb plant, 10-second defuse warnings, player death, round outcome, and MVP anthems.
*  **Zero Risk / VAC Safe:** Built strictly on Valve's documented GSI protocol. Fully safe for Premier, competitive matchmaking, and third-party platforms.
* **Independent Audio Engine:** Powered by [NAudio](https://github.com/naudio/NAudio) for low-latency playback without relying on legacy Windows Media Player components.
* **Smart Steam Discovery:** Automatically scans all connected Steam libraries (`libraryfolders.vdf`) across your drives to locate CS2 and configure the integration file.
*  **Intuitive Kit Dashboard:** Easily create, manage, switch, and test custom audio kits using `.mp3` and `.wav` formats.

---
## 🚀 Getting Started

### Prerequisites & Download

1. Download the latest compiled release archive:  
   👉 **[Download CS-Jukebox V2 (Latest Release)](https://github.com/TYFALY/CS-Jukebox-V2/releases/latest)** *(Windows only)*
2. Extract the downloaded archive to any folder on your PC.

---

## 📦 Installation & Setup

1. Launch `CS-Jukebox.exe`.
2. The application will automatically scan your Steam libraries and detect your CS2 installation directory.
   > [!TIP]
   > If automatic detection fails, click **Browse** and manually select your CS2 `game` folder:  
   > `...\Steam\steamapps\common\Counter-Strike Global Offensive\game`
3. Launch Counter-Strike 2, open the developer console (`~`), and run:
   ```cfg
   snd_music_volume 0
   ```
   *(This mutes default in-game music kits while keeping sound effects, gunshots, footsteps, and voice chat intact).*
4. Restart Counter-Strike 2 if it was already open to initialize the integration config.

---
## 🎵 Custom Music Kit Guide

### Audio Duration & Timing Reference

> [!NOTE]  
> Tracks exceeding the ideal duration will automatically cut off or fade smoothly when the next gameplay event triggers.

| Event / Track | Minimum Length | Ideal Duration | Behavior & Engine Context |
| :--- | :---: | :---: | :--- |
| **Main Menu** | Loopable | Ambient / Seamless | Loops continuously while in the main menu or lobby. |
| **Freeze Time** | 10 sec | 10–15 sec | Plays during the pre-round buy phase (15s timer). |
| **Round Start** | 5 sec | 5–10 sec | Plays at round start; automatically fades out after ~5s. |
| **Bomb Planted** | 30 sec | 30 sec | Plays on bomb plant (Premier bomb timer is 40s total). |
| **10-Sec Warning** | 10 sec | **Exactly 10 sec** | Plays when exactly 10s remain on round or bomb timer. |
| **MVP Anthem** | **7 sec** | **7 sec** | Plays when you earn round MVP; fades into the next freeze time. |
| **Round Won / Lost** | **7 sec** | **7 sec** | Fallback round-end track played when you are not the MVP. |
| **Player Death** | 3 sec | 3–5 sec | Secondary audio cue triggered immediately when you die. |

### How to Create & Assign Kits

1. Open the CS-Jukebox dashboard and click **Add**.
2. Enter a unique name for your custom music kit.
3. Click **Browse** next to each match event to assign your `.mp3` or `.wav` audio files.
4. Click **Save** and select your newly created kit from the active list.

---
## ⚙️ How It Works

```text
┌─────────────────┐        HTTP POST (JSON)        ┌──────────────────┐        Audio Output
│  Counter-Strike │ ─────────────────────────────> │    CS-Jukebox    │ ───────────────────> Speakers /
│       2         │   Valve GSI (Localhost:Port)   │   (NAudio Engine)│                      Headphones
└─────────────────┘                                └──────────────────┘
```

1. **Native Game State Integration:** CS2 natively sends JSON payloads containing real-time match events (round phases, bomb timers, player health, MVP states) to a local HTTP endpoint.
2. **Event Parsing:** CS-Jukebox receives these payloads asynchronously with zero impact on game performance.
3. **Dynamic Audio Routing:** The internal NAudio playback engine triggers, crossfades, or cuts off custom audio tracks based on current match conditions.

---

## 🛠️ Building from Source

### Prerequisites

Make sure you have the following installed:

* [.NET 8.0 SDK](https://dotnet.microsoft.com/download)
* [Git](https://git-scm.com/)
* [Visual Studio 2022](https://visualstudio.microsoft.com/) *(with .NET desktop development workload)* or [VS Code](https://code.visualstudio.com/)

### Clone & Build

```bash
# 1. Clone the repository
git clone https://github.com/TYFALY/CS-Jukebox-V2.git
cd CS-Jukebox-V2

# 2. Build the project in Release configuration
dotnet build -c Release
```

### Output Location

After a successful build, the executable will be available at:

```text
bin/Release/net8.0-windows/CS-Jukebox.exe
```

---
## ❓ Troubleshooting

<details>
<summary><b>🔍 Automatic game detection failed</b></summary>
<br>

CS-Jukebox scans your Steam directories via `libraryfolders.vdf`. If automatic detection does not find your game:
1. Click **Browse** in the directory selection window.
2. Manually select your CS2 `game` folder that contains both `bin\win64\cs2.exe` and `csgo\cfg` (typically located at `...\Steam\steamapps\common\Counter-Strike Global Offensive\game`).
</details>

<details>
<summary><b>🔇 No music plays during matches</b></summary>
<br>

1. Ensure `CS-Jukebox.exe` is running while playing.
2. Verify that the GSI config file was created at:  
   `...\Counter-Strike Global Offensive\game\csgo\cfg\gamestate_integration_csjukebox.cfg`
3. Restart Counter-Strike 2 so the game engine loads the integration file.
4. Verify your audio tracks are valid, uncorrupted `.mp3` or `.wav` files.
</details>

<details>
<summary><b>🔊 In-game music overlaps with custom tracks</b></summary>
<br>

Disable CS2 default music kits by opening the in-game developer console (`~`) and running:
```cfg
snd_music_volume 0
```
</details>

<details>
<summary><b>📁 Audio format & codec support</b></summary>
<br>

`.mp3` and `.wav` are fully supported out-of-the-box via NAudio. Formats such as `.m4a`, `.aac`, or `.wma` rely on Windows Media Foundation codecs installed on your operating system.
</details>

---

## ❤️ Support the Project

If you enjoy using **CS-Jukebox V2** and want to support ongoing development, maintenance, and future updates, donations are greatly appreciated!

<div align="center">

[![Donate with PayPal](https://img.shields.io/badge/Donate-PayPal-00457C?style=for-the-badge&logo=paypal&logoColor=white)](https://www.paypal.com/paypalme/TYFALY)

</div>

---

## 👥 Credits & Acknowledgments

* **[TYFALY](https://github.com/TYFALY)** – V2 architecture redesign, .NET framework upgrade, self-contained builds (embedded dependencies), kit import/export system, UI redesign, and general QoL maintenance.
* **[rakijah (CSGSI)](https://github.com/rakijah/CSGSI)** – Original CSGSI library architecture.
* **[TimosCodd](https://github.com/TimosCodd)** – CS2 GSI endpoint updates and schema references.
* **[alextmsv](https://github.com/alextmsv)** – Added multi-track support, NAudio integration, player death triggers and dynamic volume controls.
---

<div align="center">
  <sub><b>Disclaimer:</b> Counter-Strike, CS2, and Valve are trademarks and/or registered trademarks of Valve Corporation. This project is open-source and is not affiliated with, endorsed by, or connected to Valve Corporation.</sub>
</div>
