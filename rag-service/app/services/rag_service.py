"""
app/services/rag_service.py
===========================

THE MAIN RAG PIPELINE.

This file is the heart of the service. It orchestrates the other
services and exposes two high-level operations:

  * index_document(...) — preprocess + persist a document
  * ask(...)            — answer a question grounded in the documents

The acronym "RAG" stands for:

  R = RETRIEVAL
      Find the most relevant chunks from the vector database for the
      user's question. (See: vector_store_service.search)

  A = AUGMENTATION
      Inject those chunks into the prompt as "context" so the LLM
      grounds its answer in real document content (no hallucination).

  G = GENERATION
      Ask the LLM (or the local mock) to produce the final answer using
      the augmented prompt.

Why is this file the "main RAG pipeline"?
-----------------------------------------
Because it is the only place where document_loader, chunking_service,
embedding_service, vector_store_service, and llm_service are all
combined into a coherent workflow. Each of those modules is independent
and reusable; this file is the conductor of the orchestra.
"""

from __future__ import annotations

import logging
from typing import Any

from app.core.config import settings
from app.services.chunking_service import chunk_pages
from app.services.document_loader import load_document
from app.services.embedding_service import embedding_service
from app.services.llm_service import INSUFFICIENT_CONTEXT_REPLY, generate_answer
from app.services.vector_store_service import vector_store_service
from app.utils.file_utils import file_exists, get_file_extension, is_supported_file
from app.utils.text_utils import preview

logger = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
# INDEXING (write path)
# ---------------------------------------------------------------------------


def index_document(
    *,
    document_id: str,
    course_code: str,
    chapter: str,
    file_path: str,
    file_name: str,
    chunk_mode: str = "Characters",
    chunk_size: int | None = None,
    chunk_overlap: int | None = None,
    min_chunk_length: int | None = None,
    max_chunks: int | None = None,
) -> dict[str, Any]:
    """
    Read a file, chunk it, embed every chunk, and store everything in
    ChromaDB. If the document was already indexed, the old chunks are
    deleted first so the operation is idempotent.

    Returns a dict ready to be serialized as IndexDocumentResponse.
    """
    # --- 1. Validate the file ----------------------------------------
    if not file_path:
        raise ValueError("filePath is required.")
    if not file_exists(file_path):
        raise FileNotFoundError(f"File not found on disk: {file_path}")
    if not is_supported_file(file_path):
        raise ValueError(
            "Unsupported file type. Supported extensions: .pdf, .docx, .pptx, .txt"
        )

    file_type = get_file_extension(file_path).lstrip(".")  # "pdf", "docx", ...

    # --- 2. Load + chunk ---------------------------------------------
    logger.info("Loading document %s (%s)", document_id, file_path)
    pages = load_document(file_path)
    if not pages:
        raise RuntimeError("The document is empty or could not be read.")

    chunks = chunk_pages(
        pages,
        chunk_mode=chunk_mode,
        chunk_size=chunk_size,
        chunk_overlap=chunk_overlap,
        min_chunk_length=min_chunk_length,
        max_chunks=max_chunks,
    )
    if not chunks:
        raise RuntimeError("Chunking produced no chunks (the document may be empty).")

    # --- 3. Embed every chunk ----------------------------------------
    # Embedding is the slowest step; doing it as a single batch is much
    # faster than one-by-one.
    logger.info("Embedding %d chunks for document %s", len(chunks), document_id)
    chunk_texts = [c["chunkText"] for c in chunks]
    embeddings = embedding_service.embed_texts(chunk_texts)

    # --- 4. Re-index support: clear old chunks of this documentId ----
    removed = vector_store_service.delete_document(document_id)
    if removed:
        logger.info(
            "Re-indexing: removed %d previously stored chunks for %s",
            removed,
            document_id,
        )

    # --- 5. Persist new chunks ---------------------------------------
    total = vector_store_service.add_chunks(
        document_id=document_id,
        course_code=course_code,
        chapter=chapter,
        file_name=file_name,
        file_type=file_type,
        chunks=chunks,
        embeddings=embeddings,
    )

    return {
        "documentId": document_id,
        "status": "indexed",
        "totalChunks": total,
        "message": "Document indexed successfully.",
    }


# ---------------------------------------------------------------------------
# ASKING (read path)
# ---------------------------------------------------------------------------


def ask(
    *,
    question: str,
    document_id: str,
    course_code: str,
    top_k: int | None = None,
    conversation_history: list[dict[str, str]] | None = None,
) -> dict[str, Any]:
    """
    Answer a question using the documents we've already indexed.

    Pipeline:
      1. Validate input.
      2. Embed the question (R).
      3. Search the vector DB for the topK closest chunks, filtered by
         the relevance threshold RAG_MAX_DISTANCE (R).
      4. If fewer than RAG_MIN_RELEVANT_CHUNKS chunks passed the filter,
         skip generation entirely and return the standard fallback.
      5. Otherwise build the context (A) and ask the LLM / mock (G).
      6. Format sources for the response.

    Why steps 3 and 4 work together
    -------------------------------
    ChromaDB's `query` always returns top_k *nearest* chunks, even when
    the nearest ones are still semantically far from the question.
    Without a distance threshold, an out-of-scope question would still
    receive a confident-sounding answer based on irrelevant context.
    The combination (distance cap + minimum-count gate) makes the
    service refuse to answer when retrieval is too weak.
    """
    # 1. Validate
    question = (question or "").strip()
    if not question:
        raise ValueError("question must not be empty.")

    effective_top_k = top_k if top_k and top_k > 0 else settings.DEFAULT_TOP_K
    contextual_question = _build_contextual_question(question, conversation_history or [])

    # 2. Embed the question.
    question_embedding = embedding_service.embed_text(contextual_question)

    # 3. Retrieval — the vector store already drops chunks whose distance
    # exceeds RAG_MAX_DISTANCE, so `hits` only contains *relevant* chunks.
    hits = vector_store_service.search(
        question_embedding=question_embedding,
        document_id=document_id,
        course_code=course_code,
        top_k=effective_top_k,
    )

    # 4. Relevance gate. If retrieval found nothing relevant enough,
    # return the mandatory Vietnamese fallback and an empty sources list.
    # We do NOT call llm_service here — there is no context to ground on.
    if len(hits) < settings.RAG_MIN_RELEVANT_CHUNKS:
        logger.info(
            "Relevance gate failed: %d relevant chunks (need >= %d) for question %r",
            len(hits),
            settings.RAG_MIN_RELEVANT_CHUNKS,
            contextual_question[:80],
        )
        return {"answer": INSUFFICIENT_CONTEXT_REPLY, "sources": []}

    # 5. Augmentation + Generation.
    answer = generate_answer(contextual_question, hits)

    # 6. Build the `sources` array. `distance` is included for debugging /
    # threshold tuning; it stays None if Chroma didn't return one.
    sources: list[dict[str, Any]] = []
    for hit in hits:
        meta = hit.get("metadata") or {}
        page = meta.get("pageNumber", -1)
        sources.append(
            {
                "documentId": meta.get("documentId", document_id),
                "fileName": meta.get("fileName", ""),
                "pageNumber": int(page) if page and page != -1 else None,
                "chunkIndex": int(meta.get("chunkIndex", 0)),
                "text": preview(hit.get("text", ""), 400),
                "distance": hit.get("distance"),
            }
        )

    return {"answer": answer, "sources": sources}


def _build_contextual_question(
    question: str,
    conversation_history: list[dict[str, str]],
) -> str:
    """
    Fold recent conversation turns into the retrieval query.

    The vector search is still restricted by documentId/courseCode and the
    answer is still generated only from retrieved document chunks. History only
    helps follow-up questions such as "what about the second one?" carry enough
    context to retrieve the right passage.
    """
    recent_turns = conversation_history[-3:]

    if not recent_turns:
        return question

    lines: list[str] = ["Previous conversation in this session:"]

    for turn in recent_turns:
        previous_question = (turn.get("question") or "").strip()
        previous_answer = (turn.get("answer") or "").strip()

        if previous_question:
            lines.append(f"Student: {previous_question}")

        if previous_answer:
            lines.append(f"Assistant: {preview(previous_answer, 300)}")

    lines.append(f"Current question: {question}")

    return "\n".join(lines)


# ---------------------------------------------------------------------------
# Document admin helpers (used by the GET status / DELETE routes)
# ---------------------------------------------------------------------------


def get_document_status(document_id: str) -> dict[str, Any]:
    """Return whether a document is indexed and how many chunks it has."""
    return vector_store_service.get_document_status(document_id)


def delete_document(document_id: str) -> dict[str, Any]:
    """Delete every chunk + vector belonging to a document."""
    vector_store_service.delete_document(document_id)
    return {"documentId": document_id, "status": "deleted"}
