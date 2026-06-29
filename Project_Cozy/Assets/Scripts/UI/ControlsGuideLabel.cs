using TMPro;
using UnityEngine;

/// <summary>
/// 처음 플레이하는 사람을 위한 조작 안내 문구를 TMP 라벨에 한 번 찍는다.
/// 문구는 인스펙터에서 수정 가능하며, 에디터에서는 OnValidate로 즉시 미리보기된다.
/// 정적 텍스트라 매 프레임 갱신은 하지 않는다(프로토타입 임시 가이드).
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class ControlsGuideLabel : MonoBehaviour
{
    [SerializeField] private TMP_Text _label;

    [TextArea(3, 8)]
    [SerializeField]
    private string _text =
        "<b>조작 안내</b>\n" +
        "아무 키나 마우스를 누르면 기운이 쌓여요\n" +
        "별을 클릭하면 친구가 나타나요 (입력 카운트 100이상으로 기운이 충분할 때)\n" +
        "친구를 드래그해서 옮길 수 있어요\n" +
        "친구 위에 마우스를 올리면 쓰다듬어요\n" +
        "아래 버튼으로 친구 크기를 조절해요\n" +
        "많이 쓰다듬어 애정도가 100이 되면 우클릭으로 소녀로 변신해요\n" +
        "소녀일 때 다시 우클릭하면 동물로 돌아가요";

    private void Awake()
    {
        if (_label == null) _label = GetComponent<TMP_Text>();
    }

    private void Start()
    {
        Apply();
    }

    private void Apply()
    {
        if (_label != null) _label.text = _text;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_label == null) _label = GetComponent<TMP_Text>();
        Apply();
    }
#endif
}
