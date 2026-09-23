FROM node:22-bookworm-slim AS client
WORKDIR /client
COPY src/NidusVision.Client/package.json src/NidusVision.Client/package-lock.json ./
RUN npm ci
COPY src/NidusVision.Client/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props NidusVision.slnx ./
COPY src/ ./src/
COPY tests/ ./tests/
RUN dotnet publish src/NidusVision.Web/NidusVision.Web.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ffmpeg \
    && rm -rf /var/lib/apt/lists/* \
    && test -x /usr/bin/ffmpeg
COPY --from=build /app/publish ./
COPY --from=client /client/dist/NidusVision.Client/browser ./wwwroot
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV FFMPEG_PATH=/usr/bin/ffmpeg
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --retries=3 --start-period=15s \
  CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "NidusVision.Web.dll"]
