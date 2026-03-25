using Nop.Core.Configuration;

namespace WS.Plugin.Misc.AliyunOssStorage;

public class AliyunOssStorageSettings : ISettings
{
    public bool Enabled { get; set; }

    public string Endpoint { get; set; }

    public string BucketName { get; set; }

    public string Region { get; set; }

    public string AccessKeyId { get; set; }

    public string AccessKeySecret { get; set; }

    public bool UseHttps { get; set; }

    public string CustomBaseUrl { get; set; }

    public string BaseThumbPathPrefix { get; set; }

    public bool DeleteLocalThumbAfterUpload { get; set; }

    public bool FallbackToLocalOnFailure { get; set; }
}
