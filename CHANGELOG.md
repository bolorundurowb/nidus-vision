# Changelog

Release notes for tagged images (`bolorundurowb/nidus-vision`). The publish workflow pushes a tag and `latest` only after the build and tests pass. Dates are the tag date.

## Unreleased

- Licensing: the bundled person model (`person.onnx`) is the Ultralytics YOLOv8n-person model, licensed under **AGPL-3.0**, not MIT. The repo and the image now ship its licence text and a notice next to the model (`/app/models/NOTICE.md`, `/app/models/LICENSE-AGPL-3.0.txt`). A new `THIRD-PARTY-NOTICES.md` lists every third-party component, and the image's `org.opencontainers.image.licenses` label now reads `MIT AND AGPL-3.0`. To run a different model, use `NIDUS_PERSON_MODEL`.

- Reverse proxies:
  - New `ReverseProxy__KnownProxies` / `ReverseProxy__KnownNetworks` settings make the app trust `X-Forwarded-For` and `X-Forwarded-Proto` from your proxy. Without them, everyone behind the proxy shared one login-throttle bucket, and the cookie never got the `Secure` flag.
  - The login throttle now counts parallel attempts, forgets failures after 15 minutes, and groups IPv6 clients by /64.
- Ingest:
  - A database or file error during a supervisor pass is now logged and retried. It no longer stops the app.
  - Reconnect backoff resets after a run that stayed up for a minute, so old blips no longer cost 60 s on every later reconnect.
- Logs and error messages now mask credentials in `rtsps://` URLs and in `password=` query parameters. The raw FFmpeg output in live-stream warnings is masked too.
- Settings:
  - Sliders save when you let go, not on every step. Saves are debounced and serialized, so an older value can no longer land last.
  - If the current settings can't be loaded, the controls stay locked instead of saving defaults over them (which could drop your storage cap). Load and save errors are shown, with a Retry button.
- The Settings API rejects retention outside 1–3650 days and storage caps under 1 GB. Retention clamps any older out-of-range values, skips the segment FFmpeg is still writing, and keeps going when a single file can't be deleted.
- Camera password encryption keys are stored in the data directory (`keys/`, inside the `nidus-data` volume) so they survive a container recreate. Installs that saved camera passwords before this change need those passwords entered again after upgrading, because the previous keys lived only in the container filesystem.
- README covers codec expectations, disk sizing, backup, upgrades, and the LAN-only network default.

## v1.2.2 — 2026-09-30

- Monitor view handles multiple cameras: tiles resize and reflow instead of overflowing.
- Recording layout supports nested camera folders; ingest aligns existing footage into the expected structure.

## v1.2.1 — 2026-09-28

- Recordings page: camera form split into its own component, status pill and live tile refinements, and a new `RecordingLayout` that organizes segments by camera and date.
- Ingest: camera fingerprint includes the recording-enabled flag; supervisor restarts runs when the flag changes.
- Added `release-notes.js` helper for the publish workflow.

## v1.2.0 — 2026-09-25

- Motion-gated person detection: `FrameMotionGate` skips inference on static frames, cutting CPU/GPU load.
- `DetectionPresenceTracker` validates intervals and merges overlapping detections.
- Favicon and branding updated.

## v1.1.0 — 2026-09-18

- Container runs as the non-root `app` user; added `docker/entrypoint.sh`.
- Login throttle: serializes first-run setup and rate-limits failed logins per client.
- Camera URLs and recording files are confined to the storage root (no path traversal).
- Inference settings are bounded on write (`InferenceLimits`).
- Anonymous `/health` no longer leaks the FFmpeg path.
- Stop showing fake cameras in the UI.
- Index untracked recordings off the request path.
- Added `CHANGELOG.md` and `SECURITY.md`; encryption keys persist in the data directory.

## v1.0.1 — 2026-09-10

- Fixed Docker deploy UI issues (SPA fallback auth, static file serving).
- Expanded README with Docker quick start and deployment setup.

## v1.0.0 — 2026-09-08

- Initial release: RTSP ingest with FFmpeg segments, ONNX person detection, retention planner, live view (MSE), event library, camera management, and Docker Compose deployment.
