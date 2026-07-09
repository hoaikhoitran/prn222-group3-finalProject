"""
app/api/rag_routes.py
=====================

HTTP routes for the RAG service.

Design rule
-----------
Routes here MUST stay thin. They only:
  1. accept the request (already validated by Pydantic),
  2. call the appropriate function in rag_service,
  3. shape and return the response,
  4. translate domain errors into clear HTTP error codes.

All real RAG logic lives in app/services/rag_service.py. This keeps
controllers easy to read and lets us unit-test the pipeline without
spinning up a web server.
"""

from __future__ import annotations

import logging

from fastapi import APIRouter, Depends, HTTPException, Path, status

from app.core.config import settings
from app.core.security import verify_api_key
from app.models.rag_request import AskRequest, IndexDocumentRequest
from app.models.rag_response import (
    AskResponse,
    DeleteDocumentResponse,
    DocumentStatusResponse,
    HealthResponse,
    IndexDocumentResponse,
)
from app.services import rag_service

logger = logging.getLogger(__name__)

# All RAG routes live under /rag/* and require the API key (when one is set).
router = APIRouter(prefix="/rag", tags=["rag"], dependencies=[Depends(verify_api_key)])

# Health is a separate router with no API-key check so monitoring tools can
# call it cheaply.
health_router = APIRouter(tags=["health"])


# ---------------------------------------------------------------------------
# Health
# ---------------------------------------------------------------------------
@health_router.get("/health", response_model=HealthResponse, summary="Liveness probe")
def health() -> HealthResponse:
    return HealthResponse(status="ok", service=settings.APP_NAME)


# ---------------------------------------------------------------------------
# Index a document
# ---------------------------------------------------------------------------
@router.post(
    "/index-document",
    response_model=IndexDocumentResponse,
    summary="Index a document into the vector database",
)
def index_document(payload: IndexDocumentRequest) -> IndexDocumentResponse:
    try:
        result = rag_service.index_document(
            document_id=payload.documentId,
            course_code=payload.courseCode,
            chapter=payload.chapter,
            file_path=payload.filePath,
            file_name=payload.fileName,
            chunk_mode=payload.chunkMode,
            chunk_size=payload.chunkSize,
            chunk_overlap=payload.chunkOverlap,
            min_chunk_length=payload.minChunkLength,
            max_chunks=payload.maxPreviewChunks,
        )
    except FileNotFoundError as exc:
        # Surface "file not found" as a 400 because the .NET API supplied a bad path.
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST, detail=str(exc)
        ) from exc
    except ValueError as exc:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST, detail=str(exc)
        ) from exc
    except RuntimeError as exc:
        # Document existed but couldn't be parsed: 422 (unprocessable).
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY, detail=str(exc)
        ) from exc
    except Exception as exc:  # noqa: BLE001
        logger.exception("Unexpected error while indexing %s", payload.documentId)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Indexing failed: {exc}",
        ) from exc

    return IndexDocumentResponse(**result)


# ---------------------------------------------------------------------------
# Ask a question
# ---------------------------------------------------------------------------
@router.post(
    "/ask",
    response_model=AskResponse,
    summary="Answer a question using the indexed documents",
)
def ask(payload: AskRequest) -> AskResponse:
    try:
        result = rag_service.ask(
            question=payload.question,
            document_id=payload.documentId,
            course_code=payload.courseCode,
            top_k=payload.topK,
            conversation_history=[
                {"question": turn.question, "answer": turn.answer}
                for turn in payload.conversationHistory
            ],
        )
    except ValueError as exc:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST, detail=str(exc)
        ) from exc
    except Exception as exc:  # noqa: BLE001
        logger.exception("Unexpected error while answering question")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Ask failed: {exc}",
        ) from exc

    return AskResponse(**result)


# ---------------------------------------------------------------------------
# Document status
# ---------------------------------------------------------------------------
@router.get(
    "/documents/{documentId}/status",
    response_model=DocumentStatusResponse,
    summary="Check whether a document has been indexed",
)
def document_status(
    documentId: str = Path(..., description="Document ID to look up"),
) -> DocumentStatusResponse:
    info = rag_service.get_document_status(documentId)
    return DocumentStatusResponse(**info)


# ---------------------------------------------------------------------------
# Delete a document
# ---------------------------------------------------------------------------
@router.delete(
    "/documents/{documentId}",
    response_model=DeleteDocumentResponse,
    summary="Delete every chunk + vector of a document",
)
def delete_document(
    documentId: str = Path(..., description="Document ID to remove"),
) -> DeleteDocumentResponse:
    info = rag_service.delete_document(documentId)
    return DeleteDocumentResponse(**info)
