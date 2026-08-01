using System;

/// <summary>
/// 뷰포트(화면 설정)의 저장 데이터 컨테이너. <see cref="HeartFileFormat"/>과 같은 패턴이다.
///
/// RectInt를 그대로 담지 않고 int 넷으로 푼다 — 엔진 구조체의 직렬화 표현에 기대면 저장 파일이
/// Unity 버전에 묶이고, 사람이 열어 고치기도 어려워진다(에디터에서는 평문 JSON으로 저장된다).
///
/// 값은 베이스 공간 px(원점=좌하단, Y 위 방향)다. 베이스 공간은 실행 환경의 작업 영역 크기라
/// 기기마다 다르므로, 불러온 값은 <see cref="ViewportScreenSettings"/>가 현재 베이스 공간에
/// 맞춰 클램프한 뒤 쓴다.
/// </summary>
[Serializable]
public class ViewportFileFormat
{
    public int x;
    public int y;
    public int width;
    public int height;
}
