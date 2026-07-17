"""
app/services/embedding_service.py
=================================

STEP 3 of the RAG pipeline: turn text into vectors ("embeddings") so we
can compare two pieces of text by mathematical distance instead of
exact word matching.

What is an embedding?
---------------------
An embedding is a fixed-length list of floats (e.g. 1024 numbers for
BAAI/bge-m3). Two semantically similar texts map to nearby points in
this high-dimensional space, even if they use completely different
words (e.g. "MVC pattern" and "Mô hình MVC").

This service exposes ONE singleton model and two simple functions:
  * embed_text(text)         -> list[float]
  * embed_texts(texts)       -> list[list[float]]

Why a singleton?
----------------
Loading BAAI/bge-m3 from disk takes several seconds and ~2GB of RAM.
We MUST load it once at startup and reuse it for every request.
"""

from __future__ import annotations

import hashlib
import logging
import math
import re
import threading
import unicodedata
from typing import Any

from app.core.config import settings

logger = logging.getLogger(__name__)


class EmbeddingService:
    """
    Singleton wrapper around BAAI/bge-m3.

    We try the official BAAI library (`FlagEmbedding.BGEM3FlagModel`)
    first because it implements the exact training-time tokenization
    and pooling. If that fails (e.g. a torch / dependency mismatch),
    we automatically fall back to `sentence-transformers`, which works
    on the same checkpoint with slightly different defaults.
    """

    _instance: "EmbeddingService | None" = None
    _instance_lock = threading.Lock()

    def __init__(self) -> None:
        self._model: Any = None
        self._backend: str = ""  # "flag" or "sentence-transformers"
        self._load_lock = threading.Lock()

    # ------------------------------------------------------------------
    # Fast local feature-hashing backend
    # ------------------------------------------------------------------
    @staticmethod
    def _uses_hashing_backend() -> bool:
        return settings.EMBEDDING_BACKEND.strip().lower() in {
            "hash",
            "hashing",
            "feature-hashing",
            "local",
        }

    @staticmethod
    def _remove_diacritics(text: str) -> str:
        normalized = unicodedata.normalize("NFD", text or "")
        return "".join(
            char
            for char in normalized
            if unicodedata.category(char) != "Mn"
        ).replace("đ", "d").replace("Đ", "D")

    @classmethod
    def _tokenize_for_hashing(cls, text: str) -> list[str]:
        lower = (text or "").lower()
        no_accent = cls._remove_diacritics(lower)

        raw_words = re.findall(r"[\w]+", lower, flags=re.UNICODE)
        no_accent_words = re.findall(r"[\w]+", no_accent, flags=re.UNICODE)

        stop_words = {
            "và", "va", "của", "cua", "là", "la", "có", "co", "trong",
            "cho", "với", "voi", "các", "cac", "được", "duoc", "không",
            "khong", "này", "nay", "đó", "do", "một", "mot", "những",
            "nhung", "để", "de", "thì", "thi", "mà", "ma", "khi", "về",
            "ve", "the", "is", "in", "of", "and", "to", "a", "an", "for",
            "on", "at", "by", "with", "as",
        }

        tokens: list[str] = []
        paired_words: list[tuple[str, str]] = []

        for index, word in enumerate(raw_words):
            if len(word) < 2:
                continue

            plain = no_accent_words[index] if index < len(no_accent_words) else word
            if word in stop_words or plain in stop_words:
                continue

            paired_words.append((word, plain))
            tokens.append(word)
            if plain != word:
                tokens.append(plain)

        for index in range(len(paired_words) - 1):
            left = paired_words[index]
            right = paired_words[index + 1]
            tokens.append(f"{left[0]}_{right[0]}")
            tokens.append(f"{left[1]}_{right[1]}")

        return list(dict.fromkeys(tokens))

    @classmethod
    def _embed_text_hashing(cls, text: str) -> list[float]:
        dimension = max(128, int(settings.EMBEDDING_HASH_DIMENSION))
        vector = [0.0] * dimension
        tokens = cls._tokenize_for_hashing(text)

        if not tokens:
            return vector

        for token in tokens:
            digest = hashlib.blake2b(token.encode("utf-8"), digest_size=8).digest()
            value = int.from_bytes(digest, byteorder="big", signed=False)
            index = value % dimension
            sign = 1.0 if (value & 1) == 0 else -1.0
            vector[index] += sign

        magnitude = math.sqrt(sum(value * value for value in vector))
        if magnitude <= 1e-12:
            return vector

        return [value / magnitude for value in vector]

    # ------------------------------------------------------------------
    # Singleton accessor
    # ------------------------------------------------------------------
    @classmethod
    def instance(cls) -> "EmbeddingService":
        if cls._instance is None:
            with cls._instance_lock:
                if cls._instance is None:
                    cls._instance = cls()
        return cls._instance

    # ------------------------------------------------------------------
    # Lazy model loading
    # ------------------------------------------------------------------
    def _ensure_loaded(self) -> None:
        """
        Load the model on first use. Subsequent calls are cheap.
        Thread-safe so concurrent requests don't load twice.
        """
        if self._model is not None:
            return

        with self._load_lock:
            if self._model is not None:
                return

            model_name = settings.EMBEDDING_MODEL_NAME

            # --- Try FlagEmbedding (preferred) ---
            try:
                from FlagEmbedding import BGEM3FlagModel  # type: ignore

                logger.info("Loading embedding model via FlagEmbedding: %s", model_name)
                # use_fp16=True if a CUDA GPU is available; safe default False.
                self._model = BGEM3FlagModel(model_name, use_fp16=False)
                self._backend = "flag"
                logger.info("FlagEmbedding model loaded.")
                return
            except Exception as flag_exc:  # noqa: BLE001
                logger.warning(
                    "FlagEmbedding unavailable (%s). Falling back to sentence-transformers.",
                    flag_exc,
                )

            # --- Fallback: sentence-transformers ---
            try:
                from sentence_transformers import SentenceTransformer  # type: ignore

                logger.info(
                    "Loading embedding model via sentence-transformers: %s", model_name
                )
                self._model = SentenceTransformer(model_name)
                self._backend = "sentence-transformers"
                logger.info("sentence-transformers model loaded.")
            except Exception as st_exc:  # noqa: BLE001
                raise RuntimeError(
                    "Could not load the embedding model '"
                    f"{model_name}'. Make sure FlagEmbedding or "
                    "sentence-transformers is installed and the model is "
                    "reachable. Underlying error: "
                    f"{st_exc}"
                ) from st_exc

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------
    def embed_texts(self, texts: list[str]) -> list[list[float]]:
        """
        Convert a batch of strings into a list of float vectors.
        We always pass batches when possible — embedding 100 texts in one
        call is much faster than 100 separate calls.
        """
        if not texts:
            return []

        if self._uses_hashing_backend():
            logger.debug("Embedding %d texts with local feature hashing.", len(texts))
            return [self._embed_text_hashing(text) for text in texts]

        self._ensure_loaded()

        if self._backend == "flag":
            # FlagEmbedding's encode() returns a dict; "dense_vecs" is the
            # 1024-d numeric vector for each input string.
            result = self._model.encode(
                texts,
                return_dense=True,
                return_sparse=False,
                return_colbert_vecs=False,
            )
            dense = result["dense_vecs"]
            return [vec.tolist() for vec in dense]

        # sentence-transformers backend
        # normalize_embeddings=True makes vectors unit-length so cosine
        # similarity matches what BGE-M3 was trained for.
        vectors = self._model.encode(
            texts,
            normalize_embeddings=True,
            convert_to_numpy=True,
            show_progress_bar=False,
        )
        return [vec.tolist() for vec in vectors]

    def embed_text(self, text: str) -> list[float]:
        """Convenience wrapper around embed_texts() for a single string."""
        vectors = self.embed_texts([text])
        return vectors[0] if vectors else []


# Module-level convenience accessor.
embedding_service: EmbeddingService = EmbeddingService.instance()
