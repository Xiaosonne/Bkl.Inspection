namespace UploadTools
{
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
}