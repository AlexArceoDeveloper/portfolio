import sys
import unittest
from pathlib import Path


SERVICE_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SERVICE_ROOT))

from domain import evaluate_answer


class EvaluationTests(unittest.TestCase):
    def test_grounded_policy_compliant_answer_scores_full_marks(self) -> None:
        result = evaluate_answer(
            answer="A grounded answer [1].",
            source_count=1,
            citation_count=1,
            tool_statuses=["allowed", "blocked"],
        )

        self.assertTrue(result.grounded)
        self.assertTrue(result.policy_compliant)
        self.assertEqual(100, result.score)
        self.assertEqual((), result.findings)

    def test_missing_sources_and_unknown_state_are_reported(self) -> None:
        result = evaluate_answer(
            answer="Ungrounded answer",
            source_count=0,
            citation_count=0,
            tool_statuses=["executed_without_policy"],
        )

        self.assertFalse(result.grounded)
        self.assertFalse(result.policy_compliant)
        self.assertEqual(0, result.score)
        self.assertEqual(3, len(result.findings))


if __name__ == "__main__":
    unittest.main()
