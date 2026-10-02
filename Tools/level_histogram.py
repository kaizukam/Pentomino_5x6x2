"""問題表（Hint_pattern）のレベルごとのヒストグラムを作る。

数えるのは、各問題の Answer の最後のピース（盤の右下、どのレベルでも必ず外される）。
ピース名ごとと姿勢ごとに、レベル 1..10 と合計を出す。

書式は E:\\dwTOOLS\\14.Pentomino\\BOX_5X6X2_ANAL\\C2D_Histogram_on_Level.csv と同じ
（UTF-8 BOM 付き、0 は "-"、列は Level, FILNPTUVWXYZ, Total, 姿勢 99 種）。
あちらの C1P_Histogram_on_Level.py は B5D_Answer.csv（2,112 行）専用なので、
264 行の問題表にはこちらを使う。

使い方:
  python Tools/level_histogram.py Data_5x6x2/Hint_5x6x2/Hint_pattern.csv out.csv
"""
import argparse
import csv
import json
import re

LEVELS = range(1, 11)
PIECES = list("FILNPTUVWXYZ")
TOKEN = 7


def load_postures(path):
    # 支給の Posture_DB.json は字下げにノーブレークスペースを含むので、空白に直してから読む。
    with open(path, encoding="utf-8-sig") as f:
        return sorted(json.loads(f.read().replace("\u00a0", " ")))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("table", help="Hint_pattern の CSV（Group, Level, Answer の 3 列）")
    parser.add_argument("output", help="書き出すヒストグラムの CSV")
    parser.add_argument("--postures", default="Assets/Resources/Data/Posture_DB.json")
    args = parser.parse_args()

    postures = load_postures(args.postures)
    columns = PIECES + postures
    counts = {level: {c: 0 for c in columns} for level in LEVELS}

    with open(args.table, encoding="utf-8-sig", newline="") as f:
        reader = csv.reader(f)
        next(reader)   # 見出しは "Answer" だったり "answer" だったりするので、位置で読む
        for number, row in enumerate(reader, 2):
            if not row:
                continue
            level = int(row[1])
            answer = row[2].strip()
            if level not in counts or not re.fullmatch(r"(\w{3}\d{4}){12}", answer):
                raise SystemExit("%d 行目を読めません: %s" % (number, ",".join(row)))
            posture = answer[-TOKEN:][:3]
            counts[level][posture[0]] += 1
            counts[level][posture] += 1

    def cell(value):
        return "-" if value == 0 else str(value)

    with open(args.output, "w", encoding="utf-8-sig", newline="") as f:
        writer = csv.writer(f, lineterminator="\r\n")
        writer.writerow(["Level"] + PIECES + ["Total"] + postures)
        for level in LEVELS:
            c = counts[level]
            writer.writerow([level] + [cell(c[p]) for p in PIECES] + [cell(sum(c[p] for p in PIECES))]
                            + [cell(c[k]) for k in postures])
        totals = {k: sum(counts[level][k] for level in LEVELS) for k in columns}
        writer.writerow(["Total"] + [cell(totals[p]) for p in PIECES] + [cell(sum(totals[p] for p in PIECES))]
                        + [cell(totals[k]) for k in postures])


if __name__ == "__main__":
    main()
