using UnityEngine;

/// <summary>
/// 모든 UI 패널의 베이스. 열기/닫기를 <see cref="CanvasGroup"/>으로 비파괴 처리한다.
/// 숨길 때 Destroy/SetActive(false) 대신 alpha=0 + 클릭 차단으로 끄므로
/// 재생성 비용이 없고 내부 상태(슬라이더 값 등)가 보존되며, 추후 페이드 연출 여지도 남는다.
/// 설정 등 구체 패널은 이 클래스를 상속해 내용물을 채운다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class UIPanel : MonoBehaviour
{
    private CanvasGroup _group;

    public bool IsOpen => _group != null && _group.alpha > 0f;

    protected virtual void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        ApplyVisible(false); // 기본은 닫힘 상태
    }

    /// <summary>
    /// 닫기(X) 버튼의 On Click에 인스펙터로 거는 진입점. 타깃은 패널 루트 자신이라
    /// 프리팹 안에서 배선이 완결된다(씬의 UIManager를 프리팹에서 참조할 수 없으므로).
    /// <see cref="Close"/>를 직접 걸면 UIManager의 열린 패널 목록에서 빠지지 않아
    /// ESC가 이미 닫힌 패널을 대상으로 헛 눌린다 — 반드시 이 메서드를 건다.
    /// </summary>
    public void RequestClose() => UIManager.Instance?.Close(this);

    public virtual void Open() => ApplyVisible(true);
    public virtual void Close() => ApplyVisible(false);

    private void ApplyVisible(bool visible)
    {
        if (_group == null) _group = GetComponent<CanvasGroup>();
        _group.alpha = visible ? 1f : 0f;
        _group.interactable = visible;
        _group.blocksRaycasts = visible;
    }
}
