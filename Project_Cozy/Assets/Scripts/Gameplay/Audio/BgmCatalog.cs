using System;
using UnityEngine;

/// <summary>
/// 게임에 들어 있는 배경음악 목록. 게임 내내 바뀌지 않는 정의 데이터만 담는다 —
/// 지금 어떤 곡을 고르고 있는지는 유저 설정(<see cref="SettingsManager"/>)이 곡 id로 든다.
///
/// 첫 항목이 기본 곡이다. 설정 패널 배경음악 드롭다운의 N번째 옵션이 이 목록의 N번째 곡이므로,
/// 드롭다운 옵션(곡 이름 — 프리팹 인스펙터에서 직접 적는다)과 같은 순서를 유지해야 한다.
/// 곡마다 .asset을 나누지 않고 한 에셋에 모은 것은, 이 순서를 한 곳에서 보이게 하려는 것이다.
///
/// 곡은 파일 경로가 아니라 <see cref="AudioClip"/>을 직접 참조한다 — 경로 문자열은 파일을 옮기거나
/// 이름을 바꾸면 조용히 끊기지만, 참조는 Unity가 따라간다.
///
/// 우클릭 Create → Cozy/Audio/Bgm Catalog.
/// </summary>
[CreateAssetMenu(menuName = "Cozy/Audio/Bgm Catalog")]
public sealed class BgmCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        [Tooltip("설정 파일에 저장되는 안정적 식별자. 예: bgm_cozy_morning. 한번 정하면 바꾸지 않는다.")]
        public string id;

        [Tooltip("Assets/Audio/BGM/ 의 음원.")]
        public AudioClip clip;
    }

    [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

    public int Count => _entries.Length;

    public Entry this[int index] => _entries[index];

    /// <summary>
    /// 저장된 곡 id를 실제로 틀 곡으로 바꾼다. id가 비었거나 목록에 없으면(곡이 빠졌거나 파일을 손으로 고친 경우) 첫 곡,
    /// 목록이 비어 있으면 null. "없는 id면 첫 곡" 판단은 여기 한 곳에서만 한다.
    /// </summary>
    public Entry Resolve(string id)
    {
        int index = IndexOf(id);
        return index < 0 ? null : _entries[index];
    }

    /// <summary><see cref="Resolve"/>와 같은 규칙으로 고른 곡의 위치. 목록이 비어 있으면 -1.</summary>
    public int IndexOf(string id)
    {
        if (_entries.Length == 0) return -1;
        if (!string.IsNullOrEmpty(id))
        {
            for (int i = 0; i < _entries.Length; i++)
                if (_entries[i].id == id) return i;
        }
        return 0;
    }
}
