using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// IHoverable 위에 알파 픽셀 검사를 얹는 데코레이터.
/// <see cref="InputInteractionManager"/>로부터 hover 진입을 받으면, 매 프레임 마우스 위치의 스프라이트
/// 알파를 검사해 불투명 픽셀일 때만 <see cref="_onOpaqueHoverEnter"/> / <see cref="_onOpaqueHoverExit"/>를 발사한다.
///
/// 사용 조건: 같은 GameObject에 <see cref="Collider2D"/>가 필요하고,
/// 검사 대상 sprite 텍스처는 임포트 설정에서 <b>Read/Write Enabled</b>가 켜져 있어야 한다 (GetPixel용).
/// </summary>
[DisallowMultipleComponent]
public sealed class OpaquePixelHover : MonoBehaviour, IHoverable
{
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField, Tooltip("비우면 Awake 시 Camera.main으로 폴백.")] private Camera _camera;
    [SerializeField, Range(0f, 1f), Tooltip("알파가 이 값보다 크면 불투명으로 판정. 픽셀 아트엔 0.1 정도면 충분.")]
    private float _alphaThreshold = 0.1f;

    [Header("Events")]
    [SerializeField, Tooltip("매니저 hover ∧ 알파 불투명 진입 시 발사.")]
    private UnityEvent _onOpaqueHoverEnter;
    [SerializeField, Tooltip("opaque hover 종료 시 발사 — 콜라이더 밖으로 나가거나, 콜라이더 안의 투명 영역으로 이동했을 때.")]
    private UnityEvent _onOpaqueHoverExit;

    // 매니저가 hover 통지한 상태 (Collider2D 안에 마우스 있음)
    private bool _physicalHover;
    // 마지막 알파 검사 결과
    private bool _alphaInside;
    // 외부에 enter 발사 후 exit 안 발사한 상태인지 — 재발사 방지
    private bool _firedEnter;
    // Camera.main 캐시 (매 프레임 GameObject.FindWithTag 회피)
    private Camera _resolvedCamera;

    private void Awake()
    {
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        _resolvedCamera = _camera != null ? _camera : Camera.main;
    }

    // InputInteractionManager가 호출 — Collider2D 안으로 진입
    public void OnHoverEnter()
    {
        _physicalHover = true;
        Reevaluate();
    }

    // InputInteractionManager가 호출 — Collider2D 밖으로 이탈
    public void OnHoverExit()
    {
        _physicalHover = false;
        _alphaInside = false; // 다음 진입 시 재검사 강제
        Reevaluate();
    }

    private void Update()
    {
        // 콜라이더 밖이면 알파 검사도 의미 없음 — 매니저가 가장 빠른 컬링 이미 했음
        if (!_physicalHover) return;

        bool now = CheckAlphaAtMouse();
        if (now != _alphaInside)
        {
            _alphaInside = now;
            Reevaluate();
        }
    }

    private void Reevaluate()
    {
        bool fire = _physicalHover && _alphaInside;
        if (fire && !_firedEnter)
        {
            _firedEnter = true;
            _onOpaqueHoverEnter?.Invoke();
        }
        else if (!fire && _firedEnter)
        {
            _firedEnter = false;
            _onOpaqueHoverExit?.Invoke();
        }
    }

    private bool CheckAlphaAtMouse()
    {
        if (_spriteRenderer == null || _spriteRenderer.sprite == null) return false;

        var mouse = Mouse.current;
        if (mouse == null) return false;

        Camera cam = _resolvedCamera != null ? _resolvedCamera : Camera.main;
        if (cam == null) return false;

        Vector2 mouseScreen = mouse.position.ReadValue();
        Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 0f));

        // 월드 → SpriteRenderer 로컬 (transform.localScale, 회전 모두 자동 보정됨)
        Vector3 local = _spriteRenderer.transform.InverseTransformPoint(mouseWorld);

        // flipX/flipY는 SpriteRenderer 옵션이라 transform 외부에서 따로 보정
        if (_spriteRenderer.flipX) local.x = -local.x;
        if (_spriteRenderer.flipY) local.y = -local.y;

        Sprite sp = _spriteRenderer.sprite;
        float ppu = sp.pixelsPerUnit;

        // 로컬(Unity 단위) → 스프라이트 내 픽셀 좌표 (좌하단 0,0 기준)
        // local * ppu = 픽셀 오프셋, + pivot = 스프라이트 좌하단 기준 픽셀
        float px = local.x * ppu + sp.pivot.x;
        float py = local.y * ppu + sp.pivot.y;

        // 스프라이트 rect 밖이면 false (콜라이더 안이어도 스프라이트 영역 밖일 수 있음)
        if (px < 0f || py < 0f || px >= sp.rect.width || py >= sp.rect.height) return false;

        // sprite.rect는 텍스처 atlas 내 offset. 텍스처 픽셀 = rect 좌하단 + 스프라이트 내 좌표
        int tx = Mathf.FloorToInt(sp.rect.x + px);
        int ty = Mathf.FloorToInt(sp.rect.y + py);

        Texture2D tex = sp.texture;
        if (tex == null) return false;
        if (tx < 0 || ty < 0 || tx >= tex.width || ty >= tex.height) return false;

        // 텍스처가 Read/Write 비활성이면 GetPixel이 UnityException — 호출 시점에 명확한 에러가 뜬다
        return tex.GetPixel(tx, ty).a > _alphaThreshold;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (GetComponent<Collider2D>() == null)
        {
            Debug.LogWarning(
                $"[{nameof(OpaquePixelHover)}] '{name}' needs a Collider2D on this GameObject for InputInteractionManager to find it.",
                this);
        }
    }
#endif
}
