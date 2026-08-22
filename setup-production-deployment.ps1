$ErrorActionPreference = "Stop"

$RepoRoot = (Get-Location).Path
$DockerDir = Join-Path $RepoRoot "infrastructure\docker"
$SpikeDir = Join-Path $RepoRoot "spikes\SttSpike"

New-Item -ItemType Directory -Path $DockerDir -Force | Out-Null
New-Item -ItemType Directory -Path $SpikeDir -Force | Out-Null

# ---------------------------------------------------------------
# Dockerfile.api
# ---------------------------------------------------------------
$dockerApi = @'
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy backend project files
COPY src/Backend/Academy.Domain/Academy.Domain.csproj src/Backend/Academy.Domain/
COPY src/Backend/Academy.Application/Academy.Application.csproj src/Backend/Academy.Application/
COPY src/Backend/Academy.Infrastructure/Academy.Infrastructure.csproj src/Backend/Academy.Infrastructure/
COPY src/Backend/Academy.Api/Academy.Api.csproj src/Backend/Academy.Api/

RUN dotnet restore src/Backend/Academy.Api/Academy.Api.csproj

COPY src/Backend/ src/Backend/

RUN dotnet publish src/Backend/Academy.Api/Academy.Api.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Academy.Api.dll"]
'@

# ---------------------------------------------------------------
# Dockerfile.dashboard
# ---------------------------------------------------------------
$dockerDashboard = @'
FROM node:20-alpine AS build
WORKDIR /app

COPY src/Dashboard/academy-dashboard/package.json src/Dashboard/academy-dashboard/package-lock.json* ./
RUN npm ci

COPY src/Dashboard/academy-dashboard/ ./
RUN npm run build

FROM node:20-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production

COPY --from=build /app/.next ./.next
COPY --from=build /app/public ./public
COPY --from=build /app/package.json ./package.json
COPY --from=build /app/node_modules ./node_modules

EXPOSE 3000
CMD ["npm", "run", "start"]
'@

# ---------------------------------------------------------------
# Dockerfile.worker
# ---------------------------------------------------------------
$dockerWorker = @'
FROM python:3.12-slim
WORKDIR /app

COPY spikes/SttSpike/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY spikes/SttSpike/qa_worker.py .
CMD ["python", "-u", "qa_worker.py"]
'@

# ---------------------------------------------------------------
# docker-compose.prod.yml
# ---------------------------------------------------------------
$dockerComposeProd = @'
services:
  postgres:
    image: postgres:16-alpine
    container_name: academy-postgres
    restart: unless-stopped
    env_file:
      - .env.production
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ${POSTGRES_DB}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER} -d ${POSTGRES_DB}"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - academy

  redis:
    image: redis:7-alpine
    container_name: academy-redis
    restart: unless-stopped
    volumes:
      - redis_data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - academy

  minio:
    image: minio/minio:latest
    container_name: academy-minio
    restart: unless-stopped
    command: server /data --console-address ":9001"
    env_file:
      - .env.production
    environment:
      MINIO_ROOT_USER: ${MINIO_ROOT_USER}
      MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD}
    volumes:
      - minio_data:/data
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:9000/minio/health/live"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - academy

  api:
    build:
      context: ../..
      dockerfile: infrastructure/docker/Dockerfile.api
    container_name: academy-api
    restart: unless-stopped
    environment:
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
      AgentApiKey: ${AGENT_API_KEY}
      WorkerApiKey: ${WORKER_API_KEY}
      Storage__Endpoint: minio:9000
      Storage__AccessKey: ${MINIO_ROOT_USER}
      Storage__SecretKey: ${MINIO_ROOT_PASSWORD}
      Storage__Bucket: ${MINIO_BUCKET}
      Jwt__Issuer: HomeQuranLearning
      Jwt__Audience: HomeQuranLearning.Dashboard
      Jwt__SigningKey: ${JWT_SIGNING_KEY}
      SeedOwner__FullName: Owner
      SeedOwner__Email: ${SEED_OWNER_EMAIL}
      SeedOwner__Password: ${SEED_OWNER_PASSWORD}
    depends_on:
      postgres:
        condition: service_healthy
      minio:
        condition: service_healthy
    ports:
      - "8080:8080"
    networks:
      - academy

  dashboard:
    build:
      context: ../..
      dockerfile: infrastructure/docker/Dockerfile.dashboard
    container_name: academy-dashboard
    restart: unless-stopped
    environment:
      BACKEND_BASE_URL: http://api:8080
      NODE_ENV: production
    depends_on:
      - api
    ports:
      - "3000:3000"
    networks:
      - academy

  worker:
    build:
      context: ../..
      dockerfile: infrastructure/docker/Dockerfile.worker
    container_name: academy-qa-worker
    restart: unless-stopped
    environment:
      BACKEND_BASE_URL: http://api:8080
      WORKER_API_KEY: ${WORKER_API_KEY}
      HF_HOME: /app/hf_cache
    volumes:
      - worker_hf_cache:/app/hf_cache
    depends_on:
      - api
    networks:
      - academy

volumes:
  postgres_data:
  redis_data:
  minio_data:
  worker_hf_cache:

networks:
  academy:
    driver: bridge
'@

# ---------------------------------------------------------------
# Caddyfile
# ---------------------------------------------------------------
$caddyfile = @'
qa.homequranlearning.com {
    handle /api/* {
        reverse_proxy api:8080
    }

    handle {
        reverse_proxy dashboard:3000
    }

    encode gzip
}
'@

# ---------------------------------------------------------------
# .env.production.example
# ---------------------------------------------------------------
$envProduction = @'
# HomeQuranLearning QA — Production Environment
#
# Copy this file to .env.production and fill with real values.

POSTGRES_USER=academy
POSTGRES_PASSWORD=CHANGE_ME_STRONG_DB_PASSWORD
POSTGRES_DB=homequranlearning_qa

MINIO_ROOT_USER=academy_minio
MINIO_ROOT_PASSWORD=CHANGE_ME_STRONG_MINIO_PASSWORD
MINIO_BUCKET=academy-recordings

AGENT_API_KEY=CHANGE_ME_AGENT_API_KEY
WORKER_API_KEY=CHANGE_ME_WORKER_API_KEY

JWT_SIGNING_KEY=CHANGE_ME_LONG_RANDOM_SECRET_KEY

SEED_OWNER_EMAIL=owner@academy.local
SEED_OWNER_PASSWORD=CHANGE_ME_STRONG_OWNER_PASSWORD
'@

# ---------------------------------------------------------------
# requirements.txt
# ---------------------------------------------------------------
$requirements = @'
faster-whisper==1.2.1
'@

# Write all files
Set-Content -Path (Join-Path $DockerDir "Dockerfile.api") -Value $dockerApi -Encoding UTF8
Set-Content -Path (Join-Path $DockerDir "Dockerfile.dashboard") -Value $dockerDashboard -Encoding UTF8
Set-Content -Path (Join-Path $DockerDir "Dockerfile.worker") -Value $dockerWorker -Encoding UTF8
Set-Content -Path (Join-Path $DockerDir "docker-compose.prod.yml") -Value $dockerComposeProd -Encoding UTF8
Set-Content -Path (Join-Path $DockerDir "Caddyfile") -Value $caddyfile -Encoding UTF8
Set-Content -Path (Join-Path $DockerDir ".env.production.example") -Value $envProduction -Encoding UTF8
Set-Content -Path (Join-Path $SpikeDir "requirements.txt") -Value $requirements -Encoding UTF8

Write-Host ""
Write-Host "Production deployment files created successfully:"
Write-Host "  infrastructure\docker\Dockerfile.api"
Write-Host "  infrastructure\docker\Dockerfile.dashboard"
Write-Host "  infrastructure\docker\Dockerfile.worker"
Write-Host "  infrastructure\docker\docker-compose.prod.yml"
Write-Host "  infrastructure\docker\Caddyfile"
Write-Host "  infrastructure\docker\.env.production.example"
Write-Host "  spikes\SttSpike\requirements.txt"
Write-Host ""
Write-Host "Review the files. Do not commit yet."