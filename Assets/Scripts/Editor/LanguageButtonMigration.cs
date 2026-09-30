using System.Collections.Generic;
using Pentomino.Core;
using Pentomino.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// 言語を増やしたときに、設定画面のプレハブへボタンを足す。
    ///
    /// プレハブを作り直すと、エディタ上で加えた位置や大きさの手直しが消えてしまう。
    /// そこで既にあるプレハブを開いて、足りないボタンだけを差し込む。
    /// 並べ方（マス目の位置）は今あるボタンから読み取るので、手直しはそのまま残る。
    /// </summary>
    public static class LanguageButtonMigration
    {
        [MenuItem("Pentomino/設定画面に足りない言語ボタンを足す")]
        public static void AddMissingLanguageButtons()
        {
            var path = UiPrefabBuilder.SettingsPath;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError("設定画面のプレハブが見つかりません: " + path);
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!Migrate(root, out var report))
                {
                    Debug.Log(report);
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                AssetDatabase.SaveAssets();
                Debug.Log(report);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool Migrate(GameObject root, out string report)
        {
            report = null;

            var panel = root.GetComponent<SettingsPanel>();
            if (panel == null)
            {
                report = "SettingsPanel が付いていません。";
                return false;
            }

            // 今あるボタンを名前で引けるようにする。
            var existing = new Dictionary<string, RectTransform>();
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (!button.name.StartsWith("Lang")) continue;
                existing[button.name] = (RectTransform)button.transform;
            }

            if (existing.Count == 0)
            {
                report = "言語ボタンが 1 つも見つかりません。";
                return false;
            }

            // マス目の位置は、今あるボタンの並びから読み取る。手直しした位置がそのまま活きる。
            var slots = ReadSlots(existing);
            if (slots.Count == 0)
            {
                report = "ボタンの並びを読み取れませんでした。";
                return false;
            }

            var added = new List<string>();
            var ordered = new List<Button>();

            for (var i = 0; i < Languages.All.Length; i++)
            {
                var language = Languages.All[i];
                var name = "Lang" + language;

                if (!existing.TryGetValue(name, out var rect))
                {
                    rect = Duplicate(existing, name, language);
                    if (rect == null)
                    {
                        report = name + " を作れませんでした。";
                        return false;
                    }
                    added.Add(name);
                }

                // 並び順が変わっても、i 番目のマスに収まるようにする。
                rect.anchoredPosition = SlotAt(slots, i);
                ordered.Add(rect.GetComponent<Button>());
            }

            if (added.Count == 0)
            {
                report = "足りない言語ボタンはありません。";
                return false;
            }

            // SettingsPanel が持つ配列を、Languages.All と同じ順に差し替える。
            var serialized = new SerializedObject(panel);
            var array = serialized.FindProperty("_languageButtons");
            array.arraySize = ordered.Count;
            for (var i = 0; i < ordered.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = ordered[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            report = "言語ボタンを足しました: " + string.Join(" ", added.ToArray())
                     + "\n並び: " + Languages.All.Length + " 言語";
            return true;
        }

        /// <summary>今あるボタンの位置を、左上から順にマス目として拾う。</summary>
        private static List<Vector2> ReadSlots(Dictionary<string, RectTransform> existing)
        {
            var positions = new List<Vector2>();
            foreach (var rect in existing.Values) positions.Add(rect.anchoredPosition);

            // 上から下へ、同じ高さなら左から右へ。
            positions.Sort((a, b) =>
            {
                if (!Mathf.Approximately(a.y, b.y)) return b.y.CompareTo(a.y);
                return a.x.CompareTo(b.x);
            });
            return positions;
        }

        /// <summary>
        /// i 番目のマスの位置。今あるマスで足りなければ、2 列の並びとして次の位置を作る。
        /// </summary>
        private static Vector2 SlotAt(List<Vector2> slots, int index)
        {
            if (index < slots.Count) return slots[index];

            // 1 段目の 2 つから、列の間隔と段の高さを割り出す。
            var columnGap = slots.Count > 1 ? slots[1].x - slots[0].x : 0f;
            var rowGap = slots.Count > 2 ? slots[2].y - slots[0].y : 0f;

            var column = index % 2;
            var row = index / 2;
            return new Vector2(slots[0].x + column * columnGap, slots[0].y + row * rowGap);
        }

        /// <summary>今あるボタンを複製して、新しい言語のボタンを作る。</summary>
        private static RectTransform Duplicate(Dictionary<string, RectTransform> existing,
            string name, Language language)
        {
            RectTransform sample = null;
            foreach (var rect in existing.Values) { sample = rect; break; }
            if (sample == null) return null;

            var copy = Object.Instantiate(sample.gameObject, sample.parent);
            copy.name = name;

            var label = copy.GetComponentInChildren<Text>(true);
            if (label != null) label.text = language.NativeName();

            return (RectTransform)copy.transform;
        }
    }
}
