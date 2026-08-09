using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SiteWatch.Core.Storage;

namespace SiteWatch.Infra.Storage;

public class R2ScreenshotStore : IScreenshotStore
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;
    private readonly ILogger<R2ScreenshotStore> _logger;

    public R2ScreenshotStore(IConfiguration configuration, ILogger<R2ScreenshotStore> logger)
    {
        _logger = logger;

        var accessKeyId = configuration["R2:AccessKeyId"]
            ?? throw new InvalidOperationException("R2:AccessKeyId configuration is required.");
        var secretAccessKey = configuration["R2:SecretAccessKey"]
            ?? throw new InvalidOperationException("R2:SecretAccessKey configuration is required.");
        var endpoint = configuration["R2:Endpoint"]
            ?? throw new InvalidOperationException("R2:Endpoint configuration is required.");
        _bucketName = configuration["R2:BucketName"]
            ?? throw new InvalidOperationException("R2:BucketName configuration is required.");

        var config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            // R2 has no AWS regions; "auto" is Cloudflare's documented pseudo-region
            // for SigV4 signing. This is AuthenticationRegion, not RegionEndpoint —
            // RegionEndpoint requires a recognized AWS region name and "auto" isn't one.
            AuthenticationRegion = "auto",
            // --- R2 compatibility fixes below. Do not remove as "unnecessary AWS
            // tuning" without confirming R2 has added the corresponding support. ---
            //
            // 1. AWSSDK.S3 v4 defaults both of these to WHEN_SUPPORTED, which makes
            // the SDK proactively encode uploads as STREAMING-AWS4-HMAC-SHA256-
            // PAYLOAD-TRAILER (a streaming checksum trailer). R2 does not implement
            // that encoding and rejects the upload with "not implemented".
            // WHEN_REQUIRED tells the SDK to only add checksums when an operation
            // actually demands them, which PutObject does not.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };

        _s3 = new AmazonS3Client(new BasicAWSCredentials(accessKeyId, secretAccessKey), config);
    }

    public async Task<string?> UploadAsync(byte[] bytes, Guid siteId, CancellationToken ct)
    {
        var key = $"screenshots/{siteId}/{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}.png";

        try
        {
            using var stream = new MemoryStream(bytes);
            await _s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = stream,
                ContentType = "image/png",
                // 2. Fixing the checksum trailer (above) only stopped the SDK adding
                // a trailer — it still signed the body as chunked
                // STREAMING-AWS4-HMAC-SHA256-PAYLOAD, which R2 also rejects.
                // UseChunkEncoding = false sends the body as a single, non-chunked
                // request; DisablePayloadSigning = true switches to UNSIGNED-PAYLOAD
                // instead of a computed body signature (safe here because the R2
                // endpoint is HTTPS, so TLS still covers transport integrity).
                // Both properties are unchanged from AWSSDK v3 — v4 did not rename
                // or move them.
                UseChunkEncoding = false,
                DisablePayloadSigning = true
            }, ct);

            return key;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to upload screenshot for site {SiteId}", siteId);
            return null;
        }
    }

    public async Task<string?> GetViewUrlAsync(string key, CancellationToken ct)
    {
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddHours(1)
            };

            // GetPreSignedURLAsync takes no CancellationToken — confirmed against
            // the installed 4.0.102 assembly, unlike PutObjectAsync.
            return await _s3.GetPreSignedURLAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate a presigned URL for key {Key}", key);
            return null;
        }
    }
}
