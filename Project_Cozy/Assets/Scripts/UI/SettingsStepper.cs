using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 설정 패널의 −/+ 줄(<c>SettingsRow_Stepper</c>). 정수 단계 하나만 안다 — 몇 번째 단계인지와 단계가 몇 개인지.
/// 단계가 실제로 몇 배인지는 이 컴포넌트가 모른다. 그 변환은 값을 쓰는 쪽이 한 곳에서 한다.
///
/// 처음 단계면 − 버튼을, 마지막 단계면 + 버튼을 숨긴다(Figma 옵션 - 그래픽 "현재 크기가 최소일 경우 '-' 사라짐").
/// 버튼 OnClick은 인스펙터로 <see cref="Previous"/>/<see cref="Next"/>에 걸고, 값이 바뀌면 <see cref="_valueChanged"/>를 울린다.
/// 매니저 값을 화면에 밀어넣을 때는 알림 없는 <see cref="Setup"/>을 쓴다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SettingsStepper : MonoBehaviour
{
    [SerializeField] private GameObject _previousButton;
    [SerializeField] private GameObject _nextButton;
    [SerializeField, Tooltip("단계가 바뀌면 새 단계 번호(0부터)를 넘긴다. Setup으로 바꿀 때는 울리지 않는다.")]
    private UnityEvent<int> _valueChanged;

    private int _index;
    private int _stepCount = 1;

    public int Index => _index;

    /// <summary>단계 수와 현재 단계를 알림 없이 정한다. 시작할 때와 매니저 값이 바뀔 때 부른다.</summary>
    public void Setup(int stepCount, int index)
    {
        _stepCount = Mathf.Max(1, stepCount);
        _index = Mathf.Clamp(index, 0, _stepCount - 1);
        RefreshButtons();
    }

    // − / + 버튼의 OnClick()에 인스펙터로 거는 진입점.
    public void Previous() => Step(-1);
    public void Next() => Step(+1);

    private void Step(int delta)
    {
        int next = Mathf.Clamp(_index + delta, 0, _stepCount - 1);
        if (next == _index) return;
        _index = next;
        RefreshButtons();
        _valueChanged?.Invoke(_index);
    }

    private void RefreshButtons()
    {
        if (_previousButton != null) _previousButton.SetActive(_index > 0);
        if (_nextButton != null) _nextButton.SetActive(_index < _stepCount - 1);
    }
}
