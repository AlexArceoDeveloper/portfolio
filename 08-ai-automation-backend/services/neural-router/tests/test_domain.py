from __future__ import annotations

import sys
import unittest
from pathlib import Path


SERVICE_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SERVICE_ROOT))

from domain import NeuralIntentRouter  # noqa: E402


class NeuralIntentRouterTests(unittest.TestCase):
    def setUp(self) -> None:
        self.router = NeuralIntentRouter()

    def test_finance_intent(self) -> None:
        result = self.router.predict("check invoice payment")
        self.assertEqual("finance", result.intent)
        self.assertGreater(result.confidence, 0.5)

    def test_support_intent(self) -> None:
        result = self.router.predict("route incident error")
        self.assertEqual("support", result.intent)
        self.assertGreater(result.confidence, 0.5)


if __name__ == "__main__":
    unittest.main()
