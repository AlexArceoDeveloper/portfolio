from __future__ import annotations

from dataclasses import dataclass
from typing import Iterable


COMPLIANT_TOOL_STATES = {"allowed", "approved", "pending_approval", "blocked"}


@dataclass(frozen=True)
class Evaluation:
    grounded: bool
    citation_coverage: float
    policy_compliant: bool
    score: int
    findings: tuple[str, ...]


def evaluate_answer(
    answer: str,
    source_count: int,
    citation_count: int,
    tool_statuses: Iterable[str],
) -> Evaluation:
    grounded = bool(answer.strip()) and source_count > 0
    citation_coverage = (
        min(max(citation_count, 0) / source_count, 1.0)
        if source_count > 0
        else 0.0
    )
    statuses = tuple(tool_statuses)
    policy_compliant = all(status in COMPLIANT_TOOL_STATES for status in statuses)

    findings: list[str] = []
    if not grounded:
        findings.append("The answer is not grounded in retrieved sources.")
    if citation_coverage < 1.0:
        findings.append("Not every retrieved source is represented by a citation.")
    if not policy_compliant:
        findings.append("At least one tool has an unknown policy state.")

    score = round(
        (50 if grounded else 0)
        + 25 * citation_coverage
        + (25 if policy_compliant else 0)
    )
    return Evaluation(
        grounded=grounded,
        citation_coverage=citation_coverage,
        policy_compliant=policy_compliant,
        score=score,
        findings=tuple(findings),
    )
