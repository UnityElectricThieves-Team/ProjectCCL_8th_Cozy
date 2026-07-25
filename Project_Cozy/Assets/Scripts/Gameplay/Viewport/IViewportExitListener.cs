using UnityEngine;

/// <summary>
/// 확정 뷰포트 밖으로 밀려난 캐릭터가 받는 신호.
/// 캐릭터 프리팹의 아무 컴포넌트나 구현하면 ViewportResidencyEnforcer가 호출해 준다.
///
/// 뷰포트 밖은 창이 존재하지 않는 영역 — 렌더링도 클릭도 안 되므로 "그냥 두기"는 선택지가 아니다.
/// 반환값으로 처리 방식을 결정한다:
///   true  = 자체 처리함 (예: 걸어서 복귀하는 상태로 전환) → 기본 회수(클램프 텔레포트) 생략
///   false = 신호만 받고 처리는 위임 → Enforcer가 뷰포트 안쪽으로 클램프 텔레포트
/// </summary>
public interface IViewportExitListener
{
    /// <param name="worldViewport">확정 뷰포트의 월드 좌표 Rect.</param>
    bool OnViewportExited(Rect worldViewport);
}
