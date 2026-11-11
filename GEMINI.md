# DAU CHAT - AI AGENT RULES & ARCHITECTURE CONSTRAINTS (TEAM 5)

Bạn đang hỗ trợ phát triển dự án **DAU Chat** (Ứng dụng Chat nội bộ Trường ĐH Kiến trúc Đà Nẵng) viết bằng **C# (.NET 10.0 LTS, WPF, TCP Socket)**. Mọi đoạn code sinh ra BẮT BUỘC phải tuân thủ tuyệt đối các quy tắc kiến trúc sau:

## 1. KIẾN TRÚC LƯU TRỮ KIỂU ZALO (CẤM DÙNG SQL SERVER CHO TIN NHẮN & FILE)
- **Server Metadata (`DauChat.Server` / TV3):** Chỉ dùng SQLite (`Microsoft.Data.Sqlite`) hoặc JSON khóa luồng để lưu `Users`, `Roles`, `Classes`, `Rooms`, `RoomMembers`[cite: 2, 3].
- **Server Transient Relay (`DauChat.Server` / TV2):** Server KHÔNG lưu lịch sử chat và KHÔNG giữ file lâu dài[cite: 2, 3]. Khi người nhận Online -> đẩy thẳng qua Socket Stream (RAM-to-RAM). Khi người nhận Offline -> đưa vào `PendingQueue` (FIFO + TTL) và xóa ngay khi nhận gói `PacketAck` hoặc hết hạn TTL[cite: 2, 3].
- **Client Local-First (`DauChat.Core` / TV4 & TV5):**
  - Tin nhắn (TV4): Ghi nối tiếp (`FileMode.Append`) vào file `.jsonl` theo đường dẫn `LocalData/{UserId}/Messages/{RoomOrPartnerId}/YYYY-MM.jsonl` kèm file chỉ mục `.idx`[cite: 2, 3].
  - Tài liệu (TV5): Truyền file theo luồng (`Chunking` 64KB/chunk), ghi vào file `.part`, kiểm tra khớp mã băm `SHA-256` rồi lưu vào `LocalData/{UserId}/Files/{RoomOrPartnerId}/` và cập nhật `file_manifest.jsonl`[cite: 2, 3]. Hỗ trợ dọn rác cũ và nén `.zip` để Backup/Restore[cite: 2, 3].

## 2. RANH GIỚI PHÂN CÔNG (AI CHỈ ĐƯỢC SỬA ĐÚNG THƯ MỤC CỦA THÀNH VIÊN ĐÓ)
- **`DauChat.Shared`:** CHỈ ĐỌC (READ-ONLY) đối với TV2, TV3, TV4, TV5[cite: 2, 3]. Tuyệt đối không tự ý đổi tên hàm trong `Interfaces/`, không đổi cấu trúc `NetworkPacket` hay `PacketType` nếu chưa có lệnh từ Leader (TV1)[cite: 2, 3].
- **TV1 (Leader - Nhánh `feature/tv1-client-ui`):** Độc quyền chỉnh sửa `DauChat.Client.UI` (toàn bộ file `.xaml` và UI logic).
- **TV2 (Nghĩa - Nhánh `feature/tv2-network-server`):** Chỉ code trong `DauChat.Core/Networking` và `DauChat.Server`. Độc quyền quản lý `TcpListener`, `TcpClient`, đóng gói `Length-prefix` (4 byte độ dài + Payload) và `PacketDispatcher`[cite: 2, 3].
- **TV3 (Long - Nhánh `feature/tv3-auth-room-class`):** Chỉ code trong `DauChat.Core/AuthAndRooms` và phần Metadata của `DauChat.Server`. Không tự tạo kết nối TCP riêng, phải gửi/nhận qua `INetworkService` của TV2[cite: 2, 3].
- **TV4 (Yến Nhi - Nhánh `feature/tv4-messaging-history`):** Chỉ code trong `DauChat.Core/Messaging`[cite: 3]. Sử dụng `INetworkService` của TV2 để truyền tin và `IAuthRoomService` của TV3 để lấy thành viên phòng[cite: 2, 3].
- **TV5 (Quốc Tuấn - Nhánh `feature/tv5-file-storage-backup`):** Chỉ code trong `DauChat.Core/FileAndStorage`[cite: 3]. Truyền chunk qua `INetworkService` của TV2[cite: 2, 3].

## 3. QUY TẮC KIỂM THỬ CHO TV2 - TV5
- TV2, TV3, TV4, TV5 KHÔNG ĐƯỢC tạo cửa sổ giao diện `.xaml` trong nhánh của mình[cite: 3]. Khi cần chạy thử nghiệm thuật toán, hãy viết code test trong project `DauChat.SandboxTest`[cite: 3].