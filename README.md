# CS-Jukebox V2

Play your own custom, reactive music kits in Counter-Strike 2 using official Game State Integration.

https://github.com/user-attachments/assets/c1716a56-5743-4b0f-a908-a70c812e4016



[Watch the Trailer](https://www.youtube.com/watch?v=s9UX5aizHTY)
---

## 🚀 Getting Started

### Prerequisites & Download

1. Download the latest compiled package:
   **[Download CS-Jukebox V2 Release](https://github.com/TYFALY/CS-Jukebox-V2/releases/download/v1.0.1/CS.Jukebox.v2.rar)** *(Windows only)*

2. Extract the `.rar` archive to a folder on your PC.

---

## 📦 Installation & First-Time Setup

1. Launch `CS-Jukebox.exe`

2. The application will prompt you to select your CS2 root directory.

Navigate to:

```text
...\Steam\steamapps\common\Counter-Strike Global Offensive\game
```

3. If Counter-Strike 2 is already running, restart the game after setup to initialize the integration.

---

## 🛠️ Troubleshooting — Directory Detection Fix

If the built-in file browser fails to recognize your installation path:

1. Open your Counter-Strike 2 `game` folder in Windows Explorer.
2. Create a blank text file named:

```text
csgo.txt
```

3. Rename the extension from `.txt` to `.exe` so it becomes:

```text
csgo.exe
```

4. Run the CS-Jukebox directory browser again and re-select the folder.

The application should now successfully verify the path.

---

## 🎵 Creating Custom Music Kits

All custom audio tracks must be provided by the user.

For optimal timing, seamless transitions, and responsive gameplay syncing, these durations are recommended:

| Event / Track        | Ideal Length       | Purpose              |
| -------------------- | ------------------ | -------------------- |
| Main Menu            | Ambient / Loopable | Idle menu music      |
| Round Start          | 5–10 sec           | Spawn intro          |
| Action / Choose Team | 10–15 sec          | Warmup & team select |
| MVP Anthem           | ~10 sec            | Round MVP            |
| Bomb Planted         | ~40 sec            | Bomb timer tension   |
| 10-Second Count      | Exactly 10 sec     | Final warning        |
| Round Won / Lost     | ~10 sec            | Round result outro   |

### Configuration Steps

1. Click **Add** on the dashboard.
2. Enter a unique name for your music kit.
3. Click **Browse** next to each event trigger and assign audio files.
4. Click **Save**.

---

## ⚙️ How It Works

CS-Jukebox uses Valve’s official **Game State Integration (GSI)** system to react to real-time gameplay events such as:

* Round phases
* Bomb states
* Team changes
* MVP events

The application:

* Does **not** inject code
* Does **not** modify game memory
* Does **not** bypass anti-cheat systems

✅ 100% VAC Safe

---

## ❤️ Support the Project

If you enjoy CS-Jukebox V2 and want to support future updates, you can donate here:

**PayPal:**
https://www.paypal.com/paypalme/TYFALY

Support is completely optional, but always appreciated.

---

## 👥 Credits

* Original framework architecture by **rakijah (CSGSI)**
* CS2 endpoint migration & updates by **TimosCodd**
* Playback loop fixes, UI improvements & V2 distribution by **TYFALY**
