using System;
using System.Collections.Generic;

/// <summary>
/// <see cref="CharacterOwnership"/>의 저장 데이터 컨테이너. <see cref="ShopInventoryFileFormat"/>과 같은 패턴.
///
/// 캐릭터 하나에 레코드 하나다. 캐릭터는 1종류당 1개만 존재하므로 캐릭터 id가 곧 개체 id이고,
/// 레코드가 있다는 것 자체가 "보유했다"는 뜻이다. 보유 목록과 배치 목록을 따로 들지 않는 이유는,
/// 그러면 "배치됐지만 보유하지 않은" 상태가 형식상 생길 수 있어서다. 캐릭터별 값(친밀도 등)도 이 레코드에 붙인다.
///
/// 기본값(빈 목록)이 곧 첫 실행 상태다 — 아무 캐릭터도 보유하지 않았다.
/// </summary>
[Serializable]
public class CharacterOwnershipFileFormat
{
    public List<CharacterRecord> records = new();
}

/// <summary>보유한 캐릭터 하나의 기록.</summary>
[Serializable]
public class CharacterRecord
{
    /// <summary>캐릭터 id. 손으로 정한 저장 키라 한번 정하면 바꾸지 않는다(.claude/rules/unity/character-ownership.md).</summary>
    public string id;

    /// <summary>화면에 나와 있어야 하는가. false면 보유했지만 회수된 상태다.</summary>
    public bool placed;

    /// <summary>누적 친밀도. 줄어들지 않는다. 이 필드가 없던 옛 파일은 0으로 읽힌다.</summary>
    public int cumulativeAffinity;
}
