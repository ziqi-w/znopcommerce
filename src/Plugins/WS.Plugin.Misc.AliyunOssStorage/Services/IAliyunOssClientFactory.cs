using Aliyun.OSS;

namespace WS.Plugin.Misc.AliyunOssStorage.Services;

public interface IAliyunOssClientFactory
{
    OssClient CreateClient();

    OssClient CreateClient(AliyunOssStorageSettings settings);
}
