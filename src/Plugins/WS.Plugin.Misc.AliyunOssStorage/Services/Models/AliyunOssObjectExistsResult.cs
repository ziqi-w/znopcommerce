namespace WS.Plugin.Misc.AliyunOssStorage.Services.Models;

public record AliyunOssObjectExistsResult(bool Succeeded, bool Exists, string ErrorMessage = null)
{
    public static AliyunOssObjectExistsResult Success(bool exists)
    {
        return new(true, exists);
    }

    public static AliyunOssObjectExistsResult Failure(string errorMessage)
    {
        return new(false, false, errorMessage);
    }
}
