"""
app/core/config.py
==================

Central configuration for the Retrieval-Augmented-Generation-PRN222 service.

WHY THIS FILE EXISTS
--------------------
We must NEVER hard-code values like API keys, database paths, or model
names inside the source code. Doing so would leak secrets and make the
service impossible to deploy in different environments.

Instead, every configurable value lives in environment variables, which
are loaded from a `.env` file at startup. This file defines a single
`Settings` object that the rest of the application imports and reuses.

HOW TO USE
----------
    from app.core.config import settings
    print(settings.CHUNK_SIZE)

Edit `.env` (or `.env.example` if you are setting up the project for
the first time) to change behavior — no Python code change required.
"""

from __future__ import annotations

import os
from functools import lru_cache

from dotenv import load_dotenv
from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict

# Load variables from the local `.env` file (if present) into os.environ.
# This must happen BEFORE the Settings class is instantiated so that
# pydantic-settings can pick them up.
load_dotenv()


class Settings(BaseSettings):
    """
    Strongly-typed wrapper around every environment variable the service
    cares about. Pydantic will:
      * read each variable from `.env` / the OS environment
      * cast it to the declared Python type
      * raise a helpful error if a required value is missing
    """

    # `extra="ignore"` keeps the Settings object happy if the user has
    # other unrelated variables in their environment.
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        case_sensitive=True,
        extra="ignore",
    )

    # --- General application settings ---------------------------------
    APP_NAME: str = Field(
        default="Retrieval-Augmented-Generation-PRN222",
        description="Human-readable name of the service (shown in /health).",
    )
    APP_ENV: str = Field(
        default="development",
        description="Environment label: development | staging | production.",
    )

    # If empty: API key check is disabled (good for local development).
    # If non-empty: every request must include the X-API-Key header.
    API_KEY: str = Field(default="", description="Shared secret for inbound requests.")

    # --- Vector database (ChromaDB) -----------------------------------
    CHROMA_PERSIST_DIR: str = Field(
        default="./chroma_db",
        description="Filesystem directory where ChromaDB stores its data.",
    )
    CHROMA_COLLECTION_NAME: str = Field(
        default="prn222_documents",
        description="Name of the ChromaDB collection that holds all chunks.",
    )

    # --- Embedding model ----------------------------------------------
    EMBEDDING_BACKEND: str = Field(
        default="hashing",
        description=(
            "Embedding backend: hashing for fast local/demo indexing, "
            "or model for BAAI/sentence-transformers embeddings."
        ),
    )
    EMBEDDING_MODEL_NAME: str = Field(
        default="BAAI/bge-m3",
        description="HuggingFace ID of the embedding model.",
    )
    EMBEDDING_HASH_DIMENSION: int = Field(
        default=1024,
        ge=128,
        description="Vector dimension used by the local feature-hashing backend.",
    )

    # --- Chunking configuration ---------------------------------------
    # Defaults follow the fast page-aware flow used for local demos.
    CHUNK_SIZE: int = Field(default=800, description="Characters per chunk.")
    CHUNK_OVERLAP: int = Field(
        default=100,
        description="Characters shared between two neighboring chunks.",
    )
    DEFAULT_TOP_K: int = Field(
        default=5,
        description="Default number of chunks retrieved per question.",
    )

    # --- Retrieval relevance threshold --------------------------------
    # top_k alone is not enough: ChromaDB always returns the K *nearest*
    # chunks, even if they are still semantically far from the question.
    # We post-filter results by cosine distance:
    #   smaller distance => more similar
    #   keep only chunks with distance <= RAG_MAX_DISTANCE.
    RAG_MAX_DISTANCE: float = Field(
        default=0.65,
        ge=0.0,
        description=(
            "Maximum cosine distance for a chunk to be considered relevant. "
            "Lower = stricter. Tune per corpus + embedding model."
        ),
    )
    RAG_MIN_RELEVANT_CHUNKS: int = Field(
        default=1,
        ge=0,
        description=(
            "Minimum number of chunks that must pass the distance filter "
            "before the service will attempt to answer. If fewer chunks "
            "pass, the service returns the Vietnamese fallback message."
        ),
    )

    # --- LLM (Generation) configuration -------------------------------
    # MOCK_LLM=true => no external API call; useful for local development
    # and for users who don't have a paid LLM key yet.
    MOCK_LLM: bool = Field(default=True, description="If true, use the local mock LLM.")
    LLM_PROVIDER: str = Field(
        default="mock",
        description="mock | openai | gemini | ollama | ... (future extension).",
    )
    LLM_API_KEY: str = Field(default="", description="API key for the LLM provider.")
    LLM_MODEL_NAME: str = Field(
        default="",
        description="Model name for the LLM provider (e.g. gpt-4o-mini).",
    )

    # --- Server -------------------------------------------------------
    PORT: int = Field(default=8000, description="HTTP port the service listens on.")

    # ------------------------------------------------------------------
    # Convenience helpers
    # ------------------------------------------------------------------
    @property
    def chroma_persist_path(self) -> str:
        """Absolute path version of CHROMA_PERSIST_DIR."""
        return os.path.abspath(self.CHROMA_PERSIST_DIR)

    @property
    def is_api_key_required(self) -> bool:
        """True when callers must send the X-API-Key header."""
        return bool(self.API_KEY and self.API_KEY.strip())


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    """
    Cached factory so the whole application reuses one Settings instance.
    `lru_cache` is a tiny trick that makes the function behave like a
    singleton without any global state.
    """
    return Settings()


# Convenience: the rest of the codebase imports `settings` directly.
settings: Settings = get_settings()
