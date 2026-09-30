# -*- coding: utf-8 -*-
"""
Google Play のストア用の絵を描く。

Play は寸法が一致しない絵を受け付けない。1024x500 と 512x512 を、
アプリと同じ配色・同じ線の太さで、その場から作る。

盤は自分で解く。12 個すべてを 6x10 に収める配置を数えるので、絵に描く
問題は必ず本物になる。この遊びは「答えがただ一つ」が売りなので、
飾りのために矛盾した盤を描くわけにはいかない。

出来るもの（Store/ に置く）:

    feature_plain.png   1024x500  文字なし。あと一手の絵
    feature_title.png   1024x500  題つき。完成した盤
    icon_512.png        512x512   アイコン

文字なしのほうを推す。8 言語すべてで同じものが使えるため。題を入れると、
本来は言語ごとに作り直す話になる。

Play が撥ねる決まり:

  * 寸法は厳密。1px でも違えば受け付けない
  * 1024x500 は透過を持てない（RGB で保存する）
  * 512x512 は 32bit PNG（RGBA で保存する）。角丸も影も付けない。
    Play が自分で付けるので、付けると二重になる
  * 「今すぐダウンロード」「50% OFF」などの誘導文・宣伝、受賞の主張、
    ストアの UI に似せた部品は禁止

面によって左右が切られるので、大事なものは MARGIN より内側に置く。
題を真ん中に置かないのは、あとでプロモ動画を足すと大きな ▶ が
ど真ん中に重なるため。

使い方（プロジェクトの根で）:
    python Tools/make_feature_graphic.py
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUTPUT = os.path.join(ROOT, "Store")
ICON_SOURCE = os.path.join(ROOT, "Assets", "Textures", "AppIcon3.PNG")

# ---------------------------------------------------------------- 配色
#
# Assets/Scripts/View/PentominoStyle.cs の DefaultPieceColors と同じ。
# あちらを変えたら、こちらも合わせること。

COLORS = {
    "F": (0xF7, 0xB7, 0xA3), "I": (0xF9, 0xD5, 0xA7), "L": (0xF2, 0xE6, 0xA0),
    "N": (0xD6, 0xE8, 0xA8), "P": (0xA8, 0xDE, 0xB5), "T": (0xA5, 0xDD, 0xD5),
    "U": (0xA9, 0xD3, 0xEC), "V": (0xB7, 0xC4, 0xEE), "W": (0xC9, 0xB8, 0xE8),
    "X": (0xE0, 0xB6, 0xE0), "Y": (0xF2, 0xB3, 0xCE), "Z": (0xE3, 0xD5, 0xC0),
}

# 実機（AQUOS sense7 plus）の画面から拾った色。画面と絵を揃えるため。
BACKGROUND = (0xCA, 0xEE, 0xFB)
EMPTY = (0xED, 0xED, 0xED)
LINE = (0, 0, 0)
TITLE_COLOR = (0x14, 0x3C, 0x5A)
SUBTITLE_COLOR = (0x3A, 0x62, 0x7A)

# PentominoStyle の outerLineRatio / innerLineRatio / labelSizeRatio と同じ。
OUTER_RATIO = 0.08
INNER_RATIO = 0.03
LABEL_RATIO = 0.5

TITLE = "Pentomino"
SUBTITLE = "6 x 10"
NOTE = "2,339 puzzles"

# ---------------------------------------------------------------- 形

BASE = {
    "F": [(0, 1), (0, 2), (1, 0), (1, 1), (2, 1)],
    "I": [(0, 0), (1, 0), (2, 0), (3, 0), (4, 0)],
    "L": [(0, 0), (1, 0), (2, 0), (3, 0), (3, 1)],
    "N": [(0, 1), (1, 1), (2, 0), (2, 1), (3, 0)],
    "P": [(0, 0), (0, 1), (1, 0), (1, 1), (2, 0)],
    "T": [(0, 0), (0, 1), (0, 2), (1, 1), (2, 1)],
    "U": [(0, 0), (0, 2), (1, 0), (1, 1), (1, 2)],
    "V": [(0, 0), (1, 0), (2, 0), (2, 1), (2, 2)],
    "W": [(0, 0), (1, 0), (1, 1), (2, 1), (2, 2)],
    "X": [(0, 1), (1, 0), (1, 1), (1, 2), (2, 1)],
    "Y": [(0, 1), (1, 0), (1, 1), (2, 1), (3, 1)],
    "Z": [(0, 0), (0, 1), (1, 1), (2, 1), (2, 2)],
}

ROWS, COLS = 6, 10

# 一度大きく描いて縮める。線の縁が滑らかになる。
SCALE = 3

# 端は面によって切られる。大事なものはここより内側に置く。
MARGIN = 102


def normalize(cells):
    top = min(r for r, _ in cells)
    left = min(c for _, c in cells)
    return tuple(sorted((r - top, c - left) for r, c in cells))


def orientations(cells):
    """裏返しと回転で得られる形を、重複を除いて並べる。"""
    seen, out = set(), []
    for flip in range(2):
        current = [(r, -c) for r, c in cells] if flip else list(cells)
        for _ in range(4):
            current = [(c, -r) for r, c in current]
            key = normalize(current)
            if key not in seen:
                seen.add(key)
                out.append(key)
    return out


SHAPES = {name: orientations(cells) for name, cells in BASE.items()}


def solve():
    """6x10 に 12 個すべてを収める配置を一つ見つける。"""
    board = [[None] * COLS for _ in range(ROWS)]
    left = set(BASE)

    def place():
        # いちばん上の空きマスを、必ず今回の一手で埋める。
        # そうしないと同じ配置を何度も試すことになる。
        spot = next(((r, c) for r in range(ROWS) for c in range(COLS)
                     if board[r][c] is None), None)
        if spot is None:
            return True

        row, col = spot
        for name in sorted(left):
            for shape in SHAPES[name]:
                for anchor_r, anchor_c in shape:
                    cells = [(row + r - anchor_r, col + c - anchor_c) for r, c in shape]
                    if any(not (0 <= r < ROWS and 0 <= c < COLS) or board[r][c]
                           for r, c in cells):
                        continue

                    for r, c in cells:
                        board[r][c] = name
                    left.discard(name)

                    if place():
                        return True

                    left.add(name)
                    for r, c in cells:
                        board[r][c] = None
        return False

    if not place():
        raise SystemExit("6x10 の答えが見つかりません。形の定義を疑うこと。")
    return board


# ---------------------------------------------------------------- 書体

def find_font(names):
    """Windows と macOS の両方で見つかる書体を返す。"""
    places = [
        "C:/Windows/Fonts",
        "/System/Library/Fonts/Supplemental",
        "/System/Library/Fonts",
        "/Library/Fonts",
    ]
    for place in places:
        for name in names:
            path = os.path.join(place, name)
            if os.path.exists(path):
                return path

    raise SystemExit("書体が見つかりません: " + " / ".join(names))


REGULAR = ["arial.ttf", "Arial.ttf", "Helvetica.ttc"]
BOLD = ["arialbd.ttf", "Arial Bold.ttf", "Arial.ttf", "Helvetica.ttc"]


# ---------------------------------------------------------------- 描画

def draw_group(draw, cells, color, cell, origin, label, font):
    """同じピースのマスの集まりを、細線で仕切り太線で囲んで描く。"""
    ox, oy = origin
    inner = max(1, round(cell * INNER_RATIO))
    outer = max(1, round(cell * OUTER_RATIO))
    have = set(cells)

    for r, c in cells:
        x, y = ox + c * cell, oy + r * cell
        draw.rectangle([x, y, x + cell, y + cell], fill=color)

    # 細線は内側の仕切り。隣に同じピースが居る辺だけ。
    for r, c in cells:
        x, y = ox + c * cell, oy + r * cell
        if (r, c + 1) in have:
            draw.rectangle([x + cell - inner // 2, y, x + cell + inner // 2, y + cell],
                           fill=LINE)
        if (r + 1, c) in have:
            draw.rectangle([x, y + cell - inner // 2, x + cell, y + cell + inner // 2],
                           fill=LINE)

    # 太線は外周。隣に同じピースが居ない辺だけ。
    for r, c in cells:
        x, y = ox + c * cell, oy + r * cell
        if (r - 1, c) not in have:
            draw.rectangle([x - outer, y - outer, x + cell + outer, y + outer], fill=LINE)
        if (r + 1, c) not in have:
            draw.rectangle([x - outer, y + cell - outer, x + cell + outer, y + cell + outer],
                           fill=LINE)
        if (r, c - 1) not in have:
            draw.rectangle([x - outer, y - outer, x + outer, y + cell + outer], fill=LINE)
        if (r, c + 1) not in have:
            draw.rectangle([x + cell - outer, y - outer, x + cell + outer, y + cell + outer],
                           fill=LINE)

    for r, c in cells:
        x, y = ox + c * cell, oy + r * cell
        draw.text((x + cell / 2, y + cell / 2), label, font=font, fill=LINE, anchor="mm")


def draw_board(draw, board, cell, origin, skip, font):
    """盤を描く。skip のピースは置かず、跡を空きマスのまま残す。"""
    ox, oy = origin
    outer = max(1, round(cell * OUTER_RATIO))
    inner = max(1, round(cell * INNER_RATIO))
    width, height = COLS * cell, ROWS * cell

    draw.rectangle([ox - outer, oy - outer, ox + width + outer, oy + height + outer],
                   fill=LINE)

    for r in range(ROWS):
        for c in range(COLS):
            x, y = ox + c * cell, oy + r * cell
            draw.rectangle([x, y, x + cell, y + cell], fill=EMPTY)

    for r in range(1, ROWS):
        y = oy + r * cell
        draw.rectangle([ox, y - inner // 2, ox + width, y + inner // 2], fill=LINE)
    for c in range(1, COLS):
        x = ox + c * cell
        draw.rectangle([x - inner // 2, oy, x + inner // 2, oy + height], fill=LINE)

    for name in sorted(set(BASE) - ({skip} if skip else set())):
        cells = [(r, c) for r in range(ROWS) for c in range(COLS) if board[r][c] == name]
        draw_group(draw, cells, COLORS[name], cell, origin, name, font)


def render(path, with_title):
    board = solve()

    # 題を入れるときは盤を完成させる。題と浮いたピースが場所を取り合うため。
    floating = None
    if not with_title:
        # 左端に触れているものを外へ出すと、これから入るように見える。
        edge = [board[r][0] for r in range(ROWS)]
        floating = max(set(edge), key=edge.count)

    width, height = 1024 * SCALE, 500 * SCALE
    image = Image.new("RGB", (width, height), BACKGROUND)
    draw = ImageDraw.Draw(image)

    cell = (50 if with_title else 62) * SCALE
    board_w, board_h = COLS * cell, ROWS * cell
    gap = 46 * SCALE

    if with_title:
        board_x = width - board_w - MARGIN * SCALE
    else:
        piece = [(r, c) for r in range(ROWS) for c in range(COLS) if board[r][c] == floating]
        piece_cols = max(c for _, c in piece) - min(c for _, c in piece) + 1
        board_x = (width - (piece_cols * cell + gap + board_w)) // 2 + piece_cols * cell + gap

    board_y = (height - board_h) // 2

    font_cell = ImageFont.truetype(find_font(REGULAR), int(cell * LABEL_RATIO))
    draw_board(draw, board, cell, (board_x, board_y), floating, font_cell)

    if floating:
        # 外へ出した一個。盤のすぐ左に、収まるべき高さで浮かせる。
        top = min(r for r, _ in piece)
        left = min(c for _, c in piece)
        shifted = [(r - top, c - left) for r, c in piece]

        draw_group(draw, shifted, COLORS[floating], cell,
                   (board_x - gap - piece_cols * cell, board_y + top * cell),
                   floating, font_cell)

    if with_title:
        big = ImageFont.truetype(find_font(BOLD), 54 * SCALE)
        small = ImageFont.truetype(find_font(REGULAR), 26 * SCALE)
        x = MARGIN * SCALE
        draw.text((x, height // 2 - 42 * SCALE), TITLE, font=big,
                  fill=TITLE_COLOR, anchor="lm")
        draw.text((x, height // 2 + 12 * SCALE), SUBTITLE, font=big,
                  fill=TITLE_COLOR, anchor="lm")
        draw.text((x + 3 * SCALE, height // 2 + 60 * SCALE), NOTE, font=small,
                  fill=SUBTITLE_COLOR, anchor="lm")

    # 1024x500 は透過を持てないので RGB のまま保存する。
    image.resize((1024, 500), Image.LANCZOS).save(path, "PNG")
    return floating


def render_icon(path):
    """アイコンを 512x512 にする。角丸も影も付けない。Play が付けるので。"""
    if not os.path.exists(ICON_SOURCE):
        print("アイコンの元が見つかりません: " + ICON_SOURCE)
        return False

    source = Image.open(ICON_SOURCE)
    # Play は 32bit PNG を求める。中身は不透明のまま alpha だけ持たせる。
    source.convert("RGBA").resize((512, 512), Image.LANCZOS).save(path, "PNG")
    return True


def check(path, size, mode):
    """出したものが Play の求める通りか、その場で確かめる。"""
    image = Image.open(path)
    if image.size != size or image.mode != mode:
        raise SystemExit("%s が %s %s になっています（%s %s のはず）"
                         % (path, image.size, image.mode, size, mode))

    print("  %-20s %s %s  %d bytes"
          % (os.path.basename(path), image.size, image.mode, os.path.getsize(path)))


def main():
    os.makedirs(OUTPUT, exist_ok=True)

    plain = os.path.join(OUTPUT, "feature_plain.png")
    title = os.path.join(OUTPUT, "feature_title.png")
    icon = os.path.join(OUTPUT, "icon_512.png")

    floating = render(plain, with_title=False)
    render(title, with_title=True)

    print("Store 用の絵を作りました。")
    check(plain, (1024, 500), "RGB")
    check(title, (1024, 500), "RGB")

    if render_icon(icon):
        check(icon, (512, 512), "RGBA")

    print("  盤の外へ出した一個: " + floating)


if __name__ == "__main__":
    sys.exit(main())
