# Nidus Vision

Lightweight self-hosted NVR: .NET 10 API + Angular 21 UI, SQLite WAL, FFmpeg pass-through recording, MSE live view, and ONNX person detection (CPU).

## Install (Docker)

Docker is the supported way to run Nidus Vision. The image installs FFmpeg (`FFMPEG_PATH=/usr/bin/ffmpeg`) and serves the UI and API together.

```bash
docker compose -f docker/docker-compose.yml up --build
```

Open http://localhost:8080. First visit `/login` and set an 8+ character admin password.

Data lives in the `nidus-data` volume; recordings in `nidus-recordings`. Camera RTSP URLs must be reachable **from the container** (use a LAN IP, not `127.0.0.1`). Streams on the Docker host can use `host.docker.internal`.

Person detection uses `models/person.onnx` (copied into the image when present) or `NIDUS_PERSON_MODEL`. The ingest FFmpeg process samples a low-FPS RGB frame from the same RTSP connection used for 15-minute recordings; detection runs in a background service so a slow model cannot stall capture.

Intel iGPU / VAAPI on a Linux host (optional hardware video decode, not required for person detection) — uncomment in `docker/docker-compose.yml`:

```yaml
devices:
  - /dev/dri/renderD128:/dev/dri/renderD128
  - /dev/dri/card0:/dev/dri/card0
group_add: ["44", "109"]
environment:
  LIBVA_DRIVER_NAME: iHD
```

## Develop

```bash
dotnet tool restore
dotnet run --project src/NidusVision.Web
```

Requires .NET 10 SDK, Node.js 22+, and FFmpeg on `PATH` (or `FFMPEG_PATH`) for ingest, probe, and live. Production installs do not need these on the host; they come from the Docker image.

API: http://localhost:8080 (`/health`, `/api/...`).

```bash
cd src/NidusVision.Client
npm install
npm start
```

UI: http://localhost:4200 (proxies `/api` and `/hubs` to 8080).

## Tests

```bash
dotnet test NidusVision.slnx
```

Unit tests cover timeline merge, retention, ROI, detection overlap, YOLO decoding, recording filters, and reconnect backoff.

## CI

GitHub Actions (`.github/workflows/ci.yml`) runs on pushes to `main` and `stack/**`, and on pull requests: Release `dotnet build`/`dotnet test`, Angular production build, and a Docker image build (not pushed).

## Layout

- `src/NidusVision.Web` — ASP.NET Core host
- `src/NidusVision.Client` — Angular SPA
- `src/NidusVision.Data` — EF Core SQLite
- `src/NidusVision.Streaming` — FFmpeg ingest
- `src/NidusVision.Inference` — ONNX person detector
- `tests/NidusVision.Tests` — xUnit

License: MIT.
