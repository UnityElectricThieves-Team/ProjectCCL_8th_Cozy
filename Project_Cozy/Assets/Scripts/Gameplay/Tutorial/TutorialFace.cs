/// <summary>
/// 튜토리얼 대사창 초상화의 표정. 대사 목록(<see cref="TutorialDirector"/>)이 줄마다 고르고, 대사창(<see cref="TutorialOverlay"/>)이 그림을 바꾼다.
/// 문자열 대신 enum인 것은 오타가 컴파일에 걸리게 하려는 것이다. 저장하지 않는 값이라 순서를 바꿔도 된다.
/// </summary>
public enum TutorialFace
{
    Shock,
    Happy,
}
