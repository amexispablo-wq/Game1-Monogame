"""Shared pixel-art helpers for Steam marketing images."""

from __future__ import annotations

from PIL import ImageDraw

BG = (36, 41, 52)
PANEL = (30, 35, 46)
RED = (224, 64, 64)
GREEN = (72, 184, 96)
BLUE = (64, 128, 224)
LOGO_RED = (255, 48, 48)
LOGO_GREEN = (48, 220, 72)
LOGO_BLUE = (48, 96, 255)
TITLE_FILL = (108, 112, 122)
YELLOW = (255, 235, 64)
MAGENTA = (224, 64, 192)
CYAN = (64, 200, 224)
WHITE = (255, 255, 255)
BLACK = (0, 0, 0)

GLYPHS: dict[str, list[str]] = {
    " ": ["00000"] * 7,
    "A": ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
    "B": ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
    "C": ["01111", "10000", "10000", "10000", "10000", "10000", "01111"],
    "D": ["11110", "10001", "10001", "10001", "10001", "10001", "11110"],
    "E": ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
    "F": ["11111", "10000", "10000", "11110", "10000", "10000", "10000"],
    "G": ["01110", "10001", "10000", "10111", "10001", "10001", "01110"],
    "H": ["10001", "10001", "10001", "11111", "10001", "10001", "10001"],
    "I": ["11111", "00100", "00100", "00100", "00100", "00100", "11111"],
    "J": ["00111", "00010", "00010", "00010", "10010", "10010", "01100"],
    "K": ["10001", "10010", "10100", "11000", "10100", "10010", "10001"],
    "L": ["10000", "10000", "10000", "10000", "10000", "10000", "11111"],
    "M": ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
    "N": ["10001", "11001", "10101", "10011", "10001", "10001", "10001"],
    "O": ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
    "P": ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
    "Q": ["01110", "10001", "10001", "10001", "10101", "10010", "01101"],
    "R": ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
    "S": ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
    "T": ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
    "U": ["10001", "10001", "10001", "10001", "10001", "10001", "01110"],
    "V": ["10001", "10001", "10001", "10001", "10001", "01010", "00100"],
    "W": ["10001", "10001", "10001", "10101", "10101", "10101", "01010"],
    "X": ["10001", "10001", "01010", "00100", "01010", "10001", "10001"],
    "Y": ["10001", "10001", "01010", "00100", "00100", "00100", "00100"],
    "Z": ["11111", "00001", "00010", "00100", "01000", "10000", "11111"],
}


def measure_text(text: str, scale: int) -> tuple[int, int]:
    text = text.upper()
    if not text or scale <= 0:
        return 0, 0
    width = len(text) * (5 * scale) + max(0, len(text) - 1) * scale
    height = 7 * scale
    return width, height


def draw_platform(draw: ImageDraw.ImageDraw, x: int, y: int, w: int, h: int, color: tuple[int, int, int], border: int = 2) -> None:
    draw.rectangle((x, y, x + w, y + h), fill=color)
    for i in range(border):
        draw.rectangle((x + i, y + i, x + w - 1 - i, y + h - 1 - i), outline=BLACK)


def draw_player(draw: ImageDraw.ImageDraw, x: int, y: int, size: int, color: tuple[int, int, int]) -> None:
    draw.rectangle((x, y, x + size, y + size), fill=color, outline=BLACK, width=2)
    eye = max(3, size // 11)
    gap = max(6, size // 5)
    eye_y = y + size // 3
    left_eye_x = x + size // 2 - gap // 2 - eye
    right_eye_x = x + size // 2 + gap // 2
    draw.rectangle((left_eye_x, eye_y, left_eye_x + eye, eye_y + eye), fill=BLACK)
    draw.rectangle((right_eye_x, eye_y, right_eye_x + eye, eye_y + eye), fill=BLACK)
    smile_y = y + size // 2 + max(2, size // 12)
    smile_w = max(10, size // 4)
    for i in range(5):
        sx = x + size // 2 - smile_w // 2 + i * (smile_w // 4)
        sy = smile_y + abs(i - 2)
        dot = max(2, size // 14)
        draw.rectangle((sx, sy, sx + dot, sy + dot), fill=BLACK)


def draw_logo_block(draw: ImageDraw.ImageDraw, x: int, y: int, size: int, color: tuple[int, int, int]) -> None:
    """Inset-framed block matching Content/Steam/Untitled.png."""
    margin = max(4, round(size * 6 / 49))
    draw.rectangle((x + 1, y + 1, x + size, y + size), fill=BLACK)
    draw.rectangle((x, y, x + size - 1, y + size - 1), fill=color)

    inner = margin
    ix1 = x + inner
    iy1 = y + inner
    ix2 = x + size - inner - 1
    iy2 = y + size - inner - 1
    bar = max(2, round(size * 2 / 49))

    draw.rectangle((ix1, iy1, ix2, iy1 + bar - 1), fill=BLACK)
    draw.rectangle((ix1, iy1 + bar, ix2, iy1 + bar + bar - 1), fill=BLACK)
    draw.rectangle((ix1, iy1, ix1, iy2), fill=BLACK)
    draw.rectangle((ix2, iy1, ix2, iy2), fill=BLACK)
    draw.rectangle((ix1, iy2, ix2, iy2), fill=BLACK)


def draw_text(draw: ImageDraw.ImageDraw, text: str, center_x: int, y: int, scale: int, color: tuple[int, int, int]) -> None:
    text = text.upper()
    glyph_w = 5 * scale
    spacing = scale
    total_w = len(text) * glyph_w + max(0, len(text) - 1) * spacing
    x = center_x - total_w // 2
    for ch in text:
        glyph = GLYPHS.get(ch, GLYPHS[" "])
        for gy, row in enumerate(glyph):
            for gx, bit in enumerate(row):
                if bit == "1":
                    draw.rectangle(
                        (x + gx * scale, y + gy * scale, x + (gx + 1) * scale - 1, y + (gy + 1) * scale - 1),
                        fill=color,
                    )
        x += glyph_w + spacing


def draw_text_outlined(
    draw: ImageDraw.ImageDraw,
    text: str,
    center_x: int,
    y: int,
    scale: int,
    fill: tuple[int, int, int],
    outline: tuple[int, int, int],
) -> None:
    for ox, oy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
        draw_text(draw, text, center_x + ox, y + oy, scale, outline)
    draw_text(draw, text, center_x, y, scale, fill)


def _draw_glyph_pixels(
    draw: ImageDraw.ImageDraw,
    x: int,
    y: int,
    scale: int,
    glyph: list[str],
    color: tuple[int, int, int],
) -> None:
    for gy, row in enumerate(glyph):
        for gx, bit in enumerate(row):
            if bit == "1":
                draw.rectangle(
                    (x + gx * scale, y + gy * scale, x + (gx + 1) * scale - 1, y + (gy + 1) * scale - 1),
                    fill=color,
                )


def draw_rgb_title(draw: ImageDraw.ImageDraw, center_x: int, y: int, scale: int, text: str = "COLOR BLOCKS") -> None:
    """Segmented RGB title matching Content/Steam/Untitled1.png."""
    text = text.upper()
    outline_colors = [
        LOGO_RED,
        LOGO_GREEN,
        LOGO_RED,
        LOGO_GREEN,
        LOGO_RED,
        LOGO_BLUE,
        LOGO_BLUE,
        LOGO_BLUE,
        LOGO_RED,
        LOGO_GREEN,
        LOGO_GREEN,
    ]

    glyph_w = 5 * scale
    spacing = scale
    total_w = len(text) * glyph_w + max(0, len(text) - 1) * spacing
    x = center_x - total_w // 2

    for index, ch in enumerate(text):
        glyph = GLYPHS.get(ch, GLYPHS[" "])
        outline = outline_colors[index % len(outline_colors)]
        for ox in (-1, 0, 1):
            for oy in (-1, 0, 1):
                if ox == 0 and oy == 0:
                    continue
                _draw_glyph_pixels(draw, x + ox, y + oy, scale, glyph, outline)
        _draw_glyph_pixels(draw, x, y, scale, glyph, TITLE_FILL)
        x += glyph_w + spacing


def draw_tricolor_logo(draw: ImageDraw.ImageDraw, x: int, y: int, size: int) -> None:
    """RGB Y-split square logo from Content/Steam/Untitled1.png."""
    border = max(6, size // 36)
    divider = max(4, size // 52)
    shadow = max(4, size // 54)

    draw.rectangle((x + shadow, y + shadow, x + size + shadow, y + size + shadow), fill=(16, 18, 24))
    draw.rectangle((x, y, x + size, y + size), fill=BLACK)

    inner = border
    ix1 = x + inner
    iy1 = y + inner
    ix2 = x + size - inner
    iy2 = y + size - inner
    pivot = ((ix1 + ix2) // 2, iy1 + int((iy2 - iy1) * 0.58))
    top_mid = (pivot[0], iy1)
    bottom_left = (ix1, iy2)
    bottom_right = (ix2, iy2)

    def cross(a: tuple[int, int], b: tuple[int, int], c: tuple[int, int]) -> int:
        return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])

    for py in range(iy1, iy2 + 1):
        for px in range(ix1, ix2 + 1):
            point = (px, py)
            c_bl = cross(pivot, bottom_left, point)
            c_tm = cross(pivot, top_mid, point)
            c_br = cross(pivot, bottom_right, point)

            if c_bl >= 0 and c_tm <= 0:
                color = LOGO_GREEN
            elif c_tm >= 0 and c_br <= 0:
                color = LOGO_RED
            else:
                color = LOGO_BLUE

            draw.point((px, py), fill=color)

    draw.line((top_mid, pivot), fill=BLACK, width=divider)
    draw.line((pivot, bottom_left), fill=BLACK, width=divider)
    draw.line((pivot, bottom_right), fill=BLACK, width=divider)

