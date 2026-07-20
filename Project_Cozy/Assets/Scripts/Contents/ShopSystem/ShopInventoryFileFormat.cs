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
/// </summary>
[Serializable]
public class ShopInventoryFileFormat
{
    public Dictionary<string, int> ownedCounts = new();
}
