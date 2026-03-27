# Production Deployment

This repository now includes a GitHub Actions based CI and deployment baseline for a `raw Linux + Docker + separate PostgreSQL` setup.

## What was added

- `.github/workflows/ci.yml`
- `.github/workflows/deploy.yml`
- `ops/docker/docker-compose.production.yml`
- `ops/docker/.env.production.example`
- `ops/docker/appsettings.production.example.json`
- `ops/nginx/nopcommerce.conf`

## Recommended topology

- One Linux app server running Docker and Docker Compose
- One separate PostgreSQL server in the same VPC/region
- One reverse proxy on the app server, such as Nginx
- GitHub Actions building and pushing the image to GHCR, then deploying over SSH

This layout fits your current direction better than Baota Panel because it keeps the release path versioned and repeatable.

## Server sizing

- `2 vCPU / 4 GB RAM` is acceptable for a small initial launch if the database is separate and you are not building on the server
- `4 vCPU / 8 GB RAM` is the safer next step once traffic, imports, indexing, or background jobs increase
- Prefer a general-purpose instance over shared or burstable CPU when budget allows

## Database server checklist

- Keep the database private. Allow inbound access only from the app server private IP or security group.
- Keep the database in the same region and ideally the same zone as the app server.
- Enable TLS for app-to-database traffic.
- Enable backups and test a restore before launch.
- If you deploy to PostgreSQL, create `citext` before the initial install or migration:

```sql
CREATE EXTENSION IF NOT EXISTS citext;
```

## App server bootstrap

Install Docker, Docker Compose plugin, and Nginx on the Linux server.

Suggested deployment root:

```bash
sudo mkdir -p /opt/znopcommerce
sudo chown -R "$USER":"$USER" /opt/znopcommerce
```

Create the runtime directories:

```bash
mkdir -p /opt/znopcommerce/appsettings
mkdir -p /opt/znopcommerce/app_data/DataProtectionKeys
mkdir -p /opt/znopcommerce/app_data/TempUploads
mkdir -p /opt/znopcommerce/logs
mkdir -p /opt/znopcommerce/storage/images
mkdir -p /opt/znopcommerce/storage/files
mkdir -p /opt/znopcommerce/storage/db_backups
mkdir -p /opt/znopcommerce/storage/plugins_uploaded
mkdir -p /opt/znopcommerce/nginx
```

Copy `ops/docker/.env.production.example` to `/opt/znopcommerce/.env` and set real values.

Copy `ops/docker/appsettings.production.example.json` to `/opt/znopcommerce/appsettings/appsettings.json` and replace the placeholders with your real PostgreSQL host, database, user, and password.

Important:

- Do not reuse the tracked local development `App_Data/appsettings.json` for production.
- Set `HostingConfig.UseProxy` to `true` in the production appsettings file when you are behind Nginx or another reverse proxy.
- Replace `KnownProxies` or `KnownNetworks` with your real proxy IPs or networks.

## Nginx

Use `ops/nginx/nopcommerce.conf` as the starting point.

Adjust:

- `server_name`
- certificate paths
- upstream port if you change `APP_HTTP_BIND`

## GitHub secrets

Create these GitHub Actions secrets before using `deploy.yml`:

- `DEPLOY_HOST`
- `DEPLOY_PORT`
- `DEPLOY_USER`
- `DEPLOY_SSH_PRIVATE_KEY`
- `GHCR_DEPLOY_USERNAME`
- `GHCR_DEPLOY_TOKEN`

Recommended:

- store the deployment secrets in a GitHub Environment named `production`
- enable manual approvals for that environment

`GHCR_DEPLOY_TOKEN` should have `read:packages`.

## CI

`ci.yml` restores, builds, and runs:

```bash
dotnet test src/Tests/Nop.Tests/Nop.Tests.csproj -c Release
```

This is the release gate. Keep it green before promoting to production.

## Deployment flow

`deploy.yml` does this:

1. builds the Docker image from this repo
2. pushes the image to GHCR
3. syncs deployment assets to the server
4. logs the server into GHCR
5. runs `docker compose pull` and `docker compose up -d`

The workflow expects `/opt/znopcommerce/appsettings/appsettings.json` to already exist on the server. It will stop with a clear error if the file is missing.

## Persistent storage

The production compose file persists:

- `App_Data/DataProtectionKeys`
- `App_Data/TempUploads`
- `Logs`
- `wwwroot/images`
- `wwwroot/files`
- `wwwroot/db_backups`
- `Plugins/Uploaded`

This matters because your current `WS.Plugin.Misc.AliyunOssStorage` plugin offloads thumbnails only. Original media still needs durable app-side storage.

## After first deployment

After the first successful start:

1. finish the nopCommerce install against the production PostgreSQL server
2. confirm proxy settings and HTTPS URLs
3. install and configure the custom plugins
4. verify Latipay callback and return URLs from the public domain
5. rotate the Google Merchant feed token after fixing access control and publishing
6. run a smoke test for checkout, image upload, plugin loading, and admin login

## Current limitations

- the deployment workflow targets a single app server
- the Docker image is built from this repository and assumes Docker is available on the GitHub runner
- scaling to multiple web nodes will require shared or externalized media beyond the current thumbnail-only offload
