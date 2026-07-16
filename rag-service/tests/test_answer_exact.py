from __future__ import annotations

from app.core.config import settings
from app.services import llm_service
from app.services.llm_service import generate_answer


def test_answer_exact_block_bypasses_llm_and_returns_verbatim(monkeypatch) -> None:
    monkeypatch.setattr(settings, "MOCK_LLM", False)
    monkeypatch.setattr(settings, "LLM_PROVIDER", "gemini")

    def fail_post(*args, **kwargs):  # noqa: ANN002, ANN003
        raise AssertionError("Gemini must not be called for ANSWER_EXACT chunks.")

    monkeypatch.setattr(llm_service.httpx, "post", fail_post)

    contexts = [
        {
            "id": "doc_050::chunk::0",
            "text": (
                "CITATION_ANCHOR: Q47\n"
                "QUESTION:\n"
                "What does equivalence partitioning do?\n"
                "ANSWER_EXACT:\n"
                "Split input data into valid and invalid groups, then test one representative from each group.\n"
                "SOURCE_DOCUMENT:\n"
                "SWE301_KiemThuPhanMem.docx\n"
            ),
            "metadata": {
                "documentId": "doc_050",
                "fileName": "SWE301_KiemThuPhanMem.docx",
                "pageNumber": 47,
                "chunkIndex": 0,
            },
        }
    ]

    answer = generate_answer("What does equivalence partitioning do?", contexts)

    assert answer == (
        "Split input data into valid and invalid groups, then test one representative from each group."
    )
