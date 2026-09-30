using System.Collections.Generic;
using System.Linq;
using Pentomino.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// ヘッダに「畳む面」を足す。既存のプレハブへの追加であって、作り直しではない。
    ///
    /// 開発仕様「操作性の問題(1)」で、問題を切り替えて 2 秒経ったら
    /// タイトルと難易度を消し、4 つのボタンを説明／設定のすぐ下へ上げる。
    ///
    /// 部品をひとつずつ動かすと、位置の数値がコードに入り込んでしまう。
    /// そうではなく、タイトル以下をまとめてひとつの面にし、
    /// その面が窓（Pane）の中を上下するようにする。動かすのは面の位置ひとつだけで、
    /// 中の割り付けはプレハブで組んだままになる。
    ///
    ///   Pane      … 窓。RectMask2D で、はみ出した分は隠れる
    ///   Content   … 上下する面。タイトル以下をここへ入れる
    ///   Shown     … 表示中の面の位置（空の目印）
    ///   Folded    … 畳んだあとの面の位置（空の目印）
    ///
    /// 位置は Shown と Folded の 2 枚が持つので、Inspector で動かせば動きも変わる。
    /// コードに座標は書かない。
    ///
    /// 二度実行しても何も起きない（Pane が既にあれば、そのまま抜ける）。
    /// </summary>
    public static class HeaderFoldMigration
    {
        private const string PrefabPath = "Assets/Resources/UI/HeaderBar.prefab";

        /// <summary>窓の外に置いたままにする部品。ここは畳んでも動かない。</summary>
        private static readonly string[] StayPut = { "Pattern", "Manual", "Settings" };

        /// <summary>畳んだとき、窓の上端に来てほしい部品。ここから畳む量を出す。</summary>
        private static readonly string[] LiftTo = { "StepBack", "StepForward", "Check", "Hint" };

        [MenuItem("Pentomino/ヘッダに畳む面を足す")]
        public static void Run()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var report = Migrate(root);
                if (report == null) return;

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log(report);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string Migrate(GameObject root)
        {
            var rootRect = (RectTransform)root.transform;

            if (rootRect.Find("Pane") != null)
            {
                Debug.Log("ヘッダには既に畳む面があります。何もしませんでした。");
                return null;
            }

            // 直下の子を、動かさない組と畳む組に分ける。
            var stay = new List<RectTransform>();
            var fold = new List<RectTransform>();
            foreach (RectTransform child in rootRect)
            {
                if (StayPut.Contains(child.name)) stay.Add(child);
                else fold.Add(child);
            }

            if (fold.Count == 0)
            {
                Debug.LogError("畳む部品が見つかりません。ヘッダの構成が想定と違います。");
                return null;
            }

            // 窓の上端を決める。動かさない組の下端と、畳む組の上端の、ちょうど中間。
            // どちらにも余裕が出るので、文字の縁が切れることも、はみ出すこともない。
            var foldTop = fold.Max(c => Top(rootRect, c));
            var stayBottom = stay.Count > 0 ? stay.Min(c => Bottom(rootRect, c)) : foldTop;
            var paneTop = stayBottom > foldTop ? (stayBottom + foldTop) * 0.5f : foldTop;

            // 畳む量。指定の部品の上端が、窓の上端に来るまで。
            var liftTargets = fold.Where(c => LiftTo.Contains(c.name)).ToArray();
            if (liftTargets.Length == 0)
            {
                Debug.LogError("畳んだときに上へ来る部品が見つかりません: " + string.Join(", ", LiftTo));
                return null;
            }
            var shift = paneTop - liftTargets.Max(c => Top(rootRect, c));

            // 窓。ヘッダの下端まで。
            var pane = UiFactory.CreateRect(rootRect, "Pane");
            pane.anchoredPosition = new Vector2(0f, paneTop);
            pane.sizeDelta = new Vector2(rootRect.rect.width, paneTop + rootRect.rect.height);
            pane.gameObject.AddComponent<RectMask2D>();

            // 上下する面。窓と同じ大きさ。
            var content = UiFactory.CreateRect(pane, "Content");
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = pane.sizeDelta;

            // 順番を保ったまま面へ移す。見た目の位置は変わらない。
            foreach (var child in fold) child.SetParent(content, true);

            var shown = UiFactory.CreateRect(pane, "Shown");
            shown.anchoredPosition = Vector2.zero;
            shown.sizeDelta = Vector2.zero;

            var folded = UiFactory.CreateRect(pane, "Folded");
            folded.anchoredPosition = new Vector2(0f, shift);
            folded.sizeDelta = Vector2.zero;

            // 窓はいちばん奥に。模様やボタンより先に描く。
            pane.SetAsFirstSibling();

            var header = root.GetComponent<HeaderBar>();
            if (header == null)
            {
                Debug.LogError("HeaderBar が付いていません。");
                return null;
            }
            header.AssignFold(content, shown, folded);
            EditorUtility.SetDirty(header);

            return "ヘッダに畳む面を足しました。\n"
                   + "  窓の上端     : " + (-paneTop).ToString("0.#") + "（ヘッダ上端から）\n"
                   + "  窓の高さ     : " + pane.rect.height.ToString("0.#") + "\n"
                   + "  畳む量       : " + shift.ToString("0.#") + "\n"
                   + "  面に入れた数 : " + fold.Count + " 個\n"
                   + "\n"
                   + "動きを変えたいときは、Pane の下の Shown と Folded を Inspector で動かしてください。\n"
                   + "コード側に位置の数値はありません。";
        }

        /// <summary>ヘッダから見た上端の y（下ほど小さい）。</summary>
        private static float Top(RectTransform root, RectTransform child) =>
            RectTransformUtility.CalculateRelativeRectTransformBounds(root, child).max.y;

        private static float Bottom(RectTransform root, RectTransform child) =>
            RectTransformUtility.CalculateRelativeRectTransformBounds(root, child).min.y;
    }
}
