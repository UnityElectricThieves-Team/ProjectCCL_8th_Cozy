using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 슬라이더에서 손을 뗄 때 울린다. uGUI Slider는 값이 바뀔 때의 onValueChanged만 있고 손 뗌 이벤트가 없어서 둔다.
/// 값은 끄는 동안 바로 반영하고, 저장처럼 무거운 일은 손 뗄 때 한 번만 하려는 용도다.
///
/// 내장 EventTrigger로도 손 뗌을 받을 수 있지만, EventTrigger는 모든 이벤트 인터페이스를 구현해서
/// 슬라이더 위의 마우스 휠이 스크롤 뷰로 올라가지 않게 막는다. 그래서 손 뗌 하나만 받는다.
///
/// **Slider가 붙은 오브젝트에 붙인다.** 손 뗌은 처음 눌린 오브젝트로만 가고, 그 오브젝트가 Slider다.
/// </summary>
[RequireComponent(typeof(Slider))]
public sealed class SliderReleaseEvent : MonoBehaviour, IPointerUpHandler
{
    [SerializeField] private UnityEvent _onReleased = new();

    public void OnPointerUp(PointerEventData eventData) => _onReleased.Invoke();
}
