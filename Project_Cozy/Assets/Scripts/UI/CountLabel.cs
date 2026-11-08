using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 구름·캐릭터 위에 뜨는 수치. 구름은 누적 스폰 기운, 캐릭터는 누적 친밀도를 숫자로 보여 준다.
/// 보일지는 설정 일반 탭의 '구름 수치'·'캐릭터 수치'가 정한다(Figma '구름/캐릭터 수치 표시' 프레임).
/// - 항상 표시
/// - 자동 숨기기: 구름·캐릭터의 그림 상자 안에 커서가 있을 때만 표시
/// - 표시 안함
///
/// 대상의 프리팹 안, 수치를 띄울 자리에 놓은 TMP 글자에 붙인다. 대상은 부모 계층에서 찾는다.
/// 숫자는 언어와 무관해 번역하지 않고, 폰트도 바꾸지 않는다(LocalizationManager를 거치지 않는다).
///
/// 값은 이벤트가 아니라 매 프레임 정수 비교로 따라간다. 스폰 기운에는 변경 이벤트가 없고, 친밀도는 복원이 이벤트를 울리지
/// 않기 때문이다(.claude/rules/unity/character-affinity.md). 글자는 값이 바뀔 때만 다시 만든다.
/// </summary>
public sealed class CountLabel : MonoBehaviour
{
    private enum Source { SpawnEnergy, Affinity }

    [Tooltip("무엇을 표시할지. 구름은 SpawnEnergy, 캐릭터는 Affinity.")]
    [SerializeField] private Source _source;
    [SerializeField] private TMP_Text _label;

    private SpawnPointManager _spawnPoint;
    private BaseCharacterController _character;
    private SpriteRenderer _box;   // 자동 숨기기의 호버 판정 상자
    private SettingsManager _settings;
    private WindowsCursorToUnityScreen _cursorSource;
    private Camera _camera;

    private CountVisibility _visibility = CountVisibility.AutoHide;
    private int _shownValue = -1;

    private void Awake()
    {
        if (_label == null) _label = GetComponent<TMP_Text>();
        if (_source == Source.SpawnEnergy)
        {
            _spawnPoint = GetComponentInParent<SpawnPointManager>();
            _box = _spawnPoint != null ? _spawnPoint.GetComponentInChildren<SpriteRenderer>() : null;
        }
        else
        {
            _character = GetComponentInParent<BaseCharacterController>();
            _box = _character != null ? _character.GetComponentInChildren<SpriteRenderer>() : null;
        }
    }

    private void Start()
    {
        if (_label == null || (_spawnPoint == null && _character == null))
        {
            Debug.LogError($"[{nameof(CountLabel)}] TMP_Text 또는 대상({_source})을 찾지 못했습니다.", this);
            enabled = false;
            return;
        }

        _camera = Camera.main;
        _cursorSource = FindFirstObjectByType<WindowsCursorToUnityScreen>();
        _settings = SettingsManager.Instance;
        if (_settings != null) _settings.Changed += OnSettingsChanged;
        OnSettingsChanged();
    }

    private void OnDestroy()
    {
        if (_settings != null) _settings.Changed -= OnSettingsChanged;
    }

    private void OnSettingsChanged()
    {
        if (_settings != null)
            _visibility = _source == Source.SpawnEnergy ? _settings.SpawnerCountVisibility : _settings.AffinityVisibility;
        if (_visibility == CountVisibility.Hidden) _label.enabled = false;
    }

    private void Update()
    {
        if (_visibility == CountVisibility.Hidden) return;

        bool show = _visibility == CountVisibility.Always || IsCursorOverBox();
        _label.enabled = show;
        if (!show) return;

        int value = _source == Source.SpawnEnergy ? _spawnPoint.CumulativeEnergy : _character.Affinity.CumulativeAffinity;
        if (value == _shownValue) return;
        _shownValue = value;
        // TMP의 SetText(서식, float)는 할당이 없지만 1677만을 넘으면 float이라 끝자리가 틀린다(스폰 기운은 10억까지 간다).
        // 값이 바뀔 때만 오는 자리라 문자열 할당을 감수한다.
        _label.text = value.ToString();
    }

    // Figma "구름/캐릭터 박스 안에 마우스 포인터 호버되었을 때만". 픽셀이 아니라 그림 상자로 본다.
    private bool IsCursorOverBox()
    {
        if (_box == null || _camera == null) return false;
        // 빌드의 클릭 통과 중에는 Mouse.current가 멈추므로 OS 커서 기반 좌표를 먼저 쓴다(다른 호버 판정과 같은 방식).
        Vector2 screen;
        if (_cursorSource != null) screen = _cursorSource.UnityScreenPosition;
        else if (Mouse.current != null) screen = Mouse.current.position.ReadValue();
        else return false;
        Vector3 world = _camera.ScreenToWorldPoint(screen);
        Bounds b = _box.bounds;
        return world.x >= b.min.x && world.x <= b.max.x && world.y >= b.min.y && world.y <= b.max.y;
    }
}
