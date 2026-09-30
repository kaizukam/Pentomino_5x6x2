# -*- coding: utf-8 -*-
"""
書体を、このアプリが実際に使う文字だけに削る。

Noto Sans の CJK 書体は 1 万〜4 万字を収めているが、このアプリが出す文字は
1000 字に満たない。丸ごと積むと 28MB になり、アプリの容量の大半を占める。

残す文字は Unity 側が書き出す。数えるのは Pentomino.Data.TextInventory
ひとつだけで、説明書・画面の文言・言語の呼び名を、アプリと同じ経路で集める。

書体の権利表示も、ここで書き出す。Noto は SIL Open Font License 1.1 で、
商用利用も埋め込みも削ることも許されているが、ライセンス本文を添えることを
求めている。表示は書体そのものから読み出すので、差し替えてもずれない。

元の書体は Fonts_Full/ に控えてある（Git 対象外）。何度でも削り直せる。
文章を書き足したら、もう一度これを走らせること。忘れると、足した文字が
豆腐（□）になる。忘れていないかは Unity 側のテストが見張っている。

使い方（プロジェクトの根で）:
    1. Unity で  メニュー Pentomino ▸ フォントに要る文字を書き出す
    2. python Tools/subset_fonts.py
    3. Assets/Fonts、covered.txt、licenses.txt をコミット

    python Tools/subset_fonts.py --dry-run    削らずに、何字になるかだけ見る
"""
import glob
import io
import os
import subprocess
import sys

SOURCE_FOLDER = "Fonts_Full"
TARGET_FOLDER = "Assets/Fonts"

# 削った書体が、どの文字を持っているかの控え。Unity 側のテストが読む。
COVERAGE_PATH = "Assets/Resources/Fonts/covered.txt"

# Unity 側が書き出す、画面に出しうる文字の一覧。
CHARACTERS_PATH = "Tools/needed_characters.txt"

# 書体の権利表示。説明書の末尾に出す。
LICENSE_PATH = "Assets/Resources/Manual/licenses.txt"
OFL_PATH = "Tools/OFL-1.1.txt"


def collect():
    """
    削る文字を読む。

    数えるのは Unity 側（Pentomino.Data.TextInventory）ひとつだけ。
    ここで別に数えると、いつか食い違う。実際に一度、言語の呼び名が
    Strings.cs ではなく Language.cs にあったため数え漏れた。
    """
    if not os.path.exists(CHARACTERS_PATH):
        raise SystemExit(
            CHARACTERS_PATH + " がありません。\n"
            "先に Unity で メニュー Pentomino ▸ フォントに要る文字を書き出す "
            "を実行してください。")

    chars = set(io.open(CHARACTERS_PATH, encoding="utf-8-sig").read())
    return {c for c in chars if c not in "\r\n\t"}


def covered_by(path, wanted):
    """その書体が実際に持っている文字を返す。"""
    from fontTools.ttLib import TTFont

    with TTFont(path, lazy=True) as font:
        have = set()
        for table in font["cmap"].tables:
            have |= {chr(code) for code in table.cmap.keys()}

    return {c for c in wanted if c in have}


def subset(source, target, chars):
    text = "".join(sorted(chars))

    # --text で残す文字を指定する。字形の合成規則（liga など）も残しておかないと、
    # 欧文のアクセント付き文字が崩れることがある。
    command = [
        sys.executable, "-m", "fontTools.subset", source,
        "--text=" + text,
        "--output-file=" + target,
        "--layout-features=*",
        "--drop-tables+=DSIG",
        "--name-IDs=*",
        "--recalc-bounds",
    ]

    result = subprocess.run(command, capture_output=True, text=True)
    if result.returncode != 0:
        raise SystemExit("削れませんでした: " + source + "\n" + result.stderr)


def names_of(path):
    """書体の名前（nameID 1）と権利表示（nameID 0）を取り出す。"""
    from fontTools.ttLib import TTFont

    found = {}
    with TTFont(path, lazy=True) as font:
        for record in font["name"].names:
            if record.nameID not in (0, 1) or record.nameID in found:
                continue
            try:
                found[record.nameID] = " ".join(record.toUnicode().split())
            except Exception:
                pass

    return found


def write_licenses(sources):
    """書体の権利表示と OFL 本文を、説明書に差し込める形で書き出す。"""
    lines = []
    for path in sources:
        found = names_of(path)
        lines.append("- " + found.get(1, os.path.basename(path)))
        if found.get(0):
            lines.append("  " + found[0])

    ofl = io.open(OFL_PATH, encoding="utf-8").read().rstrip()
    text = "\n".join(lines) + "\n\n" + ofl + "\n"

    io.open(LICENSE_PATH, "w", encoding="utf-8", newline="\n").write(text)
    return len(sources), len(text)


def main():
    dry = "--dry-run" in sys.argv

    if not os.path.isdir(SOURCE_FOLDER):
        raise SystemExit(SOURCE_FOLDER + " がありません。元の書体を置いてください。")

    sources = sorted(glob.glob(os.path.join(SOURCE_FOLDER, "*.ttf")))
    wanted = collect()

    print("アプリが使う文字: %d 種" % len(wanted))
    print()

    total_before = 0
    total_after = 0
    covered_all = set()

    for source in sources:
        name = os.path.basename(source)
        target = os.path.join(TARGET_FOLDER, name)

        here = covered_by(source, wanted)
        covered_all |= here

        before = os.path.getsize(source)
        total_before += before

        if dry:
            print("  %-24s %6.2f MB  残す %4d 字" % (name, before / 1048576, len(here)))
            continue

        subset(source, target, here)

        after = os.path.getsize(target)
        total_after += after
        print("  %-24s %6.2f MB → %6.2f MB  (%4d 字)"
              % (name, before / 1048576, after / 1048576, len(here)))

    if dry:
        return

    print()
    print("合計 %.2f MB → %.2f MB  （%.0f%% 減）"
          % (total_before / 1048576, total_after / 1048576,
             (1 - total_after / total_before) * 100))

    fonts, size = write_licenses(sources)
    print("権利表示: %s （%d 書体 / %d 文字）" % (LICENSE_PATH, fonts, size))

    # どの文字を出せるかの控えを残す。Unity 側のテストがこれと突き合わせる。
    os.makedirs(os.path.dirname(COVERAGE_PATH), exist_ok=True)
    io.open(COVERAGE_PATH, "w", encoding="utf-8", newline="\n").write(
        "".join(sorted(covered_all)))

    missing = wanted - covered_all
    if missing:
        print()
        print("※ どの書体にも無い文字が %d 種あります: %s"
              % (len(missing), "".join(sorted(missing))))


if __name__ == "__main__":
    main()
