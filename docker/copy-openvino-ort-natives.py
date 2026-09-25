#!/usr/bin/env python3
from pathlib import Path
import shutil
import sys

root = Path("/ov/pkg")
dest = Path("/ov/native")
dest.mkdir(parents=True, exist_ok=True)

copied: list[str] = []
for path in root.rglob("*"):
    if not path.is_file():
        continue
    parts = path.relative_to(root).parts
    if not parts or parts[0] not in {"onnxruntime", "openvino"}:
        continue
    name = path.name
    if "pybind" in name.lower() or name.startswith("_"):
        continue
    is_native = (
        name.endswith(".so")
        or ".so." in name
        or path.suffix in {".xml", ".json", ".mvcmd"}
    )
    if not is_native:
        continue
    shutil.copy2(path, dest / name)
    copied.append(name)

if not any(name.startswith("libonnxruntime") for name in copied):
    sys.exit("onnxruntime-openvino wheel did not contain libonnxruntime")

ort = dest / "libonnxruntime.so"
if not ort.exists():
    matches = sorted(dest.glob("libonnxruntime.so*"))
    if not matches:
        sys.exit("libonnxruntime.so* missing")
    ort.symlink_to(matches[0].name)

print(f"copied {len(copied)} OpenVINO/ONNX Runtime native files")
