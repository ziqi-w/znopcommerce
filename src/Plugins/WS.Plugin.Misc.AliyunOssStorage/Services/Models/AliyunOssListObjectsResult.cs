namespace WS.Plugin.Misc.AliyunOssStorage.Services.Models;

public record AliyunOssListObjectsResult(bool Succeeded, IReadOnlyCollection<string> ObjectKeys, string ErrorMessage = null)
{
    public static AliyunOssListObjectsResult Success(IReadOnlyCollection<string> objectKeys)
    {
        return new(true, objectKeys ?? Array.Empty<string>());
    }

    public static AliyunOssListObjectsResult Failure(string errorMessage)
    {
        return new(false, Array.Empty<string>(), errorMessage);
    }
}
