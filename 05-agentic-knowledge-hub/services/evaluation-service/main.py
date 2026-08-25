from __future__ import annotations

from fastapi import FastAPI
from pydantic import BaseModel, ConfigDict, Field

from domain import evaluate_answer


app = FastAPI(title="Grounded Answer Evaluation Service", version="1.0.0")


class EvaluationRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    answer: str
    source_count: int = Field(alias="sourceCount", ge=0)
    citation_count: int = Field(alias="citationCount", ge=0)
    tool_statuses: list[str] = Field(alias="toolStatuses", default_factory=list)


class EvaluationResponse(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    grounded: bool
    citation_coverage: float = Field(alias="citationCoverage")
    policy_compliant: bool = Field(alias="policyCompliant")
    score: int
    findings: list[str]


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "healthy"}


@app.post("/evaluate", response_model=EvaluationResponse, response_model_by_alias=True)
def evaluate(request: EvaluationRequest) -> EvaluationResponse:
    result = evaluate_answer(
        answer=request.answer,
        source_count=request.source_count,
        citation_count=request.citation_count,
        tool_statuses=request.tool_statuses,
    )
    return EvaluationResponse(
        grounded=result.grounded,
        citationCoverage=result.citation_coverage,
        policyCompliant=result.policy_compliant,
        score=result.score,
        findings=list(result.findings),
    )
