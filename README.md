"# PRN222-group3-asg2" 
# Academic Document RAG System

Dự án hỗ trợ quản lý môn học, tài khoản, tài liệu học tập và cho phép sinh viên đặt câu hỏi dựa trên nội dung tài liệu đã được upload. Câu trả lời được tạo dựa trên dữ liệu truy xuất từ tài liệu, giúp hạn chế trả lời sai ngữ cảnh và hỗ trợ truy vết nguồn.

## Diagram

Chèn hình diagram tại đây sau.


## Razor Pages

Assignment 2 sử dụng ASP.NET Core Razor Pages làm presentation layer. Project vẫn giữ mô hình 3-layer gồm Razor Pages, Services và DataAccess. Razor Pages gọi service, service xử lý business logic, repository làm việc với SQL Server thông qua Entity Framework Core.

## Features

Đăng nhập và phân quyền người dùng theo vai trò Admin, Teacher, Student. Admin quản lý tài khoản và môn học. Admin tạo và chỉnh sửa tài khoản giảng viên, mỗi môn học chỉ được gán cho một giảng viên phụ trách và mỗi giảng viên chỉ phụ trách một môn học. Teacher upload tài liệu học tập theo môn học được phân công. Hệ thống hỗ trợ tài liệu PDF, DOCX, PPTX và TXT. Student đặt câu hỏi dựa trên nội dung tài liệu đã upload và đã index. Câu trả lời kèm nguồn tham chiếu từ tài liệu. Hệ thống lưu lịch sử phiên hỏi đáp, quản lý trạng thái upload, trạng thái index và log xử lý tài liệu. Danh sách môn học của giảng viên được cập nhật realtime bằng SignalR khi Admin thay đổi dữ liệu môn học.

## Hướng dẫn sử dụng ngắn

### 1. Chạy RAG service

```powershell
cd rag-service
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
copy .env.example .env
uvicorn app.main:app --reload --port 8000
```

Kiểm tra RAG service:

```text
http://localhost:8000/health
```

### 2. Chuẩn bị database

Tạo hoặc khôi phục database SQL Server:

```text
AcademicRagManagement
```

Connection string nằm trong file:

```text
dotnet-razor/HoaiKhoi_SE1950_A02/AcademicDocumentRagSystem.RazorPages/appsettings.json
```

Các script bổ sung nằm trong:

```text
dotnet-razor/HoaiKhoi_SE1950_A02/AcademicDocumentRagSystem.DataAccess/DatabaseScripts/
```

### 3. Chạy ứng dụng Razor Pages

```powershell
dotnet restore dotnet-razor/HoaiKhoi_SE1950_A02/HoaiKhoi_SE1950_A02.sln
dotnet build dotnet-razor/HoaiKhoi_SE1950_A02/HoaiKhoi_SE1950_A02.sln
dotnet run --project dotnet-razor/HoaiKhoi_SE1950_A02/AcademicDocumentRagSystem.RazorPages
```

Mở ứng dụng:

```text
https://localhost:7150/
```

### 4. Sử dụng hệ thống

Đăng nhập bằng tài khoản Admin được cấu hình trong `appsettings.json` hoặc tài khoản có sẵn trong database. Admin quản lý môn học và tài khoản. Teacher upload tài liệu cho môn học được phân công. Hệ thống tự động tạo chunk preview và gửi tài liệu sang RAG service để index. Student chọn tài liệu đã index và đặt câu hỏi. Hệ thống trả lời dựa trên nội dung tài liệu và hiển thị nguồn tham chiếu.

## Công nghệ sử dụng

### Backend Razor Pages

- ASP.NET Core Razor Pages
- .NET 8
- Entity Framework Core
- SQL Server
- SignalR
- Bootstrap

### RAG Service

- Python 3.10+
- FastAPI
- ChromaDB
- BAAI/bge-m3 embedding model
- Gemini

### Database

- SQL Server

Các bảng chính:

- Accounts
- Courses
- Documents
- DocumentChunks
- DocumentIndexLogs
- ChatSessions
- ChatMessages

## Kiến trúc tổng quan

```text
Người dùng
   |
   v
ASP.NET Core Razor Pages
   |
   |-- Quản lý tài khoản
   |-- Quản lý môn học
   |-- Upload tài liệu
   |-- Hỏi đáp tài liệu
   |-- Lưu lịch sử chat
   |
   v
Services Layer
   |
   v
Repositories / DataAccess
   |
   v
SQL Server
   |
   v
Python FastAPI RAG Service
   |
   |-- Document Loader
   |-- Chunking Service
   |-- Embedding Service
   |-- Vector Store Service
   |-- LLM Service
   |
   v
ChromaDB
```

## Repo structure

```text
PRN222-group3-asg2/
├── README.md
├── dotnet-razor/
│   └── HoaiKhoi_SE1950_A02/
│       ├── HoaiKhoi_SE1950_A02.sln
│       ├── AcademicDocumentRagSystem.RazorPages/
│       │   ├── Program.cs
│       │   ├── appsettings.json
│       │   ├── Hubs/
│       │   ├── Infrastructure/
│       │   ├── Pages/
│       │   │   ├── Accounts/
│       │   │   ├── Admin/
│       │   │   ├── Auth/
│       │   │   ├── Chat/
│       │   │   ├── Courses/
│       │   │   ├── Documents/
│       │   │   ├── Student/
│       │   │   ├── Teacher/
│       │   │   └── Shared/
│       │   ├── storage/
│       │   └── wwwroot/
│       ├── AcademicDocumentRagSystem.Services/
│       │   ├── Chunking/
│       │   ├── DTOs/
│       │   ├── Email/
│       │   ├── Implementations/
│       │   ├── Interfaces/
│       │   ├── Maintenance/
│       │   └── RagIntegration/
│       └── AcademicDocumentRagSystem.DataAccess/
│           ├── DatabaseScripts/
│           ├── Models/
│           └── Repositories/
└── rag-service/
    ├── app/
    │   ├── api/
    │   ├── core/
    │   ├── models/
    │   ├── repositories/
    │   ├── services/
    │   └── utils/
    ├── chroma_db/
    ├── storage/
    ├── tests/
    ├── .env.example
    └── requirements.txt
```

## Luồng xử lý tài liệu

```text
Teacher upload tài liệu
        |
        v
Razor Pages kiểm tra quyền upload theo môn học
        |
        v
Lưu file và metadata vào SQL Server
        |
        v
Tạo hash để kiểm tra trùng tài liệu
        |
        v
Tạo chunk preview trong database
        |
        v
Gửi file path sang RAG service
        |
        v
RAG service đọc nội dung tài liệu
        |
        v
Chia nội dung thành chunks
        |
        v
Tạo embedding cho từng chunk
        |
        v
Lưu vector và metadata vào ChromaDB
        |
        v
Cập nhật trạng thái index về hệ thống
```

## Yêu cầu môi trường

Trước khi chạy dự án, cần cài đặt:

- Visual Studio 2022 hoặc Rider
- .NET SDK 8
- SQL Server
- SQL Server Management Studio
- Python 3.10 hoặc 3.11
- Git

## Contributors

- Trần Hoài Khôi
- Chu Vương Mạnh
- Huỳnh Trần Thế Thuật
- Lâm Hoàng Nhân
