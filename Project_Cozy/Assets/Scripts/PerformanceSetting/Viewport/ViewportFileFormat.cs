using System;

/// <summary>
/// 뷰포트(화면 설정)의 저장 데이터 컨테이너. <see cref="HeartFileFormat"/>과 같은 패턴이다.
///
/// RectInt를 그대로 담지 않고 int 넷으로 푼다 — 엔진 구조체의 직렬화 표현에 기대면 저장 파일이
/// Unity 버전에 묶이고, 사람이 열어 고치기도 어려워진다(에디터에서는 평문 JSON으로 저장된다).
///
/// 값은 베이스 공간 px(마스터 캔버스 기준 px, 원점=좌하단, Y 위 방향)다. 폭 기준 배율이라 어느 해상도에서
/// 저장하든 같은 값이 화면상 같은 자리·같은 비율로 복원된다. 다만 베이스 공간의 높이는 작업 영역의
/// 종횡비를 따라 기기마다 다르므로, 불러온 값은 <see cref="ViewportScreenSettings"/>가 현재 베이스 공간에
/// 맞춰 클램프한 뒤 쓴다.
///
/// version 0(필드가 없던 옛 파일)은 작업 영역의 절대 px 단위다. 이 값을 읽는 쪽은 현재 배율로 환산한다.
/// </summary>
[Serializable]
public class ViewportFileFormat
{
    /// <summary>현재 저장 단위의 버전. 1 = 베이스 공간 px(마스터 캔버스 기준).</summary>
    public const int FormatVersion = 1;

    // 일부러 초기화하지 않는다. 형제 FileFormat의 "필드 초기화 = 옛 파일의 기본값" 관례와 반대인데,
    // 여기서는 필드가 없던 옛 파일이 0으로 읽혀야 단위를 구분할 수 있기 때문이다. `= FormatVersion`으로
    // 초기화하면 옛 파일이 전부 새 단위로 읽혀 환산이 조용히 사라진다.
    public int version;
    public int x;
    public int y;
    public int width;
    public int height;
}
