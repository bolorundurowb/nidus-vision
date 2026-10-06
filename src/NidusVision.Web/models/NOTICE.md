# Person detection model

`person.onnx` in this folder is **not** covered by the MIT licence that applies to the rest of Nidus Vision. It is a third-party model distributed under the **GNU Affero General Public License v3.0** (AGPL-3.0). The full licence text is in [`LICENSE-AGPL-3.0.txt`](LICENSE-AGPL-3.0.txt), next to this file.

| Field | Value |
|---|---|
| File | `person.onnx` |
| Model | Ultralytics YOLOv8n-person (single class: `person`) |
| Author | [Ultralytics](https://ultralytics.com) |
| Licence | AGPL-3.0 ([ultralytics.com/license](https://ultralytics.com/license)) |
| Training data | `cfg/datasets/WiderPedestrian.yaml` (per the model metadata) |
| Exported with | Ultralytics 8.3.67, ONNX opset 14, dynamic input, no embedded NMS |
| Export date | 2025-01-30 (per the model metadata) |
| SHA-256 | `e3c6981fca37d22cc3143e1385c141caea9967398ca8c8898db0bedfb73f62a0` |

The values above come from the metadata embedded in the ONNX file. You can read them with ONNX Runtime:

```python
import onnxruntime as ort
print(ort.InferenceSession("person.onnx").get_modelmeta().custom_metadata_map)
```

## Source

The model was produced with the Ultralytics YOLOv8 framework. The source code, training configuration, and documentation are available at:

- Source: <https://github.com/ultralytics/ultralytics>
- Documentation: <https://docs.ultralytics.com>
- Licence: <https://ultralytics.com/license>

Nidus Vision uses the file unmodified. It loads the model with ONNX Runtime and decodes its output in `src/NidusVision.Inference`. None of the Ultralytics Python code is included.

## Replacing the model

To use a different model, for example one under a licence that suits your deployment better, set `NIDUS_PERSON_MODEL` to its path. The detector expects a YOLOv8-style output of `[1, 4+nc, N]` or `[1, N, 4+nc]`, where class 0 is `person`. If the path does not exist, detection is turned off, and recording keeps working.
