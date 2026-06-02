using TMPro;
using UnityEngine;

/// <summary>
/// <see cref="BaseCharacterController"/>의 현재 상태 이름을 머리 위 TextMeshPro 라벨에 표시.
/// 부모 계층에서 캐릭터를 자동 탐색하므로 prefab variant 안에 그대로 배치하면 동작.
/// 매 프레임 Visual 상단 위로 위치를 갱신하고, 부모 스케일을 역보정해 항상 자연 크기로 보이게 한다.
/// </summary>
public sealed class CharacterStateLabel : MonoBehaviour
{
    [SerializeField] private BaseCharacterController _character;
    [SerializeField] private TMP_Text _label;
    [SerializeField, Tooltip("위치 기준이 되는 Visual. 비우면 캐릭터 transform 아래의 'Visual' 자식 자동 탐색.")]
    private Transform _visual;
    [SerializeField, Tooltip("Visual 상단으로부터 라벨까지의 추가 거리(월드 단위).")]
    private float _yOffset = 0.2f;
    [SerializeField, Tooltip("부모 스케일을 역보정해 항상 자연 크기로 표시. 끄면 부모와 함께 스케일됨.")]
    private bool _counterScaleParent = true;

    private SpriteRenderer _visualRenderer;

    private void Awake()
    {
        if (_label == null) _label = GetComponent<TMP_Text>();
        if (_character == null) _character = GetComponentInParent<BaseCharacterController>();
        if (_visual == null && _character != null) _visual = _character.transform.Find("Visual");
        if (_visual != null) _visualRenderer = _visual.GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (_character == null || _label == null)
        {
            Debug.LogError($"[{nameof(CharacterStateLabel)}] BaseCharacterController 또는 TMP_Text 참조가 없습니다.", this);
            return;
        }
        _character.State.StateChanged += Refresh;
        Refresh(_character.State.CurrentStateId);
    }

    private void OnDisable()
    {
        if (_character != null) _character.State.StateChanged -= Refresh;
    }

    private void LateUpdate()
    {
        if (_visual == null) return;

        // Visual 상단의 월드 Y. SpriteRenderer가 있으면 정확한 bounds, 없으면 transform 위치로 폴백.
        float topY = _visualRenderer != null ? _visualRenderer.bounds.max.y : _visual.position.y;
        var p = transform.position;
        p.x = _visual.position.x;
        p.y = topY + _yOffset;
        transform.position = p;

        if (!_counterScaleParent || transform.parent == null) return;

        // 부모 lossyScale의 역수로 localScale을 잡아 월드 스케일 1을 유지 — 폰트가 부모 스케일에 끌려가지 않게.
        var s = transform.parent.lossyScale;
        transform.localScale = new Vector3(
            s.x != 0f ? 1f / s.x : 1f,
            s.y != 0f ? 1f / s.y : 1f,
            s.z != 0f ? 1f / s.z : 1f);
    }

    private void Refresh(CharacterState id) => _label.text = _character.State.CurrentStateName;
}
