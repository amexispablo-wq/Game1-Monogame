"""Generate Steam library logo (462x174) with RGB blocks and title."""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw

from steam_art_common import (
    BG,
    BLACK,
    BLUE,
    CYAN,
    GREEN,
    MAGENTA,
    RED,
    WHITE,
    YELLOW,
    draw_platform,
    draw_player,
    draw_text,
    measure_text,
)

WIDTH = 462
HEIGHT = 174


def main() -> None:
    img = Image.new("RGB", (WIDTH, HEIGHT), BG)
    draw = ImageDraw.Draw(img)

    title_scale = 3
    title_w, title_h = measure_text("COLOR BLOCKS", title_scale)
    title_y = 18
    draw_text(draw, "COLOR BLOCKS", WIDTH // 2, title_y, title_scale, WHITE)

    block_size = 54
    gap = 14
    trio_w = block_size * 3 + gap * 2
    start_x = (WIDTH - trio_w) // 2
    block_y = title_y + title_h + 16

    draw_platform(draw, start_x, block_y + 10, block_size, block_size, RED)
    draw_platform(draw, start_x + block_size + gap, block_y, block_size, block_size, GREEN)
    draw_platform(draw, start_x + (block_size + gap) * 2, block_y + 6, block_size, block_size, BLUE)

    face_size = 34
    draw_player(draw, start_x + (block_size - face_size) // 2, block_y + 10 + (block_size - face_size) // 2, face_size, RED)
    draw_player(draw, start_x + block_size + gap + (block_size - face_size) // 2, block_y + (block_size - face_size) // 2, face_size, GREEN)
    draw_player(
        draw,
        start_x + (block_size + gap) * 2 + (block_size - face_size) // 2,
        block_y + 6 + (block_size - face_size) // 2,
        face_size,
        BLUE,
    )

    mix_size = 18
    mix_gap = 10
    mix_w = mix_size * 4 + mix_gap * 3
    mix_x = (WIDTH - mix_w) // 2
    mix_y = block_y + block_size + 14
    draw_platform(draw, mix_x, mix_y, mix_size, mix_size, YELLOW, border=1)
    draw_platform(draw, mix_x + mix_size + mix_gap, mix_y, mix_size, mix_size, MAGENTA, border=1)
    draw_platform(draw, mix_x + (mix_size + mix_gap) * 2, mix_y, mix_size, mix_size, CYAN, border=1)
    draw_platform(draw, mix_x + (mix_size + mix_gap) * 3, mix_y, mix_size, mix_size, WHITE, border=1)

    # plus signs between mix blocks = color combine hint
    for i in range(3):
        cx = mix_x + (mix_size + mix_gap) * (i + 1) - mix_gap // 2
        cy = mix_y + mix_size // 2
        draw.rectangle((cx - 1, cy - 4, cx + 1, cy + 4), fill=BLACK)
        draw.rectangle((cx - 4, cy - 1, cx + 4, cy + 1), fill=BLACK)

    out = Path(__file__).resolve().parents[1] / "Content" / "Steam" / "logo_462x174.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    img.save(out, format="PNG")
    print(f"Saved {out} ({WIDTH}x{HEIGHT})")


if __name__ == "__main__":
    main()
