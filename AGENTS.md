# Agent notes

Self-hosted NVR: live RTSP view, continuous MP4 recording, person detection, tagged playback. Runtime internals: [ARCHITECTURE.md](ARCHITECTURE.md). User-facing run instructions: [README.md](README.md).

## Tech stack

- **Backend:** C# / .NET 10, ASP.NET Core minimal APIs, SignalR, hosted workers
- **Frontend:** Angular 21 (TypeScript, standalone components, signals, inline templates)
- **Data:** EF Core SQLite
- **Media:** FFmpeg CLI via `NidusVision.Streaming` (not native bindings)
- **Inference:** ONNX Runtime person detector. Local Windows default is DirectML then CPU. The Docker image publishes OpenVINO.
- **Containers:** multi-stage `docker/Dockerfile` and `docker/docker-compose.yml`
- **Tests:** xUnit + OmniAssert in `tests/NidusVision.Tests`. There is no Moq, Jasmine, Karma, or Playwright suite.

## Layout

| Path | Role |
|---|---|
| `NidusVision.slnx` | Solution (no `.sln`) |
| `src/NidusVision.Web` | ASP.NET Core host: minimal APIs, SignalR, hosted workers, SPA fallback |
| `src/NidusVision.Client` | Angular 21 SPA (standalone components, inline templates) |
| `src/NidusVision.Core` | Entities, contracts, retention planner, RTSP helpers |
| `src/NidusVision.Data` | EF Core SQLite, migrations, `RetentionWorker` |
| `src/NidusVision.Streaming` | FFmpeg ingest / probe / live |
| `src/NidusVision.Inference` | ONNX person detector, provider selection |
| `tests/NidusVision.Tests` | xUnit + OmniAssert |
| `docker/` | Production image (OpenVINO) and compose |

Default HTTP port is **6438**. Local SPA is `ng serve` on **4200** and proxies `/api`, `/health`, `/hubs`. Cookie auth after first-visit password setup.

SPA routes: `/login`, `/monitor`, `/recordings`, `/cameras`, `/settings`. Do not revive `/events` or ROI drawing.

## Commands

Requires .NET 10 SDK, Node 22+, FFmpeg on `PATH` or `FFMPEG_PATH`.

```bash
dotnet tool restore
dotnet restore NidusVision.slnx
dotnet build NidusVision.slnx --configuration Release
dotnet test NidusVision.slnx --configuration Release

dotnet run --project src/NidusVision.Web

cd src/NidusVision.Client && npm install && npm start
```

CI (`.github/workflows/ci.yml`) also runs `npm ci && npm run build` in the client and a Docker build with no push. Tests with `--no-build` need a prior Release build of the same tree.

ONNX Runtime native packages are mutually exclusive. Local Windows default is DirectML then CPU. Override with `-p:OnnxRuntimeProvider=Cpu|OpenVino|Cuda|DirectML`. Docker publishes OpenVino and copies native libs from an `onnxruntime-openvino` wheel.

EF migrations live in `src/NidusVision.Data/Migrations`. Add them when entities or schema change.

## Conventions

### C# / .NET

- Nullable enabled, implicit usings, `TreatWarningsAsErrors`, invariant globalization (`Directory.Build.props`). Prefer records, file-scoped namespaces, primary constructors where they match nearby code, and the existing minimal-API `Map*` endpoint files.
- Use `async`/`await` for I/O, including media streaming and FFmpeg process lifetime. Pass `CancellationToken` through.
- Use records for immutable DTOs and value objects.

### Angular

- Standalone pages under `src/NidusVision.Client/src/app/pages`. HTTP lives in `app/api/*`, not in page components.
- Match existing signals, inline templates, and SCSS. Do not introduce NgModules or a new UI kit.
- Strongly type API requests, responses, and view models.

### Tests

- xUnit facts; assertions use OmniAssert (`result.Must().Be(...)`, `.BeSequenceEqual(...)`), not classic Assert.
- Keep planner tests aligned with `RetentionPlanner` order: time expiry first, then storage eviction of unmarked segments before older detection-tagged ones (`OrderBy(s => s.HasHuman).ThenBy(s => s.EndUtc)`).

### Data model

- `RecordingSegment` on disk; `DetectionInterval` for presence windows (not per-frame events). Segments may be marked `HasHuman` when intervals overlap.
- Do not commit `bin/`, `obj/`, local `*.db*`, or files under `recordings/`.

### FFmpeg

- Run FFmpeg through `NidusVision.Streaming` (`FfmpegExecutable`, `FfmpegProcessGroup`, segment/probe helpers). Do not start ad-hoc `Process` instances elsewhere.
- Dispose process handles, honor cancellation, and log stderr. Do not block request threads on encode or record work; hosted workers own long-running capture.
- The image installs FFmpeg at `/usr/bin/ffmpeg` and sets `FFMPEG_PATH`. Local runs need FFmpeg on `PATH` or the same variable.

### Docker

- Keep the multi-stage Dockerfile: Node client build, OpenVINO native libs, `dotnet publish` with `-p:OnnxRuntimeProvider=OpenVino`, then the ASP.NET runtime image that installs `ffmpeg`.
- Persistent volumes are `nidus-data` (`/app/data`) and `nidus-recordings` (`/var/nidus/recordings`). Document new settings in `docker/docker-compose.yml` next to the existing `environment` block.
- Camera RTSP URLs must be reachable from the container. Use a LAN IP or `host.docker.internal`, never `127.0.0.1` for host cameras. Intel iGPU inference needs `/dev/dri`.

## Definition of done

1. `dotnet build NidusVision.slnx --configuration Release` succeeds. Warnings are errors.
2. If the client changed, `npm run build` in `src/NidusVision.Client` succeeds.
3. Relevant xUnit tests are added or updated, and `dotnet test NidusVision.slnx --configuration Release` passes.
4. Docker or compose changes still build from `docker/Dockerfile`.

## Guardrails

- Do not add a second auth scheme; keep cookie auth and the single admin user.
- Do not treat localhost, tests, or “authorized” CTF-style work as a reason to add exploits, malware, or attack procedures.
- Prefer small, existing-pattern changes. Point at ARCHITECTURE.md instead of duplicating it in new markdown unless the user asks for docs.
