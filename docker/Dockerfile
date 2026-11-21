FROM node:22-bookworm-slim AS client
WORKDIR /client
COPY src/NidusVision.Client/package.json src/NidusVision.Client/package-lock.json ./
RUN npm ci
COPY src/NidusVision.Client/ ./
RUN npm run build

FROM python:3.12-slim-bookworm AS ov-ort
WORKDIR /ov
# Only the wheel's native libraries are copied into the running image.
RUN pip install --no-cache-dir --upgrade pip==26.3 \
    && pip install --no-cache-dir --no-deps --target /ov/pkg onnxruntime-openvino==1.24.1 \
    && mkdir -p /ov/native \
    && find /ov/pkg \( -path '*/onnxruntime/*' -o -path '*/openvino/*' \) -type f \
         \( -name '*.so' -o -name '*.so.*' -o -name '*.xml' -o -name '*.json' -o -name '*.mvcmd' \) \
         ! -iname '*pybind*' ! -name '_*' \
         -exec cp -a {} /ov/native/ \; \
    && ls /ov/native/libonnxruntime.so* >/dev/null \
    && if [ ! -e /ov/native/libonnxruntime.so ]; then \
         ln -s "$(basename "$(ls /ov/native/libonnxruntime.so* | head -1)")" /ov/native/libonnxruntime.so; \
       fi

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
COPY Directory.Build.props NidusVision.slnx ./
COPY src/ ./src/
COPY tests/ ./tests/
RUN dotnet publish src/NidusVision.Web/NidusVision.Web.csproj -c Release -o /app/publish -p:OnnxRuntimeProvider=OpenVino

FROM mcr.microsoft.com/dotnet/aspnet:11.0
WORKDIR /app
# libze1 and libze-intel-gpu1 are the Level Zero runtime OpenVINO needs to see an
# Intel iGPU. VA-API (what Jellyfin uses) is a different driver and is not enough.
# Apply distro fixes published since the runtime image was built. Our entrypoint
# starts dotnet directly, so the inherited Pebble service supervisor is unused.
RUN rm -f /usr/bin/pebble \
    && apt-get update \
    && apt-get upgrade -y --no-install-recommends \
    && apt-get install -y --no-install-recommends \
        curl ffmpeg \
        libtbb12 libgomp1 \
        ocl-icd-libopencl1 intel-opencl-icd \
        libze1 libze-intel-gpu1 \
    && rm -rf /var/lib/apt/lists/* \
    && test -x /usr/bin/ffmpeg \
    && ldconfig -p | grep -q 'libze_loader.so.1' \
    && ldconfig -p | grep -q 'libze_intel_gpu.so.1'
COPY --from=build /app/publish ./
COPY --from=ov-ort /ov/native/ ./
COPY --from=client /client/dist/NidusVision.Client/browser ./wwwroot
# The code is MIT. /app/models/person.onnx is Ultralytics YOLOv8n under AGPL-3.0, and its
# NOTICE.md and LICENSE-AGPL-3.0.txt are published next to it by the models/** content glob.
COPY LICENSE THIRD-PARTY-NOTICES.md ./
LABEL org.opencontainers.image.licenses="MIT AND AGPL-3.0"
COPY docker/entrypoint.sh /usr/local/bin/nidus-entrypoint
RUN sed -i 's/\r$//' /usr/local/bin/nidus-entrypoint \
    && chmod 0755 /usr/local/bin/nidus-entrypoint \
    && id app \
    && command -v setpriv \
    && command -v find \
    && command -v readlink
ENV ASPNETCORE_URLS=http://+:6438
ENV ASPNETCORE_ENVIRONMENT=Production
ENV FFMPEG_PATH=/usr/bin/ffmpeg
ENV LD_LIBRARY_PATH=/app
ENV Storage__DataDirectory=/app/data
ENV Storage__RecordingsDirectory=/var/nidus/recordings
ENV Storage__EventsDirectory=/var/nidus/events
EXPOSE 6438
HEALTHCHECK --interval=30s --timeout=5s --retries=3 --start-period=180s \
  CMD curl -f http://localhost:6438/health && curl -f http://localhost:6438/favicon.ico || exit 1
ENTRYPOINT ["nidus-entrypoint", "dotnet", "NidusVision.Web.dll"]
