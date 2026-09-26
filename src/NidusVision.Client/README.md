# Nidus Vision client

Angular SPA for Nidus Vision. It is not a standalone app: API calls go to the ASP.NET host.

Local development runs the API on port **6438** and this app on port **4200**. The dev server proxies `/api`, `/health`, and `/hubs` to the API. Setup, Docker, and tests are in the [repository README](../../README.md) and [ARCHITECTURE.md](../../ARCHITECTURE.md).

```bash
npm install
npm start
```

Open http://localhost:4200 after the API is running. Production and Docker builds compile this project into the host's `wwwroot`.
