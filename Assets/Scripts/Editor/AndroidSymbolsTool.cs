using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// Android のデバッグシンボルを、Google Play が受け取れる形で出す。
    ///
    /// Play Console は、落ちたときの記録を読める形にするためにネイティブの
    /// デバッグシンボルを求める（「② デバッグシンボルがアップロードされていません」）。
    /// これを出していないと、IL2CPP で作った .so の中身が記号のない住所の列になり、
    /// どこで落ちたのかが判らない。
    ///
    /// 設定は二か所にある。
    ///
    ///   ・全体の設定（Player Settings ▸ Publishing Settings）
    ///     Library に入るので、Git には乗らない。機種を移すと消える。
    ///   ・ビルドプロファイル（Assets/Settings/Build Profiles）
    ///     こちらは Git に乗る。プロファイルを選んで作ると、こちらが勝つ。
    ///
    /// 実際に作るときはプロファイルを選ぶので、そちらを直さないと効かない。
    /// 現に、全体の設定だけを入れても Play Console は「シンボルがありません」
    /// と言い続けた。両方を揃える。
    ///
    /// 呼び出しは反射で行う。この設定は Android のビルドサポートが持っている
    /// ので、それが入っていない環境（この作りでは Mac 側）では型そのものが
    /// 無く、直に書くと組み立てが通らない。
    /// </summary>
    public static class AndroidSymbolsTool
    {
        private const string SettingsType = "UnityEditor.Android.UserBuildSettings";

        /// <summary>
        /// 記号の細かさ。SymbolTable は関数名まで、Full は行番号まで入る。
        /// Full は .so が大きくなるが、送るのは symbols.zip だけなので
        /// アプリの大きさには響かない。落ちた場所を行で知りたいので Full にする。
        /// </summary>
        private const string WantedLevel = "Full";

        /// <summary>
        /// 束ね方。Google Play が受け取るのは zip。
        /// Legacy は古い形で、いまの Play Console では弾かれる。
        /// </summary>
        private const string WantedFormat = "Zip";

        [MenuItem("Pentomino/Android のデバッグシンボルを出す")]
        public static void Enable()
        {
            var debugSymbols = FindDebugSymbols(out var problem);
            if (debugSymbols == null)
            {
                Debug.LogError("Android のデバッグシンボルの設定が見つかりません。\n" + problem);
                return;
            }

            var report = new StringBuilder("Android のデバッグシンボルを出すようにしました。\n\n");

            report.AppendLine("全体の設定（Library。Git には乗りません）");
            if (!Set(debugSymbols, "level", WantedLevel, report)) return;
            if (!Set(debugSymbols, "format", WantedFormat, report)) return;

            report.AppendLine();
            report.AppendLine("ビルドプロファイル（Git に乗ります）");
            if (!SetInProfiles(debugSymbols, report)) return;

            AssetDatabase.SaveAssets();

            report.AppendLine();
            report.AppendLine("ビルドすると、出来上がりの隣に symbols.zip が並びます。");
            report.AppendLine("Play Console の「アプリバンドルエクスプローラ」から、それを上げてください。");

            Debug.Log(report.ToString());
        }

        /// <summary>
        /// ビルドプロファイルの中の同じ設定を揃える。
        ///
        /// プロファイルは数字で持っているので、名前と数字の対応は
        /// 全体の設定の列挙型から取る。数字を直に書くと、Unity が
        /// 値を振り直したときに黙って別の意味になる。
        /// </summary>
        private static bool SetInProfiles(Type debugSymbols, StringBuilder report)
        {
            var level = NumberOf(debugSymbols, "level", WantedLevel);
            var format = NumberOf(debugSymbols, "format", WantedFormat);
            if (level == null || format == null) return false;

            var found = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:BuildProfile"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null) continue;

                var serialized = new SerializedObject(asset);

                // Android のプロファイルだけがこの二つを持っている。
                // 見つからなければ、ほかの機種のプロファイル。
                var changed = Assign(serialized, "m_DebugSymbolLevel", level.Value)
                              + Assign(serialized, "m_DebugSymbolFormat", format.Value);

                if (changed == 0) continue;

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);

                found++;
                report.AppendLine("  " + System.IO.Path.GetFileName(path)
                                  + " … level=" + WantedLevel + " format=" + WantedFormat);
            }

            if (found == 0)
                report.AppendLine("  （Android のビルドプロファイルが見つかりません）");

            return true;
        }

        /// <summary>列挙値の数字。プロファイルは数字で持っている。</summary>
        private static int? NumberOf(Type debugSymbols, string name, string wanted)
        {
            var property = debugSymbols.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            if (property == null || !Enum.IsDefined(property.PropertyType, wanted)) return null;

            return Convert.ToInt32(Enum.Parse(property.PropertyType, wanted));
        }

        /// <summary>名前で探して数字を入れる。入れ子の中まで辿る。</summary>
        private static int Assign(SerializedObject serialized, string name, int value)
        {
            var found = 0;
            var walk = serialized.GetIterator();

            while (walk.Next(true))
            {
                if (walk.name != name) continue;

                walk.intValue = value;
                found++;
            }

            return found;
        }

        /// <summary>いまの設定を読むだけ。ビルド前の確かめに使う。</summary>
        [MenuItem("Pentomino/Android のデバッグシンボルを確かめる")]
        public static void Show()
        {
            var debugSymbols = FindDebugSymbols(out var problem);
            if (debugSymbols == null)
            {
                Debug.LogError("Android のデバッグシンボルの設定が見つかりません。\n" + problem);
                return;
            }

            var report = new StringBuilder("Android のデバッグシンボル\n\n");
            foreach (var name in new[] { "level", "format" })
            {
                var property = debugSymbols.GetProperty(name,
                    BindingFlags.Public | BindingFlags.Static);

                if (property == null)
                {
                    report.AppendLine("  " + name + ": （見つかりません）");
                    continue;
                }

                report.AppendLine("  " + name + ": " + property.GetValue(null));

                // 数字で保存されるので、名前との対応を出しておく。
                // ビルドプロファイルの中身を読むときに要る。
                foreach (var choice in Enum.GetValues(property.PropertyType))
                    report.AppendLine("      " + Convert.ToInt32(choice) + " = " + choice);
            }

            Debug.Log(report.ToString());
        }

        /// <summary>
        /// UnityEditor.Android.UserBuildSettings.DebugSymbols を探す。
        /// Android のビルドサポートが入っていなければ見つからない。
        /// </summary>
        private static Type FindDebugSymbols(out string problem)
        {
            problem = null;

            var owner = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => SafeGetType(a, SettingsType))
                .FirstOrDefault(t => t != null);

            if (owner == null)
            {
                problem = SettingsType + " が読み込まれていません。"
                          + "\nAndroid のビルドサポートが入っているか確かめてください。";
                return null;
            }

            var nested = owner.GetNestedType("DebugSymbols", BindingFlags.Public | BindingFlags.Static);
            if (nested == null)
            {
                problem = owner.FullName + " の中に DebugSymbols がありません。"
                          + "\n中にあるもの: "
                          + string.Join(" ", owner.GetNestedTypes().Select(t => t.Name));
            }

            return nested;
        }

        private static Type SafeGetType(Assembly assembly, string name)
        {
            try
            {
                return assembly.GetType(name);
            }
            catch (Exception)
            {
                // 読めない組み立てが混じっていても、探索そのものは続ける。
                return null;
            }
        }

        /// <summary>名前で選ぶ列挙値を、反射で入れる。</summary>
        private static bool Set(Type debugSymbols, string name, string wanted, StringBuilder report)
        {
            var property = debugSymbols.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            if (property == null)
            {
                Debug.LogError(debugSymbols.FullName + " に " + name + " がありません。"
                               + "\nあるもの: " + string.Join(" ", debugSymbols
                                   .GetProperties(BindingFlags.Public | BindingFlags.Static)
                                   .Select(p => p.Name)));
                return false;
            }

            if (!Enum.IsDefined(property.PropertyType, wanted))
            {
                Debug.LogError(property.PropertyType.FullName + " に " + wanted + " がありません。"
                               + "\nあるもの: " + string.Join(" ", Enum.GetNames(property.PropertyType)));
                return false;
            }

            var before = property.GetValue(null);
            var value = Enum.Parse(property.PropertyType, wanted);
            property.SetValue(null, value);

            report.AppendLine("  " + name + ": " + before + " → " + property.GetValue(null));
            return true;
        }
    }
}
