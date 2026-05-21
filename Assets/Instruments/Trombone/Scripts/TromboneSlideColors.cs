using UnityEngine;

namespace Instruments
{
    /// <summary>
    /// 트롬본 슬라이드 7포지션(0~6) 색 팔레트 단일 소스.
    /// 0(가장 짧음/가까움) 보라 → 6(가장 길음/멀음) 빨강.
    /// Rhythm Game 노트 색과 prefab 시각 마커가 동일 팔레트를 참조한다.
    /// </summary>
    public static class TromboneSlideColors
    {
        public static readonly Color[] Palette =
        {
            new Color(0.55f, 0.00f, 0.85f, 1f), // 0: 보라
            new Color(0.00f, 0.00f, 1.00f, 1f), // 1: 파랑
            new Color(0.00f, 0.75f, 1.00f, 1f), // 2: 하늘색
            new Color(0.00f, 0.80f, 0.20f, 1f), // 3: 초록
            new Color(1.00f, 1.00f, 0.00f, 1f), // 4: 노랑
            new Color(1.00f, 0.45f, 0.00f, 1f), // 5: 주황
            new Color(1.00f, 0.00f, 0.00f, 1f), // 6: 빨강
        };
    }
}
