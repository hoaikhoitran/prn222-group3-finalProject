/*
    2026_AddChatMessageTokenUsage.sql

    Adds REAL LLM token-usage columns to dbo.ChatMessages for the Academic
    Document RAG System.

    Target database : AcademicRagManagement
    Safe to re-run   : YES (guarded with IF NOT EXISTS)

    Columns:
      - PromptTokens     INT NULL  -> prompt tokens reported by the LLM provider
      - CompletionTokens INT NULL  -> completion tokens reported by the provider
      - TotalTokens      INT NULL  -> provider's official total token count

    All three columns are NULLABLE on purpose:
      - Existing chat history predates token tracking. Those rows stay NULL
        and the dashboard reports them as "no usage data" — we never backfill
        fake token counts for old messages.
      - New messages answered without a real LLM call (mock mode, uploader
        provenance answers) also stay NULL.

    No existing data is modified or deleted by this script.
*/

SET NOCOUNT ON;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.ChatMessages') AND name = N'PromptTokens')
BEGIN
    ALTER TABLE dbo.ChatMessages ADD PromptTokens INT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.ChatMessages') AND name = N'CompletionTokens')
BEGIN
    ALTER TABLE dbo.ChatMessages ADD CompletionTokens INT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.ChatMessages') AND name = N'TotalTokens')
BEGIN
    ALTER TABLE dbo.ChatMessages ADD TotalTokens INT NULL;
END;
GO
