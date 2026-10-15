"""Packs the CraftPix frame sequences into sprite sheets for Unity (Assets/Game/Resources).
usage: python3 Tools/pack_sprites.py Assets/catdefense_assets/Png <output Resources dir> [units.json]
"""
import sys, os, glob, json, shutil
from PIL import Image

SRC, OUT = sys.argv[1], sys.argv[2]
MAX_W = 4096

def frames(folder, step=1):
    fs = sorted(glob.glob(os.path.join(SRC, folder, "*.png")))
    return [Image.open(f).convert("RGBA") for f in fs][::step]

def union_bbox(imgs):
    box = None
    for im in imgs:
        b = im.getbbox()
        if not b: continue
        box = b if box is None else (min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3]))
    return box

anims = []

def pack_unit(name, groups, scale, fps, extra=None):
    """groups: {animName: (folder, step)}; frames of every anim share one crop box so pivots line up."""
    loaded = {k: frames(f, s) for k, (f, s) in groups.items()}
    box = union_bbox([im for v in loaded.values() for im in v])
    bw, bh = box[2] - box[0], box[3] - box[1]
    first = next(iter(loaded.values()))[0]
    fb = first.getbbox()
    # pivot: feet = bottom of first frame's opaque area, x centre of it
    piv_x = ((fb[0] + fb[2]) / 2 - box[0]) / bw
    piv_y = (box[3] - fb[3]) / bh + 0.02
    fw, fh = max(1, round(bw * scale)), max(1, round(bh * scale))
    for k, imgs in loaded.items():
        cols = max(1, min(len(imgs), MAX_W // fw))
        rows = (len(imgs) + cols - 1) // cols
        sheet = Image.new("RGBA", (cols * fw, rows * fh), (0, 0, 0, 0))
        for i, im in enumerate(imgs):
            fr = im.crop(box).resize((fw, fh), Image.LANCZOS)
            sheet.paste(fr, ((i % cols) * fw, (i // cols) * fh))
        key = f"{name}_{k}"
        sheet.save(os.path.join(OUT, "Game", "Anim", key + ".png"))
        a = {"key": key, "w": fw, "h": fh, "cols": cols, "count": len(imgs),
             "fps": fps.get(k, 30) if isinstance(fps, dict) else fps,
             "px": round(piv_x, 4), "py": round(piv_y, 4), "ppu": round(100 * scale, 3)}
        anims.append(a)
    # body centre / radius in world units (native px / 100) relative to pivot
    cx = ((fb[0] + fb[2]) / 2 - box[0]) - piv_x * bw
    cy = (box[3] - (fb[1] + fb[3]) / 2) - piv_y * bh
    info = {"cx": round(cx / 100, 3), "cy": round(cy / 100, 3),
            "r": round(min(fb[2] - fb[0], fb[3] - fb[1]) / 200 * 0.8, 3),
            "top": round(((box[3] - fb[1]) - piv_y * bh) / 100, 3)}
    if extra: info.update(extra(first, box, piv_x * bw, piv_y * bh))
    return info

def gun_tip(first, box, pvx, pvy):
    """rightmost opaque pixel of the cat = muzzle, relative to pivot, in world units"""
    a = first.crop(box).split()[3]
    w, h = a.size
    px = a.load()
    for x in range(w - 1, -1, -1):
        ys = [y for y in range(h) if px[x, y] > 128]
        if ys:
            ty = sum(ys) / len(ys)
            return {"tipX": round((x - pvx) / 100, 3), "tipY": round(((h - ty) - pvy) / 100, 3)}
    return {"tipX": 0.5, "tipY": 0.5}

for d in ["Game/Anim", "Game/Bg", "Game/UI", "Game/Fx", "Data"]:
    os.makedirs(os.path.join(OUT, d), exist_ok=True)

units = {}
# cats: Rookie C1, Sniper C5, Gunner C10, Bomber C15
for c in [1, 5, 10, 15]:
    units[f"C{c}"] = pack_unit(f"C{c}", {"idle": (f"Characters/C{c}/Idle", 1), "shoot": (f"Characters/C{c}/Shoot", 1)},
                               1.0, {"idle": 20, "shoot": 30}, gun_tip)
# enemies
for n in [1, 2, 3, 4, 5, 6, 7]:
    units[f"Reg{n}"] = pack_unit(f"Reg{n}", {"walk": (f"Enemies/Enemy Reg {n}/Walk", 1), "attack": (f"Enemies/Enemy Reg {n}/Attack", 1),
                                            "dead": (f"Enemies/Enemy Reg {n}/Dead", 2)}, 0.55, {"walk": 30, "attack": 30, "dead": 30})
units["Reg8"] = pack_unit("Reg8", {"walk": ("Enemies/Enemy Reg 8/Walk", 1), "attack": ("Enemies/Enemy Reg 8/Attack", 1),
                                   "dead": ("Enemies/Enemy Reg 8/Dead", 2)}, 0.55, {"walk": 30, "attack": 30, "dead": 30})
for n in [1, 2]:
    units[f"Boss{n}"] = pack_unit(f"Boss{n}", {"walk": (f"Enemies/Enemy Boss {n}/Walk", 1), "attack": (f"Enemies/Enemy Boss {n}/Attack", 2),
                                              "dead": (f"Enemies/Enemy Boss {n}/Dead", 2)}, 0.55, {"walk": 30, "attack": 30, "dead": 30})
units["Boss7"] = pack_unit("Boss7", {"walk": ("Enemies/Enemy Boss 7/Walk", 1), "attack": ("Enemies/Enemy Boss 7/Attack", 1),
                                     "dead": ("Enemies/Enemy Boss 7/Dead", 1)}, 0.7, {"walk": 30, "attack": 30, "dead": 30})
# fx (centre pivot)
for name, folder, scale in [("Muzzle", "ShootFx", 0.7), ("Explosion", "Explosion", 0.22)]:
    pack_unit(name, {"fx": (folder, 1)}, scale, 30)
for a in anims:
    if a["key"] in ("Muzzle_fx", "Explosion_fx"):
        a["px"], a["py"] = 0.5, 0.5
# muzzle flash should start at its left edge (the gun tip)
for a in anims:
    if a["key"] == "Muzzle_fx": a["px"] = 0.08

for i, f in enumerate(["Artboard 1.png", "Artboard 1 copy.png", "Artboard 1 copy 2.png"]):
    Image.open(os.path.join(SRC, "Bullets", f)).convert("RGBA").save(os.path.join(OUT, "Game", "Fx", f"Bullet{i}.png"))

# backgrounds + barricade cut-outs (boxes + pillar drawn into the art)
BAR = (515, 190, 760, 800)
for i in [1, 2, 3]:
    bg = Image.open(os.path.join(SRC, "Area", f"Area{i}.png")).convert("RGBA")
    bg.save(os.path.join(OUT, "Game", "Bg", f"Area{i}.png"))
    bar = bg.crop(BAR)
    # paint the art's green gauge dark so the game can draw the real HP into it
    px = bar.load()
    dark = px[165, 110]
    for x in range(145, 185):
        for y in range(70, 495):
            r, g, b, a = px[x, y]
            if g > 120 and g > r + 25 and g > b + 40:
                px[x, y] = dark
    bar.save(os.path.join(OUT, "Game", "Bg", f"Barricade{i}.png"))

UI = ["LandingScreen", "Logo", "LevelMap_Bg_Screen", "CatUpgradeScreen", "AddonScreen", "WinPopUp", "LosePopUp", "BgPaused",
      "BtnGreen", "BtnGreenPressed", "BtnOrange", "BtnOrangePressed", "BtnMusic", "BtnMusicOff", "BtnSound", "BtnSound Off",
      "BtnVibra", "BtnVibra Off", "BtnTab_Active", "BtnTab_on", "CoinBar", "CoinIcon", "WaveBar", "WallIcon", "SettingBtn",
      "Icon_home", "Icon_Cat", "Icon_Addon", "TitleRibbon", "GreenLevel", "OrangeLvl", "LockedLevel", "SelectedBorder",
      "YellowBox", "AddonBoxGrey", "AddonBtnyellow", "AddOnSlotBtn", "GemsBarBg", "BtnShopYellow", "BtnShopDisable",
      "AddonIcon1", "AddonIcon2", "AddonIcon3", "AddonIcon6", "YellowBorderAddon", "LockedCatBox",
      "0Bar", "1Bar", "2Bar", "3Bar", "4Bar", "5Bar", "Up3", "Uplogo1", "Uplogo5", "Uplogo10", "Uplogo15"]
for u in UI:
    shutil.copy(os.path.join(SRC, "Ui", u + ".png"), os.path.join(OUT, "Game", "UI", u.replace(" ", "") + ".png"))
# a slice of the long level map for the level-select panel
lm = Image.open(os.path.join(SRC, "Ui", "LevelMap.png")).convert("RGBA")
lm.crop((0, 0, 1640, 785)).save(os.path.join(OUT, "Game", "UI", "LevelMapPart.png"))

with open(os.path.join(OUT, "Game", "anims.json"), "w") as f:
    json.dump({"anims": anims, "units": [dict(key=k, **v) for k, v in units.items()]}, f, indent=1)
with open(os.path.join(sys.argv[3] if len(sys.argv) > 3 else os.devnull, ), "w") as f:
    json.dump(units, f, indent=1)
print(json.dumps(units, indent=1))
