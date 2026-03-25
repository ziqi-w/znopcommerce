namespace WS.Plugin.Misc.AliyunOssStorage.Services.Models;

public record AliyunOssOperationResult(bool Succeeded, string ErrorMessage = null)
{
    public static AliyunOssOperationResult Success()
    {
        return new(true);
    }

    public static AliyunOssOperationResult Failure(string errorMessage)
    {
        return new(false, errorMessage);
    }
}
