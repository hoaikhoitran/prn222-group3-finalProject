"""
app/models/rag_request.py
=========================

Pydantic request models. They:
  * validate the incoming JSON body
  * auto-generate clear Swagger / OpenAPI documentation
  * give the rest of the code typed objects to work with
"""

from __future__ import annotations

from pydantic import BaseModel, Field


class IndexDocumentRequest(BaseModel):
    """Request body for POST /rag/index-document."""

    documentId: str = Field(
        ...,
        description="Stable, unique ID of the document (used as ChromaDB key).",
        examples=["doc_001"],
    )
    courseCode: str = Field(
        ...,
        description="Course code (e.g. PRN222). Used to filter searches.",
        examples=["PRN222"],
    )
    chapter: str = Field(
        default="",
        description="Optional chapter / section label (e.g. 'Chapter 1').",
        examples=["Chapter 1"],
    )
    filePath: str = Field(
        ...,
        description="Local path to the document on disk (PDF/DOCX/PPTX/TXT).",
        examples=["./storage/documents/sample.pdf"],
    )
    fileName: str = Field(
        ...,
        description="Original file name (shown later in answer sources).",
        examples=["PRN222_Chapter_1.pdf"],
    )
    chunkMode: str = Field(
        default="Characters",
        description="Chunking mode sent by the .NET admin config: Characters, Words, or Paragraph.",
        examples=["Characters"],
    )
    chunkSize: int = Field(
        default=1500,
        ge=1,
        le=10000,
        description="Chunk size. Unit depends on chunkMode.",
        examples=[1500],
    )
    chunkOverlap: int = Field(
        default=250,
        ge=0,
        le=5000,
        description="Overlap between neighboring chunks. Unit depends on chunkMode.",
        examples=[250],
    )
    minChunkLength: int = Field(
        default=80,
        ge=0,
        le=2000,
        description="Drop chunks whose final text length is shorter than this many characters.",
        examples=[80],
    )
    maxPreviewChunks: int = Field(
        default=200,
        ge=1,
        le=1000,
        description="Maximum number of chunks to index for this document.",
        examples=[200],
    )


class ConversationTurn(BaseModel):
    """One previous question-answer turn from the same chat session."""

    question: str = Field(default="", description="Previous user question.")
    answer: str = Field(default="", description="Previous assistant answer.")


class AskRequest(BaseModel):
    """Request body for POST /rag/ask."""

    sessionId: str = Field(default="", description="Chat session ID (optional).")
    userId: str = Field(default="", description="User ID (optional).")
    courseCode: str = Field(
        ...,
        description="Course code to restrict the search to.",
        examples=["PRN222"],
    )
    documentId: str = Field(
        default="",
        description="Optional document ID to restrict the search to. Empty means search the whole course.",
        examples=["doc_001"],
    )
    question: str = Field(
        ...,
        min_length=1,
        description="The student question (Vietnamese or English).",
        examples=["MVC trong ASP.NET Core là gì?"],
    )
    topK: int | None = Field(
        default=None,
        ge=1,
        le=50,
        description="How many chunks to retrieve. Defaults to DEFAULT_TOP_K.",
        examples=[5],
    )
    conversationHistory: list[ConversationTurn] = Field(
        default_factory=list,
        description="Previous turns from the same chat session, oldest first.",
    )
