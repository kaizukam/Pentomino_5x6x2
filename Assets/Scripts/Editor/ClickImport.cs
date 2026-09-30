using System.IO;
using UnityEditor;
using UnityEngine;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// Assets/Resources/Audio の音を、効果音として読み込ませる。
    ///
    /// 既定のままだと鳴らす直前に展開が要る。はまった瞬間・完成した瞬間に
    /// 鳴らしたい音なので、そのぶん遅れると「起きてから鳴った」に聞こえる。
    /// どちらも読み込みの時点で展開しておく。
    ///
    /// 圧縮の仕方だけは長さで変える。40ms のクリック音は生のままでも
    /// 数キロバイトだが、5 秒のおめでとうは生だと 900KB 近くある。
    /// 長いほうは Vorbis で置き、読むときに展開する。
    ///
    /// 取り込みの設定は手で入れると、音を差し替えたときに戻ってしまう。
    /// ここに書いておけば、置いた時点でそうなる。
    /// </summary>
    public sealed class ClickImport : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/Audio/";

        /// <summary>これより大きい音は、生のまま持たずに圧縮して置く（バイト）。</summary>
        private const long CompressAbove = 100 * 1024;

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Folder)) return;

            var importer = (AudioImporter)assetImporter;

            importer.forceToMono = true;
            importer.loadInBackground = false;

            var large = new FileInfo(assetPath).Length > CompressAbove;

            // preloadAudioData は端末ごとの設定に移ったので、こちらで指定する。
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = large
                ? AudioCompressionFormat.Vorbis
                : AudioCompressionFormat.PCM;
            settings.quality = 0.7f;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
        }
    }
}
