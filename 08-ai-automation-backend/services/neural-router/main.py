from __future__ import annotations

from fastapi import FastAPI
from pydantic import BaseModel, Field

from domain import NeuralIntentRouter


app = FastAPI(title="Neural Intent Router", version="1.0.0")
router = NeuralIntentRouter()


class RoutingRequest(BaseModel):
    text: str = Field(min_length=1, max_length=8_000)


class RoutingResponse(BaseModel):
    intent: str
    confidence: float


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "healthy"}


@app.post("/classify", response_model=RoutingResponse)
def classify(request: RoutingRequest) -> RoutingResponse:
    prediction = router.predict(request.text)
    return RoutingResponse(intent=prediction.intent, confidence=prediction.confidence)
