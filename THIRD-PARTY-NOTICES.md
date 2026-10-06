# Third-party notices

Nidus Vision's own source code is released under the [MIT License](LICENSE). The repository and the Docker image also contain third-party components under their own licences, listed below.

## Person detection model (AGPL-3.0)

| Component | Location | Licence |
|---|---|---|
| Ultralytics YOLOv8n-person (`person.onnx`) | `src/NidusVision.Web/models/person.onnx` in the repo; `/app/models/person.onnx` in the image | [GNU AGPL-3.0](src/NidusVision.Web/models/LICENSE-AGPL-3.0.txt) |

The model is © Ultralytics and licensed under the GNU Affero General Public License v3.0. It is **not** covered by the MIT licence. Model details, the licence text, and source links are in [`src/NidusVision.Web/models/NOTICE.md`](src/NidusVision.Web/models/NOTICE.md). In the image they are copied next to the model, as `/app/models/NOTICE.md` and `/app/models/LICENSE-AGPL-3.0.txt`.

- Upstream source: <https://github.com/ultralytics/ultralytics>
- Upstream licence: <https://ultralytics.com/license>

Nidus Vision ships the model file unmodified and runs it through ONNX Runtime. If you redistribute the image or the model, or offer it to others over a network, check that you meet the AGPL-3.0 terms. Ultralytics also offers an Enterprise licence for uses that cannot meet them. To run without this model, set `NIDUS_PERSON_MODEL` to a different model, or to a missing path to turn detection off.

## Other components in the Docker image

These are pulled in at build time from their upstream packages. Each one keeps its own licence:

| Component | Source | Licence |
|---|---|---|
| .NET / ASP.NET Core runtime | `mcr.microsoft.com/dotnet/aspnet` | MIT |
| ONNX Runtime | NuGet `Microsoft.ML.OnnxRuntime*`, PyPI `onnxruntime-openvino` | MIT |
| OpenVINO runtime libraries | PyPI `onnxruntime-openvino` | Apache-2.0 |
| FFmpeg | Debian `ffmpeg` package | LGPL-2.1-or-later / GPL-2.0-or-later (Debian build) |
| Angular, `@microsoft/signalr` | npm | MIT |

For the full licence texts of the Debian packages, see `/usr/share/doc/<package>/copyright` inside the image.
