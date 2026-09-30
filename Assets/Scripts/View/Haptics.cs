using System;
using Pentomino.Core;
using UnityEngine;

namespace Pentomino.View
{
    /// <summary>
    /// 短い触覚。Android の振動子を、こちらで決めた長さだけ動かす。
    ///
    /// Unity の Handheld.Vibrate は長さを選べず、Android では 500ms の
    /// 「ブーッ」になる。ピースがはまった合図には長すぎるので、
    /// Android の API を直に呼んで詰める。
    ///
    /// 既定は「端末に任せる」。長さに -1 を渡したときの振る舞いで、
    ///
    ///   Android 10 以降  端末ごとに調律されたクリックの合図（EFFECT_CLICK）
    ///   それより前        昔ながらの長いブザー
    ///
    /// になる。EFFECT_CLICK はメーカーが自機の振動子に合わせて作った波形なので、
    /// 長さを数字で決め打ちするより自然に感じられる。
    ///
    /// 長さを 1 以上にすると、その長さで直に鳴らす。どこまで短くできるかは
    /// OS ではなく振動子の作りで決まる。
    ///
    ///   LRA（最近のスマホの大半）  立ち上がり 5〜10ms。10ms でも「コッ」と鳴る
    ///   ERM（偏心モーター）        錘を回すので 20〜50ms かかる。それ未満は無音
    ///
    /// 感じ方は端末ごとに大きく違うので、長さも強さも設定画面から変えられる
    /// ようにしてある。何を渡しても無音の端末があり（振動子の故障を含む）、
    /// こちらからは見分けられない。
    ///
    /// 振動子を積んでいない端末（Amazon の Fire タブレットなど）では
    /// 何も起きない。呼び出す側が気にしなくて済むよう、ここで黙って受け流す。
    ///
    /// ■ 頼む相手（Context）の探し方
    ///
    /// 振動子は Context ごしにもらう。その Context をどこから取るかが、
    /// Android の入口の作りで変わる。この遊びは入口に GameActivity を使って
    /// おり、昔からの UnityPlayer.currentActivity が空になることがある。
    /// 空のまま呼ぶと例外になり、「振動子が無い端末」と見分けが付かなくなる。
    /// AQUOS sense7 plus が音側に落ちていたのがこれだった。
    ///
    /// 取り口を順に試し、どれで取れたかを <see cref="Note"/> に残す。
    /// 実機で adb logcat から読めるよう、起動時に一度書き出している。
    ///
    /// ■ 権限
    ///
    /// 震わせるには AndroidManifest.xml に VIBRATE が要る。振動子を探す
    /// ところは権限が無くても通ってしまい、vibrate() で初めて弾かれるので、
    /// 「探せたのに震えない」という判りにくい形で出る。権限は
    /// AndroidBuildPostProcess が書き出しのたびに入れている。
    /// </summary>
    public sealed class Haptics : IDisposable
    {
        /// <summary>VibrationEffect.DEFAULT_AMPLITUDE。端末の既定の強さ。</summary>
        private const int DefaultAmplitude = -1;

        /// <summary>VibrationEffect.EFFECT_CLICK。</summary>
        private const int EffectClick = 0;

        /// <summary>VibrationEffect が入った版（Android 8.0）。</summary>
        private const int OreoSdk = 26;

        /// <summary>createPredefined が入った版（Android 10）。</summary>
        private const int QuinceTartSdk = 29;

        /// <summary>
        /// 端末に任せたとき、Android 10 より前で鳴らす長さ（ミリ秒）。
        /// クリックの合図が無い版なので、昔ながらのブザーで代える。
        /// </summary>
        private const int BuzzerMilliseconds = 500;

        /// <summary>VibratorManager 経由になった版（Android 12）。</summary>
        private const int SnowConeSdk = 31;

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _vibrator;
        private AndroidJavaClass _effects;
        private int _sdk;
#endif

        /// <summary>この端末に振動子があるか。</summary>
        public bool Available { get; private set; }

        /// <summary>
        /// どう判断したかの覚え書き。設定画面や記録に出す。
        /// 振動しないときに、機種の話かこちらの落ち度かを分けるために要る。
        /// </summary>
        public string Note { get; private set; } = "この機種では振動を使いません";

        public Haptics()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    _sdk = version.GetStatic<int>("SDK_INT");

                if (_sdk < OreoSdk)
                {
                    Note = "Android 8.0 より前なので、長さを指定できません";
                    return;
                }

                var through = string.Empty;

                using (var context = FindContext(ref through))
                {
                    if (context == null)
                    {
                        Note = "振動を頼む相手が見つかりません（SDK " + _sdk + "）";
                        return;
                    }

                    // Android 12 から、振動子は管理役ごしに取る。
                    if (_sdk >= SnowConeSdk)
                    {
                        using (var manager = context.Call<AndroidJavaObject>(
                                   "getSystemService", "vibrator_manager"))
                        {
                            if (manager != null)
                                _vibrator = manager.Call<AndroidJavaObject>("getDefaultVibrator");
                        }
                    }

                    // 管理役から取れなければ、昔ながらの取り方も試す。
                    // Android 12 以降でも、こちらは残されている。
                    if (_vibrator == null)
                        _vibrator = context.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }

                if (_vibrator == null)
                {
                    Note = "振動子を取り出せませんでした（" + through + "・SDK " + _sdk + "）";
                    return;
                }

                if (!_vibrator.Call<bool>("hasVibrator"))
                {
                    Note = "この機種に振動子がありません（" + through + "・SDK " + _sdk + "）";
                    _vibrator.Dispose();
                    _vibrator = null;
                    return;
                }

                _effects = new AndroidJavaClass("android.os.VibrationEffect");
                Available = true;
                Note = "振動できます（" + through + "・SDK " + _sdk + "）";
            }
            catch (Exception e)
            {
                // 触覚が無いだけで遊べなくなるのは割に合わない。黙って諦める。
                Note = "振動を用意できませんでした: " + e.Message;
                Available = false;
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// 振動子を頼む相手（Context）を探す。取れた道を <paramref name="through"/> に残す。
        ///
        /// 入口が Activity なら 1 番で取れる。GameActivity では 1 番が空になる
        /// ことがあるので、2 番・3 番へ落ちる。振動子は Activity でなくても
        /// もらえるので、アプリ本体（Application）でも用は足りる。
        /// </summary>
        private static AndroidJavaObject FindContext(ref string through)
        {
            // 1. 昔からの道。入口が Activity のときはこれで取れる。
            var found = Static("com.unity3d.player.UnityPlayer", "currentActivity");
            if (found != null)
            {
                through = "currentActivity";
                return found;
            }

            // 2. 入口が GameActivity のとき。Unity 6 でこちらに変わった。
            found = Static("com.unity3d.player.UnityPlayerGameActivity", "currentActivity");
            if (found != null)
            {
                through = "gameActivity";
                return found;
            }

            // 3. 画面がどうであれ、アプリ本体は必ずある。最後の頼み。
            try
            {
                using (var thread = new AndroidJavaClass("android.app.ActivityThread"))
                {
                    found = thread.CallStatic<AndroidJavaObject>("currentApplication");
                    if (found != null)
                    {
                        through = "application";
                        return found;
                    }
                }
            }
            catch (Exception)
            {
                // 新しい Android では隠されていることがある。次は無いので黙る。
            }

            through = "見つからず";
            return null;
        }

        /// <summary>Java の静的な入れ物をひとつ覗く。無ければ null。</summary>
        private static AndroidJavaObject Static(string className, string field)
        {
            try
            {
                using (var type = new AndroidJavaClass(className))
                    return type.GetStatic<AndroidJavaObject>(field);
            }
            catch (Exception)
            {
                // その道が無いだけ。次を試す。
                return null;
            }
        }
#endif

        /// <summary>
        /// 一度だけ震わせる。
        /// </summary>
        /// <param name="milliseconds">
        /// 震わせる長さ。-1 なら端末に任せる
        /// （Android 10 以降はクリックの合図、それより前はブザー）。
        /// 0 なら震わせない。
        /// </param>
        /// <param name="amplitude">強さ 1〜255。-1 なら端末の既定、0 なら震わせない。</param>
        public void Tap(int milliseconds, int amplitude)
        {
            if (!Available) return;

            // 遊ぶ人が合図を切っている。振動子を触る必要もない。
            if (!SignalScale.Signals(milliseconds, amplitude)) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                AndroidJavaObject effect;

                if (milliseconds < 0 && _sdk >= QuinceTartSdk)
                {
                    // 端末ごとに調律された合図。長さも強さも端末が決める。
                    effect = _effects.CallStatic<AndroidJavaObject>("createPredefined", EffectClick);
                }
                else
                {
                    // クリックの合図が無い版では、昔ながらのブザーで代える。
                    var length = milliseconds < 0 ? BuzzerMilliseconds : milliseconds;
                    var strength = amplitude < 0 ? DefaultAmplitude : Math.Min(amplitude, 255);

                    effect = _effects.CallStatic<AndroidJavaObject>(
                        "createOneShot", (long)length, strength);
                }

                using (effect) _vibrator.Call("vibrate", effect);
            }
            catch (Exception e)
            {
                // 一度失敗したら、以後は試さない。毎回例外を投げるのは無駄。
                Available = false;
                Note = "振動に失敗したので止めました: " + e.Message;

                // 黙って音側へ落ちると、原因が判らないまま「なぜか音になる」
                // としか見えない。実際それで遠回りしたので、必ず書き出す。
                Debug.LogWarning("はめ込みの合図: " + Note);
            }
#endif
        }

        public void Dispose()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _effects?.Dispose();
            _vibrator?.Dispose();
            _effects = null;
            _vibrator = null;
#endif
            Available = false;
        }
    }
}
