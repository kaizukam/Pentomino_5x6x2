"""B5D_Answer.csv から Hint_pattern.csv を作る。

B5D_Answer.csv は解 264 通りそれぞれ（Group）に、向きを変えた 8 通り（Member）と
そのレベルを並べたもの。ここから Group ごとに 1 つずつ採用して、問題表にする。

  レベル  各 Group で使うレベル。既定では今の問題表（--levels で渡す）と同じにして、
          級ごとの問題数を変えない。--levels を渡さなければ Group の Max を使う。
  選び方  そのレベルの Member が複数あれば、直前の問題と似ていないものを選ぶ。
  並び    レベルの順（アプリの説明書の表が、番号の範囲をこの順で出す）。
          同じレベルの中は、直前と 2 つ前の問題と共通するピース（姿勢も位置も同じ）が
          少ないものを次に置く。番号の近い Group は解が似ているので、そのまま並べると
          ◀ ▶ で送ったときに同じ問題に見える。

使い方:
  python Tools/make_hint_pattern.py Data_5x6x2/B5D_Answer.csv out.csv \
      --levels Assets/Resources/Data/Hint_pattern_5x6x2.csv
"""
import argparse
import collections
import csv
import sys

TOKEN = 7
PIECES = 12


def tokens(answer):
    return frozenset(answer[i * TOKEN:(i + 1) * TOKEN] for i in range(PIECES))


def load_source(path):
    groups = collections.defaultdict(list)
    with open(path, encoding='utf-8-sig', newline='') as f:
        for row in csv.DictReader(f):
            groups[row['Group']].append({
                'group': row['Group'],
                'member': int(row['Member']),
                'level': int(row['Level']),
                'max': int(row['Max']),
                'answer': row['Answer'].strip(),
            })
    return groups


def load_levels(path, groups):
    """今の問題表から、Group ごとのレベルと採用した Member を読む（解の文字列で Group を引く）。

    見出しは "Answer" だったり "answer" だったりするので、列の位置で読む。
    番号の頭の記号（"$" や "#"）も返し、書き出しで同じものを使う。
    """
    by_answer = {m['answer']: m for members in groups.values() for m in members}
    levels = {}
    chosen = {}
    prefix = '$'
    with open(path, encoding='utf-8-sig', newline='') as f:
        reader = csv.reader(f)
        next(reader)
        for row in reader:
            if not row:
                continue
            if row[0][:1] in '$#':
                prefix = row[0][0]
            member = by_answer.get(row[2].strip())
            if member is None:
                sys.exit('今の問題表の解が B5D_Answer.csv にありません: ' + row[0])
            levels[member['group']] = int(row[1])
            chosen[member['group']] = member
    return levels, chosen, prefix


def overlap(a, b):
    return len(a['tokens'] & b['tokens'])


def arrange(candidates_by_group, window):
    """1 つのレベルの中で、似ていない順に並べる。

    candidates_by_group: Group -> そのレベルの Member の一覧。
    直前の問題との共通数を重く（2 倍）、その前を軽く数え、いちばん小さいものを次に置く。
    同点なら Group 番号と Member 番号の小さいほう（毎回同じ結果になるように）。
    """
    remaining = dict(candidates_by_group)
    placed = []
    while remaining:
        best = None
        for group in sorted(remaining, key=int):
            for member in sorted(remaining[group], key=lambda m: m['member']):
                score = 0
                for back, previous in enumerate(reversed(placed[-window:])):
                    score += overlap(member, previous) * (2 if back == 0 else 1)
                key = (score, int(group), member['member'])
                if best is None or key < best[0]:
                    best = (key, member)
        placed.append(best[1])
        del remaining[best[1]['group']]
    return improve(placed, window)


def cost(order, window):
    """並び全体の似ている度合い。直前は 2 倍、その前は 1 倍で数える。"""
    total = 0
    for i in range(1, len(order)):
        for back in range(1, window + 1):
            if i - back < 0:
                break
            total += overlap(order[i], order[i - back]) * (2 if back == 1 else 1)
    return total


def improve(order, window):
    """2 つの問題を入れ替えて良くなるうちは入れ替える。

    前から順に決めていくと、最後に残った似た者同士が隣に並ぶことがある。
    """
    best = cost(order, window)
    improved = True
    while improved:
        improved = False
        for i in range(len(order)):
            for j in range(i + 1, len(order)):
                order[i], order[j] = order[j], order[i]
                trial = cost(order, window)
                if trial < best:
                    best = trial
                    improved = True
                else:
                    order[i], order[j] = order[j], order[i]
    return order


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('source', help='B5D_Answer.csv')
    parser.add_argument('output', help='書き出す Hint_pattern.csv')
    parser.add_argument('--levels', help='レベルを引き継ぐ今の問題表。無ければ各 Group の Max')
    parser.add_argument('--window', type=int, default=2, help='似ていないか比べる、前の問題の数')
    parser.add_argument('--keep-members', action='store_true',
                        help='--levels の表が採用した Member をそのまま使い、並び順だけを変える')
    args = parser.parse_args()

    groups = load_source(args.source)
    for members in groups.values():
        for m in members:
            m['tokens'] = tokens(m['answer'])

    if args.keep_members and not args.levels:
        sys.exit('--keep-members には --levels が要ります')
    if args.levels:
        levels, chosen, prefix = load_levels(args.levels, groups)
    else:
        levels, chosen, prefix = {g: ms[0]['max'] for g, ms in groups.items()}, {}, '$'

    missing = sorted(set(groups) - set(levels), key=int)
    if missing:
        sys.exit('レベルの決まらない Group があります: ' + ', '.join(missing))

    by_level = collections.defaultdict(dict)
    for group, members in groups.items():
        level = levels[group]
        if args.keep_members:
            candidates = [chosen[group]]
        else:
            candidates = [m for m in members if m['level'] == level]
        if not candidates:
            sys.exit('Group %s にレベル %d の Member がありません' % (group, level))
        by_level[level][group] = candidates

    rows = []
    for level in sorted(by_level):
        rows.extend(arrange(by_level[level], args.window))

    with open(args.output, 'w', encoding='utf-8-sig', newline='') as f:
        f.write('Group,Level,Answer\r\n')
        for number, m in enumerate(rows, 1):
            f.write('%s%04d,%d,%s\r\n' % (prefix, number, m['level'], m['answer']))

    # 隣り合う問題の共通ピース数の分布を出して、並べ替えの効き目を見る。
    counts = collections.Counter()
    for a, b in zip(rows, rows[1:]):
        if a['level'] == b['level']:
            counts[overlap(a, b)] += 1
    print('問題数', len(rows), ' 級ごと', dict(sorted(collections.Counter(m['level'] for m in rows).items())))
    print('隣り合う同じ級の組の、共通ピース数 -> 組数', dict(sorted(counts.items())))


if __name__ == '__main__':
    main()
