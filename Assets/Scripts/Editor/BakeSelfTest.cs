using System.Text;
using Pentomino.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// 焼き込み（RuntimeBaker）が正しく働くかを、その場で確かめる。
    ///
    /// 本物のプレハブには触らない。複製を作り、そこへ書き戻して、
    /// 期待どおりの値が入ったかを Console に出す。終われば複製は消す。
    ///
    /// 「移すべき値」と「移してはいけない値」の両方を見る。
    /// 文字の中身や帯の縮尺まで持ち帰ってしまうと、次に動かしたときに
    /// 固まった値が焼き付くので、そこを取り違えていないかが肝心。
    /// </summary>
    public static class BakeSelfTest
    {
        private const string Source = "Assets/Resources/UI/HeaderBar.prefab";
        private const string Copy = "Assets/Resources/UI/_BakeSelfTest.prefab";

        [MenuItem("Pentomino/焼き込みの自己診断")]
        public static void Run()
        {
            AssetDatabase.DeleteAsset(Copy);
            if (!AssetDatabase.CopyAsset(Source, Copy))
            {
                Debug.LogError("自己診断 複製を作れません");
                return;
            }
            AssetDatabase.Refresh();

            // 実行時の姿を真似る。プレハブを出して、値をいじる。
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            var live = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(live, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);

            var check = Find(live.transform, "Check");
            var number = Find(live.transform, "Number");
            if (check == null || number == null)
            {
                Debug.LogError("自己診断 Check か Number が見つかりません");
                return;
            }

            var beforeSize = check.sizeDelta;
            var beforeFont = number.GetComponent<Text>().fontSize;
            var beforeText = number.GetComponent<Text>().text;

            // 「Play 中にいじった」に相当する変更。
            check.sizeDelta = new Vector2(beforeSize.x, 80f);
            check.anchoredPosition = new Vector2(check.anchoredPosition.x, -111f);
            number.GetComponent<Text>().fontSize = beforeFont + 7;
            number.GetComponent<Text>().text = "9999";   // これは焼き込まれてはいけない

            // 帯の縮尺も実行時に付く。これも焼き込まれてはいけない。
            live.transform.localScale = new Vector3(0.88f, 0.88f, 1f);

            var report = new StringBuilder();
            var changed = RuntimeBaker.BakeOne(live, Copy, report);
            Debug.Log("自己診断 書き戻し " + changed + " か所\n" + report);

            AssetDatabase.Refresh();

            // 複製を読み直して確かめる。
            var baked = AssetDatabase.LoadAssetAtPath<GameObject>(Copy);
            var bakedCheck = Find(baked.transform, "Check");
            var bakedNumber = Find(baked.transform, "Number");

            Debug.Log(string.Format(
                "自己診断 高さ     {0} -> {1}   （80 になっていれば成功）", beforeSize.y, bakedCheck.sizeDelta.y));
            Debug.Log(string.Format(
                "自己診断 位置     -> {0}       （-111 になっていれば成功）", bakedCheck.anchoredPosition.y));
            Debug.Log(string.Format(
                "自己診断 文字大   {0} -> {1}   （{2} になっていれば成功）",
                beforeFont, bakedNumber.GetComponent<Text>().fontSize, beforeFont + 7));
            Debug.Log(string.Format(
                "自己診断 文字中身 -> \"{0}\"    （\"{1}\" のままなら成功）",
                bakedNumber.GetComponent<Text>().text, beforeText));
            Debug.Log(string.Format(
                "自己診断 根の縮尺 -> {0}       （1 のままなら成功）", baked.transform.localScale.x));

            var header = baked.GetComponent<HeaderBar>();
            var missing = "HeaderBar が付いていません";
            var wired = header != null && header.Validate(out missing);

            Debug.Log("自己診断 参照欄 " + (wired ? "全部つながっている" : "★ 欠け: " + missing));

            Object.DestroyImmediate(live);
            AssetDatabase.DeleteAsset(Copy);
            Debug.Log("自己診断 複製を片付けました");
        }

        private static RectTransform Find(Transform root, string name)
        {
            foreach (var r in root.GetComponentsInChildren<RectTransform>(true))
                if (r.name == name) return r;
            return null;
        }
    }
}
