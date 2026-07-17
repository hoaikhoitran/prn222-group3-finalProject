"""
app/services/chunking_service.py
================================

STEP 2 of the RAG pipeline: split each loaded page into smaller pieces
("chunks") using the same admin-managed configuration that the .NET app
uses for SQL preview chunks.

Supported modes
---------------
Characters
    chunk_size and chunk_overlap are measured in characters.
Words
    chunk_size and chunk_overlap are measured in words.
Paragraph
    chunk_size and chunk_overlap are measured in paragraphs.

The .NET app sends these values on every index request. The .env defaults
remain only as a fallback for direct API calls or local scripts.
"""

from __future__ import annotations

import re
from typing import Any


SUPPORTED_CHUNK_MODES = {"Characters", "Words", "Paragraph"}
DEFAULT_CHUNK_SIZE = 800
DEFAULT_CHUNK_OVERLAP = 100


def _normalize_mode(mode: str | None) -> str:
    if mode in SUPPORTED_CHUNK_MODES:
        return mode
    return "Characters"


def _normalize_options(
    *,
    chunk_mode: str | None,
    chunk_size: int | None,
    chunk_overlap: int | None,
    min_chunk_length: int | None,
    max_chunks: int | None,
) -> dict[str, Any]:
    default_size = DEFAULT_CHUNK_SIZE
    default_overlap = DEFAULT_CHUNK_OVERLAP

    if chunk_size is None or chunk_overlap is None:
        try:
            from app.core.config import settings

            default_size = settings.CHUNK_SIZE
            default_overlap = settings.CHUNK_OVERLAP
        except Exception:
            # Keep the pure chunking function testable without the full FastAPI
            # dependency stack installed.
            pass

    size = chunk_size if chunk_size is not None else default_size
    overlap = chunk_overlap if chunk_overlap is not None else default_overlap

    size = max(1, int(size))
    overlap = max(0, int(overlap))

    if overlap >= size:
        overlap = max(0, size // 4)

    return {
        "mode": _normalize_mode(chunk_mode),
        "size": size,
        "overlap": overlap,
        "min_length": max(0, int(min_chunk_length or 0)),
        "max_chunks": max(1, min(int(max_chunks or 10000), 10000)),
    }


def _split_characters(text: str, chunk_size: int, chunk_overlap: int) -> list[str]:
    text = (text or "").strip()
    if not text:
        return []

    chunks: list[str] = []
    start = 0

    while start < len(text):
        hard_end = min(start + chunk_size, len(text))
        end = _find_word_break(text, start, hard_end) if hard_end < len(text) else hard_end

        if end <= start:
            end = hard_end

        piece = text[start:end].strip()
        if piece:
            chunks.append(piece)

        if end >= len(text):
            break

        start = max(0, end - chunk_overlap)
        while start < end and start < len(text) and text[start].isspace():
            start += 1

    return chunks


def _split_words(text: str, chunk_size: int, chunk_overlap: int) -> list[str]:
    words = re.findall(r"\S+", text or "")
    if not words:
        return []

    chunks: list[str] = []
    stride = max(1, chunk_size - chunk_overlap)

    start = 0
    while start < len(words):
        end = min(start + chunk_size, len(words))
        piece = " ".join(words[start:end]).strip()
        if piece:
            chunks.append(piece)
        if end >= len(words):
            break
        start += stride

    return chunks


def _split_paragraphs(text: str, chunk_size: int, chunk_overlap: int) -> list[str]:
    paragraphs = [
        paragraph.strip()
        for paragraph in re.split(r"(?:\n\s*){2,}", text or "")
        if paragraph and paragraph.strip()
    ]

    if len(paragraphs) <= 1:
        paragraphs = [
            paragraph.strip()
            for paragraph in (text or "").split("\n")
            if paragraph and paragraph.strip()
        ]

    if not paragraphs:
        return []

    chunks: list[str] = []
    stride = max(1, chunk_size - chunk_overlap)

    start = 0
    while start < len(paragraphs):
        end = min(start + chunk_size, len(paragraphs))
        piece = "\n\n".join(paragraphs[start:end]).strip()
        if piece:
            chunks.append(piece)
        if end >= len(paragraphs):
            break
        start += stride

    return chunks


def _find_word_break(text: str, start: int, hard_end: int) -> int:
    for index in range(hard_end - 1, start, -1):
        if text[index].isspace():
            return index
    return hard_end


def _split_text(
    text: str,
    *,
    chunk_mode: str,
    chunk_size: int,
    chunk_overlap: int,
) -> list[str]:
    if chunk_mode == "Words":
        return _split_words(text, chunk_size, chunk_overlap)
    if chunk_mode == "Paragraph":
        return _split_paragraphs(text, chunk_size, chunk_overlap)
    return _split_characters(text, chunk_size, chunk_overlap)


def chunk_pages(
    pages: list[dict[str, Any]],
    chunk_size: int | None = None,
    chunk_overlap: int | None = None,
    chunk_mode: str | None = "Characters",
    min_chunk_length: int | None = 0,
    max_chunks: int | None = 1000,
) -> list[dict[str, Any]]:
    """
    Convert a list of pages (output of document_loader.load_document)
    into a flat list of chunks.

    Input shape (per page):
        {"text": "...", "pageNumber": 1 | None, "source": "file.pdf"}

    Output shape (per chunk):
        {"chunkText": "...", "pageNumber": 1 | None, "chunkIndex": 0}

    chunkIndex is the GLOBAL position of this chunk across the whole
    document, starting at 0.
    """
    options = _normalize_options(
        chunk_mode=chunk_mode,
        chunk_size=chunk_size,
        chunk_overlap=chunk_overlap,
        min_chunk_length=min_chunk_length,
        max_chunks=max_chunks,
    )

    chunks: list[dict[str, Any]] = []
    global_index = 0

    for page in pages:
        page_text = page.get("text", "")
        page_number = page.get("pageNumber")

        pieces = _split_text(
            page_text,
            chunk_mode=options["mode"],
            chunk_size=options["size"],
            chunk_overlap=options["overlap"],
        )

        for piece in pieces:
            if not piece.strip() or len(piece) < options["min_length"]:
                continue

            chunks.append(
                {
                    "chunkText": piece,
                    "pageNumber": page_number,
                    "chunkIndex": global_index,
                }
            )
            global_index += 1

            if len(chunks) >= options["max_chunks"]:
                return chunks

    return chunks
