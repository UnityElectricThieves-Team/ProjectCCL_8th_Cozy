using TMPro;
using UnityEngine;

/// <summary>
/// 현재 하트 보유량을 TMP 라벨에 표시한다. <see cref="HeartSystem.HeartsChanged"/>를 구독해
/// 값이 바뀔 때만 갱신한다(매 프레임 폴링 아님). 하트를 보여주고 싶은 어디에나 붙는 자기완결
/// 컴포넌트 — 상점 헤더의 재화 표시, HUD 지갑 등.
/// </summary>
public sealed class HeartCountLabel : MonoBehaviour
{
    [SerializeField] private TMP_Text _label;

    private void Awake()
    {
        if (_label == null) _label = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        var hearts = HeartSystem.Instance;
        if (hearts == null) return; // 씬에 HeartSystem이 없으면 조용히 넘어간다

        hearts.HeartsChanged += OnHeartsChanged;
        OnHeartsChanged(hearts.CurrentHearts); // 현재 잔액으로 초기 표시
    }

    private void OnDisable()
    {
        if (HeartSystem.Instance != null)
            HeartSystem.Instance.HeartsChanged -= OnHeartsChanged;
    }

    private void OnHeartsChanged(int currentHearts)
    {
        if (_label != null) _label.text = currentHearts.ToString();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_label == null && GetComponent<TMP_Text>() == null)
            Debug.LogWarning($"[{nameof(HeartCountLabel)}] _label(TMP_Text)이 비어 있음.", this);
    }
#endif
}
