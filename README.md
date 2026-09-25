# Nidus Vision

Lightweight self-hosted NVR: .NET 10 API + Angular 21 UI, SQLite WAL, FFmpeg pass-through recording, MSE live view, and GPU-first ONNX person detection.

## Install (Docker)

Docker is the supported way to run Nidus Vision. The image installs FFmpeg (`FFMPEG_PATH=/usr/bin/ffmpeg`) and serves the UI and API together.

```bash
docker compose -f docker/docker-compose.yml up --build
```

Open http://localhost:8080. First visit `/login` and set an 8+ character admin password.

Data lives in the `nidus-data` volume; recordings in `nidus-recordings`. Camera RTSP URLs must be reachable **from the container** (use a LAN IP, not `127.0.0.1`). Streams on the Docker host can use `host.docker.internal`.

Person detection uses `models/person.onnx` (copied into the image when present) or `NIDUS_PERSON_MODEL`. The ingest FFmpeg process samples a low-FPS RGB frame from the same RTSP connection used for 15-minute recordings; detection runs in a background service so a slow model cannot stall capture.

The Docker image is built with the OpenVINO execution provider and maps `/dev/dri` into the container for Intel iGPU inference. If OpenVINO cannot initialize the GPU, Nidus Vision logs the failure and retries on CPU. Ensure the Docker host exposes `/dev/dri`; remove the `devices` entry only when intentionally running CPU-only.

The legacy `nidus-events` volume is mounted for one upgrade so old extracted event clips and thumbnails can be deleted. After one successful start, that volume and its `Storage__EventsDirectory` compatibility setting can be removed.

## Develop

```bash
dotnet tool restore
dotnet run --project src/NidusVision.Web
```

Requires .NET 10 SDK, Node.js 22+, and FFmpeg on `PATH` (or `FFMPEG_PATH`) for ingest, probe, and live. Windows development builds use DirectML first and fall back to CPU. Production installs do not need these on the host; they come from the Docker image.

Execution-provider builds are mutually exclusive because ONNX Runtime variants contain conflicting native libraries:

```bash
dotnet test NidusVision.slnx
dotnet run --project src/NidusVision.Web
# Windows default: DirectML then CPU

dotnet test NidusVision.slnx -p:OnnxRuntimeProvider=Cpu
dotnet run --project src/NidusVision.Web -p:OnnxRuntimeProvider=Cpu

dotnet publish src/NidusVision.Web/NidusVision.Web.csproj -c Release -p:OnnxRuntimeProvider=OpenVino
# Docker default: OpenVINO GPU via /dev/dri, then CPU

dotnet publish src/NidusVision.Web/NidusVision.Web.csproj -c Release -p:OnnxRuntimeProvider=Cuda
# optional NVIDIA CUDA then CPU

dotnet publish src/NidusVision.Web/NidusVision.Web.csproj -c Release -p:OnnxRuntimeProvider=DirectML
```

`group_add` GIDs `44`/`109` in `docker/docker-compose.yml` should match the host `video`/`render` groups (`ls -l /dev/dri`).

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
