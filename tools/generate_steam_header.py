"""Generate Steam header capsule (920x430) from Content/Steam/Untitled1.png."""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw

from steam_art_common import (
    BG,
    BLACK,
    PANEL,
    draw_rgb_title,
    draw_tricolor_logo,
    measure_text,
)

WIDTH = 920
HEIGHT = 430


def draw_vignette(draw: ImageDraw.ImageDraw) -> None:
    for i in range(18):
        shade = 8 + i * 3
        c = (max(0, BG[0] - shade), max(0, BG[1] - shade), max(0, BG[2] - shade))
        draw.rectangle((i, 0, i, HEIGHT - 1), fill=c)
        draw.rectangle((WIDTH - 1 - i, 0, WIDTH - 1 - i, HEIGHT - 1), fill=c)
        draw.rectangle((0, i, WIDTH - 1, i), fill=c)
        draw.rectangle((0, HEIGHT - 1 - i, WIDTH - 1, HEIGHT - 1 - i), fill=c)


def main() -> None:
    img = Image.new("RGB", (WIDTH, HEIGHT), BG)
    draw = ImageDraw.Draw(img)

    draw_vignette(draw)

    panel = (196, 18, 724, 412)
    draw.rectangle(panel, fill=PANEL)
    draw.rectangle((panel[0] - 2, panel[1] - 2, panel[2] + 2, panel[3] + 2), outline=BLACK, width=2)
    draw.rectangle(panel, outline=(52, 58, 72), width=1)

    accents = [(54, 118, 74), (54, 238, 74), (792, 118, 74), (792, 238, 74)]
    for ax, ay, size in accents:
        draw_tricolor_logo(draw, ax, ay, size)

    title_scale = 5
    title_y = 44
    draw_rgb_title(draw, WIDTH // 2, title_y, title_scale)

    _, title_h = measure_text("COLOR BLOCKS", title_scale)
    logo_size = 272
    logo_x = (WIDTH - logo_size) // 2
    logo_y = title_y + title_h + 30
    draw_tricolor_logo(draw, logo_x, logo_y, logo_size)

    # subtle RGB guide lines for wide composition
    guide_y = logo_y + logo_size + 24
    draw.line((panel[0] + 28, guide_y, panel[2] - 28, guide_y), fill=(52, 58, 72), width=2)
    segment = (panel[2] - panel[0] - 56) // 3
    gx = panel[0] + 28
    for color in ((255, 48, 48), (48, 220, 72), (48, 96, 255)):
        draw.rectangle((gx, guide_y + 6, gx + segment - 8, guide_y + 12), fill=color)
        gx += segment

    out = Path(__file__).resolve().parents[1] / "Content" / "Steam" / "header_capsule_920x430.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    img.save(out, format="PNG")
    print(f"Saved {out} ({WIDTH}x{HEIGHT})")


if __name__ == "__main__":
    main()
