"""
app/models/rag_response.py
==========================

Pydantic response models. They give Swagger a precise schema and make
the response shape consistent for the .NET client.
"""

from __future__ import annotations

from pydantic import BaseModel, Field


class HealthResponse(BaseModel):
    """Response for GET /health."""

    status: str = Field(..., examples=["ok"])
    service: str = Field(..., examples=["Retrieval-Augmented-Generation-PRN222"])


class IndexDocumentResponse(BaseModel):
    """Response for POST /rag/index-document."""

    documentId: str
    status: str = Field(..., examples=["indexed"])
    totalChunks: int = Field(..., examples=[25])
    message: str = Field(..., examples=["Document indexed successfully."])


class SourceItem(BaseModel):
    """A single retrieved chunk that supports an answer."""

    citationId: str = Field(
        default="",
        description=(
            "Per-request citation ID (C1, C2, ...) assigned in retrieval "
            "order. The answer text references chunks by these IDs."
        ),
    )
    documentId: str
    fileName: str
    pageNumber: int | None = Field(
        default=None,
        description="1-based page index (PDFs) or slide index (PPTX). None for TXT/DOCX.",
    )
    chunkIndex: int = Field(..., description="0-based position of this chunk in the document.")
    text: str = Field(..., description="Plain-text preview of the chunk.")
    # Optional, for debugging / tuning RAG_MAX_DISTANCE.
    # Lower distance == more relevant (cosine distance).
    distance: float | None = Field(
        default=None,
        description="Cosine distance between the question and this chunk (lower is better).",
    )


class UsageInfo(BaseModel):
    """
    REAL token usage reported by the LLM provider for one /rag/ask call.

    Values come straight from the provider's usage metadata (for Gemini:
    usageMetadata.promptTokenCount / candidatesTokenCount / totalTokenCount).
    They are NEVER estimated from character or word counts. All fields are
    None when the provider was not called (mock mode / relevance-gate
    fallback) or did not report usage.
    """

    promptTokens: int | None = Field(
        default=None, description="Tokens consumed by the prompt (provider-reported)."
    )
    completionTokens: int | None = Field(
        default=None, description="Tokens generated in the answer (provider-reported)."
    )
    totalTokens: int | None = Field(
        default=None,
        description=(
            "Provider's official total. May exceed prompt + completion when "
            "the model spends internal reasoning tokens."
        ),
    )


class AskResponse(BaseModel):
    """Response for POST /rag/ask."""

    answer: str
    # Only the chunks the model actually cited in the answer.
    sources: list[SourceItem] = Field(default_factory=list)
    # Every chunk retrieval returned (superset of `sources`), for debugging.
    retrievedSources: list[SourceItem] = Field(default_factory=list)
    usedCitationIds: list[str] = Field(default_factory=list)
    usage: UsageInfo | None = Field(
        default=None,
        description="Real provider token usage; null when unavailable.",
    )


class DocumentStatusResponse(BaseModel):
    """Response for GET /rag/documents/{documentId}/status."""

    documentId: str
    indexed: bool
    totalChunks: int = 0


class DeleteDocumentResponse(BaseModel):
    """Response for DELETE /rag/documents/{documentId}."""

    documentId: str
    status: str = Field(..., examples=["deleted"])
