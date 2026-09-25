# Nidus Vision

Self-hosted NVR: live view, continuous recording, and person-tagged playback from RTSP cameras.

## Run

```bash
docker compose -f docker/docker-compose.yml up --build
```

Open http://localhost:6438. On first visit go to `/login` and set an admin password (8+ characters).

Add cameras with RTSP URLs the **container** can reach (LAN IP, not `127.0.0.1`). Cameras on the Docker host can use `host.docker.internal`.

Config and the database live in the `nidus-data` volume; video in `nidus-recordings`.

Intel iGPU inference needs `/dev/dri` on the host. Remove the `devices` entry in compose only if you want CPU-only.

Internals, local development, and tests: [ARCHITECTURE.md](ARCHITECTURE.md).

License: MIT.
