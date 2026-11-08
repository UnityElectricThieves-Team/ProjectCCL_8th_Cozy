using System;
using UnityEngine;

/// <summary>
/// 모든 UI 패널의 베이스. 열기/닫기를 <see cref="CanvasGroup"/>으로 비파괴 처리한다.
/// 숨길 때 Destroy/SetActive(false) 대신 alpha=0 + 클릭 차단으로 끄므로
/// 재생성 비용이 없고 내부 상태(슬라이더 값 등)가 보존되며, 추후 페이드 연출 여지도 남는다.
/// 설정 등 구체 패널은 이 클래스를 상속해 내용물을 채운다.
///
/// 보이는지는 두 상태로 정한다 — 열려 있는가(<see cref="IsOpen"/>)와 잠시 숨겨졌는가(<see cref="SetSuspended"/>).
/// 장식 설치 모드처럼 패널을 닫지 않고 잠시 비켜 두는 경우가 있어서다. 숨김을 열림과 같은 값(알파)으로 표현하면,
/// 숨긴 동안 열린 패널이 "닫힌 패널"로 읽혀 열림 상태의 뜻이 깨진다.
///
/// 모달 패널(<see cref="IsModal"/>, 확인 팝업)이 떠 있는 동안 다른 패널은 보이되 조작되지 않는다(<see cref="SetBlocked"/>).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class UIPanel : MonoBehaviour
{
    [SerializeField, Tooltip("켜면 이 패널이 열려 있는 동안 다른 패널을 열 수 없고, 열린 패널도 조작되지 않는다. 확인 팝업용.")]
    private bool _isModal;

    private CanvasGroup _group;
    private bool _isOpen;
    private bool _isSuspended;
    private bool _isBlocked;

    /// <summary>열려 있는가. 잠시 숨겨져 있어도 열려 있으면 true다.</summary>
    public bool IsOpen => _isOpen;

    public bool IsModal => _isModal;

    /// <summary>
    /// 지금 보이고 조작할 수 있는가 — 열려 있고, 잠시 숨겨지지 않았고, 모달에 막히지 않았다.
    /// <see cref="ApplyVisible"/>가 계산해 둔 값을 그대로 내보낸다. 호버 말풍선이 이 패널의 항목에 띄워도 되는지 볼 때 쓴다.
    /// </summary>
    public bool IsInteractable => _group != null && _group.interactable;

    /// <summary>
    /// 닫아도 되는지 묻는다. false를 돌려주면 <see cref="UIManager.Close"/>가 닫지 않는다.
    /// 저장하지 않은 변경이 있을 때 확인 팝업을 띄우려는 쪽이 건다. 그쪽이 팝업 확인 뒤 변경을 정리하고 다시 닫기를 부른다.
    /// X 버튼·ESC·메뉴 버튼이 모두 UIManager.Close를 지나므로 이 질문 하나로 세 경로가 다 막힌다.
    /// </summary>
    public Func<bool> CanClose;

    /// <summary>닫힌 직후 울린다. 확인 팝업이 "대답 없이 닫힘(ESC·X)"을 취소로 처리하는 데 쓴다.</summary>
    public event Action Closed;

    protected virtual void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        ApplyVisible(); // 기본은 닫힘 상태
    }

    /// <summary>
    /// 닫기(X) 버튼의 On Click에 인스펙터로 거는 진입점. 타깃은 패널 루트 자신이라
    /// 프리팹 안에서 배선이 완결된다(씬의 UIManager를 프리팹에서 참조할 수 없으므로).
    /// <see cref="Close"/>를 직접 걸면 UIManager의 열린 패널 목록에서 빠지지 않아
    /// ESC가 이미 닫힌 패널을 대상으로 헛 눌린다 — 반드시 이 메서드를 건다.
    /// </summary>
    public void RequestClose() => UIManager.Instance?.Close(this);

    public virtual void Open()
    {
        _isOpen = true;
        ApplyVisible();
    }

    public virtual void Close()
    {
        _isOpen = false;
        ApplyVisible();
        Closed?.Invoke();
    }

    /// <summary>열림 상태는 그대로 두고 잠시 숨기거나(true) 되살린다(false). <see cref="UIManager"/>가 부른다.</summary>
    public void SetSuspended(bool suspended)
    {
        _isSuspended = suspended;
        ApplyVisible();
    }

    /// <summary>보이는 채로 조작만 막거나(true) 푼다(false). 모달 팝업이 떠 있는 동안 <see cref="UIManager"/>가 부른다.</summary>
    public void SetBlocked(bool blocked)
    {
        _isBlocked = blocked;
        ApplyVisible();
    }

    private void ApplyVisible()
    {
        if (_group == null) _group = GetComponent<CanvasGroup>();
        bool visible = _isOpen && !_isSuspended;
        _group.alpha = visible ? 1f : 0f;
        _group.interactable = visible && !_isBlocked;
        // 막힌 패널도 클릭은 받아 둔다 — 놓으면 그 클릭이 뒤의 캐릭터나 바탕화면으로 새어 나간다.
        _group.blocksRaycasts = visible;
    }
}
