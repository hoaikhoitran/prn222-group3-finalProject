"""
Tests for the Gemini GENERATION provider in app/services/llm_service.py.

These tests never hit the real Gemini API. We:
  * verify the prompt builder includes the question, the retrieved chunk
    text, and the per-chunk metadata (fileName, pageNumber, chunkIndex);
  * verify a missing LLM_API_KEY produces a clear configuration error;
  * mock httpx.post to prove generate_answer() returns the parsed answer
    text and that the request body contains ONLY the retrieved chunks
    (no full document, no embeddings);
  * confirm mock mode still works.

The boundary under test: Gemini is the generation step only. The provider
receives the already-retrieved `contexts` and must build its prompt purely
from them.
"""

from __future__ import annotations

from typing import Any

import pytest

from app.core.config import settings
from app.services import llm_service
from app.services.llm_service import (
    INSUFFICIENT_CONTEXT_REPLY,
    LLMConfigurationError,
    LLMProviderError,
    build_gemini_prompt,
    generate_answer,
)


def _sample_contexts() -> list[dict[str, Any]]:
    return [
        {
            "id": "doc_007::chunk::2",
            "text": "Chó già cần được chăm sóc nhẹ nhàng hơn vì giảm thị lực và thính lực.",
            "metadata": {
                "documentId": "doc_007",
                "fileName": "cham_soc_cho.pdf",
                "pageNumber": 4,
                "chunkIndex": 2,
            },
            "distance": 0.12,
        },
        {
            "id": "doc_007::chunk::3",
            "text": "Chúng dễ bị đau khớp, rụng răng và giảm vận động.",
            "metadata": {
                "documentId": "doc_007",
                "fileName": "cham_soc_cho.pdf",
                "pageNumber": 5,
                "chunkIndex": 3,
            },
            "distance": 0.15,
        },
    ]


# ---------------------------------------------------------------------------
# Prompt builder
# ---------------------------------------------------------------------------


def test_prompt_builder_includes_question_chunks_and_metadata() -> None:
    contexts = _sample_contexts()
    prompt = build_gemini_prompt("Chó già cần được chăm sóc như thế nào?", contexts)

    # Question is present.
    assert "QUESTION:" in prompt
    assert "Chó già cần được chăm sóc như thế nào?" in prompt

    # Both retrieved chunk texts are present.
    assert "chăm sóc nhẹ nhàng hơn" in prompt
    assert "đau khớp, rụng răng" in prompt

    # Per-chunk metadata is present.
    assert "Source: cham_soc_cho.pdf" in prompt
    assert "Page: 4" in prompt
    assert "Page: 5" in prompt
    assert "Chunk: 2" in prompt
    assert "Chunk: 3" in prompt

    # Grounding instructions are present.
    assert "Answer in Vietnamese." in prompt
    assert INSUFFICIENT_CONTEXT_REPLY in prompt


def test_prompt_builder_handles_missing_page_number() -> None:
    contexts = [
        {
            "text": "Nội dung không có số trang.",
            "metadata": {"fileName": "notes.txt", "pageNumber": None, "chunkIndex": 0},
        }
    ]
    prompt = build_gemini_prompt("Câu hỏi?", contexts)
    assert "Page: n/a" in prompt
    assert "Chunk: 0" in prompt


def test_prompt_builder_caps_chunk_count() -> None:
    contexts = [
        {
            "text": f"chunk number {i}",
            "metadata": {"fileName": "f.pdf", "pageNumber": i, "chunkIndex": i},
        }
        for i in range(10)
    ]
    prompt = build_gemini_prompt("q", contexts)
    # Only the first MAX_GEMINI_CHUNKS chunks should appear.
    assert "chunk number 4" in prompt
    assert "chunk number 5" not in prompt
    assert f"[C{llm_service.MAX_GEMINI_CHUNKS}]" in prompt
    assert f"[C{llm_service.MAX_GEMINI_CHUNKS + 1}]" not in prompt


def test_prompt_builder_truncates_long_chunk_text() -> None:
    long_text = "x" * 5000
    contexts = [
        {"text": long_text, "metadata": {"fileName": "f.pdf", "pageNumber": 1, "chunkIndex": 0}}
    ]
    prompt = build_gemini_prompt("q", contexts)
    # The full 5000-char chunk must NOT be sent verbatim.
    assert long_text not in prompt
    assert "…" in prompt
    # Truncated to roughly MAX_CHUNK_CHARS characters (small margin allowed).
    assert prompt.count("x") <= llm_service.MAX_CHUNK_CHARS + 10


# ---------------------------------------------------------------------------
# Configuration errors
# ---------------------------------------------------------------------------


def test_missing_api_key_raises_clear_error(monkeypatch) -> None:
    monkeypatch.setattr(settings, "MOCK_LLM", False)
    monkeypatch.setattr(settings, "LLM_PROVIDER", "gemini")
    monkeypatch.setattr(settings, "LLM_API_KEY", "")
    monkeypatch.setattr(settings, "LLM_MODEL_NAME", "gemini-2.5-flash-lite")

    with pytest.raises(LLMConfigurationError) as exc_info:
        generate_answer("Chó già?", _sample_contexts())

    assert "LLM_API_KEY" in str(exc_info.value)


def test_missing_model_name_raises_clear_error(monkeypatch) -> None:
    monkeypatch.setattr(settings, "MOCK_LLM", False)
    monkeypatch.setattr(settings, "LLM_PROVIDER", "gemini")
    monkeypatch.setattr(settings, "LLM_API_KEY", "fake-key")
    monkeypatch.setattr(settings, "LLM_MODEL_NAME", "")

    with pytest.raises(LLMConfigurationError) as exc_info:
        generate_answer("Chó già?", _sample_contexts())

    assert "LLM_MODEL_NAME" in str(exc_info.value)


# ---------------------------------------------------------------------------
# Real Gemini call (mocked transport)
# ---------------------------------------------------------------------------


class _FakeResponse:
    def __init__(self, status_code: int, payload: dict[str, Any]) -> None:
        self.status_code = status_code
        self._payload = payload
        self.text = str(payload)

    def json(self) -> dict[str, Any]:
        return self._payload


def test_gemini_call_returns_parsed_answer_and_sends_only_chunks(monkeypatch) -> None:
    monkeypatch.setattr(settings, "MOCK_LLM", False)
    monkeypatch.setattr(settings, "LLM_PROVIDER", "gemini")
    monkeypatch.setattr(settings, "LLM_API_KEY", "fake-key-123")
    monkeypatch.setattr(settings, "LLM_MODEL_NAME", "gemini-2.5-flash-lite")

    captured: dict[str, Any] = {}

    def fake_post(url, params=None, json=None, headers=None, timeout=None):  # noqa: ANN001
        captured["url"] = url
        captured["headers"] = headers
        captured["json"] = json
        return _FakeResponse(
            200,
            {
                "candidates": [
                    {"content": {"parts": [{"text": "Chó già cần chăm sóc nhẹ nhàng hơn."}]}}
                ]
            },
        )

    monkeypatch.setattr(llm_service.httpx, "post", fake_post)

    contexts = _sample_contexts()
    answer = generate_answer("Chó già cần được chăm sóc như thế nào?", contexts)

    assert answer == "Chó già cần chăm sóc nhẹ nhàng hơn."

    # Correct model in the URL; key is sent via header (never in the URL).
    assert "gemini-2.5-flash-lite:generateContent" in captured["url"]
    assert "fake-key-123" not in captured["url"]
    assert captured["headers"]["x-goog-api-key"] == "fake-key-123"

    # System instruction is the strict grounding prompt.
    sent = captured["json"]
    sys_text = sent["system_instruction"]["parts"][0]["text"]
    assert "Answer only based on the provided document context" in sys_text
    assert INSUFFICIENT_CONTEXT_REPLY in sys_text

    # The user content contains the retrieved chunk text + metadata only.
    user_text = sent["contents"][0]["parts"][0]["text"]
    assert "chăm sóc nhẹ nhàng hơn" in user_text
    assert "Source: cham_soc_cho.pdf" in user_text

    # No embeddings / vector values and no file paths leak into the request.
    serialized = str(sent)
    assert "distance" not in serialized
    assert "embedding" not in serialized.lower()


def test_gemini_unavailable_model_raises_provider_error(monkeypatch) -> None:
    monkeypatch.setattr(settings, "MOCK_LLM", False)
    monkeypatch.setattr(settings, "LLM_PROVIDER", "gemini")
    monkeypatch.setattr(settings, "LLM_API_KEY", "fake-key")
    monkeypatch.setattr(settings, "LLM_MODEL_NAME", "gemini-does-not-exist")

    def fake_post(*args, **kwargs):  # noqa: ANN002, ANN003
        return _FakeResponse(404, {"error": {"message": "model not found"}})

    monkeypatch.setattr(llm_service.httpx, "post", fake_post)

    with pytest.raises(LLMProviderError) as exc_info:
        generate_answer("q", _sample_contexts())

    assert "unavailable" in str(exc_info.value).lower()


# ---------------------------------------------------------------------------
# Mock mode still works
# ---------------------------------------------------------------------------


def test_mock_mode_still_works(monkeypatch) -> None:
    monkeypatch.setattr(settings, "MOCK_LLM", True)
    # Even with a gemini provider configured, MOCK_LLM=true wins.
    monkeypatch.setattr(settings, "LLM_PROVIDER", "gemini")

    answer = generate_answer("Chó già cần được chăm sóc như thế nào?", _sample_contexts())

    assert answer != INSUFFICIENT_CONTEXT_REPLY
    assert "chăm sóc nhẹ nhàng hơn" in answer
    # The mock prefixes with the source document, proving no API was called.
    assert "Theo tài liệu" in answer


def test_provider_mock_uses_mock_behavior(monkeypatch) -> None:
    monkeypatch.setattr(settings, "MOCK_LLM", False)
    monkeypatch.setattr(settings, "LLM_PROVIDER", "mock")

    answer = generate_answer("Chó già cần được chăm sóc như thế nào?", _sample_contexts())
    assert answer != INSUFFICIENT_CONTEXT_REPLY
    assert "Theo tài liệu" in answer
