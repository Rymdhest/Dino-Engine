"""Lays the Dino Defenders social media set out of what the game itself prints.

The logo is grown in the game's own code (Dino Defenders/UI/Logo.cs): the tyrant posed by the game's
animator and cut to facets, the title set with the engine's own type, red, bone and ink. Nothing here
draws any of it. The game prints it, with no window, by

  "Dino Defenders.exe" --logo Marketing/Social/source/logo

which writes to source/logo/ the mark at every size a page takes and the title over nothing. This
script lays the title on pieces of the title screen's vista and copies the marks, so that a change to
the logo in the game is a change here: run the game's --logo, then this.

  source/vista.png  the title screen's vista, from
                    "Dino Defenders.exe" --menu true --heroes <empty folder> --size 4480x1600 --hidden true --sound false --shot vista.png --frames 600
                    (the menu covers x < ~820, so nothing here crops left of that)

Run: python make_social.py   ->   out/*.png
"""
import shutil
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
SRC = HERE / "source"
LOGO = SRC / "logo"
OUT = HERE / "out"

MASTER_CAP = 240  # DEFENDERS' capital height in the title the game prints


# ---------------------------------------------------------------- what the game printed

def title(cap, tagline=False):
    """The title as the game printed it, brought down so DEFENDERS' capitals are cap pixels tall."""
    master = Image.open(LOGO / ("title_tagline.png" if tagline else "title.png")).convert("RGBA")
    scale = cap / MASTER_CAP
    return master.resize((round(master.width * scale), round(master.height * scale)), Image.LANCZOS)


# ---------------------------------------------------------------- the vista

VISTA = None


def vista(box, size):
    """A piece of the title screen's vista, (x0, y0, width) of the 4480x1421 source, scaled to size."""
    global VISTA
    if VISTA is None:
        VISTA = Image.open(SRC / "vista.png").convert("RGB")
    x0, y0, w = box
    h = w * size[1] / size[0]
    assert x0 >= 820 or x0 + w <= 0, "the menu covers the vista left of x 820"
    assert y0 >= 0 and y0 + h <= VISTA.height + 0.5, (y0, h, VISTA.height)
    return VISTA.crop((int(x0), int(y0), int(x0 + w), int(round(y0 + h)))).resize(size, Image.LANCZOS)


def put(base, piece, center):
    base.alpha_composite(piece, (int(center[0] - piece.width / 2), int(center[1] - piece.height / 2)))


# ---------------------------------------------------------------- the set

# The banners carry the title and not the mark, which every platform shows beside them anyway, and
# the picture is left as the game drew it.

def reddit_wide(size, name, cap):
    """Reddit's own banner shape, about 8.4:1 (1072x128 desktop, 1080x128 app), at twice that.
    The title on the left, the giants on the right. The community icon is already there, over the
    bottom left corner (about the first 10% across and the lower 30%), so the title starts past it
    and sits a little high."""
    w, h = size
    img = vista((820, 440, 2380), size).convert("RGBA")
    mark = title(cap)
    img.alpha_composite(mark, (int(w * 0.105), int(h * 0.47 - mark.height / 2)))
    img.convert("RGB").save(OUT / name, optimize=True)


def discord_banner():
    size = (960, 540)
    img = vista((1493, 230, 1600), size).convert("RGBA")
    put(img, title(64), (480, 400))
    img.convert("RGB").save(OUT / "discord_banner_960x540.png", optimize=True)


def discord_splash():
    size = (1920, 1080)
    img = vista((1030, 0, 2526), size).convert("RGBA")
    mark = title(96, tagline=True)
    img.alpha_composite(mark, (80, 1080 - 80 - mark.height))
    img.convert("RGB").save(OUT / "discord_invite_splash_1920x1080.png", optimize=True)


def youtube_banner():
    size = (2560, 1440)
    img = vista((820, 0, 2526), size).convert("RGBA")
    # The part every device shows is the middle 1546x423 (x 507..2053, y 508..931).
    put(img, title(100, tagline=True), (960, 720))
    img.convert("RGB").save(OUT / "youtube_banner_2560x1440.png", optimize=True)

    # A guide showing what each device keeps, not for uploading.
    guide = img.convert("RGB").copy()
    g = ImageDraw.Draw(guide)
    g.rectangle((0, 508, 2559, 931), outline=(80, 200, 255), width=6)
    g.rectangle((507, 508, 2053, 931), outline=(255, 210, 60), width=6)
    guide.resize((1280, 720), Image.LANCZOS).save(OUT / "_youtube_safe_area_preview.png", optimize=True)


def main():
    if not LOGO.is_dir():
        raise SystemExit(f'No logo printed yet: run  "Dino Defenders.exe" --logo {LOGO}')
    OUT.mkdir(exist_ok=True)
    for name in ("emblem_1024.png", "reddit_icon_256.png", "discord_icon_512.png", "youtube_avatar_800.png", "lockup_512.png"):
        shutil.copyfile(LOGO / name, OUT / name)
    reddit_wide((2144, 256), "reddit_banner_desktop_2144x256.png", 58)
    reddit_wide((2160, 256), "reddit_banner_mobile_2160x256.png", 58)
    discord_banner()
    discord_splash()
    youtube_banner()
    for p in sorted(OUT.glob("*.png")):
        with Image.open(p) as im:
            print(f"{p.name:40s} {im.size[0]}x{im.size[1]}")


if __name__ == "__main__":
    main()
