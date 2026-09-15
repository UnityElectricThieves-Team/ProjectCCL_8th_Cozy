using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 격자의 한 칸. 장식 하나의 그림과 보유 개수만 보여주는 표시 전용 컴포넌트다.
///
/// 상점 슬롯(<see cref="ShopItemSlot"/>)과 달리 버튼이 없다. 기획(Figma '인벤토리' 프레임)에
/// 슬롯 자체를 눌렀을 때의 동작이 없기 때문이다 — 클릭의 의미는 '이동/회수/제거' 모드 버튼을
/// 거쳐서 생기는데, 그 모드들이 기대는 장식 설치·회수 시스템이 아직 코드에 없어 이번에는 만들지 않았다.
/// 그림에 없는 동작을 추측으로 채우지 않으려고 클릭 통로도 열어두지 않았다.
///
/// 슬롯 프리팹(InventorySlot.prefab) 루트에 붙는다.
/// </summary>
public sealed class InventorySlot : MonoBehaviour
{
    [Tooltip("장식 그림을 그릴 Image. 프리팹의 ItemThumbnail.")]
    [SerializeField] private Image _thumbnail;
    [Tooltip("보유 개수를 적을 텍스트. 프리팹의 ItemCount.")]
    [SerializeField] private TMP_Text _countText;

    /// <summary>
    /// 이 칸을 장식 하나로 채운다. <paramref name="count"/>는 그 장식을 몇 개 가지고 있는지다.
    ///
    /// 개수가 1일 때는 숫자를 숨긴다 — 기획이 그렇게 정해 두었다(Figma 인벤토리 프레임의
    /// "1 일때는 숫자 표기 X" 메모). 한 개뿐인 칸에 "1"이 붙으면 눈에 걸리기만 한다.
    /// </summary>
    public void Bind(ShopItemDefinition item, int count)
    {
        if (_thumbnail != null)
        {
            // 그림이 없는 장식은 Image를 꺼서 빈 칸으로 둔다. 스프라이트만 비워두면 Unity가
            // 흰 사각형을 그리는데, 슬롯 배경도 흰색이라 무엇이 잘못됐는지 알아보기 어렵다.
            // 조건 없이 대입하는 것도 중요하다 — 칸을 재사용하므로 건너뛰면 이전 장식 그림이 남는다.
            _thumbnail.sprite = item.icon;
            _thumbnail.enabled = item.icon != null;
        }

        if (_countText != null)
        {
            bool showCount = count > 1;
            _countText.gameObject.SetActive(showCount);
            if (showCount) _countText.text = count.ToString();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_thumbnail == null || _countText == null)
            Debug.LogWarning($"[{nameof(InventorySlot)}] 슬롯 참조(_thumbnail/_countText)가 비어 있음.", this);
    }
#endif
}
