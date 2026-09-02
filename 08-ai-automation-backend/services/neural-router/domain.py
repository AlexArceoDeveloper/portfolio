from __future__ import annotations

from dataclasses import dataclass

import numpy as np


LABELS = ("finance", "support", "knowledge")
VOCABULARY = ("invoice", "payment", "refund", "incident", "error", "access", "policy", "guide", "how")


@dataclass(frozen=True)
class Prediction:
    intent: str
    confidence: float


class NeuralIntentRouter:
    """Small deterministic dense network used to demonstrate a model-serving boundary."""

    def __init__(self) -> None:
        self._hidden_weights = np.array(
            [
                [2.0, 2.0, 1.5, 0.0, 0.0, 0.0, 0.1, 0.0, 0.0],
                [0.0, 0.0, 0.0, 2.0, 2.0, 1.2, 0.0, 0.0, 0.0],
                [0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 1.6, 1.5, 1.0],
            ],
            dtype=np.float32,
        )
        self._output_weights = np.eye(3, dtype=np.float32)

    def predict(self, text: str) -> Prediction:
        tokens = set(text.lower().replace("?", "").replace(".", "").split())
        features = np.array([1.0 if term in tokens else 0.0 for term in VOCABULARY], dtype=np.float32)
        hidden = np.maximum(self._hidden_weights @ features, 0.0)
        logits = self._output_weights @ hidden
        shifted = logits - np.max(logits)
        probabilities = np.exp(shifted) / np.exp(shifted).sum()
        index = int(np.argmax(probabilities))
        return Prediction(LABELS[index], float(probabilities[index]))
