using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <deprecated>
/// 씬 전역 일괄 sleep 정책. 개체별 정책인 <see cref="CharacterSleepPolicy"/>로 대체됨.
/// 코드/씬 인스턴스는 그대로 유지되나 신규 코드에서는 본 클래스를 쓰지 않는다.
/// 씬에 본 컴포넌트와 <see cref="CharacterSleepPolicy"/>가 동시에 살아 있으면 RequestSleep/WakeUp이
/// 양쪽에서 호출되어 개체 정책이 무력화될 수 있음 — 마이그레이션 시 씬 인스턴스 비활성화 권장.
/// </deprecated>
/// <summary>
/// 씬 전역 수면 정책. 일정 시간 무입력이면 씬의 모든 <see cref="CharacterBasicAI2D"/>를 Sleep시키고,
/// 다시 입력이 들어오면 일괄 WakeUp 시킨다 (AI_Logic.md "수면 및 방치 모드").
///
/// 본 컴포넌트는 *씬-레벨 캐릭터 조정자*. 한 캐릭터의 자율 거동은 <see cref="CharacterBasicAI2D"/>가 책임지고,
/// 여기서는 환경 신호(무입력 임계 도달)를 받아 *씬의 캐릭터들에 일괄 명령*을 내린다.
///
/// 입력 소스:
///  - 키보드: <see cref="GlobalKeyInput"/>의 KeyPressed (OS-wide, 포커스 무관)
///  - 마우스: <see cref="Mouse"/>.current.position (Unity Input System, 게임 창 포커스 한정)
///    창이 포커스를 잃으면 마우스 변화는 안 들어오지만, 키 입력은 GlobalKeyInput으로 들어오므로
///    어느 한쪽이라도 들어오면 깨어난다.
///
/// 캐릭터 목록은 Awake에서 1회 캐싱(<see cref="Object.FindObjectsByType"/>) — 런타임 Find 금지 컨벤션 준수.
/// 씬 도중 캐릭터가 동적으로 추가되는 경우는 오늘 범위 밖.
/// </summary>
public sealed class SleepController : MonoBehaviour
{
    [FormerlySerializedAs("_hook")]
    [SerializeField] private GlobalKeyInput _keyInput;
    [Tooltip("이 시간 동안 무입력이면 모든 캐릭터가 Sleep 진입.")]
    [SerializeField] private float _idleSecondsBeforeSleep = 5f;
    [SerializeField] private bool _debugLogs = true;

    private CharacterBasicAI2D[] _characters;
    private Vector2Int _lastMousePixel;
    private bool _hasLastMouse;
    private float _lastInputAt;
    private bool _isSleeping;

    private void Awake()
    {
        if (_keyInput == null) _keyInput = GetComponent<GlobalKeyInput>();

        _characters = FindObjectsByType<CharacterBasicAI2D>(FindObjectsSortMode.None);
        _lastInputAt = Time.time;
    }

    private void OnEnable()
    {
        if (_keyInput != null) _keyInput.KeyPressed += OnAnyKey;
        else Debug.LogError($"[{nameof(SleepController)}] GlobalKeyInput 참조가 없습니다.", this);
    }

    private void OnDisable()
    {
        if (_keyInput != null) _keyInput.KeyPressed -= OnAnyKey;
    }

    private void Update()
    {
        DetectMouseMovement();

        if (!_isSleeping && Time.time - _lastInputAt >= _idleSecondsBeforeSleep)
            EnterSleep();
    }

    private void DetectMouseMovement()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        var pixel = Vector2Int.FloorToInt(mouse.position.ReadValue());
        if (!_hasLastMouse)
        {
            _lastMousePixel = pixel;
            _hasLastMouse = true;
            return;
        }
        if (pixel == _lastMousePixel) return;

        _lastMousePixel = pixel;
        RecordInput();
    }

    private void OnAnyKey(Key key)
    {
        RecordInput();
    }

    private void RecordInput()
    {
        _lastInputAt = Time.time;
        if (_isSleeping) ExitSleep();
    }

    private void EnterSleep()
    {
        _isSleeping = true;
        if (_debugLogs)
            Debug.Log($"[{nameof(SleepController)}] {_idleSecondsBeforeSleep:0.0}s 무입력 → 일괄 Sleep ({_characters.Length}마리)", this);

        for (var i = 0; i < _characters.Length; i++)
        {
            if (_characters[i] != null) _characters[i].RequestSleep();
        }
    }

    private void ExitSleep()
    {
        _isSleeping = false;
        if (_debugLogs)
            Debug.Log($"[{nameof(SleepController)}] 입력 감지 → 일괄 WakeUp ({_characters.Length}마리)", this);

        for (var i = 0; i < _characters.Length; i++)
        {
            if (_characters[i] != null) _characters[i].RequestWakeUp();
        }
    }
}
