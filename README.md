# Nidus Vision

[![CI](https://github.com/bolorundurowb/nidus-vision/actions/workflows/ci.yml/badge.svg)](https://github.com/bolorundurowb/nidus-vision/actions/workflows/ci.yml)

Self-hosted NVR: live view, continuous recording, and person-tagged playback from RTSP cameras.

![Monitor Center live view with a four-camera grid and recording timeline](docs/monitor.jpg)

![IP cameras page with per-camera status, bitrate, and stream diagnostics](docs/cameras.png)

## Quick start (Docker Hub image)

Prebuilt images are published to [`bolorundurowb/nidus-vision`](https://hub.docker.com/r/bolorundurowb/nidus-vision) on every release tag. `latest` tracks the most recent release; pin a version tag (for example `v1.0.0`) for reproducible deployments.

Requirements: Docker Engine 24+ with Compose v2, one or more RTSP cameras reachable from the Docker host, and enough disk for recordings.

### 1. Create a compose file

Save this as `docker-compose.yml` in an empty directory:

```yaml
services:
  nidus-vision:
    image: bolorundurowb/nidus-vision:latest
    container_name: nidus-vision
    restart: unless-stopped
    ports:
      - "6438:6438"
    extra_hosts:
      - "host.docker.internal:host-gateway"
    # Intel iGPU inference (OpenVINO). Delete `devices` and `group_add` for CPU-only.
    devices:
      - /dev/dri:/dev/dri
    group_add:
      - "44"   # video
      - "109"  # render (check with: getent group render)
    environment:
      TZ: UTC
      Storage__DataDirectory: /app/data
      Storage__RecordingsDirectory: /var/nidus/recordings
      # NIDUS_PERSON_MODEL: /app/models/person.onnx
    volumes:
      - nidus-data:/app/data
      - nidus-recordings:/var/nidus/recordings

volumes:
  nidus-data:
  nidus-recordings:
```

To store recordings on a specific disk, replace the `nidus-recordings` named volume with a bind mount, e.g. `- /mnt/cctv:/var/nidus/recordings`.

### 2. Start the container

```bash
docker compose pull
docker compose up -d
```

Check it is healthy:

```bash
docker compose ps
curl -f http://localhost:6438/health
```

### 3. Set the admin password

Open http://localhost:6438 (or `http://<host-ip>:6438` from another machine). The first visit redirects to `/login`, where you set an admin password (8+ characters). There is a single admin user; the password is stored in the `nidus-data` volume.

### 4. Add cameras

Go to **Cameras** and add each camera with its RTSP URL, for example `rtsp://user:pass@192.168.1.50:554/stream1`.

The URL must be reachable from the **container**:

- Use the camera's LAN IP, not `127.0.0.1` or `localhost`.
- For a stream served by the Docker host itself, use `host.docker.internal` (the compose file maps it to the host gateway).

Once a camera connects, the Cameras page shows its status, bitrate, and stream diagnostics. Recording starts automatically.

### 5. Watch and play back

- **Monitor** shows the live grid and the recording timeline.
- **Recordings** lists segments per camera; segments with a detected person are tagged so you can jump straight to them.
- **Settings** controls retention (separate day limits for general and person-tagged footage, plus an optional storage cap), person detection on/off, sample FPS, and confidence threshold. When the storage cap is hit, untagged footage is evicted before person-tagged footage.

### 6. Update

```bash
docker compose pull
docker compose up -d
```

Data and recordings live in the named volumes and survive image updates. To upgrade to a specific release, change the `image:` tag and rerun the two commands above.

### Notes

- **GPU vs CPU.** The image ships ONNX Runtime with the OpenVINO provider. With `/dev/dri` mapped it runs person detection on an Intel iGPU; without it, detection falls back to CPU. The `group_add` IDs must match the host's `video` and `render` groups (`getent group video render`).
- **Ports.** The app listens on `6438` inside the container. Change the left side of the `ports` mapping to expose it elsewhere.
- **Logs.** `docker compose logs -f nidus-vision`.
- **Reset the admin password.** Stop the container and remove the `nidus-data` volume (`docker volume rm <project>_nidus-data`). This also deletes camera configuration and the recording index, but not the video files in `nidus-recordings`.

## Build from source

```bash
git clone https://github.com/bolorundurowb/nidus-vision.git
cd nidus-vision
docker compose -f docker/docker-compose.yml up --build -d
```

This builds the same multi-stage image locally (Angular client, OpenVINO native libs, `dotnet publish`). Internals, local development without Docker, and tests: [ARCHITECTURE.md](ARCHITECTURE.md).

License: MIT.
