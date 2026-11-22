# Nidus Vision

[![CI](https://github.com/bolorundurowb/nidus-vision/actions/workflows/ci.yml/badge.svg)](https://github.com/bolorundurowb/nidus-vision/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Nidus Vision is a self-hosted video recorder for RTSP cameras. Watch live feeds, record continuously to your own disk, and use person detection to find recordings worth reviewing. In **Recordings**, you can play footage, download an MP4 segment, or save a screenshot.

![Monitor Center showing four live camera feeds and a recording timeline](docs/monitor.jpg)

## Get started with Docker

You need Docker with the Compose plugin, an x86-64 computer that can run Linux containers, an RTSP camera on your network, and disk space for recordings. Person detection can run on the CPU. An Intel GPU is optional.

### 1. Start Nidus Vision

Save this as `compose.yaml` in a new directory.

```yaml
services:
  nidus-vision:
    image: bolorundurowb/nidus-vision:latest
    restart: unless-stopped
    ports:
      - "6438:6438"
    extra_hosts:
      - "host.docker.internal:host-gateway"
    volumes:
      - nidus-data:/app/data
      - nidus-recordings:/var/nidus/recordings

volumes:
  nidus-data:
  nidus-recordings:
```

From that directory, run:

```bash
docker compose up -d
```

Open [http://localhost:6438](http://localhost:6438) on the Docker host, or use `http://<host-ip>:6438` from another device on your LAN. The first visit asks you to set the admin password. Use at least eight characters. Nidus Vision has one administrator account.

### 2. Add your first camera

1. Open **Cameras** and enter the camera's RTSP URL. Check your camera's app or manual for the stream path. A typical URL looks like `rtsp://user:password@192.168.1.50:554/stream1`.
2. Choose an **H.264** stream for browser playback. Nidus Vision saves the camera's video as sent and drops audio. HEVC can be recorded, but browser playback depends on HEVC support.
3. Test and save the camera, then open **Monitor** to see the live feed. With recording enabled, footage appears in **Recordings** as segments finish.

Use the camera's LAN IP in its URL. If the stream runs on the Docker host, use `host.docker.internal`. Inside the container, `localhost` refers to the container itself.

![Cameras page showing connection status, bitrate, and stream diagnostics](docs/cameras.png)

## Manage recordings and disk space

- **Estimate disk use.** Multiply each camera's video bitrate in Mbit/s by about 11 to get GB per day. A 4 Mbit/s camera uses about 43 GB per day. **Cameras** shows the measured bitrate after the stream connects.
- **Choose how long to keep footage.** In **Settings**, set separate retention periods for general and person-tagged footage. You can override general retention for an individual camera. An optional storage cap removes older untagged footage before person-tagged footage.
- **Choose the recording segment length.** In **Settings**, set it from 5 to 30 minutes. The default is 15 minutes.
- **Use another disk.** Replace the `nidus-recordings:/var/nidus/recordings` service mount with a host path such as `/mnt/cctv:/var/nidus/recordings` when setting up the instance.

Person detection is on by default. In **Settings**, you can turn it off or change the sample rate and confidence threshold. The bundled detection model has its own [license](#license).

## Update and back up

The [Docker Hub image](https://hub.docker.com/r/bolorundurowb/nidus-vision) uses `latest` for the most recent published release. To stay on a specific release, select a published version tag in `image:`. Read the [changelog](CHANGELOG.md) before upgrading.

```bash
docker compose pull
docker compose up -d
```

The named volumes survive container replacement. Nidus Vision applies database migrations at startup. Back up both volumes before upgrading, and stop the app first so the SQLite database and its write-ahead log are copied together.

```bash
docker compose stop
mkdir -p backup/data backup/recordings
# Replace PROJECT with the volume prefix shown by docker volume ls.
docker run --rm -v PROJECT_nidus-data:/from:ro -v "$PWD/backup/data:/to" alpine cp -a /from/. /to/
docker run --rm -v PROJECT_nidus-recordings:/from:ro -v "$PWD/backup/recordings:/to" alpine cp -a /from/. /to/
docker compose start
```

If you use a host path for recordings, back up that directory instead of the recordings volume. To restore, stop Nidus Vision and copy each backup's contents back into its corresponding volume or directory before restarting.

Keep the data backup private. It contains the database, camera settings, recording index, and keys used to decrypt saved camera passwords. Removing the data volume resets the admin account and removes camera settings and the recording index. Keeping only the MP4 files does not restore the Recordings library.

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| The web page does not open | Run `docker compose ps` and `docker compose logs --tail=100 nidus-vision`. On the host, try `curl -f http://localhost:6438/health`. |
| A camera won't connect | Check the RTSP path, credentials, and whether the container can reach the camera's LAN address. |
| The camera connects but the browser shows no video | Select the camera's H.264 stream and update its RTSP URL in **Cameras**. |
| Saved camera passwords stopped working after upgrading an older image | Re-enter the passwords if that image stored encryption keys in the old container filesystem. New installs keep those keys in the data volume. |
| The first start after an upgrade takes a while | Older volumes may need ownership repairs before the server starts. Large recordings volumes can take several minutes. Check the container logs. |

## Optional setup

### Intel GPU for person detection

On a Linux host with an Intel GPU, add the following under the Compose service. Replace the example group IDs with those reported by `getent group video render` on your host.

```yaml
devices:
  - /dev/dri:/dev/dri
group_add:
  - "44"
  - "109"
```

The published image uses OpenVINO on Linux x86-64. The default CPU setup also works on Docker Desktop. The published image does not target Raspberry Pi or NVIDIA GPU inference (I don't have the devices to verify against).

### Network access

Keep port 6438 on a trusted LAN. Nidus Vision serves HTTP and uses a single admin account without two-factor authentication. For HTTPS, use a reverse proxy and limit access to the backend port. For a proxy running on the same host, bind the published port to `127.0.0.1:6438:6438`.

Configure `ReverseProxy__KnownProxies` with the proxy's source IP as seen by the app, or `ReverseProxy__KnownNetworks` with its CIDR range. Both accept comma-separated values. A host proxy connecting to Docker typically appears to come from the Docker network gateway. Use `docker network inspect <project>_default` to find it. Trust only your proxy so the app can use the real client IP for login throttling and mark the cookie secure for HTTPS requests.

### Build from source

```bash
git clone https://github.com/bolorundurowb/nidus-vision.git
cd nidus-vision
docker compose -f docker/docker-compose.yml up --build -d
```

The source Compose file includes Intel GPU settings. Remove `devices` and `group_add` for CPU-only use. See [ARCHITECTURE.md](ARCHITECTURE.md) for local development and tests, and [SECURITY.md](SECURITY.md) for vulnerability reporting.

## License

Nidus Vision's source code is available under the [MIT License](LICENSE). The bundled Ultralytics YOLOv8n person-detection model is separately licensed under [AGPL-3.0](src/NidusVision.Web/models/LICENSE-AGPL-3.0.txt).

To use a different YOLOv8-compatible model, mount it in the container and point `NIDUS_PERSON_MODEL` at that path. See the [model notice](src/NidusVision.Web/models/NOTICE.md) and [third-party notices](THIRD-PARTY-NOTICES.md) for attribution and license details.
