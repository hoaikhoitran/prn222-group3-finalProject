"""
Tests for citation tracking + real token usage in the ask pipeline.

Covered here:
  * extract_used_citation_ids: valid IDs, hallucinated IDs, duplicates,
    grouped forms ("[C1, C3]"), ordering.
  * rag_service.ask(): only the chunks the model cited come back in
    `sources`; the full retrieval stays in `retrievedSources`; the
    provider-reported usage is passed through untouched.
  * Gemini usage extraction from usageMetadata (mocked transport) —
    values must come from the provider, never from len/4 style estimates.
  * Mock mode: usage is None (no provider was called).
"""

from __future__ import annotations

from typing import Any

import app.services.rag_service as rag_service
from app.core.config import settings
from app.services import llm_service
from app.services.llm_service import (
    INSUFFICIENT_CONTEXT_REPLY,
    build_gemini_prompt,
    generate_answer_with_usage,
)
from app.services.rag_service import extract_used_citation_ids


# ---------------------------------------------------------------------------
# extract_used_citation_ids
# ---------------------------------------------------------------------------

VALID_IDS = ["C1", "C2", "C3", "C4", "C5"]


def test_extract_single_and_multiple_citations() -> None:
    answer = "Nội dung A [C1]. Nội dung B [C3]."
    assert extract_used_citation_ids(answer, VALID_IDS) == ["C1", "C3"]


def test_extract_grouped_citations() -> None:
    answer = "Cả hai ý đều đúng [C1, C3]."
    assert extract_used_citation_ids(answer, VALID_IDS) == ["C1", "C3"]


def test_extract_drops_hallucinated_ids() -> None:
    answer = "Thông tin [C2] và một nguồn bịa [C9]."
    assert extract_used_citation_ids(answer, VALID_IDS) == ["C2"]


def test_extract_deduplicates() -> None:
    answer = "Ý một [C2]. Ý hai [C2]. Ý ba [C2, C1]."
    assert extract_used_citation_ids(answer, VALID_IDS) == ["C1", "C2"]


def test_extract_is_case_insensitive() -> None:
    answer = "Nội dung [c4]."
    assert extract_used_citation_ids(answer, VALID_IDS) == ["C4"]


def test_extract_ignores_unbracketed_mentions() -> None:
    # "C1" without brackets is prose, not a citation marker.
    answer = "Vitamin C1 rất tốt nhưng không phải citation."
    assert extract_used_citation_ids(answer, VALID_IDS) == []


def test_extract_empty_answer() -> None:
    assert extract_used_citation_ids("", VALID_IDS) == []


# ---------------------------------------------------------------------------
# rag_service.ask() — used-source filtering + usage passthrough
# ---------------------------------------------------------------------------


class _FakeEmbeddingService:
    def embed_text(self, text: str) -> list[float]:
        return [0.1] * 8

    def embed_texts(self, texts: list[str]) -> list[list[float]]:
        return [[0.1] * 8 for _ in texts]


class _FakeVectorStore:
    def __init__(self, hits: list[dict[str, Any]]) -> None:
        self._hits = hits

    def search(self, **kwargs: Any) -> list[dict[str, Any]]:
        return self._hits


def _five_hits() -> list[dict[str, Any]]:
    return [
        {
            "id": f"doc_1::chunk::{i}",
            "text": f"Nội dung chunk số {i}.",
            "metadata": {
                "documentId": "doc_1",
                "fileName": "lecture01.pdf",
                "pageNumber": i + 1,
                "chunkIndex": i,
            },
            "distance": 0.1 + i * 0.02,
        }
        for i in range(5)
    ]


def _patch_retrieval(monkeypatch, hits: list[dict[str, Any]]) -> None:
    monkeypatch.setattr(rag_service, "embedding_service", _FakeEmbeddingService())
    monkeypatch.setattr(rag_service, "vector_store_service", _FakeVectorStore(hits))


def test_ask_returns_only_cited_sources(monkeypatch) -> None:
    _patch_retrieval(monkeypatch, _five_hits())

    monkeypatch.setattr(
        rag_service,
        "generate_answer_with_usage",
        lambda question, contexts: {
            "answer": "Ý đầu [C1]. Ý sau [C3].",
            "usage": {"promptTokens": 120, "completionTokens": 45, "totalTokens": 165},
        },
    )

    result = rag_service.ask(question="q?", document_id="doc_1", course_code="PRN222")

    assert result["usedCitationIds"] == ["C1", "C3"]
    assert [s["citationId"] for s in result["sources"]] == ["C1", "C3"]
    # C1 maps to hits[0] (chunkIndex 0), C3 maps to hits[2] (chunkIndex 2).
    assert [s["chunkIndex"] for s in result["sources"]] == [0, 2]
    # Full retrieval is still available for debugging.
    assert len(result["retrievedSources"]) == 5
    # Provider usage passes through untouched.
    assert result["usage"] == {
        "promptTokens": 120,
        "completionTokens": 45,
        "totalTokens": 165,
    }


def test_ask_hallucinated_citation_never_maps_to_wrong_chunk(monkeypatch) -> None:
    _patch_retrieval(monkeypatch, _five_hits())

    monkeypatch.setattr(
        rag_service,
        "generate_answer_with_usage",
        lambda question, contexts: {
            "answer": "Ý thật [C2]. Nguồn bịa [C77].",
            "usage": None,
        },
    )

    result = rag_service.ask(question="q?", document_id="doc_1", course_code="PRN222")

    assert result["usedCitationIds"] == ["C2"]
    assert [s["citationId"] for s in result["sources"]] == ["C2"]


def test_ask_duplicate_citations_yield_one_source(monkeypatch) -> None:
    _patch_retrieval(monkeypatch, _five_hits())

    monkeypatch.setattr(
        rag_service,
        "generate_answer_with_usage",
        lambda question, contexts: {
            "answer": "A [C2]. B [C2]. C [C2].",
            "usage": None,
        },
    )

    result = rag_service.ask(question="q?", document_id="doc_1", course_code="PRN222")
    assert [s["citationId"] for s in result["sources"]] == ["C2"]


def test_ask_no_citations_falls_back_to_full_retrieval(monkeypatch) -> None:
    _patch_retrieval(monkeypatch, _five_hits())

    monkeypatch.setattr(
        rag_service,
        "generate_answer_with_usage",
        lambda question, contexts: {"answer": "Trả lời không kèm citation.", "usage": None},
    )

    result = rag_service.ask(question="q?", document_id="doc_1", course_code="PRN222")
    # Legacy behaviour: without citation markers we keep full provenance.
    assert len(result["sources"]) == 5


def test_ask_insufficient_answer_has_no_sources(monkeypatch) -> None:
    _patch_retrieval(monkeypatch, _five_hits())

    monkeypatch.setattr(
        rag_service,
        "generate_answer_with_usage",
        lambda question, contexts: {"answer": INSUFFICIENT_CONTEXT_REPLY, "usage": None},
    )

    result = rag_service.ask(question="q?", document_id="doc_1", course_code="PRN222")
    assert result["sources"] == []
    assert result["usedCitationIds"] == []


def test_ask_relevance_gate_returns_null_usage(monkeypatch) -> None:
    _patch_retrieval(monkeypatch, [])
    monkeypatch.setattr(settings, "RAG_MIN_RELEVANT_CHUNKS", 1)

    result = rag_service.ask(question="q?", document_id="doc_1", course_code="PRN222")

    assert result["answer"] == INSUFFICIENT_CONTEXT_REPLY
    assert result["sources"] == []
    assert result["usage"] is None


# ---------------------------------------------------------------------------
# Gemini prompt carries citation IDs
# ---------------------------------------------------------------------------


def test_gemini_prompt_labels_chunks_with_citation_ids() -> None:
    contexts = _five_hits()
    prompt = build_gemini_prompt("q?", contexts)

    assert "[C1]" in prompt
    assert "[C5]" in prompt
    assert "citation ID" in prompt
    # The model must be told not to invent IDs.
    assert "Never invent new citation IDs" in prompt


# ---------------------------------------------------------------------------
# Gemini usage extraction (mocked transport — no real API call)
# ---------------------------------------------------------------------------


class _FakeResponse:
    def __init__(self, status_code: int, payload: dict[str, Any]) -> None:
        self.status_code = status_code
        self._payload = payload
        self.text = str(payload)

    def json(self) -> dict[str, Any]:
        return self._payload


def _gemini_env(monkeypatch) -> None:
    monkeypatch.setattr(settings, "MOCK_LLM", False)
    monkeypatch.setattr(settings, "LLM_PROVIDER", "gemini")
    monkeypatch.setattr(settings, "LLM_API_KEY", "fake-key")
    monkeypatch.setattr(settings, "LLM_MODEL_NAME", "gemini-2.5-flash-lite")


def test_gemini_usage_comes_from_usage_metadata(monkeypatch) -> None:
    _gemini_env(monkeypatch)

    def fake_post(url, params=None, json=None, headers=None, timeout=None):  # noqa: ANN001
        return _FakeResponse(
            200,
            {
                "candidates": [
                    {"content": {"parts": [{"text": "Trả lời [C1]."}]}}
                ],
                "usageMetadata": {
                    "promptTokenCount": 321,
                    "candidatesTokenCount": 87,
                    "totalTokenCount": 408,
                },
            },
        )

    monkeypatch.setattr(llm_service.httpx, "post", fake_post)

    contexts = _five_hits()
    result = generate_answer_with_usage("q?", contexts)

    assert result["answer"] == "Trả lời [C1]."
    assert result["usage"] == {
        "promptTokens": 321,
        "completionTokens": 87,
        "totalTokens": 408,
    }


def test_gemini_missing_usage_metadata_yields_none(monkeypatch) -> None:
    _gemini_env(monkeypatch)

    def fake_post(url, params=None, json=None, headers=None, timeout=None):  # noqa: ANN001
        return _FakeResponse(
            200,
            {"candidates": [{"content": {"parts": [{"text": "Trả lời."}]}}]},
        )

    monkeypatch.setattr(llm_service.httpx, "post", fake_post)

    result = generate_answer_with_usage("q?", _five_hits())
    # No usageMetadata -> usage stays None. It must never be estimated.
    assert result["usage"] is None


def test_mock_mode_reports_no_usage(monkeypatch) -> None:
    monkeypatch.setattr(settings, "MOCK_LLM", True)

    result = generate_answer_with_usage("chunk?", _five_hits())

    assert result["usage"] is None
    # The mock answers from contexts[0] and cites it.
    assert "[C1]" in result["answer"]
