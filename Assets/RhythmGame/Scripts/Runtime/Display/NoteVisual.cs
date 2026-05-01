using UnityEngine;

/// <summary>
/// 낙하 노트 단위 오브젝트. NoteDisplayPanel이 스폰할 때 Init()으로 초기화한다.
/// </summary>
public class NoteVisual : MonoBehaviour
{
    [SerializeField] float fallSpeed;

    float lifetime;
    float elapsed;

    /// <summary>
    /// 스폰 시 패널이 호출해 낙하 속도와 수명을 주입한다.
    /// </summary>
    /// <param name="fallSpeed">초당 localPosition.y 감소량 (양수)</param>
    /// <param name="lifetime">이 초 이후 자동 제거</param>
    public void Init(float fallSpeed, float lifetime)
    {
        this.fallSpeed = fallSpeed;
        this.lifetime  = lifetime;
        this.elapsed   = 0f;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 pos = transform.localPosition;
        pos.y -= fallSpeed * Time.deltaTime;
        transform.localPosition = pos;
    }
}
