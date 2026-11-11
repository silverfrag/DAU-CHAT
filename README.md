# DAU Chat — Ứng dụng Chat nội bộ Trường ĐH Kiến trúc Đà Nẵng (Team 5)

Hệ thống Chat & trao đổi tài liệu đồ án nội bộ xây dựng trên **C# (.NET 10.0 LTS, WPF, TCP Socket)** theo kiến trúc **Local-First + Transient Relay Server** (kiểu Zalo).

## Yêu cầu môi trường
- **.NET 10.0 SDK (LTS)** trở lên (cấu hình trong `global.json`: `10.0.100`, `rollForward: latestFeature`).
- Cài nhanh trên Windows:
  ```powershell
  winget install --id Microsoft.DotNet.SDK.10 -e
  ```

## Cấu trúc Solution (`DauChat.sln`)
1. **`DauChat.Shared`** (`net10.0`, Class Library — **READ-ONLY** đối với TV2–TV5):
   - `Constants/`: `ProtocolConstants` (cổng `9050`, header 4 byte, chunk `64 KB`, TTL `PendingQueue`).
   - `Enums/`: `PacketType` (`MOD_01_01`–`MOD_08_04`), `UserRole`, `AccountStatus`, `PresenceStatus`, `RoomType`, `MessageType`, `FileCategory`.
   - `Models/`: `UserDto`, `ClassDto`, `RoomDto`, `MessageDto`, `FileChunkDto`, `FileManifestDto`, `ServerStatsDto` và các DTO hỗ trợ.
   - `Networking/`: `NetworkPacket` (hỗ trợ Length-prefix framing: 4 byte độ dài + Payload).
   - `Interfaces/`: `INetworkService` (TV2), `IAuthRoomService` (TV3), `IMessagingService` (TV4), `IFileStorageService` (TV5).
2. **`DauChat.Core`** (`net10.0`, Class Library):
   - `Networking/` — TV2 (`feature/tv2-network-server`)
   - `AuthAndRooms/` — TV3 (`feature/tv3-auth-room-class`)
   - `Messaging/` — TV4 (`feature/tv4-messaging-history`)
   - `FileAndStorage/` — TV5 (`feature/tv5-file-storage-backup`)
3. **`DauChat.Server`** (`net10.0`, Console App) — Server trung chuyển tạm & lưu trữ Metadata (SQLite / JSON khóa luồng).
4. **`DauChat.Client.UI`** (`net10.0-windows`, WPF App) — Độc quyền giao diện bởi TV1 (`feature/tv1-client-ui`).
5. **`DauChat.SandboxTest`** (`net10.0`, Console App) — Môi trường kiểm thử logic cho TV2–TV5.

## Lệnh build và chạy thử
```powershell
dotnet build DauChat.sln
dotnet run --project DauChat.SandboxTest/DauChat.SandboxTest.csproj
dotnet run --project DauChat.Server/DauChat.Server.csproj
```

