from __future__ import annotations

from app.core.config import settings
from app.services import llm_service
from app.services.llm_service import generate_answer
from app.services.llm_service import generate_exact_answer_with_usage
from app.services.llm_service import generate_answer_with_usage


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


def test_answer_exact_stops_before_inline_metadata() -> None:
    contexts = [
        {
            "id": "doc_050::chunk::0",
            "text": (
                "CITATION_ANCHOR: Q49 | Expected source file: SWE301_KiemThuPhanMem.docx "
                "QUESTION: Phân tích giá trị biên (Boundary Value Analysis) tập trung vào đâu? "
                "ANSWER_EXACT: Tập trung kiểm tra các giá trị tại biên của các phân vùng "
                "(điểm cực tiểu, cực đại, giá trị sát biên). "
                "COURSE_TOPIC: SWE301 - Thiết kế test case SOURCE_DOCUMENT: "
                "SWE301_KiemThuPhanMem.docx END_OF_TEST_ITEM Q50 "
                "QUESTION: Vì sao cần phân tích giá trị biên? "
                "ANSWER_EXACT: Vì biên là nơi lập trình viên dễ viết sai logic nhất."
            ),
            "metadata": {
                "documentId": "doc_050",
                "fileName": "SWE301_KiemThuPhanMem.docx",
                "pageNumber": 49,
                "chunkIndex": 0,
            },
        }
    ]

    result = generate_answer_with_usage(
        "Phân tích giá trị biên (Boundary Value Analysis) tập trung vào đâu?",
        contexts,
    )

    assert result["answer"] == (
        "Tập trung kiểm tra các giá trị tại biên của các phân vùng "
        "(điểm cực tiểu, cực đại, giá trị sát biên)."
    )
    assert result["sourceCitationIds"] == ["C1"]


def test_answer_exact_does_not_match_near_question() -> None:
    contexts = [
        {
            "id": "doc_050::chunk::0",
            "text": (
                "QUESTION: Phân vùng tương đương (Equivalence Partitioning) làm gì? "
                "ANSWER_EXACT: Chia dữ liệu đầu vào thành các nhóm hợp lệ và không hợp lệ, "
                "chỉ cần chọn một đại diện trong mỗi nhóm để test."
            ),
            "metadata": {
                "documentId": "doc_050",
                "fileName": "SWE301_KiemThuPhanMem.docx",
                "pageNumber": 10,
                "chunkIndex": 0,
            },
        }
    ]

    result = generate_exact_answer_with_usage(
        '"Phân vùng tương đương" trong tiếng Anh là gì?',
        contexts,
    )

    assert result is None


def test_answer_exact_selects_matching_question_in_multi_item_chunk() -> None:
    contexts = [
        {
            "id": "doc_050::chunk::0",
            "text": (
                "QUESTION: Phân tích giá trị biên tập trung vào đâu? "
                "ANSWER_EXACT: Tập trung kiểm tra các giá trị tại biên của các phân vùng "
                "(điểm cực tiểu, cực đại, giá trị sát biên). "
                "COURSE_TOPIC: SWE301 SOURCE_DOCUMENT: SWE301_KiemThuPhanMem.docx "
                "END_OF_TEST_ITEM Q50 "
                "QUESTION: Vì sao cần phân tích giá trị biên? "
                "ANSWER_EXACT: Vì biên là nơi lập trình viên dễ viết sai logic nhất. "
                "COURSE_TOPIC: SWE301 SOURCE_DOCUMENT: SWE301_KiemThuPhanMem.docx"
            ),
            "metadata": {
                "documentId": "doc_050",
                "fileName": "SWE301_KiemThuPhanMem.docx",
                "pageNumber": 49,
                "chunkIndex": 0,
            },
        }
    ]

    result = generate_answer_with_usage(
        "Vì sao cần phân tích giá trị biên?",
        contexts,
    )

    assert result["answer"] == "Vì biên là nơi lập trình viên dễ viết sai logic nhất."
    assert result["sourceCitationIds"] == ["C1"]


def test_answer_exact_reconstructs_item_split_across_chunks() -> None:
    contexts = [
        {
            "id": "doc_050::chunk::10",
            "text": (
                "QUESTION: Khác biệt cơ bản giữa kiểm thử hộp đen và hộp trắng là gì? "
            ),
            "metadata": {
                "documentId": "doc_050",
                "fileName": "SWE301_KiemThuPhanMem.docx",
                "pageNumber": 50,
                "chunkIndex": 10,
            },
        },
        {
            "id": "doc_050::chunk::11",
            "text": (
                "ANSWER_EXACT: Kiểm thử hộp đen dựa trên đầu vào và đầu ra "
                "mà không xét cấu trúc bên trong, còn kiểm thử hộp trắng dựa "
                "trên cấu trúc và logic bên trong chương trình. SOURCE_DOCUMENT: "
                "SWE301_KiemThuPhanMem.docx END_OF_TEST_ITEM"
            ),
            "metadata": {
                "documentId": "doc_050",
                "fileName": "SWE301_KiemThuPhanMem.docx",
                "pageNumber": 50,
                "chunkIndex": 11,
            },
        },
    ]

    result = generate_exact_answer_with_usage(
        "Khác biệt cơ bản giữa kiểm thử hộp đen và hộp trắng là gì?",
        contexts,
    )

    assert result is not None
    assert result["answer"] == (
        "Kiểm thử hộp đen dựa trên đầu vào và đầu ra mà không xét cấu trúc "
        "bên trong, còn kiểm thử hộp trắng dựa trên cấu trúc và logic bên "
        "trong chương trình."
    )
    assert result["sourceCitationIds"] == ["C2"]
