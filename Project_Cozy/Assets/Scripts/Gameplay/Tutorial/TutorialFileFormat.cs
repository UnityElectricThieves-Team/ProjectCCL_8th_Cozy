using System;

/// <summary>
/// 첫 실행 튜토리얼의 저장 데이터. 끝냈는지 하나만 든다.
/// settings 파일에 넣지 않는 이유: [게임 초기화]가 settings를 남기는데, 튜토리얼 상태는 초기화로 되돌려야 한다.
/// 캐릭터 기록에 붙이지 않는 이유: 튜토리얼은 캐릭터 기록이 아니다.
/// 파일이 아예 없는지는 이 형식이 아니라 <see cref="TutorialDirector"/>가 읽기 전에 따로 본다 — 불러오기는 없음과 깨짐을 가리지 않는다.
/// </summary>
[Serializable]
public class TutorialFileFormat
{
    public bool completed;
}
