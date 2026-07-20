using System;
using Assets.Scripts.Contents.CollectionSystem.Model;
using UnityEngine;

/// <summary>
/// 도감 데이터의 사진(<see cref="PhotoData"/>, Base64 문자열)을 화면에 붙일 수 있는
/// <see cref="Sprite"/>로 바꾼다. CollectionTool이 내보낸 collection.dat 안의 사진은
/// 에셋이 아니라 문자열이라, 런타임에 한 번 디코드해야 Image에 물릴 수 있다.
///
/// 여기서 만든 스프라이트는 <b>쓰는 쪽이 수명을 책임진다</b> — 다 쓰면 <see cref="Destroy"/>를
/// 불러 텍스처까지 같이 정리한다. 항상 떠 있는 게임이라 방치하면 그대로 누수가 된다.
/// </summary>
public static class PhotoSprite
{
    /// <summary>사진 하나를 스프라이트로 만든다. 사진이 없거나 데이터가 깨졌으면 null.</summary>
    public static Sprite Create(PhotoData photo)
    {
        if (photo == null || string.IsNullOrEmpty(photo.PhotoBase64)) return null;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(photo.PhotoBase64);
        }
        catch (FormatException)
        {
            Debug.LogWarning($"[{nameof(PhotoSprite)}] Base64가 깨져 있어 사진을 건너뜁니다. PhotoId={photo.PhotoId}");
            return null;
        }

        // LoadImage가 실제 크기·포맷으로 덮어쓰므로 초기 2x2는 자리만 잡는 값이다.
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
        {
            UnityEngine.Object.Destroy(texture);
            Debug.LogWarning($"[{nameof(PhotoSprite)}] 이미지로 디코드하지 못했습니다. PhotoId={photo.PhotoId}");
            return null;
        }

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    /// <summary><see cref="Create"/>가 만든 스프라이트를 텍스처까지 함께 파괴한다.</summary>
    public static void Destroy(Sprite sprite)
    {
        if (sprite == null) return;
        if (sprite.texture != null) UnityEngine.Object.Destroy(sprite.texture);
        UnityEngine.Object.Destroy(sprite);
    }
}
