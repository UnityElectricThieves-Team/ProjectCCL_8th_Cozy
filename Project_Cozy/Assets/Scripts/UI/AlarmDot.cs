using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 알림이 있을 때만 보이는 빨간 원. GameObject를 껐다 켜는 대신 <see cref="Image"/>의 알파만
/// 0과 1 사이로 바꾼다. 레이아웃 그룹 안에 있어도 자리를 그대로 차지하므로 옆 요소가 밀리지 않는다.
///
/// 안 보이는 동안에는 <see cref="Graphic.raycastTarget"/>도 같이 꺼서, 투명한 상태로 아래 요소의
/// 클릭을 가로채지 않게 한다.
///
/// 처음에 보일지 말지는 프리팹/인스턴스의 Image Color 알파값이 그대로 초기 상태가 된다.
/// </summary>
[RequireComponent(typeof(Image))]
public sealed class AlarmDot : MonoBehaviour
{
    private Image _image;

    private void Awake() => _image = GetComponent<Image>();

    public void Activate() => SetVisible(true);

    public void Deactivate() => SetVisible(false);

    private void SetVisible(bool visible)
    {
        var color = _image.color;
        color.a = visible ? 1f : 0f;
        _image.color = color;
        _image.raycastTarget = visible;
    }
}
