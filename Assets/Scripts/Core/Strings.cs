using System;

namespace Pentomino.Core
{
    /// <summary>画面に出す文言の種類。</summary>
    public enum StringId
    {
        /// <summary>難易度の名前。柔道の帯にちなむ。</summary>
        GradeGuided,
        GradeTurn,
        GradeClassic,

        /// <summary>難易度の下に添える一行説明。</summary>
        GradeGuidedNote,
        GradeTurnNote,
        GradeClassicNote,

        Settings,
        Difficulty,
        DifficultyWarning,
        LanguageLabel,
        Close,
        Manual,
        Exit,

        /// <summary>おめでとうパネルの見出し。</summary>
        Congratulations,

        CheckCount,
        HintCount,

        /// <summary>フリックの感度調整。</summary>
        FlickTuning,
        FlickReleaseSpeed,
        FlickFilter,
        FlickStill,
        FlickMinTravel,
        FlickAxisRatio,
        FlickTry,
        FlickResult,
        ResetToDefault,

        /// <summary>説明書の末尾に出す、書体の権利表示の見出し。</summary>
        Licenses,

        /// <summary>級ごとの記録を消すボタン。</summary>
        ClearRecord,

        /// <summary>消す前の問いかけ。</summary>
        ClearRecordAsk,

        /// <summary>問いかけに「消す」と答えるボタン。</summary>
        ClearRecordYes,

        /// <summary>問いかけをやめるボタン。</summary>
        Cancel,

        /// <summary>音の入り切りボタン。</summary>
        SoundLabel,

        /// <summary>振動の長さと強さ。</summary>
        VibrationLength,
        VibrationStrength,

        /// <summary>つまみを左端まで戻したときの「端末に任せる」。</summary>
        DeviceDefault,

        /// <summary>その隣の広い区画。ここに入れると合図が出ない。</summary>
        SignalOff,

        /// <summary>
        /// 合図が音で出るか振動で出るかの断り。機種によって決まる。
        /// </summary>
        NoVibrator,
        ByVibration,
    }

    /// <summary>
    /// 画面の文言。言語ごとの表を持つだけの単純な作り。
    /// 文言が増えて管理しきれなくなったら、Unity Localization パッケージへの移行を検討する。
    /// </summary>
    public static class Strings
    {
        // 列は Languages.All の順（en, ja, es, fr, de, ko, zh-Hans, zh-Hant）。
        private static readonly string[][] Table =
        {
            // 級の名前は「腕前 : 帯の色」。ボタンの絵柄が帯そのものなので、
            // 色の名前を添えると、絵と文字が同じことを言う。
            /* GradeGuided  */ new[]
            {
                "Beginner : White belt", "初心者：白帯", "Principiante : Cinturón blanco",
                "Débutant : Ceinture blanche", "Anfänger : Weißer Gürtel",
                "초심자 : 흰 띠", "初学者：白带", "初學者：白帶",
            },
            /* GradeTurn    */ new[]
            {
                "Intermediate : Yellow belt", "中級：黄帯", "Intermedio : Cinturón amarillo",
                "Intermédiaire : Ceinture jaune", "Mittelstufe : Gelber Gürtel",
                "중급 : 노란 띠", "中级：黄带", "中級：黃帶",
            },
            /* GradeClassic */ new[]
            {
                "Advanced : Black belt", "有段者：黒帯", "Avanzado : Cinturón negro",
                "Avancé : Ceinture noire", "Fortgeschritten : Schwarzer Gürtel",
                "유단자 : 검은 띠", "高段：黑带", "高段：黑帶",
            },

            /* GradeGuidedNote */ new[]
            {
                "Practice moving and fitting the pieces",
                "ピースの移動とはめ込みの練習",
                "Practica mover y encajar las piezas",
                "Entraînez-vous à déplacer et emboîter les pièces",
                "Übe das Bewegen und Einsetzen der Steine",
                "조각을 옮기고 끼우는 연습", "练习移动和放入拼块",
                "練習移動和放入拼塊",
            },
            /* GradeTurnNote */ new[]
            {
                "Practice matching each piece's turn and face",
                "ピースの向きと表裏を合わせる練習",
                "Practica ajustar el giro y la cara de cada pieza",
                "Entraînez-vous à ajuster le sens et la face des pièces",
                "Übe Drehung und Seite jedes Steins anzupassen",
                "조각의 방향과 앞뒤를 맞추는 연습", "练习调整拼块的方向和正反",
                "練習調整拼塊的方向和正反",
            },
            /* GradeClassicNote */ new[]
            {
                "Now solve a real puzzle",
                "実際に問題を解いて見よう",
                "Ahora resuelve un puzle de verdad",
                "Résolvez maintenant un vrai puzzle",
                "Löse jetzt ein echtes Rätsel",
                "이제 실제 문제를 풀어 보세요", "来实际解一道题吧",
                "來實際解一道題吧",
            },

            /* Settings     */ new[] { "Settings", "設定", "Ajustes", "Réglages", "Einstellungen", "설정", "设置", "設定" },
            /* Difficulty   */ new[] { "Mode", "難易度", "Modo", "Mode", "Modus", "난이도", "难度", "難度" },
            // 級を変えても進捗は消えない。級ごとに別々に残るようになった。
            /* DifficultyWarning */ new[]
            {
                "Each mode keeps its own progress.",
                "途中経過は難易度ごとに別々に残ります。",
                "Cada modo guarda su propio progreso.",
                "Chaque mode garde sa propre progression.",
                "Jeder Modus behält seinen eigenen Fortschritt.",
                "난이도마다 진행 상황이 따로 남습니다", "每个难度分别保存进度。",
                "每個難度分別保存進度。",
            },
            /* LanguageLabel */ new[] { "Language", "言語", "Idioma", "Langue", "Sprache", "언어", "语言", "語言" },
            /* Close        */ new[] { "Close", "閉じる", "Cerrar", "Fermer", "Schließen", "닫기", "关闭", "關閉" },
            /* Manual       */ new[] { "Manual", "説明", "Manual", "Aide", "Anleitung", "설명", "说明", "說明" },
            /* Exit         */ new[] { "Exit", "終了", "Salir", "Quitter", "Beenden", "종료", "退出", "退出" },

            /* Congratulations */ new[]
            {
                "Congratulations!", "おめでとう", "¡Enhorabuena!", "Félicitations !",
                "Glückwunsch!", "축하합니다", "恭喜！", "恭喜！",
            },

            // おめでとうパネルに出る減点の名前。ボタンの文字とは別で、こちらは訳す。
            /* CheckCount   */ new[]
            {
                "Check", "チェック", "Revisión", "Vérification",
                "Prüfung", "확인", "检查", "檢查",
            },
            /* HintCount    */ new[]
            {
                "Hint", "ヒント", "Pista", "Indice",
                "Tipp", "힌트", "提示", "提示",
            },
            /* FlickTuning */ new[] { "Flick sensitivity", "感度調整", "Sensibilidad del gesto", "Sensibilité du geste", "Wischempfindlichkeit", "감도 조정", "滑动灵敏度", "滑動靈敏度" },
            /* FlickReleaseSpeed */ new[] { "Flip speed", "反転する速さ", "Velocidad de giro", "Vitesse de retournement", "Wendegeschwindigkeit", "뒤집기 속도", "翻转速度", "翻轉速度" },
            /* FlickFilter */ new[] { "Smoothing", "なめらかさ", "Suavizado", "Lissage", "Glättung", "부드럽게", "平滑", "平滑" },
            /* FlickStill */ new[] { "Stop time", "止まったとみなす間", "Tiempo de parada", "Temps d'arrêt", "Haltezeit", "정지 시간", "停止时间", "停止時間" },
            /* FlickMinTravel */ new[] { "Least travel", "最低の移動量", "Recorrido mínimo", "Course minimale", "Mindestweg", "최소 이동", "最小移动", "最小移動" },
            /* FlickAxisRatio */ new[] { "Straightness", "斜めの許容", "Rectitud", "Rectitude", "Geradheit", "직선 정도", "方向容差", "方向容差" },
            /* FlickTry */ new[] { "Slide this piece to try", "ここでピースを滑らせて試す", "Desliza esta pieza para probar", "Faites glisser cette pièce pour essayer", "Zum Testen diesen Stein wischen", "이 조각을 밀어서 시험해 보세요", "滑动此方块试试", "滑動此方塊試試" },
            /* FlickResult */ new[] { "Result", "判定", "Resultado", "Résultat", "Ergebnis", "판정", "判定结果", "判定結果" },
            /* ResetToDefault */ new[] { "Reset", "既定に戻す", "Restablecer", "Réinitialiser", "Zurücksetzen", "기본값", "恢复默认", "恢復預設" },
            /* Licenses */ new[] { "Licenses", "ライセンス", "Licencias", "Licences", "Lizenzen", "라이선스", "许可", "授權" },
            /* ClearRecord */ new[]
            {
                "Clear history", "履歴を消す", "Borrar historial", "Effacer l'historique",
                "Verlauf löschen", "기록 지우기", "清除记录", "清除紀錄",
            },
            /* ClearRecordAsk */ new[]
            {
                "Erase the progress of this mode?",
                "記録を本当に消しますか？",
                "¿Borrar el progreso de este modo?",
                "Effacer la progression de ce mode ?",
                "Fortschritt dieses Modus löschen?",
                "이 난이도의 기록을 지울까요?", "确定要清除该难度的进度吗？",
                "確定要清除該難度的進度嗎？",
            },
            /* ClearRecordYes */ new[] { "Erase", "消す", "Borrar", "Effacer", "Löschen", "지우기", "清除", "清除" },
            /* Cancel */ new[] { "Cancel", "やめる", "Cancelar", "Annuler", "Abbrechen", "취소", "取消", "取消" },
            /* SoundLabel */ new[]
            {
                "Finish sound", "完成の音", "Sonido final", "Son de fin",
                "Abschlusston", "완성 소리", "完成提示音", "完成提示音",
            },
            /* VibrationLength */ new[]
            {
                "Signal length", "合図の長さ", "Duración del aviso", "Durée du signal",
                "Dauer des Signals", "신호 길이", "提示时长", "提示時長",
            },
            /* VibrationStrength */ new[]
            {
                "Signal strength", "合図の強さ", "Fuerza del aviso", "Force du signal",
                "Stärke des Signals", "신호 세기", "提示强度", "提示強度",
            },
            /* DeviceDefault */ new[]
            {
                "Device default", "端末に任せる", "Del dispositivo", "Réglage du téléphone",
                "Gerätevorgabe", "기기에 맡김", "交给设备", "交給裝置",
            },
            /* SignalOff */ new[]
            {
                "Off", "出さない", "Sin aviso", "Aucun",
                "Aus", "내지 않음", "不提示", "不提示",
            },
            /* NoVibrator */ new[]
            {
                "Signalled by sound", "音で知らせます",
                "Aviso por sonido", "Signal sonore",
                "Signal per Ton", "소리로 알립니다",
                "以声音提示", "以聲音提示",
            },
            /* ByVibration */ new[]
            {
                "Signalled by vibration", "振動で知らせます",
                "Aviso por vibración", "Signal par vibration",
                "Signal per Vibration", "진동으로 알립니다",
                "以振动提示", "以振動提示",
            },
        };

        /// <summary>いま選ばれている言語。設定モードで切り替える。</summary>
        public static Language Current { get; set; } = Language.Japanese;

        public static string Get(StringId id) => Get(id, Current);

        public static string Get(StringId id, Language language)
        {
            var row = (int)id;
            if (row < 0 || row >= Table.Length) return id.ToString();

            var column = Array.IndexOf(Languages.All, language);
            if (column < 0) column = Array.IndexOf(Languages.All, Language.English);

            var values = Table[row];
            if (column < 0 || column >= values.Length) return values[0];
            return values[column];
        }

        /// <summary>
        /// 級の呼び名。「腕前 : 帯の色」で、柔道の帯にちなむ。
        ///
        /// 内部の名前（Guided / Turn / Classic）は保存データの数値と結びついていて
        /// 変えられないので、画面に出す名前だけをここで対応させる。
        /// </summary>
        public static string GradeName(Difficulty difficulty) => GradeName(difficulty, Current);

        public static string GradeName(Difficulty difficulty, Language language)
        {
            switch (difficulty)
            {
                case Difficulty.Turn: return Get(StringId.GradeTurn, language);
                case Difficulty.Classic: return Get(StringId.GradeClassic, language);
                default: return Get(StringId.GradeGuided, language);
            }
        }

        /// <summary>難易度に添える一行説明。</summary>
        public static string GradeNote(Difficulty difficulty) => GradeNote(difficulty, Current);

        public static string GradeNote(Difficulty difficulty, Language language)
        {
            switch (difficulty)
            {
                case Difficulty.Turn: return Get(StringId.GradeTurnNote, language);
                case Difficulty.Classic: return Get(StringId.GradeClassicNote, language);
                default: return Get(StringId.GradeGuidedNote, language);
            }
        }

        /// <summary>表の作りが崩れていないかを確かめる（テスト用）。</summary>
        public static bool Validate(out string problem)
        {
            problem = null;

            var expected = Enum.GetValues(typeof(StringId)).Length;
            if (Table.Length != expected)
            {
                problem = "行数が StringId の数と合いません: " + Table.Length + " != " + expected;
                return false;
            }

            for (var row = 0; row < Table.Length; row++)
            {
                if (Table[row].Length != Languages.All.Length)
                {
                    problem = ((StringId)row) + " の言語数が合いません: " + Table[row].Length;
                    return false;
                }

                foreach (var value in Table[row])
                {
                    if (!string.IsNullOrEmpty(value)) continue;
                    problem = ((StringId)row) + " に空の文言があります";
                    return false;
                }
            }

            return true;
        }
    }
}
