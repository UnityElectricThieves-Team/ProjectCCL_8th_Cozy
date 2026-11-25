using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 첫 실행 튜토리얼의 지휘자. 장면 순서(대사 → 구름 → 대사 → 쓰담·변신 → 대사 → 끝)를 진행하고, 끝냈는지를 저장한다.
/// 기획 원본은 Figma `튜토리얼` 섹션이고, 규약과 이유는 .claude/rules/unity/tutorial.md.
///
/// - 기존 시스템을 직접 고치지 않고 각 주인이 연 입구만 부른다: 입력 잠금(<see cref="WorldInputLock"/>), 구름 이벤트·묶기,
///   캐릭터 해금·갈아 끼우기·친밀도 맞추기(<see cref="CharacterOwnership"/>), 변신 금지 잠시 풀기(<see cref="CharacterManager"/>),
///   잡기 막기·변신 준비(<see cref="BaseCharacterController"/>), 머리 위 수치 숨기기(<see cref="CountLabel.Suppressed"/>).
/// - 레이어 교차: Gameplay에 있지만 UI(<see cref="TutorialOverlay"/>, <see cref="UIManager"/>, <see cref="ConfirmPopup"/>)를 부른다.
///   게임 진행(해금·변신·저장)을 쥐는 쪽이라 Gameplay에 둔다.
/// - 시작 판정은 뷰포트가 처음 확정된 뒤 한 번만 한다. 진행 중이 아니면 꺼 두어(enabled=false) 상주 비용이 없다.
///   진행 중에는 끄지 않는다 — OnDisable이 걸어 둔 제한을 모두 풀기 때문이다.
/// - 제한을 푸는 일은 <see cref="ReleaseAll"/> 하나로 모은다. 끝, 건너뛰기, OnDisable이 모두 이것을 부른다 — 푸는 목록이 갈라지지 않게.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialDirector : MonoBehaviour
{
    public static TutorialDirector Instance { get; private set; }

    private const string CheeseId = "character_cheese";
    private const string ReplayNoticeId = "UITutorial.notice.replay";

    private enum Step { None, Dialogue1, Cloud, PreparingCheese, Dialogue3, Pet, Dialogue5 }

    private readonly struct Line
    {
        public readonly string Id;
        public readonly TutorialFace Face;
        public Line(string id, TutorialFace face) { Id = id; Face = face; }
    }

    // 대사 목록. 13줄뿐이고 장면 흐름과 함께 읽혀야 해서 데이터 에셋으로 빼지 않는다.
    private static readonly Line[] Lines1 =
    {
        new Line("UITutorial.dialogue.s1_1", TutorialFace.Shock),
        new Line("UITutorial.dialogue.s1_2", TutorialFace.Shock),
    };
    private static readonly Line LineCloud = new Line("UITutorial.dialogue.s2_1", TutorialFace.Shock);
    private static readonly Line[] Lines3 =
    {
        new Line("UITutorial.dialogue.s3_1", TutorialFace.Happy),
        new Line("UITutorial.dialogue.s3_2", TutorialFace.Shock),
        new Line("UITutorial.dialogue.s3_3", TutorialFace.Shock),
        new Line("UITutorial.dialogue.s3_4", TutorialFace.Shock),
        new Line("UITutorial.dialogue.s3_5", TutorialFace.Shock),
        new Line("UITutorial.dialogue.s3_6", TutorialFace.Shock),
    };
    private static readonly Line LinePet = new Line("UITutorial.dialogue.s4_1", TutorialFace.Shock);
    private static readonly Line[] Lines5 =
    {
        new Line("UITutorial.dialogue.s5_1", TutorialFace.Shock),
        new Line("UITutorial.dialogue.s5_2", TutorialFace.Happy),
        new Line("UITutorial.dialogue.s5_3", TutorialFace.Happy),
        new Line("UITutorial.dialogue.s5_4", TutorialFace.Happy),
    };

    [Header("Refs (씬 인스턴스에서 연결)")]
    [Tooltip("튜토리얼 치즈 프리팹(Character_Cheese의 배리언트 + 안내 그림). 대응표에는 넣지 않는다.")]
    [SerializeField] private GameObject _tutorialCheesePrefab;
    [SerializeField] private TutorialOverlay _overlay;
    [SerializeField] private CloudController _cloud;
    [Tooltip("마지막에 여는 튜토리얼 패널.")]
    [SerializeField] private UIPanel _tutorialPanel;
    [SerializeField] private ViewportScreenSettings _viewport;
    [Tooltip("월드 딤(이 프리팹의 자식). 구름·쓰담 장면에만 켠다. 비우면 딤 없이 진행한다.")]
    [SerializeField] private GameObject _worldDim;

    private bool _decided;
    private bool _quitting;
    private Step _step;

    private Line[] _lines;
    private int _lineIndex;

    private int _cloudClicks;
    private bool _cloudSubscribed;
    private TutorialClickHint _cloudHint;

    private bool _summonTried;
    private int _missingCheeseFrames;

    private BaseCharacterController _cheese;
    private TutorialClickHint _cheeseHint;
    private bool _recordSubscribed;
    private bool _transformReadyMarked;

    private bool _overlaySubscribed;

    private void Awake()
    {
        Instance = this;
        Application.quitting += OnQuitting;

        if (_viewport == null)
        {
            Debug.LogWarning($"[{nameof(TutorialDirector)}] {nameof(ViewportScreenSettings)}가 연결되지 않아 튜토리얼을 시작하지 않습니다.", this);
            _decided = true;
            enabled = false;
            return;
        }
        // 처음부터 꺼 두면 Start가 불리지 않으므로 프리팹에는 켠 채로 두고, 확정 신호는 여기서 구독한다.
        _viewport.ViewportConfirmed += OnViewportConfirmed;
    }

    private void Start()
    {
        // 첫 확정 신호가 이미 지나갔을 수 있다(viewport-coordinates.md의 "뷰포트가 처음 확정되기 전에는").
        if (_viewport != null && _viewport.IsReady) Decide();
    }

    private void OnDestroy()
    {
        Application.quitting -= OnQuitting;
        if (_viewport != null) _viewport.ViewportConfirmed -= OnViewportConfirmed;
        if (Instance == this) Instance = null;
    }

    private void OnQuitting() => _quitting = true;

    private void OnDisable()
    {
        // 앱을 닫을 때는 파괴 순서가 정해져 있지 않아, 남의 오브젝트를 켜고 끄지 않고 정적 상태만 되돌린다.
        if (_quitting)
        {
            WorldInputLock.Release(this);
            CountLabel.Suppressed = false;
            return;
        }
        ReleaseAll();
    }

    // ViewportConfirmed는 줌·[완료]·다시 맞추기마다 다시 울리므로 첫 판정 뒤 바로 구독을 푼다.
    private void OnViewportConfirmed(RectInt _) => Decide();

    // ===== 시작 판정 =====

    private void Decide()
    {
        if (_decided) return;
        _decided = true;
        _viewport.ViewportConfirmed -= OnViewportConfirmed;

        // 불러오기는 "파일 없음"과 "깨짐"을 가리지 않으므로 읽기 전에 따로 본다(설정의 첫 실행 언어 판정과 같은 방식).
        bool fileExists = File.Exists(GameDataPaths.Tutorial);
        if (fileExists && UserDataSaveIO.Load<TutorialFileFormat>(GameDataPaths.Tutorial).completed)
        {
            enabled = false;
            return;
        }

        // 이 기능 전부터 하던 저장(파일 없음·캐릭터 있음)은 옛 플레이어로 보고 건너뛴다.
        var ownership = CharacterOwnership.Instance;
        if (!fileExists && ownership != null && ownership.HasAnyOwned)
        {
            Save(completed: true);
            enabled = false;
            return;
        }

        // 튜토리얼을 시작하는 갈래. 배선 확인보다 먼저 파일을 만든다 — 도중에 끄거나 배선 때문에 미룬 실행에서
        // 캐릭터를 열어도 다음 실행이 옛 플레이어로 읽히지 않게.
        Save(completed: false);

        // 배선이 비었으면 시작하지 않는다. 완료로 저장하지는 않는다 — 배선을 고치면 다음 실행에 다시 뜬다.
        if (!CheckWiring())
        {
            enabled = false;
            return;
        }
        Begin();
    }

    private bool CheckWiring()
    {
        string missing = null;
        if (CharacterOwnership.Instance == null || CharacterManager.Instance == null) missing = "CharacterOwnership/CharacterManager";
        else if (_tutorialCheesePrefab == null || _tutorialCheesePrefab.GetComponentInChildren<TutorialClickHint>(true) == null)
            missing = "튜토리얼 치즈 프리팹(안내 그림 포함)";
        else if (_overlay == null || !_overlay.IsWired) missing = "TutorialOverlay(클릭 판·대사 글자·메뉴 바 딤·메뉴 바)";
        else if (_cloud == null || _cloud.GetComponentInChildren<TutorialClickHint>(true) == null) missing = "구름(안내 그림 포함)";
        else if (_tutorialPanel == null) missing = "튜토리얼 패널";

        if (missing == null) return true;
        Debug.LogWarning($"[{nameof(TutorialDirector)}] 배선이 비어 있어 튜토리얼을 시작하지 않습니다: {missing}", this);
        return false;
    }

    private void Begin()
    {
        CharacterManager.Instance.SetGirlTransformBanSuspended(true);
        CountLabel.Suppressed = true;
        _cloud.SetForcedIdle(true);
        _cloudHint = _cloud.GetComponentInChildren<TutorialClickHint>(true);

        _overlay.ScreenClicked += OnScreenClicked;
        _overlaySubscribed = true;
        _overlay.Show();

        // 치즈를 이미 가졌으면 S3부터 — S1·S2로 가면 해금이 아무 일도 하지 않아 S2에서 빠져나올 수 없다.
        if (CharacterOwnership.Instance.IsOwned(CheeseId)) EnterPreparingCheese();
        else EnterDialogue(Step.Dialogue1, Lines1);
    }

    // ===== 장면 =====

    private void Update()
    {
        switch (_step)
        {
            case Step.PreparingCheese: TickPreparingCheese(); break;
            case Step.Pet: TickPet(); break;
        }
    }

    // 같은 owner는 조건을 바꿀 수 없어서, 장면이 바뀔 때마다 풀고 다시 건다. allowed가 null이면 전체 잠금.
    private void SetLock(Transform allowed)
    {
        WorldInputLock.Release(this);
        if (allowed == null) WorldInputLock.Acquire(this);
        else WorldInputLock.AcquireAllowing(this, allowed);
    }

    private void SetDim(bool on)
    {
        _overlay.SetMenuBarDim(on);
        if (_worldDim != null) _worldDim.SetActive(on);
    }

    private void EnterDialogue(Step step, Line[] lines)
    {
        _step = step;
        _lines = lines;
        _lineIndex = 0;
        SetLock(null);
        SetDim(false);
        _overlay.SetClickCatcher(true);
        _overlay.ShowLine(lines[0].Id, lines[0].Face);
    }

    // 대사 장면의 "화면 어디든 클릭". 버튼은 뗄 때 울린다.
    private void OnScreenClicked()
    {
        if (_step != Step.Dialogue1 && _step != Step.Dialogue3 && _step != Step.Dialogue5) return;

        _lineIndex++;
        if (_lineIndex < _lines.Length)
        {
            _overlay.ShowLine(_lines[_lineIndex].Id, _lines[_lineIndex].Face);
            return;
        }

        switch (_step)
        {
            case Step.Dialogue1: EnterCloud(); break;
            case Step.Dialogue3: EnterPet(); break;
            case Step.Dialogue5: Finish(); break;
        }
    }

    // ----- S2 구름 -----

    private void EnterCloud()
    {
        _step = Step.Cloud;
        SetLock(_cloud.transform);
        _overlay.SetClickCatcher(false);
        SetDim(true);
        _overlay.ShowLine(LineCloud.Id, LineCloud.Face);

        _cloudClicks = 0;
        _cloudHint.ShowLeftClick();
        // 구름 이벤트는 이 장면 동안만 듣는다. 끄는 중에 우클릭해 넘어가도 손을 뗄 때 오는 클릭이 다음 장면에서 세지지 않게.
        _cloud.ClickReleased += OnCloudClick;
        _cloud.RightClicked += OnCloudRightClick;
        _cloudSubscribed = true;
    }

    private void OnCloudClick()
    {
        const int target = GameBalance.TutorialCloudClicks;
        if (_cloudClicks >= target) return;
        _cloudClicks++;
        _cloudHint.SetCounter(_cloudClicks, target);
        if (_cloudClicks >= target) _cloudHint.ShowRightClick();
    }

    private void OnCloudRightClick()
    {
        if (_cloudClicks < GameBalance.TutorialCloudClicks) return;
        UnsubscribeCloud();

        // 이미 가졌으면 false지만, 어느 쪽이든 같은 준비 단계에서 치즈를 찾고 튜토리얼 판으로 맞춘다.
        CharacterOwnership.Instance.UnlockWithAffinity(CheeseId, GameBalance.TutorialStartAffinity, _tutorialCheesePrefab);
        EnterPreparingCheese();
    }

    // ----- S3 준비: 치즈를 찾아 튜토리얼 치즈로 맞춘다 -----

    private void EnterPreparingCheese()
    {
        _step = Step.PreparingCheese;
        SetLock(null);
        if (_cloudHint != null) _cloudHint.Hide();
        _overlay.SetClickCatcher(false);
        _summonTried = false;
        _missingCheeseFrames = 0;
    }

    private void TickPreparingCheese()
    {
        var ownership = CharacterOwnership.Instance;
        if (ownership == null) { Skip(); return; }

        // 숨긴 치즈면 한 번만 소환한다. 다음 프레임에는 이미 나와 있어 다시 부르지 않는다(다시 부르면 false가 와서 거절로 읽힌다).
        if (!ownership.IsPlaced(CheeseId))
        {
            if (_summonTried || !ownership.Summon(CheeseId)) { Skip(); return; }
            _summonTried = true;
        }

        // 시작 재개 때는 같은 확정 신호에서 CharacterOwnership이 치즈를 만들므로, 못 찾으면 한 프레임만 더 기다린다.
        if (!ownership.TryGetController(CheeseId, out _))
        {
            if (++_missingCheeseFrames > 1) Skip();
            return;
        }

        if (!ownership.EnsureSpawnedWithPrefab(CheeseId, _tutorialCheesePrefab)
            || !ownership.TryGetController(CheeseId, out var cheese)
            || !cheese.gameObject.activeInHierarchy)
        {
            Skip();
            return;
        }

        _cheese = cheese;
        _cheeseHint = cheese.GetComponentInChildren<TutorialClickHint>(true);
        EnterDialogue(Step.Dialogue3, Lines3);
    }

    // ----- S4 쓰담·변신 -----

    private void EnterPet()
    {
        _step = Step.Pet;
        var ownership = CharacterOwnership.Instance;

        // 초기화 뒤 구름으로 치즈를 먼저 열어 둔 경우 친밀도가 시작값보다 낮을 수 있다(국기님 결정 — 시작값까지 올림).
        ownership.EnsureAffinityAtLeast(CheeseId, GameBalance.TutorialStartAffinity);

        SetLock(_cheese.transform);
        _overlay.SetClickCatcher(false);
        SetDim(true);
        _overlay.ShowLine(LinePet.Id, LinePet.Face);

        // 쓰담하다 잡히면 낙하가 되어 우클릭 변신이 거부되므로 잡기만 막는다.
        _cheese.SetGrabBlocked(true);
        _transformReadyMarked = false;
        if (_cheeseHint != null) _cheeseHint.ShowLeftClick();

        // 위의 맞추기가 울린 RecordChanged로 카운터가 먼저 보이지 않게 그 뒤에 구독한다.
        ownership.RecordChanged += OnRecordChanged;
        _recordSubscribed = true;

        // 깨어 있는 시간으로도 친밀도가 오르므로, 대사 장면 동안 이미 목표를 넘었을 수 있다.
        CheckTransformReady();
    }

    private void OnRecordChanged(string id)
    {
        if (_step != Step.Pet || id != CheeseId) return;
        if (_cheeseHint != null)
            _cheeseHint.SetCounter(CharacterOwnership.Instance.GetCumulativeAffinity(CheeseId), GameBalance.AffinityToTransform);
        CheckTransformReady();
    }

    private void CheckTransformReady()
    {
        if (_transformReadyMarked || _cheese == null) return;
        if (CharacterOwnership.Instance.GetCumulativeAffinity(CheeseId) < GameBalance.AffinityToTransform) return;

        // 시작값에서 10만 올랐으므로 변신 조건("동물이 된 뒤 쌓인 양")에 닿지 않는다. 여기서 채운다. 누적값은 바뀌지 않는다.
        _cheese.MarkHumanTransformReady();
        _transformReadyMarked = true;
        if (_cheeseHint != null) _cheeseHint.ShowRightClick();
    }

    private void TickPet()
    {
        if (_cheese == null) { Skip(); return; }
        if (_cheese.CurrentForm != CharacterForm.Girl) return;

        UnsubscribeRecord();
        _cheese.SetGrabBlocked(false);
        if (_cheeseHint != null) _cheeseHint.Hide();
        EnterDialogue(Step.Dialogue5, Lines5);
    }

    // ----- 끝 -----

    private void Finish()
    {
        Save(completed: true);
        ReleaseAll();
        _step = Step.None;
        if (UIManager.Instance != null) UIManager.Instance.Open(_tutorialPanel);
        if (ConfirmPopup.Instance != null) ConfirmPopup.Instance.ShowNotice(ReplayNoticeId);
        enabled = false;
    }

    // 진행 중 실패(소환 거절·치즈 못 찾음·갈아 끼우기 실패·꺼짐). 갇혀서 아무것도 못 하는 것보다 나으므로
    // 제한을 모두 풀고 완료로 저장하고 건너뛴다(우리 판단 — tutorial.md).
    private void Skip()
    {
        Debug.LogWarning($"[{nameof(TutorialDirector)}] 치즈를 준비하지 못해 튜토리얼을 건너뜁니다.", this);
        ReleaseAll();
        Save(completed: true);
        _step = Step.None;
        enabled = false;
    }

    /// <summary>
    /// 튜토리얼이 걸어 둔 제한을 모두 푼다. 이미 풀린 것을 다시 풀어도 무해하다.
    /// 앱 종료·씬 내림 때도 불릴 수 있어 대상마다 null·파괴 여부를 먼저 본다.
    /// </summary>
    private void ReleaseAll()
    {
        WorldInputLock.Release(this);
        UnsubscribeCloud();
        UnsubscribeRecord();
        if (_overlaySubscribed && _overlay != null) _overlay.ScreenClicked -= OnScreenClicked;
        _overlaySubscribed = false;

        if (_cloud != null) _cloud.SetForcedIdle(false);
        if (_cheese != null) _cheese.SetGrabBlocked(false);
        if (CharacterManager.Instance != null) CharacterManager.Instance.SetGirlTransformBanSuspended(false);
        CountLabel.Suppressed = false;

        if (_cloudHint != null) _cloudHint.Hide();
        if (_cheeseHint != null) _cheeseHint.Hide();
        if (_overlay != null) _overlay.Hide();
        if (_worldDim != null) _worldDim.SetActive(false);
    }

    private void UnsubscribeCloud()
    {
        if (!_cloudSubscribed) return;
        _cloudSubscribed = false;
        if (_cloud == null) return;
        _cloud.ClickReleased -= OnCloudClick;
        _cloud.RightClicked -= OnCloudRightClick;
    }

    private void UnsubscribeRecord()
    {
        if (!_recordSubscribed) return;
        _recordSubscribed = false;
        if (CharacterOwnership.Instance != null) CharacterOwnership.Instance.RecordChanged -= OnRecordChanged;
    }

    // ===== 저장·초기화 =====

    /// <summary>
    /// 게임 초기화(<see cref="GameProgressReset"/>). 끝냈다는 기록을 비운다. 튜토리얼은 그 자리에서 다시 시작하지 않고
    /// 다음 실행 때 시작한다(국기님 결정 2026-11-22). 초기화는 튜토리얼 도중에 불릴 수 없어(메뉴 바가 막힘) 풀 제한이 없다.
    /// </summary>
    public void ResetProgress() => Save(completed: false);

    // 쓰기 실패는 로그만 남기고 삼킨다(HeartSystem과 같은 판단).
    private void Save(bool completed)
    {
        try
        {
            UserDataSaveIO.Save(GameDataPaths.Tutorial, new TutorialFileFormat { completed = completed });
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Debug.LogError($"[{nameof(TutorialDirector)}] 튜토리얼 상태 저장 실패: {e.Message}", this);
        }
    }
}
