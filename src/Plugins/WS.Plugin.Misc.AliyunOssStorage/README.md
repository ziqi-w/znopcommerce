# WS.Plugin.Misc.AliyunOssStorage

## Purpose

`WS.Plugin.Misc.AliyunOssStorage` offloads generated nopCommerce picture thumbnails to Alibaba Cloud OSS.

## Supported scope

- Thumbnail storage only
- Thumbnail URL resolution only
- Thumbnail cleanup only
- Existing local thumbnail migration only

This plugin does **not** offload original images in this phase.

## Required Aliyun OSS settings

- `Endpoint`
- `BucketName`
- `Region`
- `AccessKeyId`
- `AccessKeySecret`

Optional settings:

- `UseHttps`
- `CustomBaseUrl`
- `BaseThumbPathPrefix`
- `DeleteLocalThumbAfterUpload`
- `FallbackToLocalOnFailure`

## Installation

1. Build the plugin project:

   ```bash
   dotnet build src/Plugins/WS.Plugin.Misc.AliyunOssStorage/WS.Plugin.Misc.AliyunOssStorage.csproj
   ```

2. Restart the nopCommerce application if needed.
3. Open the nopCommerce admin plugin list.
4. Install `WS.Plugin.Misc.AliyunOssStorage`.

## Configuration

1. Open the plugin configuration page in nopCommerce admin.
2. Enter the required OSS connection settings.
3. Optionally set `CustomBaseUrl` if thumbnails should be served through a CDN or custom domain.
4. Optionally adjust `BaseThumbPathPrefix`. The default is `thumbs/`.
5. Click `Test connection`.
6. Save the configuration.
7. Enable the plugin when ready.

If you want to backfill existing local thumbnails, use the migration tool after saving valid settings.

## Migration

- The migration tool scans the local `images/thumbs` directory only.
- Thumbnails already present in OSS are skipped.
- Local thumb files are deleted only when `DeleteLocalThumbAfterUpload` is enabled and the current upload succeeds.
- The migration is safe to rerun and can be staged with the batch size field.

## Known limitations

- Original images remain in nopCommerce built-in storage.
- Order attachments are not offloaded.
- Downloadable product files are not offloaded.
- Some downstream nopCommerce code paths still assume a local thumbnail path and may rely on the same behavior as the native external thumbnail plugins.

## Explicit scope note

This plugin is intentionally limited to thumbnails. Original images are **NOT** offloaded in this phase.
