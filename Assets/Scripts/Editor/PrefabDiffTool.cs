using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// プレハブに加えられた手直しを見つける。
    ///
    /// プレハブを保存し直すと Unity は中の fileID を振り直し、YAML の並び順も変える。
    /// そのため git の差分では、値が同じでも数百行動き、値が変わっても差分に埋もれる。
    /// fileID は「前回と同じ物か」を示さないので、変更の検出には使えない。
    ///
    /// そこで「いま生成したらこうなる」という姿を組み立て、既存のプレハブと
    /// プロパティの値そのものを突き合わせる。
    /// 出てきた違いが、エディタ上で手直しされた箇所にあたる。
    /// </summary>
    public static class PrefabDiffTool
    {
        /// <summary>比べても意味が無いプロパティ。</summary>
        private static readonly string[] Ignored =
        {
            "m_ObjectHideFlags",
            "m_CorrespondingSourceObject",
            "m_PrefabInstance",
            "m_PrefabAsset",
            "m_GameObject",
            "m_Script",
            "m_Father",
            "m_Children",
            "m_LocalEulerAnglesHint",
            "m_Component",

            // Text.OnValidate() が maxSize を文字サイズまで引き上げ、minSize を切り下げる。
            // 保存して読み直した側だけこれが効くので、組み立てただけの側と必ず食い違う。
            // 手直しではなく保存処理の副作用なので、比べても意味が無い。
            // どちらも Best Fit を使うときだけ効く値で、この画面では使っていない。
            "m_FontData.m_MaxSize",
            "m_FontData.m_MinSize",
        };

        [MenuItem("Pentomino/プレハブの手直しを調べる")]
        public static void Report()
        {
            var report = new StringBuilder();
            report.AppendLine("プレハブの手直し調べ");
            report.AppendLine("「生成」= いま生成ツールを走らせたときの値");
            report.AppendLine("「現在」= プレハブに入っている値");
            report.AppendLine();

            var differences = 0;

            var built = UiPrefabBuilder.BuildForComparison();
            try
            {
                foreach (var pair in built)
                {
                    var path = pair.Key;
                    var name = System.IO.Path.GetFileName(path);

                    if (!System.IO.File.Exists(path))
                    {
                        report.AppendLine("  ― " + name + "  … まだ作られていません");
                        continue;
                    }

                    var current = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var lines = new List<string>();
                        Compare(pair.Value, current, string.Empty, lines);

                        if (lines.Count == 0)
                        {
                            report.AppendLine("  ○ " + name + "  … 生成した姿のままです");
                            continue;
                        }

                        report.AppendLine("  ● " + name);
                        foreach (var line in lines) report.AppendLine("      " + line);
                        differences += lines.Count;
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(current);
                    }
                }
            }
            finally
            {
                foreach (var go in built.Values)
                    if (go != null) Object.DestroyImmediate(go);
            }

            report.AppendLine();
            if (differences == 0)
            {
                report.AppendLine("手直しは見つかりませんでした。作り直しても失われる物はありません。");
                Debug.Log(report.ToString());
                return;
            }

            report.AppendLine("違いが " + differences + " 件あります。");
            report.AppendLine("作り直すとこれらは失われます。");
            report.AppendLine("残したい値は UiPrefabBuilder.cs の定数に取り込んでください。");
            Debug.LogWarning(report.ToString());
        }

        /// <summary>2 つの枝を、名前をたどりながら比べる。</summary>
        private static void Compare(GameObject expected, GameObject actual, string path, List<string> lines)
        {
            var here = string.IsNullOrEmpty(path) ? actual.name : path;

            CompareComponents(expected, actual, here, lines);

            // 子は名前で対応づける。同じ名前が並ぶときは出てきた順に組にする。
            var expectedChildren = ChildrenByName(expected.transform);
            var actualChildren = ChildrenByName(actual.transform);

            foreach (var pair in expectedChildren)
            {
                if (!actualChildren.TryGetValue(pair.Key, out var mates))
                {
                    lines.Add(here + "/" + pair.Key + "  生成にはあるが、現在は無い");
                    continue;
                }

                for (var i = 0; i < pair.Value.Count; i++)
                {
                    if (i >= mates.Count)
                    {
                        lines.Add(here + "/" + pair.Key + "  生成のほうが " + (pair.Value.Count - mates.Count) + " 個多い");
                        break;
                    }
                    Compare(pair.Value[i].gameObject, mates[i].gameObject, here + "/" + pair.Key, lines);
                }
            }

            foreach (var pair in actualChildren)
            {
                if (!expectedChildren.ContainsKey(pair.Key))
                    lines.Add(here + "/" + pair.Key + "  現在にだけある（手で足した物）");
            }
        }

        private static void CompareComponents(GameObject expected, GameObject actual, string path,
            List<string> lines)
        {
            var expectedComponents = expected.GetComponents<Component>();
            var actualComponents = actual.GetComponents<Component>();

            foreach (var component in expectedComponents)
            {
                if (component == null) continue;

                var mate = Find(actualComponents, component.GetType());
                if (mate == null)
                {
                    lines.Add(path + "  " + component.GetType().Name + " が現在の側に無い");
                    continue;
                }

                CompareProperties(component, mate, path, lines);
            }
        }

        private static Component Find(Component[] components, System.Type type)
        {
            foreach (var component in components)
                if (component != null && component.GetType() == type) return component;
            return null;
        }

        private static void CompareProperties(Component expected, Component actual, string path,
            List<string> lines)
        {
            var a = new SerializedObject(expected);
            var b = new SerializedObject(actual);

            var pa = a.GetIterator();
            var pb = b.GetIterator();

            var typeName = expected.GetType().Name;

            var moreA = pa.NextVisible(true);
            var moreB = pb.NextVisible(true);

            while (moreA && moreB)
            {
                if (pa.propertyPath != pb.propertyPath) break;

                if (!IsIgnored(pa.propertyPath) && Differs(pa, pb))
                {
                    lines.Add(string.Format("{0}  {1}.{2}   生成 {3} → 現在 {4}",
                        path, typeName, pa.propertyPath, Describe(pa), Describe(pb)));
                }

                // 入れ子の中へも降りる。m_FontData の中の文字サイズなどはここでしか見えない。
                moreA = pa.NextVisible(true);
                moreB = pb.NextVisible(true);
            }
        }

        /// <summary>
        /// この項目に違いがあるか。
        ///
        /// 入れ子になった項目（m_FontData や配列）は丸ごと比べない。
        /// 中にオブジェクト参照を含むため、実際には同じでも違うと出てしまう。
        /// 中の一つ一つは、このあとの繰り返しで別々に比べられる。
        ///
        /// 参照は、指している資産の名前で比べる。
        /// フォントや絵を差し替えたのは残すべき手直しなので、見つけたい。
        /// </summary>
        private static bool Differs(SerializedProperty a, SerializedProperty b)
        {
            if (a.propertyType == SerializedPropertyType.Generic) return false;

            if (a.propertyType == SerializedPropertyType.ObjectReference)
                return NameOf(a.objectReferenceValue) != NameOf(b.objectReferenceValue);

            return !SerializedProperty.DataEquals(a, b);
        }

        private static string NameOf(Object asset) => asset == null ? "なし" : asset.name;

        private static bool IsIgnored(string propertyPath)
        {
            foreach (var ignored in Ignored)
                if (propertyPath.StartsWith(ignored)) return true;
            return false;
        }

        /// <summary>値を読める形にする。</summary>
        private static string Describe(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer: return property.intValue.ToString();
                case SerializedPropertyType.Boolean: return property.boolValue.ToString();
                case SerializedPropertyType.Float: return property.floatValue.ToString("0.##");
                case SerializedPropertyType.String: return "\"" + property.stringValue + "\"";
                case SerializedPropertyType.Color: return property.colorValue.ToString();
                case SerializedPropertyType.Enum: return property.enumValueIndex.ToString();
                case SerializedPropertyType.Vector2: return property.vector2Value.ToString();
                case SerializedPropertyType.Vector3: return property.vector3Value.ToString();
                case SerializedPropertyType.Vector4: return property.vector4Value.ToString();
                case SerializedPropertyType.Rect: return property.rectValue.ToString();
                case SerializedPropertyType.ArraySize: return property.intValue + " 個";
                case SerializedPropertyType.ObjectReference:
                    return NameOf(property.objectReferenceValue);
                default: return "(" + property.propertyType + ")";
            }
        }

        private static Dictionary<string, List<Transform>> ChildrenByName(Transform parent)
        {
            var map = new Dictionary<string, List<Transform>>();
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (!map.TryGetValue(child.name, out var list))
                {
                    list = new List<Transform>();
                    map[child.name] = list;
                }
                list.Add(child);
            }
            return map;
        }
    }
}
