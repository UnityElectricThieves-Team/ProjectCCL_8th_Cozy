using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Contents.CollectionSystem.Model
{
    /// <summary>
    /// 사진 데이터
    /// </summary>
    public class PhotoData
    {
        public Guid PhotoId { get; set; } = Guid.NewGuid();
        public string PhotoBase64 { get; set; } = string.Empty;
    }
    /// <summary>
    /// 도감 내의 회상 데이터
    /// </summary>
    public class MemoryData
    {
        //일단 사진 Base 64만 사용하자
        public PhotoData MemoryPhoto { get; set; } = null;
        public string Description = string.Empty;
    }
    /// <summary>
    /// 도감 내의 사진첩 데이터
    /// </summary>
    public class PhotoBookData
    {
        //사진 Base64
        public List<PhotoData> PhotoList { get; set; } = new List<PhotoData>();
        public void AddPhoto(PhotoData photo)
        {
            if(photo == null)
            {
                throw new ArgumentNullException(nameof(photo), "Photo cannot be null.");
            }
            if(photo.PhotoBase64 == null || photo.PhotoBase64.Length == 0)
            {
                throw new ArgumentException("PhotoBase64 cannot be null or empty.", nameof(photo));
            }
            if(photo.PhotoId == Guid.Empty)
            {
                throw new ArgumentException("PhotoId cannot be empty.", nameof(photo));
            }
            if(PhotoList.Any(p => p.PhotoId == photo.PhotoId))
            {
                throw new InvalidOperationException($"A photo with the same PhotoId already exists in the PhotoList. PhotoId: {photo.PhotoId}");
            }
            if(PhotoList.Any(p => p.PhotoBase64 == photo.PhotoBase64))
            {
                throw new InvalidOperationException("A photo with the same PhotoBase64 already exists in the PhotoList.");
            }

            PhotoList.Add(photo);
        }
        public void RemovePhoto(Guid photoId)
        {
            var photoToRemove = PhotoList.FirstOrDefault(p => p.PhotoId == photoId);
            if(photoToRemove == null)
            {
                throw new KeyNotFoundException($"No photo found with the specified PhotoId: {photoId}");
            }
            PhotoList.Remove(photoToRemove);
        }
    }
    /// <summary>
    /// 도감 내의 Collection 데이터 구조
    /// </summary>
    public class CollectionData
    {
        public Guid CollectionId { get; set; } = Guid.NewGuid();
        public bool IsCollected { get; set; } = false;
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; } = 0;
        public int Height { get; set; } = 0;
        public DateTime Birthday { get; set; } = DateTime.MinValue;
        public string Hobby { get; set; } = string.Empty;
        // 모으기 전 보여줄 사진
        public PhotoData ProfilePictureBase64_Main_Hidden { get; set; } = null;
        public PhotoData ProfilePictureBase64_Sub_Hidden { get; set; } = null;
        //모은 후 보여줄 사진
        public PhotoData ProfilePictureBase64_Main { get; set; } = null;
        public PhotoData ProfilePictureBase64_Sub { get; set; } = null;
        public PhotoBookData PhotoBook { get; set; } = new PhotoBookData();
        public MemoryData Memory { get; set; } = new MemoryData();
    }

    /// <summary>
    /// Collection 데이터들을 묶은 데이터 - 최종 Json데이터
    /// </summary>
    public class CollectionBoolData
    {
        public List<CollectionData> collectionDataList { get; set; } = new List<CollectionData>();
    }
}
