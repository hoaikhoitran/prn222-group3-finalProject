"""Tests for the fixed-size chunking service."""

from __future__ import annotations

from app.services.chunking_service import chunk_pages


def test_chunking_returns_no_empty_chunks() -> None:
    """Even with whitespace pages we must never emit empty chunks."""
    pages = [
        {"text": "   ", "pageNumber": 1, "source": "a.txt"},
        {"text": "Hello world. " * 100, "pageNumber": 2, "source": "a.txt"},
        {"text": "", "pageNumber": 3, "source": "a.txt"},
    ]
    chunks = chunk_pages(pages, chunk_size=200, chunk_overlap=50)

    assert len(chunks) > 0
    for chunk in chunks:
        assert chunk["chunkText"].strip(), "chunk text must not be blank"
        assert "chunkIndex" in chunk
        assert "pageNumber" in chunk


def test_chunking_overlap_behavior() -> None:
    """Consecutive chunks must share `chunk_overlap` characters at the boundary."""
    long_text = "".join(chr(ord("a") + (i % 26)) for i in range(1000))
    pages = [{"text": long_text, "pageNumber": 1, "source": "x.txt"}]

    chunk_size = 200
    overlap = 50
    chunks = chunk_pages(pages, chunk_size=chunk_size, chunk_overlap=overlap)

    assert len(chunks) >= 2, "expected at least two chunks for a 1000-char input"

    # The tail of chunk N should appear at the head of chunk N+1.
    first_tail = chunks[0]["chunkText"][-overlap:]
    second_head = chunks[1]["chunkText"][:overlap]
    assert first_tail == second_head


def test_chunking_preserves_page_numbers() -> None:
    """Each chunk should carry the pageNumber of the page it came from."""
    pages = [
        {"text": "alpha " * 200, "pageNumber": 1, "source": "x.pdf"},
        {"text": "beta " * 200, "pageNumber": 2, "source": "x.pdf"},
    ]
    chunks = chunk_pages(pages, chunk_size=300, chunk_overlap=50)

    page_numbers = {c["pageNumber"] for c in chunks}
    assert page_numbers == {1, 2}


def test_chunking_indexes_are_monotonic() -> None:
    """chunkIndex must start at 0 and increase by 1 across the whole document."""
    pages = [{"text": "lorem ipsum " * 300, "pageNumber": 1, "source": "x.txt"}]
    chunks = chunk_pages(pages, chunk_size=400, chunk_overlap=100)

    indexes = [c["chunkIndex"] for c in chunks]
    assert indexes == list(range(len(chunks)))


def test_short_text_produces_single_chunk() -> None:
    """Text shorter than chunk_size should become exactly one chunk."""
    pages = [{"text": "Hi there", "pageNumber": 1, "source": "x.txt"}]
    chunks = chunk_pages(pages, chunk_size=200, chunk_overlap=50)

    assert len(chunks) == 1
    assert chunks[0]["chunkText"] == "Hi there"
    assert chunks[0]["chunkIndex"] == 0


def test_word_mode_uses_word_windows() -> None:
    """Word mode measures size and overlap in words, not characters."""
    text = "one two three four five six seven eight nine ten"
    pages = [{"text": text, "pageNumber": None, "source": "x.txt"}]

    chunks = chunk_pages(
        pages,
        chunk_mode="Words",
        chunk_size=4,
        chunk_overlap=1,
        min_chunk_length=0,
    )

    assert [c["chunkText"] for c in chunks] == [
        "one two three four",
        "four five six seven",
        "seven eight nine ten",
    ]


def test_paragraph_mode_uses_paragraph_windows() -> None:
    """Paragraph mode measures size and overlap in paragraphs."""
    text = "Para 1\n\nPara 2\n\nPara 3\n\nPara 4"
    pages = [{"text": text, "pageNumber": None, "source": "x.txt"}]

    chunks = chunk_pages(
        pages,
        chunk_mode="Paragraph",
        chunk_size=2,
        chunk_overlap=1,
        min_chunk_length=0,
    )

    assert [c["chunkText"] for c in chunks] == [
        "Para 1\n\nPara 2",
        "Para 2\n\nPara 3",
        "Para 3\n\nPara 4",
    ]


def test_min_chunk_length_filters_short_chunks() -> None:
    """Chunks shorter than the configured minimum are dropped."""
    pages = [{"text": "short\n\nthis paragraph is long enough", "pageNumber": None, "source": "x.txt"}]

    chunks = chunk_pages(
        pages,
        chunk_mode="Paragraph",
        chunk_size=1,
        chunk_overlap=0,
        min_chunk_length=10,
    )

    assert [c["chunkText"] for c in chunks] == ["this paragraph is long enough"]


def test_max_chunks_caps_output() -> None:
    """The configured max chunk count caps indexed output."""
    pages = [{"text": " ".join(str(i) for i in range(100)), "pageNumber": None, "source": "x.txt"}]

    chunks = chunk_pages(
        pages,
        chunk_mode="Words",
        chunk_size=5,
        chunk_overlap=0,
        max_chunks=3,
    )

    assert len(chunks) == 3
    assert [c["chunkIndex"] for c in chunks] == [0, 1, 2]
