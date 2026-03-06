Absolutely, Zach. Here’s the setup I’d use on your Mac for the best debug experience:

* **Run nopCommerce locally with `dotnet run`**
* **Run PostgreSQL in Docker**
* **Use the nopCommerce 4.90.3 source package or source repo**
* **Connect through the nopCommerce install screen to the Docker Postgres DB**

nopCommerce 4.90 is based on **.NET 9**, and nopCommerce officially supports **PostgreSQL** alongside SQL Server and MySQL. For developers editing source code, nopCommerce also requires the **.NET 9 SDK**. ([nopCommerce Documentation][1])

## 1) What to install on your Mac

Install these first:

* **Homebrew** if you do not already have it
* **Git**
* **.NET 9 SDK**
* **Docker Desktop for Mac**
* Optional but useful: **VS Code** or **JetBrains Rider**

nopCommerce 4.90 development requires the .NET 9 SDK, and the official PostgreSQL Docker image is a standard way to run PostgreSQL locally with configurable `POSTGRES_USER`, `POSTGRES_PASSWORD`, and `POSTGRES_DB` values. ([nopCommerce Documentation][2])

Recommended install commands:

```bash
brew install git
brew install --cask docker
brew install --cask visual-studio-code
```

For **.NET 9 SDK**, install it from Microsoft’s official .NET 9 download page, then verify:

```bash
dotnet --version
```

You should see a **9.x** version. nopCommerce’s official docs say 4.90 is based on .NET 9. ([nopCommerce Documentation][1])

## 2) Start PostgreSQL in Docker

First make sure Docker Desktop is installed and running.

Then run PostgreSQL with a named volume so your data survives container restarts:

```bash
docker run --name nop-postgres \
  -e POSTGRES_USER=nopuser \
  -e POSTGRES_PASSWORD=StrongLocalPassword123! \
  -e POSTGRES_DB=nopcommerce_dev \
  -p 5432:5432 \
  -v nop_postgres_data:/var/lib/postgresql/data \
  -d postgres:17
```

The official PostgreSQL image supports `POSTGRES_USER`, `POSTGRES_PASSWORD`, and `POSTGRES_DB`, and Docker notes that initialization variables only take effect when the data directory is empty, which matters if you later recreate the container but keep the same volume. ([Docker Hub][3])

To confirm the container is running:

```bash
docker ps
```

To test the DB quickly:

```bash
docker exec -it nop-postgres psql -U nopuser -d nopcommerce_dev
```

If that opens `psql`, your database is ready.

## 3) Get nopCommerce 4.90.3 source

Since you want to develop and debug, use the **4.90.3 source package** or the matching source branch/tag rather than a no-source deployment package. The GitHub releases page explicitly provides `nopCommerce_4.90.3_Source.zip` for developers planning to customize nopCommerce. ([GitHub][4])

You have two clean options.

### Option A: download the 4.90.3 source zip

Download the source package from the nopCommerce GitHub releases page and unzip it into a folder such as:

```bash
~/dev/nopcommerce-4.90.3
```

### Option B: clone the repo

If you prefer Git from the start:

```bash
git clone https://github.com/nopSolutions/nopCommerce.git
cd nopCommerce
git checkout release-4.90.3
```

If the exact checkout name differs in the repo, use the 4.90.3 source release package instead, which is the safest path for matching that exact version. The releases page clearly lists the 4.90.3 source asset. ([GitHub][4])

## 4) Restore and run nopCommerce

Open Terminal in the nopCommerce source folder and restore packages:

```bash
dotnet restore
```

Then go to the web project and run it. In most source layouts this is the web app folder:

```bash
cd src/Presentation/Nop.Web
dotnet run
```

Because you are running locally for development, `dotnet run` is the simplest choice and gives you the easiest debugging workflow. nopCommerce’s local installation docs are aimed at installing and running the app in a local environment, while the development docs point developers to the .NET SDK-based workflow. ([nopCommerce Documentation][5])

When the app starts, it will print local URLs such as:

```text
http://localhost:5000
https://localhost:5001
```

Open the URL in your browser.

## 5) Complete the nopCommerce install screen

On first run, nopCommerce should show its installation page. The nopCommerce installation docs state that you can choose **Microsoft SQL Server, MySQL, or PostgreSQL**, and they recommend creating the database and user beforehand for a smoother installation. ([nopCommerce Documentation][6])

Use values like these:

```text
Database: PostgreSQL
Server name: localhost
Port: 5432
Database name: nopcommerce_dev
User: nopuser
Password: StrongLocalPassword123!
```

If there is an option like “create database if it doesn’t exist,” you usually do not need it here because the Docker container already created `nopcommerce_dev`.

Then fill in your admin account details and continue.

## 6) A cleaner way: use Docker Compose for PostgreSQL

If you want something easier to restart later, use Compose instead of a long `docker run` command.

Create a file called `docker-compose.postgres.yml`:

```yaml
services:
  postgres:
    image: postgres:17
    container_name: nop-postgres
    restart: unless-stopped
    environment:
      POSTGRES_USER: nopuser
      POSTGRES_PASSWORD: StrongLocalPassword123!
      POSTGRES_DB: nopcommerce_dev
    ports:
      - "5432:5432"
    volumes:
      - nop_postgres_data:/var/lib/postgresql/data

volumes:
  nop_postgres_data:
```

Start it with:

```bash
docker compose -f docker-compose.postgres.yml up -d
```

This uses the same official PostgreSQL image and the same environment variables supported by that image. ([Docker Hub][3])

## 7) Daily developer workflow

Once installed, your normal workflow becomes:

Start PostgreSQL:

```bash
docker start nop-postgres
```

Or if using Compose:

```bash
docker compose -f docker-compose.postgres.yml up -d
```

Then start nopCommerce:

```bash
cd src/Presentation/Nop.Web
dotnet run
```

Stop Postgres when done:

```bash
docker stop nop-postgres
```

That keeps the app local for easy debugging while the DB stays isolated.

## 8) Things you might overlook

A few items are easy to miss.

### HTTPS certificate warning on Mac

The first time you run an ASP.NET app locally, HTTPS can complain about the dev certificate. If needed, run:

```bash
dotnet dev-certs https --trust
```

This is not nopCommerce-specific, but it often trips people up on first local launch.

### The Docker volume preserves old DB state

If you change `POSTGRES_DB`, `POSTGRES_USER`, or `POSTGRES_PASSWORD` later, those changes may not apply because the PostgreSQL image only initializes them when the data directory is empty. If you want a fresh database, remove the volume too. Docker’s official image docs explicitly warn about this. ([Docker Hub][3])

Fresh reset:

```bash
docker rm -f nop-postgres
docker volume rm nop_postgres_data
```

Then recreate the container.

### Keep secrets out of source control

nopCommerce uses configuration sources such as `appsettings.json`, environment-specific files, and user secrets. The docs explicitly mention `User Secrets` as a configuration source for sensitive settings. So do not hardcode real passwords or API keys into committed config files. ([nopCommerce Documentation][7])

### Use the source package, not the no-source package

The GitHub release page separates the **source** package for developers from the **no-source** deployment packages. Since you want to debug and customize, use the source package. ([GitHub][4])

### Pick one DB engine and stay consistent

If local is PostgreSQL, keep staging and production on PostgreSQL too. nopCommerce supports all three databases, but mixing engines across environments creates avoidable differences in behavior and troubleshooting. ([nopCommerce Documentation][1])

### Plugin-first customization

nopCommerce’s extension model is plugin-oriented, and that matters a lot for future upgrades. Try to keep business logic in plugins rather than patching core files. ([nopCommerce Documentation][8])

### File/media storage is separate from DB

Your database is only one part. Product images, imports, exports, logs, and generated files live outside the DB unless you intentionally redesign storage. Plan for that early, even in local dev.

### Apple Silicon image compatibility

On newer Macs, most official images work well, but if an image ever pulls under emulation, it can be slower. PostgreSQL is usually much less painful on Mac than trying to force a local SQL Server-style setup.

## 9) My recommended exact setup summary

Use this stack locally:

```text
Mac
├─ .NET 9 SDK
├─ Git
├─ Docker Desktop
├─ PostgreSQL 17 in Docker
└─ nopCommerce 4.90.3 source running with dotnet run
```

Use these DB values:

```text
Host: localhost
Port: 5432
Database: nopcommerce_dev
Username: nopuser
Password: StrongLocalPassword123!
```

## 10) Copy-paste quick start

If you want the shortest path:

```bash
# 1. Start PostgreSQL
docker run --name nop-postgres \
  -e POSTGRES_USER=nopuser \
  -e POSTGRES_PASSWORD=StrongLocalPassword123! \
  -e POSTGRES_DB=nopcommerce_dev \
  -p 5432:5432 \
  -v nop_postgres_data:/var/lib/postgresql/data \
  -d postgres:17

# 2. Get nopCommerce source
git clone https://github.com/nopSolutions/nopCommerce.git
cd nopCommerce

# 3. Switch to the 4.90 branch/release if using git, or use the 4.90.3 source zip instead
# git checkout release-4.90.3

# 4. Restore
dotnet restore

# 5. Run the web app
cd src/Presentation/Nop.Web
dotnet run
```

Then open the local URL and complete the installer with PostgreSQL values above. The nopCommerce release page provides the 4.90.3 source package for developers, and nopCommerce 4.90 requires .NET 9 with PostgreSQL as a supported DB option. ([GitHub][4])

I can turn this into a **Mac-specific checklist with exact folder paths and a ready-to-use `docker-compose.yml` plus local run notes**.

[1]: https://docs.nopcommerce.com/en/installation-and-upgrading/technology-and-system-requirements.html?utm_source=chatgpt.com "Technology & system requirements"
[2]: https://docs.nopcommerce.com/en/developer/tutorials/system-requirements-for-developing.html?utm_source=chatgpt.com "System Requirements for Development"
[3]: https://hub.docker.com/_/postgres?utm_source=chatgpt.com "postgres - Official Image"
[4]: https://github.com/nopSolutions/nopCommerce/releases?utm_source=chatgpt.com "Releases · nopSolutions/nopCommerce"
[5]: https://docs.nopcommerce.com/en/installation-and-upgrading/installing-nopcommerce/installing-local.html?utm_source=chatgpt.com "Installing local"
[6]: https://docs.nopcommerce.com/en/installation-and-upgrading/installing-nopcommerce/installing-on-windows.html?utm_source=chatgpt.com "Installing on Windows"
[7]: https://docs.nopcommerce.com/en/developer/tutorials/appsettings-json-file.html?q=Cache&utm_source=chatgpt.com "Settings in appsettings.json"
[8]: https://docs.nopcommerce.com/en/developer/tutorials/instruction-on-how-to-start-developing-on-nopcommerce.html?utm_source=chatgpt.com "Getting Started with nopCommerce Development"
