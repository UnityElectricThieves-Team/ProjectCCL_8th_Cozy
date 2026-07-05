using System;

/// <summary>
/// 하트 재화의 저장 데이터 컨테이너. 미래 통합 저장 시스템이 <see cref="HeartSystem.ExportSave"/>/
/// <see cref="HeartSystem.ImportSave"/>로 주고받는 직렬화 타입.
/// 지금은 보유량 하나지만, 필드를 묶어 확장할 여지를 위해 타입으로 둔다.
/// </summary>
[Serializable]
public class HeartFileFormat
{
    public int hearts;
}
