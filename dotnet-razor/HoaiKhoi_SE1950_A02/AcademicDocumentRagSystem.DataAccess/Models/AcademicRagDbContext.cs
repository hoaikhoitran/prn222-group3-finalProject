using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace AcademicDocumentRagSystem.DataAccess.Models;

public partial class AcademicRagDbContext : DbContext
{
    public AcademicRagDbContext(DbContextOptions<AcademicRagDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<ChatMessage> ChatMessages { get; set; }

    public virtual DbSet<ChatSession> ChatSessions { get; set; }

    public virtual DbSet<Course> Courses { get; set; }

    public virtual DbSet<Document> Documents { get; set; }

    public virtual DbSet<DocumentChunk> DocumentChunks { get; set; }

    public virtual DbSet<DocumentChunkConfig> DocumentChunkConfigs { get; set; }

    public virtual DbSet<DocumentIndexLog> DocumentIndexLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.AccountId).HasName("PK__Accounts__349DA5A650ED84E5");

            entity.HasIndex(e => e.Email, "IX_Accounts_Email");

            entity.HasIndex(e => e.Email, "UQ_Accounts_Email").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.Password).HasMaxLength(255);
            entity.Property(e => e.Status).HasDefaultValue(true);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.ChatMessageId).HasName("PK__ChatMess__9AB6103558828A11");

            entity.HasIndex(e => e.ChatSessionId, "IX_ChatMessages_ChatSessionId");

            entity.HasIndex(e => e.CreatedAt, "IX_ChatMessages_CreatedAt");

            entity.HasIndex(e => e.DocumentId, "IX_ChatMessages_DocumentId");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.SourcesJson).HasDefaultValue("[]");

            entity.HasOne(d => d.Account).WithMany(p => p.ChatMessages)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChatMessages_Accounts");

            entity.HasOne(d => d.ChatSession).WithMany(p => p.ChatMessages)
                .HasForeignKey(d => d.ChatSessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChatMessages_ChatSessions");

            entity.HasOne(d => d.Document).WithMany(p => p.ChatMessages)
                .HasForeignKey(d => d.DocumentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChatMessages_Documents");
        });

        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.HasKey(e => e.ChatSessionId).HasName("PK__ChatSess__9AB8244FCD54DCFE");

            entity.HasIndex(e => e.AccountId, "IX_ChatSessions_AccountId");

            entity.HasIndex(e => e.DocumentId, "IX_ChatSessions_DocumentId");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Title).HasMaxLength(255);

            entity.HasOne(d => d.Account).WithMany(p => p.ChatSessions)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChatSessions_Accounts");

            entity.HasOne(d => d.Course).WithMany(p => p.ChatSessions)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChatSessions_Courses");

            entity.HasOne(d => d.Document).WithMany(p => p.ChatSessions)
                .HasForeignKey(d => d.DocumentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChatSessions_Documents");
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(e => e.CourseId).HasName("PK__Courses__C92D71A7628A359E");

            entity.HasIndex(e => e.Code, "IX_Courses_Code");

            entity.HasIndex(e => e.Code, "UQ_Courses_Code").IsUnique();

            // NOT unique on purpose: one teacher may own many courses. A course
            // can only reference one teacher because this is a plain FK column.
            entity.HasIndex(e => e.TeacherAccountId, "IX_Courses_TeacherAccountId");

            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Status).HasDefaultValue(true);

            entity.HasOne(e => e.TeacherAccount).WithMany(a => a.TeachingCourses)
                .HasForeignKey(e => e.TeacherAccountId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Courses_TeacherAccount");
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.DocumentId).HasName("PK__Document__1ABEEF0FB0381B6F");

            entity.HasIndex(e => e.CourseCode, "IX_Documents_CourseCode");

            entity.HasIndex(e => e.CourseId, "IX_Documents_CourseId");

            entity.HasIndex(e => e.IndexStatus, "IX_Documents_IndexStatus");

            entity.HasIndex(e => e.SubmittedByAccountId, "IX_Documents_SubmittedByAccountId");

            entity.HasIndex(e => e.UploadStatus, "IX_Documents_UploadStatus");

            // Database-level guard against re-uploading the same file content into the
            // same course. Filtered so soft-deleted documents do not block a re-upload.
            entity.HasIndex(e => new { e.CourseId, e.FileHashSha256 }, "UX_Documents_Course_FileHash_Active")
                .IsUnique()
                .HasFilter("[UploadStatus] <> 'Deleted'");

            entity.Property(e => e.Chapter).HasMaxLength(100);
            entity.Property(e => e.FileHashSha256).HasMaxLength(64).IsFixedLength();
            entity.Property(e => e.ContentType).HasMaxLength(255);
            entity.Property(e => e.CourseCode).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.FilePath).HasMaxLength(1000);
            entity.Property(e => e.FileType).HasMaxLength(20);
            entity.Property(e => e.IndexStatus)
                .HasMaxLength(30)
                .HasDefaultValue("Pending");
            entity.Property(e => e.OriginalFileName).HasMaxLength(255);
            entity.Property(e => e.StoredFileName).HasMaxLength(255);
            entity.Property(e => e.SubmittedByEmail).HasMaxLength(255);
            entity.Property(e => e.Title).HasMaxLength(255);
            entity.Property(e => e.UploadStatus)
                .HasMaxLength(30)
                .HasDefaultValue("Submitted");

            entity.HasOne(d => d.Course).WithMany(p => p.Documents)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Documents_Courses");

            entity.HasOne(d => d.SubmittedByAccount).WithMany(p => p.Documents)
                .HasForeignKey(d => d.SubmittedByAccountId)
                .HasConstraintName("FK_Documents_Accounts");
        });

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.HasKey(e => e.DocumentChunkId).HasName("PK_DocumentChunks");

            entity.HasIndex(e => new { e.DocumentId, e.ChunkIndex }, "UQ_DocumentChunks_DocumentId_ChunkIndex").IsUnique();

            entity.HasIndex(e => e.DocumentId, "IX_DocumentChunks_DocumentId");

            entity.HasIndex(e => new { e.DocumentId, e.ChunkIndex }, "IX_DocumentChunks_DocumentId_ChunkIndex");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Document).WithMany(p => p.DocumentChunks)
                .HasForeignKey(d => d.DocumentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_DocumentChunks_Documents");
        });

        modelBuilder.Entity<DocumentChunkConfig>(entity =>
        {
            entity.HasKey(e => e.DocumentChunkConfigId).HasName("PK_DocumentChunkConfigs");

            entity.HasIndex(e => e.IsActive, "IX_DocumentChunkConfigs_IsActive");

            entity.HasIndex(e => e.UpdatedByAccountId, "IX_DocumentChunkConfigs_UpdatedByAccountId");

            entity.Property(e => e.ChunkMode).HasMaxLength(30);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.UpdatedByAccount).WithMany()
                .HasForeignKey(d => d.UpdatedByAccountId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_DocumentChunkConfigs_Accounts");
        });

        modelBuilder.Entity<DocumentIndexLog>(entity =>
        {
            entity.HasKey(e => e.DocumentIndexLogId).HasName("PK_DocumentIndexLogs");

            entity.HasIndex(e => e.DocumentId, "IX_DocumentIndexLogs_DocumentId");

            entity.HasIndex(e => e.PerformedAt, "IX_DocumentIndexLogs_PerformedAt");

            entity.HasIndex(e => e.PerformedByAccountId, "IX_DocumentIndexLogs_PerformedByAccountId");

            entity.Property(e => e.Action).HasMaxLength(30);
            entity.Property(e => e.Status).HasMaxLength(30);
            entity.Property(e => e.PerformedByEmail).HasMaxLength(255);
            entity.Property(e => e.PerformedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Document).WithMany(p => p.DocumentIndexLogs)
                .HasForeignKey(d => d.DocumentId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_DocumentIndexLogs_Documents");

            entity.HasOne(d => d.PerformedByAccount).WithMany(p => p.DocumentIndexLogs)
                .HasForeignKey(d => d.PerformedByAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_DocumentIndexLogs_Accounts");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
