# Nidus Vision

[![CI](https://github.com/bolorundurowb/nidus-vision/actions/workflows/ci.yml/badge.svg)](https://github.com/bolorundurowb/nidus-vision/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

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
      # Optional override. The image already includes /app/models/person.onnx.
      # Set this only when the file exists; a missing path turns detection off.
      # NIDUS_PERSON_MODEL: /models/custom-person.onnx
    volumes:
      - nidus-data:/app/data
      - nidus-recordings:/var/nidus/recordings
    healthcheck:
      test: ["CMD", "sh", "-c", "curl -f http://localhost:6438/health && curl -f http://localhost:6438/favicon.ico"]
      interval: 30s
      timeout: 5s
      retries: 3
      start_period: 15s

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

Go to **Cameras** and add each camera with its RTSP URL, for example `rtsp://user:pass@192.168.1.50:554/stream1`. One URL is used for both recording and live view.

Use an H.264 stream. Recording copies the video and drops the audio, and the browser plays that same stream. H.265/HEVC is stored if that is what the camera sends, but Chrome and Firefox will not play it. If the camera's main stream is H.265, paste its H.264 URL instead.

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

Data and recordings live in the named volumes and survive image updates. On startup the new container applies database migrations before it serves traffic. To upgrade to a specific release, change the `image:` tag and rerun the two commands above. Read [CHANGELOG.md](CHANGELOG.md) before moving off `latest`.

Camera passwords are encrypted with keys in the `nidus-data` volume. Images built before those keys were stored there kept them in the container filesystem, which is discarded on recreate. After the first update to a build that includes this, open each camera and save the RTSP password again.

## Scope

Nidus Vision records the camera stream continuously, shows a live grid, tags segments where a person was detected, and deletes old footage by age and an optional disk cap. From **Recordings** you can play a segment, download that MP4, or save a screenshot.

It does not discover cameras with ONVIF, draw detection zones, send phone or webhook alerts, trim a clip out of a segment, record audio, or add users beyond the single admin.

## Capacity

Recording does not re-encode, and audio is dropped, so disk use follows the camera's video bitrate. Multiply Mbit/s by 11 for a rough GB-per-day figure: a 4 Mbit/s stream is about 43 GB per camera per day. The Cameras page shows the measured bitrate after the stream connects. Default segments are 15 minutes.

Person detection is on by default and samples about one frame per second at 640×640. That work is separate from recording. Each live tile you open runs its own FFmpeg process.

The published image is Linux x86-64 and runs ONNX Runtime with OpenVINO. On a Linux host with an Intel iGPU, map `/dev/dri` and set `group_add` to the host's `video` and `render` group IDs (`getent group video render`; the example uses `44` and `109`). Delete `devices` and `group_add` to run detection on CPU, including under Docker Desktop where `/dev/dri` is absent. A Raspberry Pi or an NVIDIA GPU is outside this image.

## Backup

Stop the container first. SQLite uses WAL, and a copy taken while the app is running can produce a database that will not open.

```bash
docker compose stop
mkdir -p backup/data backup/recordings
# Names from `docker volume ls`. Compose prefixes them with the project directory.
docker run --rm -v PROJECT_nidus-data:/from -v "$PWD/backup/data":/to alpine cp -a /from/. /to/
docker run --rm -v PROJECT_nidus-recordings:/from -v "$PWD/backup/recordings":/to alpine cp -a /from/. /to/
docker compose start
```

`backup/data` holds the database, the admin password, camera configuration, and the encryption keys for camera passwords. `backup/recordings` holds the MP4 files. Restore by stopping the container and copying those directories back onto the same volumes, with the `/from` and `/to` mounts swapped.

Removing the `nidus-data` volume deletes the admin password, cameras, and the recording index. The MP4s in `nidus-recordings` stay on disk, and there is no reindex: Recordings will not list them again.

## Network

The app speaks plain HTTP and keeps a long-lived cookie for the one admin account. There is no second factor. Keep port 6438 on the LAN, or bind it to localhost and terminate TLS at a reverse proxy. Do not port-forward 6438 on the router. Report vulnerabilities as described in [SECURITY.md](SECURITY.md).

To expose it only through Caddy on the same host, publish the port on localhost:

```yaml
ports:
  - "127.0.0.1:6438:6438"
```

```caddyfile
nvr.example.com {
  reverse_proxy localhost:6438
}
```

## Notes

- **Clock.** `TZ` sets the container clock, which names segment files in UTC. Times in the UI follow the browser's time zone.
- **Logs.** `docker compose logs -f nidus-vision`.
- **Reset the admin password.** Stop the container and remove the `nidus-data` volume (`docker volume rm <project>_nidus-data`). This also deletes camera configuration, encryption keys, and the recording index. Video files in `nidus-recordings` remain, unindexed.

## Build from source

```bash
git clone https://github.com/bolorundurowb/nidus-vision.git
cd nidus-vision
docker compose -f docker/docker-compose.yml up --build -d
```

This builds the same multi-stage image locally (Angular client, OpenVINO native libs, `dotnet publish`). Internals, local development without Docker, and tests: [ARCHITECTURE.md](ARCHITECTURE.md).

License: MIT.
