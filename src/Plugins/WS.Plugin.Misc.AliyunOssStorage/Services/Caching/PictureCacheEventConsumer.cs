using Nop.Core.Domain.Media;
using Nop.Services.Caching;

namespace WS.Plugin.Misc.AliyunOssStorage.Services.Caching;

public class PictureCacheEventConsumer : CacheEventConsumer<Picture>
{
    protected override async Task ClearCacheAsync(Picture entity)
    {
        await RemoveByPrefixAsync(AliyunOssStorageDefaults.ThumbsExistsPrefix);
    }
}
