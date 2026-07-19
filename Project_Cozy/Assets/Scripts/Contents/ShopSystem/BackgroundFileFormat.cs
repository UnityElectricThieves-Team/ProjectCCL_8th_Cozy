using System;
using System.Collections.Generic;

/// <summary>
/// 배경의 저장 데이터 컨테이너. <see cref="ShopInventoryFileFormat"/>과 같은 패턴이며,
/// <see cref="BackgroundSystem"/>이 런타임 상태로 그대로 들고 쓴다.
///
/// 장식과 달리 개수가 없다 — 배경은 "하나라도 가졌는가"만 의미가 있어서 id 집합으로 충분하다.
/// 대신 장식에는 없는 상태가 하나 있다: 지금 어느 배경을 쓰고 있는가(<see cref="activeId"/>).
/// 소유를 잃는 것보다 쓰던 배경이 초기화되는 쪽이 눈에 더 잘 띄므로, 이쪽도 같이 저장한다.
///
/// id는 <see cref="ShopItemDefinition.id"/>다. 이름이나 파일 경로가 아니라 손으로 정한 안정적 식별자여서,
/// 정의 에셋의 이름을 바꾸거나 폴더를 옮겨도 저장된 소유가 날아가지 않는다.
/// </summary>
[Serializable]
public class BackgroundFileFormat
{
    public HashSet<string> ownedIds = new();

    /// <summary>사용 중인 배경 id. 빈 문자열이면 활성 배경 없음(기본 상태).</summary>
    public string activeId = string.Empty;
}
