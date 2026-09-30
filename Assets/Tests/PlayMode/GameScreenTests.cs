using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Pentomino.Core;
using Pentomino.Data;
using Pentomino.View;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Pentomino.Tests
{
    /// <summary>
    /// 画面を実際に組み立てて、レイアウトと座標変換が仕様どおりかを確かめる。
    /// 指の入力は伴わないので、はめ込みの座標計算は GameScreen が使うのと同じ経路で検証する。
    /// </summary>
    public class GameScreenTests
    {
        private const float ReferenceWidth = 1080f;
        private const float SideMargin = 18f;   // 2026-09-13 の案の余白。GameScreen の既定と場面（Game.unity）に揃える

        private string _savePath;
        private GameObject _canvasGo;
        private GameScreen _screen;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savePath = Path.Combine(Path.GetTempPath(), "pentomino_play_" + Path.GetRandomFileName() + ".json");
            ProgressStore.DefaultPath = _savePath;

            _canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = _canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = _canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, 1920f);
            // 実際の場面と同じ合わせ方にする。
            // Expand なら縦横どちらも基準に収まるので、画面の形に関わらず組み立てが決まる。
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0f;

            var screenGo = new GameObject("GameScreen", typeof(RectTransform), typeof(GameScreen));
            screenGo.transform.SetParent(_canvasGo.transform, false);
            _screen = screenGo.GetComponent<GameScreen>();

            yield return null;   // Awake と最初のレイアウトを終わらせる
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
            ProgressStore.DefaultPath = null;
            if (File.Exists(_savePath)) File.Delete(_savePath);
        }

        private BoardWidget Board => _screen.GetComponentInChildren<BoardWidget>();

        [Test]
        public void 画面が組み立てられセッションが始まっている()
        {
            Assert.IsNotNull(_screen.Session);
            Assert.IsNotNull(Board);
            Assert.IsFalse(_screen.Session.IsSolved);
        }

        /// <summary>名前で子孫から探す。階層の組み替えに引きずられないように。</summary>
        private static RectTransform Deep(Transform root, string name)
        {
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name) return rect;

            Assert.Fail(name + " が帯の中に見つかりません");
            return null;
        }

        /// <summary>ワールド座標での左右の端。</summary>
        private static (float Left, float Right) Edges(RectTransform rect)
        {
            var c = new Vector3[4];
            rect.GetWorldCorners(c);
            return (c[0].x, c[2].x);
        }

        [UnityTest]
        public IEnumerator 帯とボタンの段がBOXと左右で揃う()
        {
            yield return null;

            var board = Edges(Board.RectTransform);
            var header = _screen.Header.RectTransform;

            var bar = Edges(header);
            Assert.AreEqual(board.Left, bar.Left, 0.5f, "帯の左端が BOX と揃う");
            Assert.AreEqual(board.Right, bar.Right, 0.5f, "帯の右端が BOX と揃う");

            // ボタンの段は左端が Check、右端が級の帯。左右の余白の設定を変えても崩れないこと。
            // ボタンは畳む面（Pane/Content）の中にある。直下ではないので、名前で潜って探す。
            var check = Edges(Deep(header, "Check"));
            var grade = Edges(Deep(header, "Grade"));

            Assert.AreEqual(board.Left, check.Left, 0.5f, "Check が BOX の左端と揃う");
            Assert.LessOrEqual(grade.Right, board.Right + 0.5f, "級の帯が BOX の右端より内側にある");

            // ボタンは左に寄せてある（ピースを置く場所の近くで Hint を押さないように）。
            // 級の帯は Check / Hint / ← / → の右。絵の左端 50 は字が無いので、
            // そこだけは → の下に潜ってよい（帯の縮尺ぶん換算する）。
            var forward = Edges(Deep(header, "StepForward"));
            var blank = 50f * header.localScale.x;
            Assert.LessOrEqual(forward.Right, grade.Left + blank + 0.5f, "級の帯の字は → の右にある");
        }

        [UnityTest]
        public IEnumerator 帯は画面の上端から始まり歯車は緩衝の中にある()
        {
            yield return null;

            var header = _screen.Header.RectTransform;
            var c = new Vector3[4];
            header.GetWorldCorners(c);
            var headerTop = c[1].y;

            var rootRect = (RectTransform)_screen.transform;
            rootRect.GetWorldCorners(c);
            var screenTop = c[1].y;

            // でっぱりの逃げは帯の中（緩衝の 250）に取ってあるので、帯そのものは上端に付く。
            Assert.AreEqual(screenTop, headerTop, 0.5f, "帯が画面の上端から始まる");

            // 歯車は緩衝の中（0〜250）に収まり、タイトルの絵はその下（250〜）から。
            var gear = Deep(header, "Settings");
            var title = Deep(header, "Title");
            gear.GetWorldCorners(c);
            var gearBottom = c[0].y;
            title.GetWorldCorners(c);
            var titleTop = c[1].y;

            Assert.GreaterOrEqual(gearBottom, titleTop - 0.5f, "歯車はタイトルより上にある");
        }

        /// <summary>基準（1080x1920）の物差しで測った画面の大きさ。</summary>
        private static Vector2 ScreenSizeInReferenceUnits()
        {
            // Canvas Scaler は Expand なので、縮尺は縦横の比のうち小さいほう。
            var scale = Mathf.Min(Screen.width / ReferenceWidth, Screen.height / 1920f);
            return scale <= 0f
                ? new Vector2(ReferenceWidth, 1920f)
                : new Vector2(Screen.width / scale, Screen.height / scale);
        }

        [Test]
        public void BOXが表示幅いっぱいになる()
        {
            // 開発仕様「画面対応」: 表示に使う幅は、画面幅と画面高さの半分の小さいほう。
            // 縦が横の 2 倍以上ある細長い画面では画面幅、そうでなければ高さの半分に規制する。
            var screen = ScreenSizeInReferenceUnits();
            var display = Mathf.Min(screen.x, screen.y / 2.14f);   // 開発仕様「操作性の問題(3)」

            var expected = display - SideMargin * 2f;
            Assert.AreEqual(expected, Board.Size.x, 0.5f, "BOX の幅が表示幅から余白を引いた値と一致すること");

            // セルは正方形なので、高さは 6 行分になる。
            var style = Board.Style;
            Assert.AreEqual(Board.CellSize * Core.Board.Rows + style.OuterLineWidth, Board.Size.y, 0.5f);
        }

        [Test]
        public void 待機場所のピースはBOX幅に収まる()
        {
            var trayWidth = ReferenceWidth - SideMargin * 2f;

            foreach (var widget in _screen.GetComponentsInChildren<PieceWidget>())
            {
                if (widget.IsOnBoard) continue;

                var size = widget.View.Graphic.PreferredSize;
                Assert.LessOrEqual(size.x, trayWidth + 0.5f, widget.Piece + " が BOX 幅に収まらない");

                var x = widget.RectTransform.anchoredPosition.x;
                Assert.GreaterOrEqual(x, -0.5f, widget.Piece + " が左にはみ出している");
                Assert.LessOrEqual(x + size.x, trayWidth + 0.5f, widget.Piece + " が右にはみ出している");
            }
        }

        [Test]
        public void 待機場所は縦スクロールだけで横はスクロールしない()
        {
            var scroll = _screen.GetComponentInChildren<ScrollRect>();
            Assert.IsNotNull(scroll);
            Assert.IsTrue(scroll.vertical);
            Assert.IsFalse(scroll.horizontal, "横方向はスクロールしない");
        }

        [UnityTest]
        public IEnumerator 盤上に置いたピースがセルにぴったり合う()
        {
            var session = _screen.Session;

            // 立てて置くピースがあればそれを選ぶ。左右の格子にまたがっても合うことを確かめたい。
            var target = default(Placement);
            foreach (var placement in session.Puzzle.Hidden)
            {
                if (!target.IsValid || placement.Posture.IsStanding) target = placement;
                if (placement.Posture.IsStanding) break;
            }

            // 待機場所の姿勢を解と揃えてから置く。
            Assert.IsTrue(TurnTo(session, target.Posture), "解の姿勢に回せること");
            Assert.IsTrue(session.TryPlace(target.Piece, target.Row, target.Col, target.Layer), "解の位置に置けること");
            _screen.Refresh();
            yield return null;

            PieceWidget placed = null;
            foreach (var widget in _screen.GetComponentsInChildren<PieceWidget>())
            {
                if (widget.Piece != target.Piece) continue;
                placed = widget;
                break;
            }

            Assert.IsNotNull(placed, "置いたピースの表示が見つからない");
            Assert.IsTrue(placed.IsOnBoard);

            // ピースのどのセルの角も、盤の同じセルの角と一致していること。
            // 立てたピースなら、右の格子にかかる部分も盤の右の格子に合う。
            var graphic = placed.View.Graphic;
            foreach (var cell in target.Posture.Cells)
            {
                var onBoard = new Cell(target.Row + cell.Row, target.Col + cell.Col, target.Layer + cell.Layer);
                var expected = Board.CellCornerWorld(onBoard);
                var actual = graphic.transform.TransformPoint(graphic.CellCorner(cell));
                Assert.AreEqual(expected.x, actual.x, 0.5f, "X がずれている " + onBoard);
                Assert.AreEqual(expected.y, actual.y, 0.5f, "Y がずれている " + onBoard);
            }
        }

        /// <summary>タップとフリックだけで、待機場所のピースを姿勢 to に回す。</summary>
        private static bool TurnTo(PuzzleSession session, Posture to)
        {
            if (!session.TryGetTrayPiece(to.Piece, out var tray)) return false;

            var moves = new[] { FlickAxis.None, FlickAxis.Left, FlickAxis.Right, FlickAxis.Up, FlickAxis.Down };
            var previous = new Dictionary<Posture, KeyValuePair<Posture, FlickAxis>>();
            var queue = new Queue<Posture>();
            previous[tray.Posture] = default;
            queue.Enqueue(tray.Posture);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var move in moves)
                {
                    var q = move == FlickAxis.None ? p.RotatedCcw : p.Rolled(move);
                    if (q == null || previous.ContainsKey(q)) continue;
                    previous[q] = new KeyValuePair<Posture, FlickAxis>(p, move);
                    queue.Enqueue(q);
                }
            }
            if (!previous.ContainsKey(to)) return false;

            var path = new List<FlickAxis>();
            for (var p = to; !ReferenceEquals(p, tray.Posture); p = previous[p].Key) path.Add(previous[p].Value);
            path.Reverse();

            foreach (var move in path)
            {
                if (move == FlickAxis.None) session.RotateCcw(to.Piece);
                else session.Roll(to.Piece, move);
            }
            return ReferenceEquals(tray.Posture, to);
        }

        [UnityTest]
        public IEnumerator 盤の座標変換が往復する()
        {
            yield return null;

            for (var i = 0; i < Core.Board.CellCount; i++)
            {
                // CellSize は基準（1080 幅）での長さなので、ワールド座標に足してはいけない。
                // カンバスの縮尺がかかっているぶん、セル何個分もずれてしまう。
                var cell = Core.Board.CellAt(i);
                var center = Board.CellCenterWorld(cell);

                Assert.IsTrue(Board.TryWorldToCell(center, out var found), cell.ToString());
                Assert.AreEqual(cell, found);
            }
        }

        [Test]
        public void 盤の外は範囲外として扱われる()
        {
            // 隣のセルとの差でワールド座標での 1 セル分を測り、盤の外へ 1 つずらす。
            var step = Board.CellCornerWorld(new Cell(1, 1, 0)) - Board.CellCornerWorld(new Cell(0, 0, 0));
            var outside = Board.CellCornerWorld(new Cell(0, 0, 0)) - step;
            Assert.IsFalse(Board.TryWorldToCell(outside, out _));
        }

        [Test]
        public void 左右の格子の間の隙間は盤の外()
        {
            // 左の格子の右端と、右の格子の左端のちょうど中ほど。
            var leftEdge = Board.CellCornerWorld(new Cell(2, Core.Board.Cols - 1, 0))
                           + (Board.CellCornerWorld(new Cell(2, 1, 0)) - Board.CellCornerWorld(new Cell(2, 0, 0)));
            var rightEdge = Board.CellCornerWorld(new Cell(2, 0, 1));
            Assert.Greater(rightEdge.x, leftEdge.x, "右の格子は左の格子より右にある");

            var gap = (leftEdge + rightEdge) * 0.5f;
            gap.y += (Board.CellCornerWorld(new Cell(3, 0, 0)) - Board.CellCornerWorld(new Cell(2, 0, 0))).y * 0.5f;   // 行の中ほどへ
            Assert.IsFalse(Board.TryWorldToCell(gap, out var cell), "隙間なのに " + cell + " とみなされた");
        }
    }
}
