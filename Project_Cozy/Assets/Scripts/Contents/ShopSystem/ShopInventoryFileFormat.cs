using System;
using System.Collections.Generic;

/// <summary>
/// 상점에서 산 장식의 저장 데이터 컨테이너. <see cref="HeartFileFormat"/>과 같은 패턴.
///
/// 소유를 "샀다/안 샀다"가 아니라 <b>id별 개수</b>로 들고 있다. 개수가 1 이상이면 소유한 것이므로
/// 소유 여부도 이 하나로 표현되고, 같은 장식을 여러 개 두는 기획이 나와도 저장 형태를 바꾸지 않아도 된다.
///
/// key는 <see cref="ShopItemDefinition.id"/> — 이름이나 파일 경로가 아니라 손으로 정한 안정적 식별자다.
/// 정의 에셋의 이름을 바꾸거나 폴더를 옮겨도 저장된 소유가 날아가지 않아야 하기 때문이다.
///
/// 화면에 놓인 장식은 <see cref="placed"/>에 따로 적는다. <see cref="ownedCounts"/>는 그대로 "산 개수 전체"이고,
/// 놓인 장식을 뺀 잔여 개수는 회수 모드가 생길 때 다룬다. 이 필드가 없던 옛 파일은 빈 목록으로 읽힌다.
/// </summary>
[Serializable]
public class ShopInventoryFileFormat
{
    public Dictionary<string, int> ownedCounts = new();

    public List<PlacedDecorationData> placed = new();
}

/// <summary>
/// 화면에 놓인 장식 하나. 지금은 모든 장식이 바닥에 붙으므로 가로 위치만 저장하고, 세로는 불러올 때마다 지면에 맞춘다.
/// x는 베이스 공간 px(마스터 캔버스 기준 px)이고 장식 그림의 가로 중앙이다 — 월드 좌표로 저장하지 않는 이유는
/// .claude/rules/unity/viewport-coordinates.md의 저장 단위 규약을 따른다.
///
/// <c>Vector2</c>로 담지 않는다. 저장 직렬화(Newtonsoft)가 <c>Vector2.normalized</c> 같은 자기 참조 속성을 따라가다
/// 예외를 낸다.
/// </summary>
[Serializable]
public class PlacedDecorationData
{
    public string itemId;
    public float x;
}
