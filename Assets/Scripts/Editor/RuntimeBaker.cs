using System.Collections.Generic;
using System.Text;
using Pentomino.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// Play 中に整えた見た目を、そのままプレハブへ書き戻す。
    ///
    /// これまでは Play で数値を決め、覚えて、Play を止め、プレハブに打ち直し、
    /// また Play で確かめる、という往復が要った。数字を写す手間と、
    /// 写し間違いの両方が入り込む。
    ///
    /// Play 中の変更が捨てられるのは Unity の仕組みだが、捨てられる前に
    /// 読み取ってプレハブへ移せばよい。Play 中に Hierarchy でいじり、
    /// この項目を一度押す。それだけで残る。
    ///
    /// 移すのは「見た目を決めている値」だけ。
    /// 実行時に決まる物（文字の中身、言語ごとの書体、帯の縮尺、画面上の位置）は
    /// 移さない。移すと、次に動かしたとき固まった値が焼き付いてしまう。
    ///
    /// とくに、機種の横幅に合わせて毎回決め直している所には触らない。
    /// ここを焼き込むと、測った機種の値がプレハブに残り、
    /// 「幅に合わせて取り直す」という仕掛けそのものが読めなくなる。
    /// 実行時に上書きされるので動きは変わらないが、プレハブを見た人には
    /// その数字が設計値に見えてしまう。触らない先は二種類:
    ///
    ///   ・並べ物（LayoutGroup / ContentSizeFitter / ScrollRect）が毎コマ決める枠
    ///   ・子画面と帯の根。GameScreen が画面の柱に合わせて決め直す
    ///
    /// 飛ばした所は報告に出す。境目が判るように。
    /// </summary>
    public static class RuntimeBaker
    {
        /// <summary>
        /// 生きている部品と、その元になったプレハブ。
        ///
        /// RootIsDesign は「根の大きさがプレハブで決めた値か」。
        /// 成績のパネルだけは、決めた大きさを縮尺で収めるので設計値。
        /// ほかは GameScreen が画面の柱に合わせて毎回決め直すので、持ち帰らない。
        /// </summary>
        private static readonly (System.Type Type, string Path, bool RootIsDesign)[] Parts =
        {
            (typeof(HeaderBar), UiPrefabBuilder.HeaderPath, false),
            (typeof(SettingsPanel), UiPrefabBuilder.SettingsPath, false),
            (typeof(ManualPanel), UiPrefabBuilder.ManualPath, false),
            (typeof(ResultPanel), UiPrefabBuilder.ResultPath, true),
            (typeof(FlickTuningPanel), UiPrefabBuilder.FlickTuningPath, false),
        };

        [MenuItem("Pentomino/いまの画面をプレハブに焼き込む %#h")]
        public static void Bake()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("焼き込み",
                    "Play 中に使う道具です。\n\n"
                    + "Play を始めて、Hierarchy で見た目を整えてから、\n"
                    + "もう一度この項目を選んでください（Ctrl+Shift+H）。",
                    "分かりました");
                return;
            }

            var screen = Object.FindFirstObjectByType<GameScreen>(FindObjectsInactive.Include);
            if (screen == null)
            {
                Debug.LogWarning("画面（GameScreen）が見つかりません。焼き込みませんでした。");
                return;
            }

            var report = new StringBuilder();
            var baked = 0;

            foreach (var part in Parts)
            {
                // 画面の下から探す。隠れている設定画面なども拾えるよう、無効な物も含める。
                var live = screen.GetComponentInChildren(part.Type, true);

                if (live == null)
                {
                    report.AppendLine("  " + part.Type.Name + " … 画面に出ていないので、そのまま");
                    continue;
                }

                if (BakeOne(live.gameObject, part.Path, report, part.RootIsDesign) > 0) baked++;
            }

            AssetDatabase.SaveAssets();

            report.Insert(0, baked == 0
                ? "焼き込むものはありませんでした。\n\n"
                : baked + " 件のプレハブに書き戻しました。\nPlay を止めても残ります。\n\n");

            Debug.Log(report.ToString());
        }

        /// <summary>生きている部品ひとつを、元のプレハブへ書き戻す。</summary>
        internal static int BakeOne(GameObject live, string path, StringBuilder report,
            bool rootIsDesign = true)
        {
            var asset = PrefabUtility.LoadPrefabContents(path);
            if (asset == null)
            {
                report.AppendLine("  " + path + " … 開けません");
                return 0;
            }

            try
            {
                var changed = 0;
                var missing = new List<string>();
                var skipped = new List<string>();

                // 根は別扱い。位置と縮尺は画面に合わせて実行時に決まるので触らない。
                var liveRoot = (RectTransform)live.transform;
                var assetRoot = (RectTransform)asset.transform;

                if (rootIsDesign)
                {
                    // 大きさは設計の値なので、そのまま持ち帰る。
                    if (assetRoot.sizeDelta != liveRoot.sizeDelta)
                    {
                        assetRoot.sizeDelta = liveRoot.sizeDelta;
                        changed++;
                    }
                }
                else if (assetRoot.sizeDelta != liveRoot.sizeDelta)
                {
                    skipped.Add("（根）… 画面の柱に合わせて毎回決め直す大きさ");
                }

                foreach (Transform child in liveRoot)
                    changed += BakeBranch(child, assetRoot, string.Empty, missing, skipped);

                if (changed > 0)
                {
                    Backup(path);
                    PrefabUtility.SaveAsPrefabAsset(asset, path);
                }

                report.AppendLine("  " + System.IO.Path.GetFileName(path) + " … " + changed + " か所");
                foreach (var name in missing)
                    report.AppendLine("      ※ プレハブに無いので飛ばしました: " + name);
                foreach (var name in skipped)
                    report.AppendLine("      － 実行時に決まるので飛ばしました: " + name);

                return changed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(asset);
            }
        }

        /// <summary>
        /// 書き戻す前に、そのままの姿を Backup/UI/ へ控える。
        ///
        /// 手で整えたプレハブを上書きする道具なので、取り返しがつく形にしておく。
        /// Backup/ は Assets の外なので Unity は見に行かないし、Git にも入らない。
        /// </summary>
        private static void Backup(string path)
        {
            var folder = System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(), "Backup", "UI");
            System.IO.Directory.CreateDirectory(folder);

            var name = System.IO.Path.GetFileNameWithoutExtension(path)
                       + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".prefab";

            System.IO.File.Copy(path, System.IO.Path.Combine(folder, name), true);
        }

        /// <summary>名前をたどって、同じ場所の部品へ値を移す。</summary>
        private static int BakeBranch(Transform live, Transform assetParent, string path,
            List<string> missing, List<string> skipped)
        {
            var here = path + "/" + live.name;

            var mate = assetParent.Find(live.name);
            if (mate == null)
            {
                missing.Add(here);
                return 0;
            }

            // 枠を毎コマ決め直している所は持ち帰らない。見た目（色や書体）は持ち帰る。
            var driven = IsDriven(live);

            var changed = (driven ? 0 : CopyRect(live as RectTransform, mate as RectTransform))
                          + CopyText(live, mate)
                          + CopyImage(live, mate)
                          + CopyButton(live, mate);

            if (driven && !Same(live as RectTransform, mate as RectTransform))
                skipped.Add(here + " … 並べ物が決める枠");

            foreach (Transform child in live)
                changed += BakeBranch(child, mate, here, missing, skipped);

            return changed;
        }

        /// <summary>
        /// 枠を自分で決めていない部品か。
        ///
        /// 並べ物（VerticalLayoutGroup）の中身、中身に合わせて伸びる枠
        /// （ContentSizeFitter）、巻き取りの中身（ScrollRect）は、
        /// 実行時に毎コマ決め直される。持ち帰っても次の実行で上書きされるだけで、
        /// プレハブには意味のない長い小数が残る。
        /// </summary>
        private static bool IsDriven(Transform live)
        {
            if (live == null) return false;

            if (live.GetComponent<ContentSizeFitter>() != null) return true;
            if (live.GetComponent<LayoutGroup>() != null) return true;

            var parent = live.parent;
            if (parent != null && parent.GetComponent<LayoutGroup>() != null) return true;

            // 巻き取りの中身と窓。位置は指でも動く。
            var scroll = live.GetComponentInParent<ScrollRect>();
            if (scroll != null
                && (live == (Transform)scroll.content || live == (Transform)scroll.viewport))
                return true;

            return false;
        }

        private static bool Same(RectTransform a, RectTransform b)
        {
            if (a == null || b == null) return true;

            return a.sizeDelta == b.sizeDelta
                   && a.anchoredPosition == b.anchoredPosition
                   && a.anchorMin == b.anchorMin
                   && a.anchorMax == b.anchorMax;
        }

        private static int CopyRect(RectTransform live, RectTransform mate)
        {
            if (live == null || mate == null) return 0;

            var changed = 0;
            changed += Set(mate.anchorMin != live.anchorMin, () => mate.anchorMin = live.anchorMin);
            changed += Set(mate.anchorMax != live.anchorMax, () => mate.anchorMax = live.anchorMax);
            changed += Set(mate.pivot != live.pivot, () => mate.pivot = live.pivot);
            changed += Set(mate.sizeDelta != live.sizeDelta, () => mate.sizeDelta = live.sizeDelta);
            changed += Set(mate.localScale != live.localScale, () => mate.localScale = live.localScale);
            changed += Set(mate.localRotation != live.localRotation,
                () => mate.localRotation = live.localRotation);

            // 畳む面の位置は実行時に動く。焼き込むと、畳んだ姿がそのまま
            // 既定になってしまうので、ここだけは持ち帰らない。
            // 畳んだときの位置を変えたいときは、Pane の下の Folded を動かす。
            if (live.name != "Content")
            {
                changed += Set(mate.anchoredPosition != live.anchoredPosition,
                    () => mate.anchoredPosition = live.anchoredPosition);
            }

            return changed;
        }

        /// <summary>
        /// 文字の見た目。中身（text）と書体は持ち帰らない。
        /// 中身は問題番号や成績で毎回変わり、書体は言語で差し替わる。
        /// </summary>
        private static int CopyText(Transform live, Transform mate)
        {
            var a = live.GetComponent<Text>();
            var b = mate.GetComponent<Text>();
            if (a == null || b == null) return 0;

            var changed = 0;
            changed += Set(b.fontSize != a.fontSize, () => b.fontSize = a.fontSize);
            changed += Set(b.fontStyle != a.fontStyle, () => b.fontStyle = a.fontStyle);
            changed += Set(b.alignment != a.alignment, () => b.alignment = a.alignment);
            changed += Set(b.color != a.color, () => b.color = a.color);
            changed += Set(b.lineSpacing != a.lineSpacing, () => b.lineSpacing = a.lineSpacing);
            changed += Set(b.resizeTextForBestFit != a.resizeTextForBestFit,
                () => b.resizeTextForBestFit = a.resizeTextForBestFit);
            changed += Set(b.horizontalOverflow != a.horizontalOverflow,
                () => b.horizontalOverflow = a.horizontalOverflow);
            changed += Set(b.verticalOverflow != a.verticalOverflow,
                () => b.verticalOverflow = a.verticalOverflow);
            changed += Set(b.raycastTarget != a.raycastTarget, () => b.raycastTarget = a.raycastTarget);
            return changed;
        }

        private static int CopyImage(Transform live, Transform mate)
        {
            var a = live.GetComponent<Image>();
            var b = mate.GetComponent<Image>();
            if (a == null || b == null) return 0;

            var changed = 0;
            changed += Set(b.color != a.color, () => b.color = a.color);
            changed += Set(b.sprite != a.sprite, () => b.sprite = a.sprite);
            changed += Set(b.type != a.type, () => b.type = a.type);
            changed += Set(b.preserveAspect != a.preserveAspect,
                () => b.preserveAspect = a.preserveAspect);
            changed += Set(b.fillCenter != a.fillCenter, () => b.fillCenter = a.fillCenter);
            changed += Set(b.pixelsPerUnitMultiplier != a.pixelsPerUnitMultiplier,
                () => b.pixelsPerUnitMultiplier = a.pixelsPerUnitMultiplier);
            changed += Set(b.raycastTarget != a.raycastTarget, () => b.raycastTarget = a.raycastTarget);
            return changed;
        }

        /// <summary>ボタンの押した色・触れた色。</summary>
        private static int CopyButton(Transform live, Transform mate)
        {
            var a = live.GetComponent<Button>();
            var b = mate.GetComponent<Button>();
            if (a == null || b == null) return 0;

            var changed = 0;
            changed += Set(b.transition != a.transition, () => b.transition = a.transition);
            changed += Set(!b.colors.Equals(a.colors), () => b.colors = a.colors);
            return changed;
        }

        private static int Set(bool differs, System.Action apply)
        {
            if (!differs) return 0;

            apply();
            return 1;
        }
    }
}
