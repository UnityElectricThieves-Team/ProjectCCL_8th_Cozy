using UnityEngine;

/// <summary>
/// SpriteRenderer의 flipX/flipY를 렌더러 로컬 좌표에 적용한다.
/// flip은 transform이 아니라 렌더러 옵션이라, 렌더러 로컬 좌표를 그림 기준 좌표로 바꿀 때(또는 그 반대)
/// 따로 뒤집어야 한다. 그림 좌표는 pivot 기준이므로 부호만 바꾸면 된다.
/// <see cref="OpaqueHoverable"/>(커서 → 그림)과 <see cref="SpritePhysicsShapeSync"/>(외곽선 → 콜라이더)가 같이 쓴다.
/// </summary>
public static class SpriteFlip
{
    public static Vector2 Apply(Vector2 local, bool flipX, bool flipY)
    {
        if (flipX) local.x = -local.x;
        if (flipY) local.y = -local.y;
        return local;
    }
}
