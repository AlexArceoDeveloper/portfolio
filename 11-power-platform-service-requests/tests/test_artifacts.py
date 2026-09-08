from __future__ import annotations

import json
import re
import unittest
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[1]
REQUEST_SCHEMA = PROJECT_ROOT / "sharepoint/lists/service-requests.schema.json"
HISTORY_SCHEMA = PROJECT_ROOT / "sharepoint/lists/request-history.schema.json"
FLOW_DEFINITION = PROJECT_ROOT / "power-automate/flows/request-intake.definition.json"
TEXT_FILES = [
    PROJECT_ROOT / "README.md",
    PROJECT_ROOT / "docs/architecture.md",
    PROJECT_ROOT / "docs/power-apps-canvas-spec.md",
    PROJECT_ROOT / "copilot-studio/request-triage-topic.md",
]
SENSITIVE_PATTERN = re.compile(
    r"(?i)(?:password|api[_ -]?key|client[_ -]?secret|access[_ -]?token)\\s*[:=]\\s*[^<\\s]+"
)


class ServiceRequestArtifactsTests(unittest.TestCase):
    def test_required_artifacts_exist(self) -> None:
        for artifact in [REQUEST_SCHEMA, HISTORY_SCHEMA, FLOW_DEFINITION, *TEXT_FILES]:
            self.assertTrue(artifact.is_file(), f"Missing required artifact: {artifact}")

    def test_request_schema_has_operational_fields(self) -> None:
        payload = json.loads(REQUEST_SCHEMA.read_text(encoding="utf-8"))
        required = set(payload["required"])
        self.assertTrue({"Title", "Category", "Impact", "Status", "Priority", "OwnerTeam"}.issubset(required))
        self.assertIn("Assigned", payload["properties"]["Status"]["enum"])

    def test_history_schema_supports_safe_retry_tracking(self) -> None:
        payload = json.loads(HISTORY_SCHEMA.read_text(encoding="utf-8"))
        self.assertIn("WorkflowRunId", payload["required"])
        self.assertIn("AutomationFailed", payload["properties"]["EventType"]["enum"])

    def test_flow_contract_has_validation_history_and_notification(self) -> None:
        payload = json.loads(FLOW_DEFINITION.read_text(encoding="utf-8"))
        step_names = {step["name"] for step in payload["steps"]}
        self.assertTrue({"Validate request", "Write history", "Notify owner"}.issubset(step_names))
        self.assertEqual("SharePoint", payload["trigger"]["connector"])

    def test_public_text_has_no_literal_credentials(self) -> None:
        for file_path in TEXT_FILES + [FLOW_DEFINITION]:
            self.assertIsNone(
                SENSITIVE_PATTERN.search(file_path.read_text(encoding="utf-8")),
                f"Credential-like value found in {file_path}",
            )


if __name__ == "__main__":
    unittest.main()
