# Security

Nidus Vision is a single-admin NVR. It stores camera passwords and a view of the cameras you add. The app listens on HTTP and sets a long-lived, HTTP-only cookie. Run it on a trusted LAN, or publish it only through a TLS reverse proxy bound to localhost. Do not port-forward port 6438.

## Camera credentials

Camera passwords are encrypted at rest with keys in the data directory (`keys/`). The app masks the credentials in any FFmpeg output it logs or shows in the UI.

FFmpeg does receive the full RTSP URL, credentials included, as a command-line argument. On the host, any user who can list processes can see it (`ps`, `/proc/<pid>/cmdline`). That includes the Docker host's users, for a container. Give cameras their own low-privilege viewer account instead of reusing the admin password, and limit shell access to the host.

## Reporting a vulnerability

Use a [private GitHub security advisory](https://github.com/bolorundurowb/nidus-vision/security/advisories/new). Please include the version or image tag, the deployment (Docker or a local build), and enough detail to reproduce the issue. Do not open a public issue for an unfixed vulnerability.
