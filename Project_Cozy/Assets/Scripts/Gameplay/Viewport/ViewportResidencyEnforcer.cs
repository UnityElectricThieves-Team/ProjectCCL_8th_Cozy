using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 뷰포트가 확정 적용될 때마다(ViewportScreenSettings.ViewportApplied) 살아있는 캐릭터의
/// "뷰포트 거주"를 보장한다. 뷰포트 밖 캐릭터는:
///   1) IViewportExitListener 구현 컴포넌트가 있으면 신호를 보낸다 (자체 연출 기회).
///   2) 아무도 자체 처리(true 반환)하지 않으면 뷰포트 안쪽으로 클램프 텔레포트 (기본 회수).
///
/// 편집 중 프리뷰 조작에는 반응하지 않는다 — 저장/취소/직접 드래그로 확정된 순간에만.
/// (프리뷰마다 회수하면 핸들을 끄는 동안 캐릭터가 질질 끌려다닌다.)
/// </summary>
[DisallowMultipleComponent]
public class ViewportResidencyEnforcer : MonoBehaviour
{
    [Header("협력자 (미할당 시 자동 탐색)")]
    [SerializeField] private ViewportScreenSettings _viewportSettings;
    [SerializeField] private BaseSpaceCameraFitter _cameraFitter;

    [Header("판정/회수")]
    [SerializeField, Tooltip("이탈 판정 여유(월드 유닛). 경계에 걸친 캐릭터를 이탈로 치지 않게.")]
    private float _exitTolerance = 0.1f;
    [SerializeField, Tooltip("기본 회수 시 경계에서 안쪽으로 들여놓는 거리(월드 유닛).")]
    private float _recallPadding = 0.5f;
    [SerializeField] private bool _debugLogs;

    private readonly List<IViewportExitListener> _listenerBuffer = new List<IViewportExitListener>();

    private void Start()
    {
        if (_viewportSettings == null) _viewportSettings = FindFirstObjectByType<ViewportScreenSettings>();
        if (_cameraFitter == null)     _cameraFitter     = FindFirstObjectByType<BaseSpaceCameraFitter>();

        if (_viewportSettings == null || _cameraFitter == null)
        {
            Debug.LogWarning("[ViewportResidencyEnforcer] ViewportScreenSettings/BaseSpaceCameraFitter 없음 — 비활성.");
            enabled = false;
            return;
        }
        _viewportSettings.ViewportApplied += OnViewportApplied;
    }

    private void OnDestroy()
    {
        if (_viewportSettings != null)
            _viewportSettings.ViewportApplied -= OnViewportApplied;
    }

    private void OnViewportApplied(RectInt viewportPx)
    {
        if (CharacterManager.Instance == null) return;

        Rect world = _cameraFitter.BaseRectToWorld(viewportPx, _viewportSettings.BaseSpaceSize);
        Rect judge = Inflate(world, _exitTolerance);

        // TODO: Alive 캐릭터 확인해볼 수 있는 방법 개발 요청하기 
        // 아래는 구현 예시. CharacterManager.Instance.Alive가 IReadOnlyList<GameObject>를 반환한다고 가정.
        //IReadOnlyList<GameObject> alive = CharacterManager.Instance.Alive;
        //for (int i = 0; i < alive.Count; i++)
        //{
        //    GameObject character = alive[i];
        //    if (character == null) continue;

        //    Vector3 pos = character.transform.position;
        //    if (judge.Contains(new Vector2(pos.x, pos.y))) continue;

        //    if (!NotifyListeners(character, world))
        //        Recall(character, world);
        //}
    }

    /// <summary>이탈 신호 전파. 하나라도 자체 처리(true)하면 true.</summary>
    private bool NotifyListeners(GameObject character, Rect worldViewport)
    {
        character.GetComponents(_listenerBuffer);
        bool handled = false;
        for (int i = 0; i < _listenerBuffer.Count; i++)
            handled |= _listenerBuffer[i].OnViewportExited(worldViewport);
        _listenerBuffer.Clear();
        return handled;
    }

    // TODO: 회수 관련 논의 필요. 회수는 캐릭터가 뷰포트 밖으로 나갔을 때 강제적으로 위치를 바꾸는 것이므로, 게임 디자인 상 적절한지 확인 필요.
    // 캐릭터 쪽에서 구현하는 것이 나으려나..? 논의해보자.
    ///// <summary>기본 회수 — 뷰포트 안쪽(padding 들여쓴 위치)으로 클램프 텔레포트.</summary>
    //private void Recall(GameObject character, Rect world)
    //{
    //    Rect inner = Inflate(world, -_recallPadding);
    //    Vector3 pos = character.transform.position;
    //    pos.x = Mathf.Clamp(pos.x, inner.xMin, inner.xMax);
    //    pos.y = Mathf.Clamp(pos.y, inner.yMin, inner.yMax);
    //    character.transform.position = pos;

    //    if (_debugLogs)
    //        Debug.Log($"[ViewportResidencyEnforcer] {character.name} 뷰포트 밖 → {pos}로 회수");
    //}

    private static Rect Inflate(Rect r, float amount)
        => new Rect(r.xMin - amount, r.yMin - amount, r.width + amount * 2f, r.height + amount * 2f);
}
