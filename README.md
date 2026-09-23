# Nidus Vision

Lightweight self-hosted NVR: .NET 10 API + Angular 21 UI, SQLite WAL, FFmpeg pass-through recording, MSE live view, and optional ONNX/OpenVINO person detection.

## Requirements

- .NET 10 SDK
- Node.js 22+ (Angular CLI is installed locally)
- FFmpeg on `PATH` (ingest, probe, live)
- Optional: Intel iGPU + OpenVINO/ONNX model at `NIDUS_PERSON_MODEL` (default `models/person.onnx`)

## Develop

```bash
dotnet tool restore
dotnet run --project src/NidusVision.Web
```

API: http://localhost:8080 (`/health`, `/api/...`).

```bash
cd src/NidusVision.Client
npm install
npm start
```

UI: http://localhost:4200 (proxies `/api` and `/hubs` to 8080).

First visit `/login` and set an 8+ character admin password.

## Tests

```bash
dotnet test NidusVision.slnx
```

Unit tests cover timeline merge, retention, ROI, detection overlap, event filters, and reconnect backoff.

## CI

GitHub Actions (`.github/workflows/ci.yml`) runs on pushes to `main` and `stack/**`, and on pull requests: Release `dotnet build`/`dotnet test`, Angular production build, and a Docker image build (not pushed).

## Docker

```bash
docker compose -f docker/docker-compose.yml up --build
```

Open http://localhost:8080. Data lives in the `nidus-data` volume; recordings in `nidus-recordings`.

Intel QuickSync / OpenVINO iGPU (Linux host) — uncomment in `docker/docker-compose.yml`:

```yaml
devices:
  - /dev/dri/renderD128:/dev/dri/renderD128
  - /dev/dri/card0:/dev/dri/card0
group_add: ["44", "109"]
environment:
  LIBVA_DRIVER_NAME: iHD
  OPENVINO_DEVICE: GPU
```

## Layout

- `src/NidusVision.Web` — ASP.NET Core host
- `src/NidusVision.Client` — Angular SPA
- `src/NidusVision.Data` — EF Core SQLite
- `src/NidusVision.Streaming` — FFmpeg ingest
- `src/NidusVision.Inference` — ONNX person detector
- `tests/NidusVision.Tests` — xUnit

License: MIT.
