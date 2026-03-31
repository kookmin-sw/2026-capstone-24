using UnityEditor;
using UnityEngine;

/// <summary>
/// 프로젝트 내에 추가되는 모든 오디오 클립에 대해 표준 설정을 자동으로 적용하는 에디터 스크립트입니다.
/// 이 스크립트가 있으면 다른 작업자가 파일을 추가해도 자동으로 '기본 설정'이 적용됩니다.
/// </summary>
public class AudioClipSettingsPostprocessor : AssetPostprocessor
{
    // 오디오 클립이 프로젝트에 추가되거나 변경될 때 호출됩니다.
    void OnPreprocessAudio()
    {
        AudioImporter importer = (AudioImporter)assetImporter;

        // 특정 폴더(예: Audio) 내의 파일에만 적용하고 싶다면 아래 조건을 사용할 수 있습니다.
        // if (!importer.assetPath.Contains("/Audio/")) return;

        // 1. Force To Mono: MONO 설정
        importer.forceToMono = true;

        // 2. Normalize: 사용안함
        // (AudioImporter에는 직접적인 Normalize 필드가 없으나, 
        // 믹스다운 시 영향을 주는 설정을 기본값으로 유지합니다.)

        // 3. Load In Background: 사용안함
        importer.loadInBackground = false;

        // 4. Ambisonic: 사용안함
        importer.ambisonic = false;

        // 플랫폼별 기본 설정 가져오기
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;

        // 5. Preload Audio Data: 사용안함 (유저 요청)
        settings.preloadAudioData = false;

        // 6. Load Type: Decompress on Load (레이턴시 최소화)
        settings.loadType = AudioClipLoadType.DecompressOnLoad;

        // 7. Compression Format: PCM
        settings.compressionFormat = AudioCompressionFormat.PCM;

        // 8. Sample Rate Setting: 44.1khz (Override)
        settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
        settings.sampleRateOverride = 44100;

        // 변경된 설정 적용
        importer.defaultSampleSettings = settings;
        
        Debug.Log($"[AudioPostprocessor] 기본 오디오 설정 자동 적용됨: {importer.assetPath}");
    }
}
