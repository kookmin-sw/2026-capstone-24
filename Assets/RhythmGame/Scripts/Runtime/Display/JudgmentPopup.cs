using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// NoteDisplayPanel 자식 오브젝트. 판정 결과(Perfect / Good / Miss)를 텍스트로 표시하고
/// displayDuration 초 동안 유지한 뒤 알파를 0으로 페이드아웃한다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class JudgmentPopup : MonoBehaviour
{
    [SerializeField] Text judgmentText;
    [SerializeField] float displayDuration = 0.6f;

    CanvasGroup canvasGroup;
    Coroutine   fadeCoroutine;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        if (judgmentText == null)
            judgmentText = GetComponentInChildren<Text>();
    }

    /// <summary>
    /// 판정 결과에 따라 텍스트·색상을 설정하고 페이드아웃 시퀀스를 시작한다.
    /// </summary>
    public void Show(JudgmentGrade grade)
    {
        if (judgmentText != null)
        {
            switch (grade)
            {
                case JudgmentGrade.Perfect:
                    judgmentText.text  = "Perfect";
                    judgmentText.color = Color.yellow;
                    break;
                case JudgmentGrade.Good:
                    judgmentText.text  = "Good";
                    judgmentText.color = Color.green;
                    break;
                case JudgmentGrade.Miss:
                    judgmentText.text  = "Miss";
                    judgmentText.color = Color.red;
                    break;
            }
        }

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeOut());
    }

    IEnumerator FadeOut()
    {
        canvasGroup.alpha = 1f;

        float elapsed = 0f;
        while (elapsed < displayDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / displayDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        fadeCoroutine = null;
    }
}
