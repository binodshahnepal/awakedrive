"""Exports PyTorch face landmark regression model to ONNX format (.onnx).

Output tensor: 68 2D facial landmarks (136 float values: x0, y0, x1, y1, ...).
Input tensor: [batch_size, 3, 128, 128] RGB normalized image.
"""
import os
import torch
import torch.nn as nn


class FaceLandmarkModel(nn.Module):
    """Convolutional Neural Network for 68-point 2D facial landmark regression."""

    def __init__(self) -> None:
        super().__init__()
        self.features = nn.Sequential(
            nn.Conv2d(3, 16, kernel_size=3, stride=2, padding=1),  # 64x64
            nn.BatchNorm2d(16),
            nn.ReLU(inplace=True),
            nn.Conv2d(16, 32, kernel_size=3, stride=2, padding=1),  # 32x32
            nn.BatchNorm2d(32),
            nn.ReLU(inplace=True),
            nn.Conv2d(32, 64, kernel_size=3, stride=2, padding=1),  # 16x16
            nn.BatchNorm2d(64),
            nn.ReLU(inplace=True),
            nn.Conv2d(64, 128, kernel_size=3, stride=2, padding=1),  # 8x8
            nn.BatchNorm2d(128),
            nn.ReLU(inplace=True),
            nn.AdaptiveAvgPool2d((4, 4)),
        )
        self.regressor = nn.Sequential(
            nn.Flatten(),
            nn.Linear(128 * 4 * 4, 256),
            nn.ReLU(inplace=True),
            nn.Linear(256, 136),  # 68 (x, y) landmark points normalized [0, 1]
            nn.Sigmoid(),
        )

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        x = self.features(x)
        return self.regressor(x)


def export_onnx(output_path: str = "models/face_landmarks.onnx") -> str:
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    model = FaceLandmarkModel()
    model.eval()

    dummy_input = torch.randn(1, 3, 128, 128, dtype=torch.float32)

    torch.onnx.export(
        model,
        dummy_input,
        output_path,
        export_params=True,
        opset_version=14,
        do_constant_folding=True,
        input_names=["input"],
        output_names=["landmarks"],
        dynamic_axes={"input": {0: "batch_size"}, "landmarks": {0: "batch_size"}},
    )
    print(f"Exported ONNX model successfully to '{output_path}'")
    return output_path


if __name__ == "__main__":
    export_onnx()
