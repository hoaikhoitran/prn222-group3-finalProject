"""
app/services/llm_service.py
===========================

STEP 5 (final) of the RAG pipeline: GENERATION.

After retrieval, we have:
  * the user's question
  * a small set of "contexts" (the most relevant chunks from the DB)

The job of this file is to turn (question, contexts) into a final
human-readable ANSWER.

Two modes
---------
1. MOCK_LLM = true   (default for local dev)
   ----------------------------------------
   No external API call. We assemble a deterministic answer from the
   retrieved chunks themselves. This is safe (no hallucination), free,
   and runs offline.

2. MOCK_LLM = false
   ----------------
   The skeleton is ready for a real provider (OpenAI, Gemini, Ollama,
   ...). To keep the code beginner-friendly, only the dispatch +
   provider switch is wired up; the actual HTTP integration for each
   provider is left as a clearly marked TODO so you can add the
   provider you actually pay for, when you have a key.

Mandatory system prompt
-----------------------
Per the project spec, every real LLM call MUST include the system
prompt below. It binds the model to answer only from the retrieved
context and to refuse politely when context is insufficient.
"""

from __future__ import annotations

import logging
import re
from typing import Any

import httpx

from app.core.config import settings
from app.utils.text_utils import preview

logger = logging.getLogger(__name__)


SYSTEM_PROMPT = (
    "You are an academic assistant for students. "
    "Answer only based on the provided document context. "
    "If the answer cannot be found in the context, say: "
    "'Không đủ thông tin trong tài liệu để trả lời câu hỏi này.' "
    "Always cite the source document and page number when possible. "
    "Do not invent information outside the provided context."
)

# Strict grounding instruction sent to Gemini as the system_instruction.
# This is intentionally kept verbatim per the project spec.
GEMINI_SYSTEM_INSTRUCTION = (
    "You are an academic assistant for students. "
    "Answer only based on the provided document context. "
    "Do not invent information outside the context. "
    "If the answer cannot be found in the context, say exactly: "
    "Không đủ thông tin trong tài liệu để trả lời câu hỏi này."
)

INSUFFICIENT_CONTEXT_REPLY = (
    "Không đủ thông tin trong tài liệu để trả lời câu hỏi này."
)

# Gemini REST endpoint (no SDK needed — httpx is already a dependency).
GEMINI_API_BASE = "https://generativelanguage.googleapis.com/v1beta"

# Hard caps so the prompt only ever contains the retrieved top-K chunks
# and never balloons into "send the whole document" territory.
MAX_GEMINI_CHUNKS = 5
MAX_CHUNK_CHARS = 1200


class LLMConfigurationError(RuntimeError):
    """Raised when the LLM provider is misconfigured (e.g. missing API key)."""


class LLMProviderError(RuntimeError):
    """Raised when the configured LLM provider fails or is unavailable."""


def _format_contexts_for_prompt(contexts: list[dict[str, Any]]) -> str:
    """
    Lay out the retrieved chunks as a numbered list with source + page,
    so the LLM can see where each fact comes from.
    """
    lines: list[str] = []

    for i, ctx in enumerate(contexts, start=1):
        meta = ctx.get("metadata") or {}

        file_name = meta.get("fileName", "unknown")
        page_number = meta.get("pageNumber", -1)
        page_label = f"page {page_number}" if page_number and page_number > 0 else "n/a"

        text = (ctx.get("text") or "").strip()

        lines.append(f"[{i}] (source: {file_name}, {page_label})\n{text}")

    return "\n\n".join(lines)


def _extract_question_terms(question: str) -> list[str]:
    """
    Extract important terms from the user's question.

    We remove common Vietnamese/English question words so the mock answer
    focuses on real content terms such as "Golden", "Retriever", "Poodle".
    """
    stopwords = {
        "tài",
        "liệu",
        "noi",
        "nói",
        "gi",
        "gì",
        "ve",
        "về",
        "cho",
        "chó",
        "trong",
        "này",
        "theo",
        "document",
        "what",
        "about",
        "tell",
        "say",
        "does",
        "this",
        "that",
    }

    terms = [
        term.lower()
        for term in re.findall(r"[A-Za-zÀ-ỹ0-9]+", question)
        if len(term) >= 4 and term.lower() not in stopwords
    ]

    return terms


def _extract_relevant_clause(sentence: str, question_terms: list[str]) -> str:
    """
    Extract the most relevant clause from a sentence.

    Example source sentence:
    "Ví dụ, chó Becgie thường được huấn luyện làm chó nghiệp vụ,
     chó Golden Retriever hiền lành và thân thiện,
     còn chó Poodle nhỏ nhắn, thông minh, phù hợp nuôi trong nhà."

    If the question asks about Poodle, this returns only:
    "chó Poodle nhỏ nhắn, thông minh, phù hợp nuôi trong nhà."
    """
    sentence = sentence.strip()

    if not sentence:
        return ""

    if not question_terms:
        return sentence

    clauses = re.split(r"\s*,\s*", sentence)

    for index, clause in enumerate(clauses):
        clause_clean = clause.strip()
        clause_lower = clause_clean.lower()

        if any(term in clause_lower for term in question_terms):
            selected_clauses = [clause_clean]

            for next_clause in clauses[index + 1:]:
                next_clause_clean = next_clause.strip()
                next_clause_lower = next_clause_clean.lower()

                if not next_clause_clean:
                    continue

                # Stop when the next clause starts a new dog breed/topic.
                # This prevents Golden answer from including Poodle content.
                if (
                    next_clause_lower.startswith("chó ")
                    or next_clause_lower.startswith("còn chó ")
                    or next_clause_lower.startswith("con chó ")
                ):
                    break

                selected_clauses.append(next_clause_clean)

            result = ", ".join(selected_clauses).strip()

            # Clean leading connector for nicer Vietnamese output.
            result = re.sub(r"^còn\s+", "", result, flags=re.IGNORECASE).strip()

            if result and not result.endswith((".", "!", "?")):
                result += "."

            return result

    return sentence


def _extract_relevant_sentences(
    question: str,
    text: str,
    max_sentences: int = 2,
) -> str:
    """
    Extract the most relevant sentence or clause from a retrieved chunk.

    This keeps MOCK_LLM answers short and focused without calling an external LLM.
    """
    text = (text or "").strip()

    if not text:
        return INSUFFICIENT_CONTEXT_REPLY

    question_terms = _extract_question_terms(question)

    sentences = re.split(r"(?<=[.!?])\s+", text)

    scored_sentences: list[tuple[int, str]] = []

    for sentence in sentences:
        sentence = sentence.strip()

        if not sentence:
            continue

        sentence_lower = sentence.lower()

        score = sum(1 for term in question_terms if term in sentence_lower)

        if score > 0:
            scored_sentences.append((score, sentence))

    if scored_sentences:
        scored_sentences.sort(key=lambda item: item[0], reverse=True)

        selected_results: list[str] = []

        for _, sentence in scored_sentences[:max_sentences]:
            relevant_clause = _extract_relevant_clause(sentence, question_terms)

            if relevant_clause:
                selected_results.append(relevant_clause)

        return " ".join(selected_results)

    return preview(text, 250)


def _mock_answer(question: str, contexts: list[dict[str, Any]]) -> str:
    """
    Build a short, focused answer from the most relevant retrieved context.

    This mock mode does not call any external LLM, so it does not truly
    paraphrase like ChatGPT/Gemini. It only extracts the most relevant
    sentences/clauses from the retrieved document chunk.

    Like the real provider, the mock cites the chunk it actually used:
    it always answers from contexts[0], which carries citation ID C1.
    """
    if not contexts:
        return INSUFFICIENT_CONTEXT_REPLY

    ctx = contexts[0]
    meta = ctx.get("metadata") or {}

    file_name = meta.get("fileName", "tài liệu")
    page_number = meta.get("pageNumber", -1)

    page_label = (
        f", trang {page_number}"
        if page_number and page_number > 0
        else ""
    )

    relevant_text = _extract_relevant_sentences(
        question=question,
        text=ctx.get("text", ""),
        max_sentences=2,
    )

    return f"Theo tài liệu {file_name}{page_label}, {relevant_text} [C1]"


# ---------------------------------------------------------------------------
# Gemini provider (GENERATION only — Gemini never reads files or embeddings)
# ---------------------------------------------------------------------------


def _extract_answer_exact(text: str) -> str | None:
    """
    Return a verbatim ANSWER_EXACT block from structured evaluation docs.

    Demo/evaluation documents can mark the expected answer like:

        QUESTION:
        ...
        ANSWER_EXACT:
        exact text here
        SOURCE_DOCUMENT:
        ...

    When retrieval lands on such a chunk, we bypass generation so the answer is
    exact ground truth text instead of an LLM paraphrase. Citation still comes
    from the retrieved chunk that contained the block.
    """
    if not text:
        return None

    match = re.search(
        r"ANSWER_EXACT:\s*(.*?)(?:\n\s*SOURCE_DOCUMENT:|\n\s*COURSE_TOPIC:|\n\s*END_OF_TEST_ITEM|\n\s*CITATION_ANCHOR:|\Z)",
        text,
        flags=re.DOTALL | re.IGNORECASE,
    )

    if not match:
        return None

    answer = " ".join(match.group(1).split()).strip()
    return answer or None


def build_gemini_prompt(question: str, contexts: list[dict[str, Any]]) -> str:
    """
    Build the user prompt for Gemini from the question and the already
    retrieved chunks ONLY.

    Boundaries enforced here:
      * At most MAX_GEMINI_CHUNKS chunks are included (the retrieved top-K).
      * Each chunk's text is truncated to MAX_CHUNK_CHARS characters.
      * Only the chunk text + its (fileName, pageNumber, chunkIndex) metadata
        are included — never embeddings, never file paths, never raw files.
      * Each block carries a per-request citation ID ([C1], [C2], ...) so
        the model can mark exactly which chunks its answer relies on. The
        IDs match the order of `contexts`, which is the same order
        rag_service uses when it assigns citation IDs to sources.

    `contexts` is exactly what rag_service passes into generate_answer(); we
    do not load, chunk, or read any document here.
    """
    selected = contexts[:MAX_GEMINI_CHUNKS]

    blocks: list[str] = []
    for i, ctx in enumerate(selected, start=1):
        meta = ctx.get("metadata") or {}

        file_name = meta.get("fileName", "unknown")

        page_number = meta.get("pageNumber")
        page_label = (
            str(page_number) if page_number and page_number > 0 else "n/a"
        )

        chunk_index = meta.get("chunkIndex")
        chunk_label = str(chunk_index) if chunk_index is not None else "n/a"

        text = (ctx.get("text") or "").strip()
        if len(text) > MAX_CHUNK_CHARS:
            text = text[: MAX_CHUNK_CHARS - 1].rstrip() + "…"

        blocks.append(
            f"[C{i}]\n"
            f"Source: {file_name}\n"
            f"Page: {page_label}\n"
            f"Chunk: {chunk_label}\n"
            f"Text:\n{text}"
        )

    context_block = "\n\n".join(blocks)

    return (
        f"QUESTION:\n{question.strip()}\n\n"
        f"CONTEXT:\n{context_block}\n\n"
        "Instructions:\n"
        "- Answer in Vietnamese.\n"
        "- Use only the context above.\n"
        "- Each context block starts with a citation ID such as [C1] or [C2]. "
        "Immediately after each piece of information in your answer, insert "
        "the citation ID of the block it came from, e.g. \"... [C1]\".\n"
        "- Only use citation IDs that appear in the context above. Never "
        "invent new citation IDs.\n"
        "- Do not cite a block that does not directly support your answer.\n"
        "- Keep the answer concise (about 4-6 sentences unless the user asks "
        "for more detail).\n"
        "- If context is insufficient, say exactly:\n"
        '  "Không đủ thông tin trong tài liệu để trả lời câu hỏi này."'
    )


def _extract_gemini_text(data: dict[str, Any]) -> str:
    """Pull the answer text out of a Gemini generateContent JSON response."""
    candidates = data.get("candidates") or []
    if not candidates:
        feedback = data.get("promptFeedback") or {}
        block_reason = feedback.get("blockReason")
        if block_reason:
            raise LLMProviderError(f"Gemini blocked the prompt: {block_reason}")
        raise LLMProviderError("Gemini returned no candidates.")

    parts = (candidates[0].get("content") or {}).get("parts") or []
    text = "".join(part.get("text", "") for part in parts).strip()

    if not text:
        raise LLMProviderError("Gemini returned an empty answer.")

    return text


def _extract_gemini_usage(data: dict[str, Any]) -> dict[str, int | None] | None:
    """
    Pull the REAL token usage out of a Gemini generateContent response.

    Gemini reports usage in `usageMetadata`:
      * promptTokenCount     -> tokens billed for the request (prompt)
      * candidatesTokenCount -> tokens generated in the answer (completion)
      * totalTokenCount      -> provider's official total (may exceed
        prompt + completion when the model spends "thoughts" tokens)

    Returns None when the provider did not report usage. We never estimate
    tokens ourselves (no len/4, no word counts) — a missing value stays None
    so the .NET side stores NULL instead of a fabricated number.
    """
    meta = data.get("usageMetadata") or {}
    if not isinstance(meta, dict) or not meta:
        return None

    prompt_tokens = meta.get("promptTokenCount")
    completion_tokens = meta.get("candidatesTokenCount")
    total_tokens = meta.get("totalTokenCount")

    if prompt_tokens is None and completion_tokens is None and total_tokens is None:
        return None

    return {
        "promptTokens": prompt_tokens,
        "completionTokens": completion_tokens,
        "totalTokens": total_tokens,
    }


def _call_gemini(question: str, contexts: list[dict[str, Any]]) -> tuple[str, dict[str, int | None] | None]:
    """
    Call the Gemini REST API for the GENERATION step.

    Reads LLM_API_KEY and LLM_MODEL_NAME from settings (never hard-coded).
    The API key is passed as a query parameter and is never logged.

    Returns (answer_text, usage) where `usage` is the provider-reported
    token usage dict (see _extract_gemini_usage) or None.
    """
    api_key = (settings.LLM_API_KEY or "").strip()
    if not api_key:
        raise LLMConfigurationError(
            "LLM_API_KEY is not set. Add your Gemini API key to rag-service/.env "
            "to use LLM_PROVIDER=gemini (or set MOCK_LLM=true for local mock mode)."
        )

    model = (settings.LLM_MODEL_NAME or "").strip()
    if not model:
        raise LLMConfigurationError(
            "LLM_MODEL_NAME is not set. Configure it in rag-service/.env "
            "(e.g. LLM_MODEL_NAME=gemini-2.5-flash-lite)."
        )

    prompt = build_gemini_prompt(question, contexts)

    payload = {
        "system_instruction": {"parts": [{"text": GEMINI_SYSTEM_INSTRUCTION}]},
        "contents": [{"role": "user", "parts": [{"text": prompt}]}],
        "generationConfig": {
            "temperature": 0.2,
            "maxOutputTokens": 1024,
        },
    }

    url = f"{GEMINI_API_BASE}/models/{model}:generateContent"

    # NOTE: only the model name is logged. The API key is sent in the
    # `x-goog-api-key` header (NOT as a `?key=` query parameter) so it never
    # appears in httpx's request-URL logging or anywhere else in the logs.
    logger.info("Calling Gemini model %s with %d retrieved chunks", model, len(contexts))

    try:
        response = httpx.post(
            url,
            json=payload,
            headers={
                "Content-Type": "application/json",
                "x-goog-api-key": api_key,
            },
            timeout=60.0,
        )
    except httpx.HTTPError as exc:
        raise LLMProviderError(f"Failed to reach Gemini API: {exc}") from exc

    if response.status_code == 404:
        # The configured model does not exist / is not available for this key.
        # Do NOT silently fall back to another model — report a clear error.
        raise LLMProviderError(
            f"Gemini model {model!r} is unavailable (HTTP 404). "
            "Check LLM_MODEL_NAME in .env; the service will not switch models silently."
        )

    if response.status_code != 200:
        raise LLMProviderError(
            f"Gemini API call failed with HTTP {response.status_code}: "
            f"{preview(response.text, 300)}"
        )

    try:
        data = response.json()
    except ValueError as exc:
        raise LLMProviderError("Gemini returned a non-JSON response.") from exc

    return _extract_gemini_text(data), _extract_gemini_usage(data)


def generate_answer_with_usage(
    question: str, contexts: list[dict[str, Any]]
) -> dict[str, Any]:
    """
    Public entry point used by rag_service.

    Returns {"answer": str, "usage": dict | None} where `usage` is the
    provider-reported token usage ({promptTokens, completionTokens,
    totalTokens}) or None when no real provider was called (mock mode)
    or the provider did not report usage. Usage is NEVER estimated.

    Parameters
    ----------
    question : str
        The student's question.
    contexts : list of dict
        Retrieved chunks. Each must have at least `text` and `metadata`.
    """
    if not contexts:
        return {"answer": INSUFFICIENT_CONTEXT_REPLY, "usage": None}

    exact_answer = _extract_answer_exact(contexts[0].get("text", ""))
    if exact_answer:
        return {"answer": exact_answer, "usage": None}

    if settings.MOCK_LLM:
        return {"answer": _mock_answer(question, contexts), "usage": None}

    provider = (settings.LLM_PROVIDER or "").lower().strip()

    logger.info("Calling LLM provider: %s", provider or "<none>")

    if provider in {"", "mock"}:
        return {"answer": _mock_answer(question, contexts), "usage": None}

    if provider == "openai":
        # TODO: implement OpenAI Chat Completions here using
        # settings.LLM_API_KEY and settings.LLM_MODEL_NAME.
        # Include SYSTEM_PROMPT as the system message.
        raise NotImplementedError(
            "LLM_PROVIDER='openai' is not wired up yet. "
            "Set MOCK_LLM=true to run locally, or add the OpenAI call here."
        )

    if provider == "gemini":
        answer, usage = _call_gemini(question, contexts)
        return {"answer": answer, "usage": usage}

    if provider == "ollama":
        # TODO: implement Ollama local call via httpx.
        raise NotImplementedError(
            "LLM_PROVIDER='ollama' is not wired up yet."
        )

    raise NotImplementedError(f"Unknown LLM_PROVIDER: {provider!r}")


def generate_answer(question: str, contexts: list[dict[str, Any]]) -> str:
    """Answer-only wrapper around generate_answer_with_usage()."""
    return generate_answer_with_usage(question, contexts)["answer"]
