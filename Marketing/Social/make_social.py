"""Builds the Dino Defenders social media set from the game's own pictures.

Sources (in source/), both made by the game itself:
  vista.png        the title screen's vista, from
                   "Dino Defenders.exe" --menu true --heroes <empty folder> --size 4480x1600 --hidden true --sound false --shot vista.png --frames 600
                   (the menu covers x < ~820, so nothing here crops left of that)
  tyrant_roar.png  the tyrant roaring, from
                   dotnet "Dino AnimLab/bin/Release/net8.0/Dino AnimLab.dll" sheet --body tyrant --do roar --view side --tile 1000 --frames 12 --columns 6 --out tyrant_roar.png

Type is Palatino Linotype, the menu's own face (UI/Ui.cs), in the menu's parchment, gold and blood (Ui.Parchment, Ui.Gold, Ui.Blood).

Run: python make_social.py   ->   out/*.png
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont
from scipy import ndimage

HERE = Path(__file__).resolve().parent
SRC = HERE / "source"
OUT = HERE / "out"
FONTS = Path("C:/Windows/Fonts")

# The menu's own colours (Dino Defenders/UI/Ui.cs, MenuKit.cs).
PARCHMENT = (228, 214, 186)
GOLD = (214, 172, 92)
BLOOD = (172, 40, 30)
SCRIM = (9, 7, 6)
# Shades of them.
GOLD_LIGHT = (246, 218, 150)
GOLD_DEEP = (168, 120, 50)
INK = (24, 18, 14)
PAPER_TOP = (238, 227, 202)
PAPER_BOTTOM = (212, 193, 156)

TITLE = "DINO DEFENDERS"
TAGLINE = "Take it back, a place at a time"


def font(name, size):
    return ImageFont.truetype(str(FONTS / name), int(round(size)))


def gradient(size, top, bottom):
    w, h = size
    t = np.linspace(0.0, 1.0, h)[:, None, None]
    rows = np.array(top, float) * (1 - t) + np.array(bottom, float) * t
    return Image.fromarray(np.broadcast_to(rows, (h, w, 3)).astype(np.uint8))


def smoothstep(x):
    x = np.clip(x, 0.0, 1.0)
    return x * x * (3 - 2 * x)


# ---------------------------------------------------------------- the tyrant

def tyrant_silhouette():
    """The roaring tyrant (frame 5 of the sheet) as a smooth white-on-black mask, cropped to itself."""
    sheet = Image.open(SRC / "tyrant_roar.png").convert("RGB")
    tile = np.asarray(sheet.crop((4000, 0, 5000, 1000))).astype(int)
    background = tile[5, 5]
    differs = np.abs(tile - background).sum(axis=2)

    # The ground line runs the whole width: find its rows from a column the tyrant never reaches.
    line_rows = np.where(differs[:, :40].max(axis=1) > 6)[0]
    mask = differs > 24
    mask &= ~((tile[..., 1] - tile[..., 0]) > 40)  # the teal foot marker
    if len(line_rows):
        mask[line_rows.min() - 1:, :] = False  # the feet stand on the line: cut there
    mask =ndimage.binary_opening(mask, iterations=1)
    labels, count = ndimage.label(mask)
    sizes = ndimage.sum(mask, labels, range(1, count + 1))
    mask = ndimage.binary_fill_holes(labels == (int(np.argmax(sizes)) + 1))

    img = Image.fromarray((mask * 255).astype(np.uint8))
    img = img.crop(img.getbbox())
    img = img.resize((img.width * 4, img.height * 4), Image.LANCZOS)
    img = img.filter(ImageFilter.GaussianBlur(3)).point(lambda v: 255 if v > 128 else 0)
    return img


# ---------------------------------------------------------------- the emblem

def emblem(size=2048):
    """Round badge: the tyrant in ink against a blood sun on parchment, in a dark ring with gold lines."""
    S = size
    c = S / 2
    scene = gradient((S, S), PAPER_TOP, PAPER_BOTTOM)
    draw = ImageDraw.Draw(scene)

    # The sun, behind the tyrant's raised head.
    sun_c = (c + 0.12 * S, c - 0.07 * S)
    sun_r = 0.27 * S
    sun = gradient((S, S), (200, 60, 42), (142, 28, 20))
    sun_mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(sun_mask).ellipse(
        (sun_c[0] - sun_r, sun_c[1] - sun_r, sun_c[0] + sun_r, sun_c[1] + sun_r), fill=255)
    scene.paste(sun, (0, 0), sun_mask)

    # The whole tyrant, roaring on a rise: its outline is what reads at 32 px.
    tyrant = tyrant_silhouette()
    width = int(0.86 * S)
    height = int(tyrant.height * width / tyrant.width)
    tyrant = tyrant.resize((width, height), Image.LANCZOS)
    feet_y = int(0.735 * S)
    left = int(0.925 * S) - width
    top = feet_y - height
    scene.paste(INK, (left, top, left + width, top + height), tyrant)

    # The ground it stands on, with a few blades of grass.
    ground_top = feet_y - int(0.006 * S)
    draw.ellipse((-0.6 * S, ground_top, 1.6 * S, ground_top + 1.2 * S), fill=INK)
    rng = np.random.default_rng(7)
    for _ in range(70):
        x = rng.uniform(0.05, 0.95) * S
        dx = x - c
        y = ground_top + 0.6 * S * (1 - np.sqrt(max(0.0, 1 - (dx / (1.1 * S)) ** 2))) + 4
        h = rng.uniform(0.018, 0.05) * S
        lean = rng.uniform(-0.4, 0.4) * h
        w = rng.uniform(0.004, 0.008) * S
        draw.polygon([(x - w, y + 2), (x + w, y + 2), (x + lean, y - h)], fill=INK)

    # Ring.
    badge = Image.new("RGB", (S, S), INK)
    inner_r = 0.452 * S
    hole = Image.new("L", (S, S), 0)
    ImageDraw.Draw(hole).ellipse((c - inner_r, c - inner_r, c + inner_r, c + inner_r), fill=255)
    badge.paste(scene, (0, 0), hole)
    ring = ImageDraw.Draw(badge)
    for r, w in ((0.468 * S, 0.008 * S), (0.452 * S, 0.006 * S)):
        ring.ellipse((c - r, c - r, c + r, c + r), outline=GOLD, width=int(w))
    return badge


def save_icon(badge, size, name):
    badge.resize((size, size), Image.LANCZOS).save(OUT / name, optimize=True)


# ---------------------------------------------------------------- the wordmark

def wordmark(cap, tagline=True, tracking=0.10):
    """DINO DEFENDERS in tracked Palatino bold, gold, with a soft shadow, and the tagline under it."""
    em = cap / 0.69  # Palatino's capitals are about 0.69 em tall
    f = font("palab.ttf", em)
    pad = int(cap * 0.6)
    track = tracking * em
    advances = [f.getlength(ch) for ch in TITLE]
    title_w = sum(advances) + track * (len(TITLE) - 1)
    ascent, descent = f.getmetrics()

    tag_f = font("palai.ttf", cap * 0.50)
    tag_w = tag_f.getlength(TAGLINE) if tagline else 0
    tag_gap = int(cap * 0.55)
    tag_h = int(cap * 0.62) if tagline else 0

    W = int(max(title_w, tag_w + cap * 2.2)) + pad * 2
    H = pad + ascent + descent + (tag_gap + tag_h if tagline else 0) + pad

    text = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(text)
    x = (W - title_w) / 2
    for ch, a in zip(TITLE, advances):
        d.text((x, pad), ch, font=f, fill=255)
        x += a + track
    cap_top = pad + ascent - cap
    # Gradient only across the capitals' height.
    t = np.clip((np.arange(H) - cap_top) / cap, 0, 1)[:, None, None]
    gold = Image.fromarray((np.array(GOLD_LIGHT, float) * (1 - t) + np.array(GOLD_DEEP, float) * t)
                           .repeat(W, axis=1).astype(np.uint8))

    tag = Image.new("L", (W, H), 0)
    if tagline:
        td = ImageDraw.Draw(tag)
        ty = pad + ascent + descent + tag_gap - int(cap * 0.25)
        td.text(((W - tag_w) / 2, ty), TAGLINE, font=tag_f, fill=255)
        # Gold rules either side of it.
        mid = ty + tag_f.getmetrics()[0] * 0.62
        rule = cap * 0.85
        gap = cap * 0.35
        lw = max(1, int(cap * 0.035))
        lx = (W - tag_w) / 2 - gap
        rx = (W + tag_w) / 2 + gap
        d.line((lx - rule, mid, lx, mid), fill=255, width=lw)
        d.line((rx, mid, rx + rule, mid), fill=255, width=lw)
        for px in (lx - rule - cap * 0.12, rx + rule + cap * 0.12):
            s = cap * 0.07
            d.polygon([(px, mid - s), (px + s, mid), (px, mid + s), (px - s, mid)], fill=255)

    shadow_src = Image.fromarray(np.maximum(np.asarray(text), np.asarray(tag)))
    shadow = shadow_src.filter(ImageFilter.GaussianBlur(cap * 0.14)).point(lambda v: min(255, int(v * 1.6)))
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    out.paste((*SCRIM, 255), (0, int(cap * 0.04)), shadow.point(lambda v: int(v * 0.85)))
    out.paste((*PARCHMENT, 255), (0, 0), tag)
    out.paste(gold, (0, 0), text)
    return out


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


def shade(img, alpha):
    """Lays the menu's scrim over img, alpha being a 2D array of 0..1 the size of img."""
    a = Image.fromarray((np.clip(alpha, 0, 1) * 255).astype(np.uint8))
    img.paste(SCRIM, (0, 0, *img.size), a)
    return img


def horizontal(size, start, end, strength):
    """Scrim strongest at x=start, gone by x=end (either way round)."""
    w, h = size
    x = np.arange(w)[None, :].repeat(h, axis=0)
    return strength * (1 - smoothstep((x - start) / (end - start)))


def vertical(size, start, end, strength):
    w, h = size
    y = np.arange(h)[:, None].repeat(w, axis=1)
    return strength * (1 - smoothstep((y - start) / (end - start)))


def glow(size, center, radii, strength):
    """A soft dark pool behind type, so it reads over anything bright."""
    w, h = size
    y, x = np.mgrid[0:h, 0:w]
    d = ((x - center[0]) / radii[0]) ** 2 + ((y - center[1]) / radii[1]) ** 2
    return strength * np.exp(-d * 1.6)


def put(base, layer, center):
    x = int(center[0] - layer.width / 2)
    y = int(center[1] - layer.height / 2)
    base.paste(layer, (x, y), layer)


def round_badge(badge, size):
    """The emblem cut round, with a soft shadow, for laying over a picture."""
    b = badge.resize((size, size), Image.LANCZOS).convert("RGBA")
    m = Image.new("L", (size * 4, size * 4), 0)
    ImageDraw.Draw(m).ellipse((0, 0, size * 4 - 1, size * 4 - 1), fill=255)
    b.putalpha(m.resize((size, size), Image.LANCZOS))
    pad = int(size * 0.12)
    out = Image.new("RGBA", (size + pad * 2, size + pad * 2), (0, 0, 0, 0))
    sh = Image.new("L", out.size, 0)
    sh.paste(b.getchannel("A"), (pad, pad + int(size * 0.02)))
    sh = sh.filter(ImageFilter.GaussianBlur(size * 0.05)).point(lambda v: int(v * 0.7))
    out.paste((*SCRIM, 255), (0, 0), sh)
    out.alpha_composite(b, (pad, pad))
    return out


# ---------------------------------------------------------------- the set

def reddit_wide(size, name, cap):
    """Reddit's own banner shape, about 8.4:1 (1072x128 desktop, 1080x128 app), at twice that.
    The title on the left under the menu's scrim, the giants on the right. No badge: the community
    icon is already there, over the bottom left corner (about the first 10% across and the lower 30%),
    so the title starts past it and sits a little high."""
    w, h = size
    img = vista((820, 440, 2380), size)
    img = shade(img, np.maximum(horizontal(size, w * 0.20, w * 0.52, 0.88),
                                glow(size, (w * 0.27, h * 0.45), (w * 0.24, h * 0.8), 0.6)))
    img = img.convert("RGBA")
    mark = wordmark(cap)
    left = int(w * 0.105) - int(cap * 0.6)  # the wordmark's own padding
    img.paste(mark, (left, int(h * 0.45 - mark.height / 2)), mark)
    img.convert("RGB").save(OUT / name, optimize=True)


def discord_banner(badge):
    size = (960, 540)
    img = vista((1493, 230, 1600), size)
    img = shade(img, np.maximum(vertical(size, 540, 300, 0.85), glow(size, (480, 455), (330, 80), 0.5)))
    img = img.convert("RGBA")
    put(img, wordmark(34), (480, 450))
    img.convert("RGB").save(OUT / "discord_banner_960x540.png", optimize=True)


def discord_splash(badge):
    size = (1920, 1080)
    img = vista((1030, 0, 2526), size)
    img = shade(img, vertical(size, 1080, 760, 0.75))
    img = img.convert("RGBA")
    put(img, round_badge(badge, 150), (150, 960))
    put(img, wordmark(36), (560, 966))
    img.convert("RGB").save(OUT / "discord_invite_splash_1920x1080.png", optimize=True)


def youtube_banner(badge):
    size = (2560, 1440)
    img = vista((820, 0, 2526), size)
    # The part every device shows is the middle 1546x423 (x 507..2053, y 508..931).
    img = shade(img, np.maximum(horizontal(size, 200, 1500, 0.55), glow(size, (895, 720), (560, 300), 0.8)))
    img = img.convert("RGBA")
    put(img, round_badge(badge, 220), (895, 632))
    put(img, wordmark(46), (895, 842))
    img.convert("RGB").save(OUT / "youtube_banner_2560x1440.png", optimize=True)

    # A guide showing what each device keeps, not for uploading.
    guide = img.convert("RGB").copy()
    g = ImageDraw.Draw(guide)
    g.rectangle((0, 508, 2559, 931), outline=(80, 200, 255), width=6)
    g.rectangle((507, 508, 2053, 931), outline=(255, 210, 60), width=6)
    guide.resize((1280, 720), Image.LANCZOS).save(OUT / "_youtube_safe_area_preview.png", optimize=True)


def main():
    OUT.mkdir(exist_ok=True)
    badge = emblem()
    save_icon(badge, 256, "reddit_icon_256.png")
    save_icon(badge, 512, "discord_icon_512.png")
    save_icon(badge, 800, "youtube_avatar_800.png")
    save_icon(badge, 1024, "emblem_1024.png")
    reddit_wide((2144, 256), "reddit_banner_desktop_2144x256.png", 52)
    reddit_wide((2160, 256), "reddit_banner_mobile_2160x256.png", 52)
    discord_banner(badge)
    discord_splash(badge)
    youtube_banner(badge)
    for p in sorted(OUT.glob("*.png")):
        with Image.open(p) as im:
            print(f"{p.name:40s} {im.size[0]}x{im.size[1]}")


if __name__ == "__main__":
    main()
