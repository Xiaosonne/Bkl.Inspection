using Google.Protobuf.Collections;
using Minio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using static Bkl.Inspection.ImageController;

public class MinioPolicy
{
    public class Statements
    {
        public string Effect { get; set; }
        public Principal Principal { get; set; }
        public string[] Action { get; set; }
        public string[] Resource { get; set; }
    }

    public class Principal
    {
        public string[] AWS { get; set; }
    }

    public string Version { get; set; }
    public Statements[] Statement { get; set; }

    public static MinioPolicy Download(string bucket) => new MinioPolicy
    {
        Version = "2012-10-17",
        Statement = new MinioPolicy.Statements[]
        {
                    new MinioPolicy.Statements
                    {
                        Effect = "Allow",
                        Principal = new Principal { AWS = new string[] { "*" } },
                        Action = new string[] { "s3:GetBucketLocation", "s3:ListBucket" },
                        Resource = new string[] { $"arn:aws:s3:::{bucket}" }
                    },
                    new MinioPolicy.Statements
                    {
                        Effect = "Allow",
                        Principal = new Principal { AWS = new string[] { "*" } },
                        Action = new string[] { "s3:GetObject" },
                        Resource = new string[] { $"arn:aws:s3:::{bucket}/*" }
                    }
        },
    };
}

public class MinioBucketNotification
{
    public string EventName { get; set; }
    public string Key { get; set; }
    public Record[] Records { get; set; }
    public class Record
    {
        public string eventVersion { get; set; }
        public string eventSource { get; set; }
        public string awsRegion { get; set; }
        public DateTime eventTime { get; set; }
        public string eventName { get; set; }
        public Useridentity userIdentity { get; set; }
        public RequestParameters requestParameters { get; set; }
        public ResponseElements responseElements { get; set; }
        public S3 s3 { get; set; }
        public Source source { get; set; }
    }

    public class Useridentity
    {
        public string principalId { get; set; }
    }

    public class RequestParameters
    {
        public string principalId { get; set; }
        public string region { get; set; }
        public string sourceIPAddress { get; set; }
    }
    //{"EventName":"s3:ObjectCreated:Put","Key":"cloud-bucket/DSC_ (151).png","Records":[{"eventVersion":"2.0","eventSource":"minio:s3","awsRegion":"","eventTime":"2023-08-15T08:29:39.944Z","eventName":"s3:ObjectCreated:Put","userIdentity":{"principalId":"minioadmin"},"requestParameters":{ "principalId":"minioadmin","region":"","sourceIPAddress":"192.168.31.173"},"responseElements":{ "content-length":"0","x-amz-request-id":"177B813C03E9E097","x-minio-deployment-id":"ea74984f-6d1e-4be7-86c5-edd96f700049","x-minio-origin-endpoint":"http://192.168.31.173:9030"},"s3":{ "s3SchemaVersion":"1.0","configurationId":"Config","bucket":{ "name":"cloud-bucket","ownerIdentity":{ "principalId":"minioadmin"},"arn":"arn:aws:s3:::cloud-bucket"},"object":{ "key":"DSC_+%28151%29.png","size":3909203,"eTag":"08bb5a137debef8e9e88435f60344bfe","contentType":"image/png","userMetadata":{ "content-type":"image/png"},"sequencer":"177B813C0E3FA944"} },"source":{ "host":"192.168.31.173","port":"","userAgent":"MinIO (linux; amd64) minio-go/v7.0.27"}}]}
    public class ResponseElements
    {
        [JsonPropertyName("content-length")]
        public string ContentLength { get; set; }
        [JsonPropertyName("x-amz-request-id")]
        public string RequestId { get; set; }
        [JsonPropertyName("x-minio-deployment-id")]
        public string DeploymentId { get; set; }
        [JsonPropertyName("x-minio-origin-endpoint")]
        public string OriginEndPoint { get; set; }
    }

    public class S3
    {
        public string s3SchemaVersion { get; set; }
        public string configurationId { get; set; }
        public Bucket bucket { get; set; }
        [JsonPropertyName("object")]
        public BucketObject bucketObject { get; set; }
    }

    public class Bucket
    {
        public string name { get; set; }
        public Owneridentity ownerIdentity { get; set; }
        public string arn { get; set; }
    }

    public class Owneridentity
    {
        public string principalId { get; set; }
    }

    public class BucketObject
    {
        public string key { get; set; }
        public int size { get; set; }
        public string eTag { get; set; }
        public string contentType { get; set; }
        public UserMetadata userMetadata { get; set; }
        public string sequencer { get; set; }
    }

    public class UserMetadata
    {
        public string contenttype { get; set; }
    }

    public class Source
    {
        public string host { get; set; }
        public string port { get; set; }
        public string userAgent { get; set; }
    }
}

public static class MinioHelper
{
    public static async Task UploadStream(this MinioClient minio, Stream stream, string objectName, string bucketName, Dictionary<string, string> headers = null, Dictionary<string, string> tags = null)
    {
        var args = new PutObjectArgs()
                       .WithObject(objectName)
                       .WithObjectSize(stream.Length)
                       .WithBucket(bucketName)
                       .WithStreamData(stream);
        if (headers != null)
            args = args.WithHeaders(headers);
        if (tags != null)
            args = args.WithTagging(new Minio.DataModel.Tags.Tagging(tags, false));

        await minio.PutObjectAsync(args);
    }
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

        try
        {
            using MemoryStream ms = new MemoryStream();
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
        catch (Exception e)
        {
            return default(Tobj);
        }

    }
    public static async Task<Stream> ReadStream(this MinioClient minio, string objectName, string bucketName, Func<Stream, Stream> convert)
    {
        try
        {
            TaskCompletionSource<Stream> taskCompletionSource = new TaskCompletionSource<Stream>();
            MemoryStream ret = new MemoryStream();
            var state = await minio.GetObjectAsync(
                new GetObjectArgs()
                    .WithObject(objectName)
                    .WithBucket(bucketName)
                    .WithCallbackStream(stream =>
                    {
                        taskCompletionSource.SetResult(convert(stream));
                    }));

            return await taskCompletionSource.Task;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    public static async Task<Stream> ReadStream(this MinioClient minio, string objectName, string bucketName)
    {
        try
        {
            TaskCompletionSource<Stream> taskCompletionSource = new TaskCompletionSource<Stream>();
            MemoryStream ret = new MemoryStream();
            var state = await minio.GetObjectAsync(
                new GetObjectArgs()
                    .WithObject(objectName)
                    .WithBucket(bucketName)
                    .WithCallbackStream(stream =>
                    {
                        stream.CopyTo(ret);
                    }));

            return ret;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return null;
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
    public static Task<List<string>> ListObjects(this MinioClient minio, string bucket, string prefix)
    {
        var lisobj = new ListObjectsArgs().WithBucket(bucket)
                .WithPrefix(prefix);
        var observable = minio.ListObjectsAsync(lisobj);
        List<string> list = new List<string>();
        TaskCompletionSource<List<string>> taskCompletionSource = new TaskCompletionSource<List<string>>();
        observable.Subscribe(p =>
        {
            if (p.Key.StartsWith(prefix))
            {
                list.Add(p.Key);
            }
        },
        (err) =>
        {
            taskCompletionSource.SetResult(list);
        },
        () =>
        {
            taskCompletionSource.SetResult(list);
        });

        return taskCompletionSource.Task;
    }
    public static Task<bool> ObjectExists(this MinioClient minio, string bucket, string objectName, string etag = null
        )
    {
        var lisobj = new ListObjectsArgs().WithBucket(bucket)
                .WithPrefix(objectName);
        var observable = minio.ListObjectsAsync(lisobj);
        bool exists = false;
        TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();
        observable.Subscribe(p =>
        {
            if (p.Key == objectName && (etag == null || p.ETag == etag))
            {
                exists = true;
            }
        }, () =>
        {
            taskCompletionSource.SetResult(exists);
        });

        return taskCompletionSource.Task;
    }
}
