# HƯỚNG DẪN DEMO HỆ THỐNG DAU CHAT TRÊN 2 - 3 LAPTOP (MẠNG LAN)
### Đề tài: Xây dựng phần mềm trò chuyện và truyền tệp nội bộ bảo mật cho trường học theo kiến trúc Socket Client - Server
**Đơn vị:** Nhóm 5 • Trường Đại học Kiến trúc Đà Nẵng (DAU)  
**Nền tảng:** C# (.NET 10.0 LTS), WPF, Raw TCP Socket  

---

## 1. MÔ HÌNH MẠNG THỬ NGHIỆM ĐỀ XUẤT (VÀNG - TRÁNH BẪY LỖI)

> [!WARNING]
> **TỬ HUYỆT CẦN TRÁNH KHI ĐI THI / BẢO VỆ:**  
> Tuyệt đối **KHÔNG** sử dụng mạng Wi-Fi công cộng của trường hoặc quán cafe để demo 3 laptop với nhau.  
> **Lý do:** Wi-Fi công cộng luôn bật tính năng **"AP Isolation"** (Cách ly thiết bị để tránh nghe lén). Tính năng này sẽ chặn các laptop không cho kết nối hay ping thấy nhau, dẫn đến lỗi *Connection Timeout / Connection Refused*.

### Giải pháp kết nối an toàn 100%:
* Dùng **1 điện thoại di động bật tính năng "Điểm phát sóng cá nhân" (Mobile Hotspot)** (hoặc 1 cục phát Wi-Fi mini).
* Cả 2 hoặc 3 chiếc laptop của các thành viên cùng kết nối vào mạng Wi-Fi phát từ điện thoại này.
* *(Ghi chú: Điện thoại không cần bật 4G/Internet; chỉ cần phát sóng Wi-Fi để cấp phát cùng dải IP nội bộ LAN như `192.168.43.x`).*

---

## 2. PHÂN VAI 3 LAPTOP TRONG BUỔI BẢO VỆ

| Thiết bị | Vai trò hệ thống | Tài khoản demo | Mục đích kịch bản |
| :--- | :--- | :--- | :--- |
| **Laptop 1** *(Chạy Server)* | **MÁY CHỦ + GIẢNG VIÊN** | `gv_demo01`<br>Mật khẩu: `123456` | Khởi chạy `DauChat.Server.exe`. Đăng nhập Giảng viên: gửi đề bài CAD 30MB, ghim thông báo thi. |
| **Laptop 2** | **SINH VIÊN 1** | `2351220198`<br>Mật khẩu: `123456` | Bùi Trần Nhật Hưng (K23 CNTT). Nhận đề, chat 1-1, xuất bản vẽ. Đóng vai rời máy phòng Lab cũ. |
| **Laptop 3** | **SINH VIÊN 2** | `2351220205`<br>Mật khẩu: `123456` | Lê Thị Thu Hà (K23 CNTT). Nhận đề, nộp đồ án. Sau đó đăng nhập lại nick bạn Hưng để demo **Đồng bộ đa máy qua LAN**. |

---

## 3. CÁC BƯỚC THIẾT LẬP CHI TIẾT TRƯỚC GIỜ BẢO VỆ

### Bước 1: Cấu hình trên Laptop 1 (Máy chủ Server)
1. **Lấy địa chỉ IPv4:**
   - Bấm tổ hợp `Windows + R`, gõ `cmd` và bấm Enter.
   - Nhập lệnh: `ipconfig`
   - Tìm đến card mạng Wi-Fi (ví dụ: `Wireless LAN adapter Wi-Fi`), ghi nhớ dòng **IPv4 Address** (Ví dụ: `192.168.43.105`).
2. **Cấu hình Windows Defender Firewall (Rất quan trọng):**
   - Vào `Control Panel` > `Windows Defender Firewall` > `Allow an app or feature through Windows Defender Firewall`.
   - Tìm đến `DauChat.Server.exe` hoặc mở port `5050` TCP.
   - Hoặc đơn giản: Lần đầu chạy Server, khi Windows hiện popup hỏi phân quyền mạng, **tích chọn cả Private và Public Networks** rồi bấm **Allow access**.
3. **Chạy Server:**
   - Chạy file: `DauChat.Server\bin\Debug\net10.0\DauChat.Server.exe`.
   - Màn hình console xuất hiện dòng:
     `[Server] TcpListener đang lắng nghe tại Cổng 5050...`

---

### Bước 2: Cấu hình trên Laptop 2 & Laptop 3 (Máy Client)
1. Mở ứng dụng: `DauChat.Client.UI\bin\Debug\net10.0-windows\DauChat.Client.UI.exe`.
2. Tại màn hình Đăng nhập:
   - Nhấp vào nút màu đỏ: **[ Máy chủ TCP ]** (ngay bên cạnh ô Ghi nhớ).
   - Nhập thông tin kết nối tới Laptop 1:
     - **Địa chỉ IP Máy chủ:** Nhập đúng IP Laptop 1 đã lấy ở Bước 1 (Ví dụ: `192.168.43.105`).
     - **Cổng TCP:** `5050`.
3. Nhấp chọn tài khoản nhanh tương ứng hoặc nhập MSSV/Mật khẩu để đăng nhập.

---

## 4. KỊCH BẢN DIỄN DEMO TRƯỚC HỘI ĐỒNG (3 - 5 PHÚT)

### 🎬 Màn 1: Tốc độ thời gian thực của Socket TCP (Real-time Broadcast)
- **Hành động:** Trên Laptop 1 (Giảng viên), nhập tin nhắn:  
  *"🔔 Thông báo lớp 23CT1: Lịch nộp đồ án môn Lập trình mạng vào 15/01/2027 tại phòng A101"* và bấm **Ghim tin nhắn**.
- **Quan sát:** Trên Laptop 2 và Laptop 3 lập tức nảy chuông thông báo, thanh banner ghim trên đầu đổi sang tin thông báo mới mà **không có độ trễ (< 1ms)**.

### 🎬 Màn 2: Truyền tệp đồ án nặng (Bản vẽ CAD / PDF 30MB)
- **Hành động:** Giảng viên kéo thả file bản vẽ `MatBang_DoAn.dwg` vào ô chat của lớp `23CT1` và bấm Gửi.
- **Quan sát:**
  - Thẻ tệp lập tức hiện trên Laptop 2 & Laptop 3 với đầy đủ tên, dung lượng, mã băm SHA-256.
  - Trên Laptop 2, bấm nút **"Mở file"** -> Tệp được kéo từ mạng LAN về trong chớp mắt và mở trực tiếp bằng phần mềm mặc định của máy.
  - Bấm nút **"Lưu về máy"** -> Bật cửa sổ Windows SaveFileDialog để lưu vào thư mục mong muốn.
  - *Điểm nhấn thuyết trình:* Thuyết minh cho thầy cô rằng file lớn được xẻ thành các **Chunk 64KB** truyền qua Socket Stream và kiểm tra toàn vẹn bằng **SHA-256**, ổ cứng Server không bị quá tải.

### 🎬 Màn 3: Đổi máy phòng Lab & Đồng bộ qua Socket LAN (Cú chốt ăn điểm)
- **Hành động:**
  - Sinh viên 1 (Laptop 2) bấm **Đăng xuất**. App hiện xác nhận: *"Đẩy dữ liệu phiên làm việc lên Server LAN?"* -> Bấm **Đồng ý**.
  - Sang Laptop 3 (coi như bạn Hưng sang máy phòng Lab khác), đăng nhập tài khoản `2351220198`.
  - Màn hình lập tức hiện hộp thoại:  
    *"Phát hiện bản dữ liệu trò chuyện của bạn trên Server LAN. Bạn có muốn đồng bộ về máy này để tiếp tục làm việc không?"*
  - Bấm **"Đồng bộ ngay qua mạng LAN"**.
- **Quan sát:** Chỉ trong **0.3 giây**, toàn bộ lịch sử tin nhắn của bạn Hưng hiện ra đầy đủ trên Laptop 3 mà **hoàn toàn không cần cắm USB hay xuất file zip thủ công**!

---

## 5. XỬ LÝ SỰ CỐ NHANH KHI DEMO (TROUBLESHOOTING)

| Lỗi thường gặp | Nguyên nhân | Cách xử lý trong 10 giây |
| :--- | :--- | :--- |
| **"Không thể kết nối đến máy chủ"** | Nhập sai IP của Laptop 1 hoặc kết nối khác Wi-Fi. | Mở `cmd` gõ `ipconfig` xem lại IP Laptop 1. Đảm bảo cả 3 laptop đang bắt chung 1 điểm Wi-Fi Hotspot. |
| **"Connection Timed Out"** | Windows Firewall trên Laptop 1 đang chặn cổng `5050`. | Vào Windows Firewall tắt tạm Private Network, hoặc gõ lệnh PowerShell quyền Admin trên Laptop 1:<br>`New-NetFirewallRule -DisplayName "DAU Chat Port" -Direction Inbound -LocalPort 5050 -Protocol TCP -Action Allow` |
| **Cổng 5050 bị chiếm** | Server lần trước chưa tắt hẳn còn chạy ngầm. | Mở Task Manager tắt tiến trình `DauChat.Server.exe`, sau đó mở lại. |

---
*DAU Chat • Đồ án Lập trình mạng C# .NET 10 • Khoa CNTT Trường ĐH Kiến trúc Đà Nẵng*
