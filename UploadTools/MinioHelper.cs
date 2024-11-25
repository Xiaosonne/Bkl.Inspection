using Minio;
using System.Text;
using System.Text.Json;

namespace UploadTools
{
    public static class MinioHelper
    {
        public static async Task<string> UploadFile(this MinioClient minio, string localStringPath, string objectName, string bucketName)
        {
            using (var stream = new FileStream(localStringPath, FileMode.OpenOrCreate))
            {
                await minio.PutObjectAsync(
                    new PutObjectArgs()
                        .WithObject(objectName)
                        .WithObjectSize(stream.Length)
                        .WithBucket(bucketName)
                        .WithStreamData(stream));
                var url = await minio.PresignedGetObjectAsync(
                     new PresignedGetObjectArgs()
                         .WithBucket(bucketName)
                         .WithObject(objectName)
                         .WithExpiry(600000));
                return url;
            }
        }
        public static async Task UploadStream(this MinioClient minio, Stream stream, string objectName, string bucketName)
        {

            await minio.PutObjectAsync(
                       new PutObjectArgs()
                           .WithObject(objectName)
                           .WithObjectSize(stream.Length)
                           .WithBucket(bucketName)
                           .WithStreamData(stream));
        }
        public static async Task<string> WriteObject<Tobj>(this MinioClient minio, Tobj obj, string objectName, string bucketName)
        {
            var json = JsonSerializer.Serialize(obj);
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                ms.Seek(0, SeekOrigin.Begin);
                await minio.PutObjectAsync(
                  new PutObjectArgs()
                      .WithObject(objectName)
                      .WithObjectSize(ms.Length)
                      .WithBucket(bucketName)
                      .WithStreamData(ms));
                var url = await minio.PresignedGetObjectAsync(
                     new PresignedGetObjectArgs()
                         .WithBucket(bucketName)
                         .WithObject(objectName)
                         .WithExpiry(600000));
                return url;
            }

        }
        public static async Task<Tobj> ReadObject<Tobj>(this MinioClient minio, string objectName, string bucketName)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Seek(0, SeekOrigin.Begin);
                await minio.GetObjectAsync(
                  new GetObjectArgs()
                      .WithObject(objectName)
                      .WithBucket(bucketName)
                      .WithCallbackStream(stream =>
                      {
                          stream.CopyTo(ms);
                          ms.Seek(0, SeekOrigin.Begin);
                      }));
                return JsonSerializer.Deserialize<Tobj>(new StreamReader(ms).ReadToEnd());
            }
        }
        public static async Task CreateBucket(this MinioClient minio, string bucketName)
        {
            var bucketExist = await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName));
            if (!bucketExist)
            {
                await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName));
                await minio.SetPolicyAsync((new SetPolicyArgs())
                    .WithBucket(bucketName)
                    .WithPolicy(JsonSerializer.Serialize(MinioPolicy.Download(bucketName))));
            }
        }
    }
}