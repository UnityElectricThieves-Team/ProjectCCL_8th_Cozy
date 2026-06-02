using UnityEngine;

/// <summary>
/// 게임 내 모든 <see cref="ScaleMultiplier"/> 종류를 모은 ScriptableObject 에셋.
/// 현재는 Character만, 향후 UI/Background 등 같은 패턴으로 필드 추가.
/// </summary>
[CreateAssetMenu(menuName = "Cozy/Scale Multiplier Settings")]
public class ScaleMultiplierSettings : ScriptableObject
{
    [SerializeField] private ScaleMultiplier _character = new ScaleMultiplier();
    public ScaleMultiplier Character => _character;
}
