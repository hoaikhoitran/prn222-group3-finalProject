"""
Tests for the relevance-threshold gate in rag_service.ask().

These tests are deliberately "narrow": they monkeypatch the embedding
service and the vector store so we never need to download BAAI/bge-m3
and never spin up a real ChromaDB. The behavior under test is the
contract between rag_service and its collaborators:

  * If retrieval returns enough relevant chunks  -> answer + sources.
  * If retrieval returns nothing (everything was filtered out by
    the distance threshold) -> Vietnamese fallback + empty sources,
    and llm_service is never called.
"""

from __future__ import annotations

from typing import Any

import app.services.rag_service as rag_service
from app.services.llm_service import INSUFFICIENT_CONTEXT_REPLY


class _FakeEmbeddingService:
    """A deterministic embedder. Returns a fixed-size vector for any text."""

    def embed_text(self, text: str) -> list[float]:
        return [0.1] * 8

    def embed_texts(self, texts: list[str]) -> list[list[float]]:
        return [[0.1] * 8 for _ in texts]


class _FakeVectorStore:
    """
    Stub that returns whatever was preloaded into `hits`.
    The real distance filter lives inside the real VectorStoreService,
    so when we want to simulate "no chunk passed the threshold" we just
    return an empty list here — that is exactly what the real store
    would return after filtering.
    """

    def __init__(self, hits: list[dict[str, Any]]) -> None:
        self.hits = hits
        self.calls: list[dict[str, Any]] = []

    def search(self, **kwargs: Any) -> list[dict[str, Any]]:
        self.calls.append(kwargs)
        return list(self.hits)


def _install_fakes(monkeypatch, hits: list[dict[str, Any]]) -> _FakeVectorStore:
    """Patch rag_service to use the fake embedder and vector store."""
    fake_store = _FakeVectorStore(hits)
    monkeypatch.setattr(rag_service, "embedding_service", _FakeEmbeddingService())
    monkeypatch.setattr(rag_service, "vector_store_service", fake_store)
    return fake_store


def test_in_scope_query_returns_sources(monkeypatch) -> None:
    """When retrieval returns a relevant chunk, the answer includes sources."""
    relevant_hit = {
        "id": "doc_001::chunk::4",
        "text": "MVC tách ứng dụng thành Model, View và Controller.",
        "metadata": {
            "documentId": "doc_001",
            "courseCode": "PRN222",
            "fileName": "PRN222_Chapter_1.pdf",
            "pageNumber": 3,
            "chunkIndex": 4,
        },
        "distance": 0.18,  # well below the 0.45 threshold
    }
    _install_fakes(monkeypatch, [relevant_hit])

    result = rag_service.ask(
        question="MVC là gì?",
        document_id="doc_001",
        course_code="PRN222",
        top_k=5,
    )

    assert result["answer"] != INSUFFICIENT_CONTEXT_REPLY
    assert len(result["sources"]) == 1
    source = result["sources"][0]
    assert source["documentId"] == "doc_001"
    assert source["fileName"] == "PRN222_Chapter_1.pdf"
    assert source["pageNumber"] == 3
    assert source["chunkIndex"] == 4
    assert "MVC" in source["text"]
    # The optional `distance` field should be carried through.
    assert source["distance"] == 0.18


def test_out_of_scope_query_returns_fallback_and_empty_sources(monkeypatch) -> None:
    """
    When the vector store returns 0 chunks (because the distance filter
    threw everything out), rag_service must return the Vietnamese fallback
    with `sources == []` and must NOT call the LLM.
    """
    _install_fakes(monkeypatch, [])  # nothing passed the threshold

    # Spy on the generation entry point to prove it is NOT called.
    call_count = {"n": 0}

    def spy_generate_answer(question, contexts):  # noqa: ANN001
        call_count["n"] += 1
        return {"answer": "SHOULD NOT BE CALLED", "usage": None}

    monkeypatch.setattr(rag_service, "generate_answer_with_usage", spy_generate_answer)

    result = rag_service.ask(
        question="React Native là gì?",
        document_id="doc_001",
        course_code="PRN222",
        top_k=5,
    )

    assert result["answer"] == INSUFFICIENT_CONTEXT_REPLY
    assert result["sources"] == []
    assert call_count["n"] == 0, "LLM must not be called when no chunks are relevant"


def test_min_relevant_chunks_gate(monkeypatch) -> None:
    """
    With RAG_MIN_RELEVANT_CHUNKS=2, a single relevant chunk is not enough
    and the service must return the fallback.
    """
    from app.core.config import settings

    # Tighten the gate just for this test.
    original = settings.RAG_MIN_RELEVANT_CHUNKS
    monkeypatch.setattr(settings, "RAG_MIN_RELEVANT_CHUNKS", 2)
    try:
        _install_fakes(
            monkeypatch,
            [
                {
                    "id": "doc_001::chunk::0",
                    "text": "MVC short description.",
                    "metadata": {
                        "documentId": "doc_001",
                        "fileName": "f.pdf",
                        "pageNumber": 1,
                        "chunkIndex": 0,
                    },
                    "distance": 0.2,
                }
            ],
        )

        result = rag_service.ask(
            question="MVC là gì?",
            document_id="doc_001",
            course_code="PRN222",
            top_k=5,
        )

        assert result["answer"] == INSUFFICIENT_CONTEXT_REPLY
        assert result["sources"] == []
    finally:
        # Tests should not bleed config into each other.
        monkeypatch.setattr(settings, "RAG_MIN_RELEVANT_CHUNKS", original)


def test_llm_service_returns_fallback_for_empty_contexts() -> None:
    """Direct check on llm_service: empty contexts => fallback string, no exception."""
    from app.services import llm_service

    answer = llm_service.generate_answer("any question", [])
    assert answer == INSUFFICIENT_CONTEXT_REPLY
