"""Tests for the fast local embedding backend."""

from __future__ import annotations

import math

from app.core.config import settings
from app.services.embedding_service import EmbeddingService


def test_hashing_embedding_is_fast_normalized_and_deterministic(monkeypatch) -> None:
    monkeypatch.setattr(settings, "EMBEDDING_BACKEND", "hashing")
    monkeypatch.setattr(settings, "EMBEDDING_HASH_DIMENSION", 1024)

    service = EmbeddingService()
    first = service.embed_text("Phân tích giá trị biên trong kiểm thử phần mềm")
    second = service.embed_text("Phân tích giá trị biên trong kiểm thử phần mềm")

    assert len(first) == 1024
    assert first == second

    magnitude = math.sqrt(sum(value * value for value in first))
    assert 0.99 <= magnitude <= 1.01


def test_hashing_embedding_handles_empty_text(monkeypatch) -> None:
    monkeypatch.setattr(settings, "EMBEDDING_BACKEND", "hashing")
    monkeypatch.setattr(settings, "EMBEDDING_HASH_DIMENSION", 1024)

    vector = EmbeddingService().embed_text("   ")

    assert len(vector) == 1024
    assert all(value == 0 for value in vector)
