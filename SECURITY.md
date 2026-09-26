# Security

Nidus Vision is a single-admin NVR. It stores camera passwords and a view of the cameras you add. The app listens on HTTP and sets a long-lived, HTTP-only cookie. Run it on a trusted LAN, or publish it only through a TLS reverse proxy bound to localhost. Do not port-forward port 6438.

## Reporting a vulnerability

Use a [private GitHub security advisory](https://github.com/bolorundurowb/nidus-vision/security/advisories/new). Please include the version or image tag, the deployment (Docker or a local build), and enough detail to reproduce the issue. Do not open a public issue for an unfixed vulnerability.
