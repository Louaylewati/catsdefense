# Cat Defense

A small 2D wave-defense game made with Unity. Pick an armed cat, move it around the defense zone in front of the barricade, shoot the zombies, earn coins and upgrade your weapon. There are 2 levels; the second ends with a boss that holds another cat hostage.

---

## Requirements

| What | Version / notes |
|---|---|
| **Unity Editor** | **2022.3.62f3** (LTS). Use this exact version to avoid upgrade prompts. |
| **Unity Hub** | Latest version. |
| **Git** | Needed to clone the repo, and Unity also uses it to download one package listed in `Packages/manifest.json`. |
| OS | Windows 10/11 (64-bit) or Linux (Ubuntu 22.04+ or similar, 64-bit) |
| Disk | About 2 GB free (the project plus the `Library` folder Unity builds on the first open) |

---

## 1. Install Git

**Windows:** download and install from <https://git-scm.com/download/win>. Keep the default options. Then open a **new** terminal (or restart Unity Hub) so `git` is on the PATH.

**Linux (Ubuntu/Debian):**
```bash
sudo apt update
sudo apt install git
```

Check it works:
```bash
git --version
```

---

## 2. Install Unity Hub

**Windows:** download Unity Hub from <https://unity.com/download> and run the installer.

**Linux (Ubuntu/Debian):**
```bash
wget -qO - https://hub.unity3d.com/linux/keys/public | gpg --dearmor | sudo tee /usr/share/keyrings/Unity_Technologies_ApS.gpg > /dev/null
sudo sh -c 'echo "deb [signed-by=/usr/share/keyrings/Unity_Technologies_ApS.gpg] https://hub.unity3d.com/linux/repos/deb stable main" > /etc/apt/sources.list.d/unityhub.list'
sudo apt update
sudo apt install unityhub
```
Other distributions can use the Unity Hub AppImage from <https://unity.com/download>.

Open Unity Hub and sign in with a Unity account. Under **Preferences → Licenses**, add a free **Personal** license.

---

## 3. Install Unity 2022.3.62f3

Choose one way:

- **Easiest:** do step 5 first. When Unity Hub sees the project needs 2022.3.62f3, it offers to install it. Click **Install**.
- **Manual:** go to <https://unity.com/releases/editor/archive>, find **2022.3.62f3** and click **Install** (it opens in Unity Hub).

When Unity Hub asks which modules to add, pick:
- **Windows Build Support (Mono)** on Windows, or **Linux Build Support (Mono)** on Linux. You only need this if you want to build a standalone game.
- Optional: **WebGL Build Support** to build a browser version.

---

## 4. Clone the project

```bash
git clone https://github.com/SaifRbai/CatDefense.git
```

> Tip (Windows): clone into a short path without special characters, e.g. `C:\Projects\CatDefense`.

---

## 5. Open the project

1. Open **Unity Hub → Projects → Add → Add project from disk**.
2. Select the cloned `CatDefense` folder (the one that contains `Assets`, `Packages` and `ProjectSettings`).
3. Click the project to open it with **2022.3.62f3**.

The **first open takes a few minutes**: Unity rebuilds the `Library` folder, imports all the art and downloads the packages. Later opens are fast.

---

## 6. Play

1. In the **Project** window, open the scene **`Assets/Game/Scenes/Main.unity`** (double-click it).
2. In the **Game** view, set the aspect to **16:9** (or **1280×720**).
3. Press **Play** ▶.

### Controls

| Action | Key |
|---|---|
| Move | **W A S D** or the arrow keys |
| Hop (dodge zombies) | **Space** |
| Aim | Mouse |
| Shoot | Hold **left click** |
| Reload | **R** (automatic when the magazine is empty) |
| Special ability | **E**, **Q** or **right click** (or the button at the bottom right) |
| Open upgrades (between waves) | **U** |
| Pause | **Esc** |

---

## 7. Build a standalone game (optional)

1. **File → Build Settings…**
2. Check that `Assets/Game/Scenes/Main.unity` is in **Scenes In Build**.
3. Choose your platform:
   - **Windows, Mac, Linux** → Target Platform **Windows** or **Linux**.
   - **WebGL** for a browser build (needs the WebGL module).
4. Click **Build** and choose an output folder **outside** the project (e.g. `CatDefense-Build`).
5. Run the game:
   - Windows: `CatDefense.exe`
   - Linux: `chmod +x CatDefense.x86_64 && ./CatDefense.x86_64`

---

## Project layout

```
Assets/
  Game/
    Scenes/Main.unity        # the only scene; everything is built from code at runtime
    Scripts/                 # game code (Game, Battle, CatShooter, Enemy, Hostage, ...)
    Editor/                  # editor helpers (import settings, menu "Cat Defense/Create Main Scene")
    Resources/
      Data/                  # cats.json, enemies.json, levels.json: all balancing numbers
      Game/                  # packed sprite sheets, UI, backgrounds, font, shader
  catdefense_assets/         # the original CraftPix art kit (source files)
Packages/                    # Unity package list
ProjectSettings/             # Unity project settings
Tools/pack_sprites.py        # repacks the kit's PNG frames into the sprite sheets in Resources/Game
```

### Changing the balance

All numbers (HP, damage, speed, coins, waves, upgrade costs and so on) are in the JSON files in `Assets/Game/Resources/Data/`. Edit them and press Play again; you don't need to touch any code.

### Repacking the art (only if you change the kit's PNGs)

```bash
pip install pillow
python3 Tools/pack_sprites.py Assets/catdefense_assets/Png Assets/Game/Resources
```

---

## Troubleshooting

| Problem | Fix |
|---|---|
| *"An error occurred while resolving packages"* / git package fails | Git isn't installed or isn't on the PATH. Install it (step 1), restart Unity Hub and reopen the project. |
| Unity Hub asks to **upgrade** the project | Cancel and install **2022.3.62f3** instead (step 3). |
| Opens in Safe Mode with compile errors | Usually a package failed to download. Check your internet and Git, then **Assets → Reimport All**. |
| Game view is empty / only a black screen | Make sure `Assets/Game/Scenes/Main.unity` is open (not `SampleScene`) and press Play. |
| Screen layout looks squashed | Set the Game view to **16:9** or **1280×720**. |
| Old progress / want a fresh start | On the title screen: **Settings (gear) → Reset progress**. |
| Linux: Unity Hub won't start | Install `libfuse2` for the AppImage (`sudo apt install libfuse2`), or use the apt install from step 2. |

---

## Credits

- Art: [Free Cartoon Cat Defense Game Asset Kit](https://craftpix.net/) by CraftPix (free for personal and commercial use).
- Sound effects and music are generated in code at runtime (`Sfx.cs`).
