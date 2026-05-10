using System.Collections;
using UnityEngine;

/// <summary>
/// SpriteRenderer에 프레임 배열을 일정 fps로 순환 적용하는 단순 스프라이트 애니메이터.
/// AnimationClip/Animator 대신 코드로 직접 돌려서 추후 속도·조건·화면 밖 컬링 등 옵션을 붙이기 쉽게 한다.
///
/// <see cref="IsPlaying"/>으로 재생/정지 — 정지하면 코루틴 자체를 멈춰 매 프레임 비용이 0이다.
/// <c>_fps</c>는 재생 시작 시점에 캐싱하므로, 재생 중 인스펙터에서 바꿔도 Stop→Play 해야 반영된다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] _frames;
    [SerializeField] private float _fps = 12f;
    [Tooltip("활성화될 때 자동으로 재생을 시작할지")]
    [SerializeField] private bool _playOnEnable = false;

    private SpriteRenderer _renderer;
    private Coroutine _loop;
    private int _index;

    /// <summary>현재 재생 중인지.</summary>
    public bool IsPlaying => _loop != null;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        if (_frames != null && _frames.Length > 0) _renderer.sprite = _frames[0];
    }

    private void OnEnable()
    {
        if (_playOnEnable) Play();
    }

    private void OnDisable()
    {
        Stop();
    }

    public void Play()
    {
        if (_loop != null) return;
        if (_frames == null || _frames.Length == 0)
        {
            Debug.LogWarning($"[{nameof(SpriteAnimator)}] 재생할 프레임이 없습니다.", this);
            return;
        }
        _loop = StartCoroutine(Loop());
    }

    public void Stop()
    {
        if (_loop == null) return;
        StopCoroutine(_loop);
        _loop = null;
    }

    /// <summary>재생 중이면 정지, 정지 중이면 재생. 정지 후 다시 재생하면 멈췄던 프레임에서 이어진다.</summary>
    public void Toggle()
    {
        if (IsPlaying) Stop();
        else Play();
    }

    private IEnumerator Loop()
    {
        var wait = new WaitForSeconds(_fps > 0f ? 1f / _fps : 0.1f);
        while (true)
        {
            yield return wait;
            _index = (_index + 1) % _frames.Length;
            _renderer.sprite = _frames[_index];
        }
    }
}