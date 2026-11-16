# Section 4 and 5 Data and Builder Functions for DAU Chat Report (Academic Tech Dev Edition)

import docx
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx_helpers import set_table_borders, add_header_styled_row, add_data_row, add_tech_box, add_figure

def add_ipo_module_table(doc, mod_id, mod_name, subsystem, priority, input_text, process_text, output_text, exception_text, note_text):
    p_h = doc.add_paragraph()
    p_h.paragraph_format.space_before = Pt(8)
    p_h.paragraph_format.space_after = Pt(2)
    p_h.paragraph_format.keep_with_next = True
    r_id = p_h.add_run(f"• Module: [{mod_id}] – {mod_name}\n")
    r_id.font.name = 'Times New Roman'
    r_id.font.size = Pt(11.5)
    r_id.font.bold = True
    r_id.font.color.rgb = RGBColor(0, 0, 0)

    r_sub = p_h.add_run(f"Thuộc phân hệ: {subsystem} | Mức độ ưu tiên: {priority}")
    r_sub.font.name = 'Times New Roman'
    r_sub.font.size = Pt(10.5)
    r_sub.font.italic = True
    r_sub.font.color.rgb = RGBColor(0, 0, 0)

    tbl = doc.add_table(rows=1, cols=2)
    set_table_borders(tbl)
    tbl.alignment = WD_TABLE_ALIGNMENT.CENTER
    widths = [Inches(1.8), Inches(5.4)]
    add_header_styled_row(tbl, ["Hạng mục IPO", "Nội dung Chi tiết Quy trình Nghiệp vụ & Kỹ thuật"], widths)

    items = [
        ("Input (Đầu vào)", input_text),
        ("Process (Thuật toán / Xử lý)", process_text),
        ("Output (Đầu ra)", output_text),
        ("Xử lý Ngoại lệ (Exception)", exception_text),
        ("Ràng buộc & Hiệu năng", note_text)
    ]
    for idx, (cat, desc) in enumerate(items):
        add_data_row(tbl, [cat, desc], is_even=(idx % 2 == 1), col_widths=widths)

    p_after = doc.add_paragraph()
    p_after.paragraph_format.space_before = Pt(0)
    p_after.paragraph_format.space_after = Pt(4)

def add_section_4(doc):
    doc.add_heading("4. PHÂN TÍCH CHỨC NĂNG ĐẾN TẬN CÙNG (CHỨC NĂNG CON → MODULE)", level=1)

    doc.add_heading("4.1. Cấu trúc phân rã chức năng (Functional Decomposition Tree)", level=2)
    p_tree_intro = doc.add_paragraph(
        "Nhằm quản lý và kiểm soát chất lượng mã nguồn .NET 10 một cách chặt chẽ nhất, toàn bộ 8 phân hệ của hệ thống DAU Chat được phân rã đến tận cùng thành 63 module chức năng đơn nguyên (Atomic Modules, mã hiệu MOD_XX_YY). Mỗi module được gắn nhãn phân quyền rõ ràng:\n"
        "• [ALL]: Mọi người dùng (Admin, Giảng viên, Sinh viên) đều có quyền thực thi.\n"
        "• [A]: Quyền độc quyền của Quản trị viên (Admin).\n"
        "• [GV]: Quyền hạn của Giảng viên.\n"
        "• [SV]: Quyền hạn của Sinh viên.\n"
        "• [HT]: Chức năng nền tảng của Hệ thống mạng / Server."
    )
    p_tree_intro.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY

    tree_text = (
        "1. PHÂN HỆ QUẢN LÝ TÀI KHOẢN & XÁC THỰC (TV3 phụ trách Core, TV1 phụ trách UI)\n"
        "├── 1.1. Xác thực người dùng [ALL]\n"
        "│   ├── 1.1.1. Đăng nhập hệ thống bằng MSSV/Mã GV [ALL] (MOD_01_01)\n"
        "│   ├── 1.1.2. Đăng xuất khỏi hệ thống [ALL] (MOD_01_02)\n"
        "│   └── 1.1.3. Đổi mật khẩu tài khoản [ALL] (MOD_01_03)\n"
        "├── 1.2. Quản lý thông tin hồ sơ [ALL]\n"
        "│   ├── 1.2.1. Xem thông tin cá nhân chi tiết [ALL] (MOD_01_04)\n"
        "│   └── 1.2.2. Cập nhật thông tin liên hệ và Avatar [ALL] (MOD_01_05)\n"
        "├── 1.3. Quản trị tài khoản người dùng [A]\n"
        "│   ├── 1.3.1. Thêm mới tài khoản sinh viên/giảng viên [A] (MOD_01_06)\n"
        "│   ├── 1.3.2. Cập nhật thông tin tài khoản người dùng [A] (MOD_01_07)\n"
        "│   ├── 1.3.3. Khóa tạm thời tài khoản người dùng [A] (MOD_01_08)\n"
        "│   ├── 1.3.4. Mở khóa tài khoản người dùng [A] (MOD_01_09)\n"
        "│   └── 1.3.5. Cấp lại mật khẩu mặc định (Reset password) [A] (MOD_01_10)\n"
        "└── 1.4. Phân bổ vai trò & quyền hạn (RBAC) [A]\n"
        "    ├── 1.4.1. Thiết lập vai trò Quản trị viên [A] (MOD_01_11)\n"
        "    ├── 1.4.2. Thiết lập vai trò Giảng viên phụ trách [A] (MOD_01_12)\n"
        "    └── 1.4.3. Thiết lập vai trò Sinh viên lớp học [A] (MOD_01_13)\n\n"
        "2. PHÂN HỆ QUẢN LÝ KẾT NỐI & HÀNG ĐỢI TRUNG CHUYỂN (TV2 phụ trách độc quyền)\n"
        "├── 2.1. Quản lý kết nối TCP Socket [HT]\n"
        "│   ├── 2.1.1. Lắng nghe và tiếp nhận kết nối Client qua TCP Socket [HT] (MOD_02_01)\n"
        "│   ├── 2.1.2. Xử lý ngắt kết nối Client chủ động hoặc đột ngột [HT] (MOD_02_02)\n"
        "│   └── 2.1.3. Quản lý đa kết nối đồng thời (Multi-Client Async/Await) [HT] (MOD_02_03)\n"
        "├── 2.2. Theo dõi trạng thái hiện diện (Presence) [HT]\n"
        "│   ├── 2.2.1. Phát tín hiệu Heartbeat và cập nhật trạng thái Online [HT] (MOD_02_04)\n"
        "│   └── 2.2.2. Phát hiện mất kết nối quá hạn và cập nhật Offline [HT] (MOD_02_05)\n"
        "└── 2.3. Hàng đợi trung chuyển tạm thời (Pending Queue) [HT]\n"
        "    ├── 2.3.1. Xếp hàng đợi tin nhắn khi người nhận Offline (FIFO Queue) [HT] (MOD_02_06)\n"
        "    ├── 2.3.2. Tự động xả gói tin khi người dùng đăng nhập lại [HT] (MOD_02_07)\n"
        "    ├── 2.3.3. Xóa gói tin khỏi RAM sau khi nhận PacketAck thành công [HT] (MOD_02_08)\n"
        "    └── 2.3.4. Quản lý thời gian sống TTL và tự hủy tin quá hạn (7 ngày) [HT] (MOD_02_09)\n\n"
        "3. PHÂN HỆ CHAT CÁ NHÂN 1-1 (TV4 phụ trách Engine, TV1 phụ trách UI)\n"
        "├── 3.1. Soạn thảo & Gửi tin nhắn cá nhân [ALL]\n"
        "│   ├── 3.1.1. Soạn thảo nội dung văn bản tin nhắn [ALL] (MOD_03_01)\n"
        "│   ├── 3.1.2. Đóng gói MessagePacket và gửi qua NetworkClient [ALL] (MOD_03_02)\n"
        "│   └── 3.1.3. Ghi nối tiếp Append-only vào file .jsonl người gửi [ALL] (MOD_03_03)\n"
        "├── 3.2. Tiếp nhận & Hiển thị tin nhắn [ALL]\n"
        "│   ├── 3.2.1. Bắt gói tin MessagePacket từ Socket và giải mã [ALL] (MOD_03_04)\n"
        "│   ├── 3.2.2. Ghi nối tiếp vào file .jsonl người nhận và sinh chỉ mục .idx [ALL] (MOD_03_05)\n"
        "│   ├── 3.2.3. Gửi gói tin phản hồi xác nhận nhận tin (PacketAck) [ALL] (MOD_03_06)\n"
        "│   └── 3.2.4. Cập nhật bóng chat lên giao diện WPF thời gian thực [ALL] (MOD_03_07)\n"
        "└── 3.3. Trạng thái tin nhắn & Lịch sử trò chuyện [ALL]\n"
        "    ├── 3.3.1. Cập nhật trạng thái tin nhắn (Đang gửi/Đã gửi/Đã nhận) [ALL] (MOD_03_08)\n"
        "    ├── 3.3.2. Tải phân trang lịch sử chat từ file .jsonl qua chỉ mục .idx [ALL] (MOD_03_09)\n"
        "    └── 3.3.3. Tìm kiếm tin nhắn theo từ khóa trong tệp cục bộ [ALL] (MOD_03_10)\n\n"
        "4. PHÂN HỆ CHAT NHÓM / PHÒNG HỌC TẬP (TV3 quản lý Phòng, TV4 xử lý Chat)\n"
        "├── 4.1. Quản lý phòng chat nhóm [GV/A]\n"
        "│   ├── 4.1.1. Tạo mới phòng chat môn học hoặc nhóm đồ án [GV/A] (MOD_04_01)\n"
        "│   ├── 4.1.2. Chỉnh sửa thông tin phòng chat [GV/A] (MOD_04_02)\n"
        "│   └── 4.1.3. Giải tán hoặc lưu trữ phòng chat nhóm [GV/A] (MOD_04_03)\n"
        "├── 4.2. Quản lý thành viên phòng chat [ALL]\n"
        "│   ├── 4.2.1. Mời thành viên mới vào phòng chat [GV/SV] (MOD_04_04)\n"
        "│   ├── 4.2.2. Xóa thành viên khỏi phòng chat [GV] (MOD_04_05)\n"
        "│   └── 4.2.3. Rời khỏi phòng chat nhóm [ALL] (MOD_04_06)\n"
        "└── 4.3. Phát tán tin nhắn nhóm (Broadcast Engine) [ALL]\n"
        "    ├── 4.3.1. Gửi tin nhắn nhóm kèm định danh RoomId [ALL] (MOD_04_07)\n"
        "    ├── 4.3.2. Server tra cứu thành viên và broadcast đa luồng [HT] (MOD_04_08)\n"
        "    └── 4.3.3. Tiếp nhận và ghi nhật ký nhóm tại từng máy trạm [ALL] (MOD_04_09)\n\n"
        "5. PHÂN HỆ TRUYỀN TỆP SOCKET STREAM (TV5 phụ trách Engine, TV1 phụ trách UI)\n"
        "├── 5.1. Chuẩn bị & Phân mảnh tệp tin [ALL]\n"
        "│   ├── 5.1.1. Khay soạn thảo tệp chờ gửi (Staged File Tray) [ALL] (MOD_05_01)\n"
        "│   ├── 5.1.2. Tính toán mã băm gốc SHA-256 của tệp gửi [ALL] (MOD_05_02)\n"
        "│   ├── 5.1.3. Gửi yêu cầu truyền tệp FileTransferRequest [ALL] (MOD_05_03)\n"
        "│   └── 5.1.4. Chia nhỏ tệp thành các Chunk 64KB và truyền qua Socket [ALL] (MOD_05_04)\n"
        "├── 5.2. Chuyển tiếp & Ghép tệp tại Bên nhận [ALL]\n"
        "│   ├── 5.2.1. Server chuyển tiếp RAM-to-RAM trực tiếp các Chunk [HT] (MOD_05_05)\n"
        "│   ├── 5.2.2. Tiếp nhận Chunk và ghi nối tiếp vào tệp filename.part [ALL] (MOD_05_06)\n"
        "│   ├── 5.2.3. Kiểm tra tính toàn vẹn SHA-256 sau khi nhận đủ 100% [ALL] (MOD_05_07)\n"
        "│   └── 5.2.4. Chuyển đổi tên tệp chính thức và ghi file_manifest.jsonl [ALL] (MOD_05_08)\n"
        "└── 5.3. Tương tác tệp của Bên nhận (Dual Receiver Actions) [ALL]\n"
        "    ├── 5.3.1. Thao tác 'Mở file' (Quick View qua app mặc định hệ điều hành) [ALL] (MOD_05_09)\n"
        "    └── 5.3.2. Thao tác 'Lưu về máy' (SaveFileDialog chọn thư mục đích) [ALL] (MOD_05_10)\n\n"
        "6. PHÂN HỆ QUẢN LÝ LỚP & PHÂN BỔ SINH VIÊN (TV3 phụ trách Core, TV1 phụ trách UI)\n"
        "├── 6.1. Quản lý danh mục lớp học [A/GV]\n"
        "│   ├── 6.1.1. Tạo mới lớp sinh hoạt hoặc lớp học phần [A/GV] (MOD_06_01)\n"
        "│   ├── 6.1.2. Cập nhật thông tin niên khóa và giảng viên phụ trách [A/GV] (MOD_06_02)\n"
        "│   └── 6.1.3. Khóa lớp hoặc kết thúc học phần [A/GV] (MOD_06_03)\n"
        "└── 6.2. Phân bổ thành viên lớp học [A/GV]\n"
        "    ├── 6.2.1. Thêm sinh viên vào lớp học theo MSSV [A/GV] (MOD_06_04)\n"
        "    ├── 6.2.2. Nhập danh sách sinh viên hàng loạt từ danh sách chuẩn [A/GV] (MOD_06_05)\n"
        "    └── 6.2.3. Xóa sinh viên khỏi danh sách lớp học [A/GV] (MOD_06_06)\n\n"
        "7. PHÂN HỆ QUẢN LÝ DỮ LIỆU LOCAL-FIRST & ĐỒNG BỘ LAN VAULT (TV4 + TV5 phụ trách)\n"
        "├── 7.1. Quản lý kho dữ liệu cục bộ Client [ALL]\n"
        "│   ├── 7.1.1. Tổ chức thư mục lưu trữ LocalData/{UserId}/ [ALL] (MOD_07_01)\n"
        "│   ├── 7.1.2. Thống kê dung lượng chiếm dụng ổ đĩa của tin nhắn và tệp [ALL] (MOD_07_02)\n"
        "│   └── 7.1.3. Dọn dẹp tệp tạm và xóa dữ liệu tệp tải về quá hạn [ALL] (MOD_07_03)\n"
        "├── 7.2. Sao lưu & Khôi phục ngoại tuyến (.zip) [ALL]\n"
        "│   ├── 7.2.1. Đóng gói toàn bộ LocalData thành tệp nén .zip [ALL] (MOD_07_04)\n"
        "│   └── 7.2.2. Giải nén và tái thiết lập lịch sử từ tệp .zip [ALL] (MOD_07_05)\n"
        "└── 7.3. Đồng bộ mạng LAN Socket Sync & Chế độ Lab PC [ALL]\n"
        "    ├── 7.3.1. Đẩy bản sao lưu đồng bộ lên Server Vault (VaultPush 701) [ALL] (MOD_07_06)\n"
        "    ├── 7.3.2. Server kiểm soát hạn ngạch 1GB và lưu latest_snapshot.bin [HT] (MOD_07_07)\n"
        "    ├── 7.3.3. Tải bản sao lưu từ Server Vault khi đăng nhập máy mới (703) [ALL] (MOD_07_08)\n"
        "    └── 7.3.4. Chế độ Lab PC Mode: Tự động Push Vault và xóa sạch máy trạm [ALL] (MOD_07_09)\n\n"
        "8. PHÂN HỆ QUẢN TRỊ SERVER & GIÁM SÁT HỆ THỐNG (TV2 + TV3 phụ trách)\n"
        "├── 8.1. Giám sát kết nối mạng Socket [A/HT]\n"
        "│   ├── 8.1.1. Thống kê số lượng kết nối Client hoạt động thời gian thực [A/HT] (MOD_08_01)\n"
        "│   └── 8.1.2. Tra cứu danh sách địa chỉ IP và trạng thái phiên kết nối [A/HT] (MOD_08_02)\n"
        "└── 8.2. Quản trị kho đệm Server Vault & Nhật ký hệ thống [A/HT]\n"
        "    ├── 8.2.1. Theo dõi tổng dung lượng kho Server Vault và thực thi LRU [A/HT] (MOD_08_03)\n"
        "    ├── 8.2.2. Xóa bỏ bản sao lưu Vault của người dùng khi hết hạn [A/HT] (MOD_08_04)\n"
        "    └── 8.2.3. Ghi nhận và truy vấn nhật ký kiểm toán SystemAuditLogs [A/HT] (MOD_08_05)"
    )

    add_tech_box(doc, "CÂY PHÂN RÃ CHỨC NĂNG 63 MODULES (FUNCTIONAL DECOMPOSITION TREE)", tree_text)

    doc.add_heading("4.2. Mô tả Module chi tiết (IPO – Input / Process / Output)", level=2)
    p_ipo_intro = doc.add_paragraph(
        "Dưới đây là đặc tả chi tiết theo chuẩn IPO (Input - Process - Output) cho 9 module cốt lõi đại diện của các phân hệ quan trọng nhất trong hệ thống DAU Chat, tuân thủ đúng biểu mẫu Table 3 quy định:"
    )
    p_ipo_intro.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY

    # 1. MOD_01_01
    add_ipo_module_table(
        doc,
        mod_id="MOD_01_01",
        mod_name="Đăng nhập hệ thống bằng MSSV / Mã Giảng viên",
        subsystem="Phân hệ 1: Xác thực & Quản lý Tài khoản (TV3)",
        priority="Bắt buộc (Cao nhất)",
        input_text="• Username (chuỗi ký tự, là MSSV 10 chữ số chuẩn K23, ví dụ: 2351220198 hoặc mã GV: gv_demo01).\n• Password (chuỗi ký tự mật khẩu dạng plaintext người dùng nhập tại giao diện LoginView).\n• Nguồn dữ liệu: Nhập từ bàn phím người dùng trên màn hình đăng nhập Client.\n• Điều kiện tiên quyết: Client đã thiết lập thành công kết nối TCP Socket tới Server.",
        process_text="1. Client băm mật khẩu: hash = SHA256(password + salt) hoặc gửi an toàn qua gói tin PacketLogin (PacketType 101).\n2. Server tiếp nhận gói tin, phân tích Username và tìm kiếm trong bảng Users (SQLite Metadata).\n3. Nếu không tìm thấy User hoặc trường IsActive == 0: Trả về LoginResponsePacket thất bại kèm mã lỗi.\n4. Nếu tìm thấy: So sánh hash mật khẩu lưu trong CSDL với hash gửi lên.\n5. Khi mật khẩu khớp: Server tạo SessionToken ngẫu nhiên, gắn kết nối TCP của Client với UserId, lấy thông tin Role, FullName, Email, Lớp sinh hoạt và trả về gói tin LoginResponsePacket (PacketType 102) thành công.\n6. Client nhận gói phản hồi, lưu trạng thái đăng nhập vào AppSession và chuyển hướng giao diện sang MainShellView.",
        output_text="• Thành công: Token phiên làm việc, thông tin người dùng (UserDto) được lưu trong bộ nhớ Client; giao diện chuyển sang MainShellView.\n• Thất bại: Thông báo lỗi chi tiết ('Sai tài khoản hoặc mật khẩu' / 'Tài khoản đang bị khóa').",
        exception_text="• Lỗi mất kết nối Socket trước khi gửi: Hiển thị cảnh báo 'Không thể kết nối đến máy chủ DAU Chat'.\n• Lỗi tài khoản bị khóa: Thông báo 'Tài khoản của bạn đã bị khóa bởi Quản trị viên, vui lòng liên hệ Khoa CNTT'.\n• Timeout quá 5 giây không có phản hồi: Tự động hủy yêu cầu và cho phép người dùng thử lại.",
        note_text="• Bảo mật: Mật khẩu tuyệt đối không bao giờ được ghi ra file log hay lưu trữ dưới dạng plaintext.\n• Hiệu năng: Truy vấn bảng Users trên trường Username có đánh chỉ mục Index UNIQUE, thời gian tra cứu < 2ms."
    )

    # 2. MOD_02_01
    add_ipo_module_table(
        doc,
        mod_id="MOD_02_01",
        mod_name="Đóng gói TCP Length-prefix Framing & Điều phối gói tin (Dispatcher)",
        subsystem="Phân hệ 2: Quản lý Kết nối & Hàng đợi Trung chuyển (TV2)",
        priority="Bắt buộc (Cốt lõi)",
        input_text="• Luồng byte nhị phân liên tục (Byte Stream) nhận được từ NetworkStream của kết nối TcpClient.\n• Gói tin khởi tạo từ tầng ứng dụng cần gửi đi (NetworkPacket: PacketType 1 byte, Payload mảng byte).",
        process_text="1. Quá trình Gửi (Send Packet):\n   a. Tính kích thước Payload: payloadLength = packet.Payload.Length.\n   b. Tổng độ dài gói tin = 1 (PacketType) + payloadLength.\n   c. Ghi 4 byte Big-Endian biểu diễn tổng độ dài vào đầu mảng byte: BinaryPrimitives.WriteInt32BigEndian(headerBytes, totalLength).\n   d. Ghép mảng: [4B Length][1B PacketType][PayloadBytes].\n   e. Ghi bất đồng bộ qua Socket: await stream.WriteAsync(buffer, 0, buffer.Length).\n2. Quá trình Nhận (Receive Packet Dispatcher):\n   a. Đọc đúng 4 byte đầu tiên để xác định độ dài gói tin: await ReadExactAsync(stream, 4).\n   b. Chuyển đổi 4 byte sang số nguyên 32-bit Big-Endian (packetSize).\n   c. Cấp phát buffer kích thước packetSize và đọc chính xác packetSize byte tiếp theo.\n   d. Trích xuất byte đầu tiên làm PacketType, phần còn lại là Payload.\n   e. Kích hoạt PacketDispatcher để điều hướng gói tin tới Handler xử lý tương ứng (TV3/TV4/TV5).",
        output_text="• Đóng gói: Mảng byte hoàn chỉnh sẵn sàng truyền qua mạng Socket không bị dính gói hay vỡ gói.\n• Điều phối: Đối tượng NetworkPacket được chuyển giao chính xác cho service nghiệp vụ tương ứng.",
        exception_text="• Lỗi kết nối bị đóng giữa chừng: Bắt SocketException/IOException, kích hoạt sự kiện ngắt kết nối và đóng socket.\n• Gói tin có kích thước bất thường (> 100MB): Server chủ động đóng kết nối phòng chống tấn công DoS/Memory Overflow.",
        note_text="• Tính toàn vẹn: Đảm bảo giải quyết triệt để hiện tượng 'TCP Packet Fragmentation' (vỡ gói) và 'Packet Sticking' (dính gói).\n• Hiệu năng: Sử dụng ArrayPool<byte>.Shared để tái sử dụng bộ đệm byte, hạn chế Garbage Collection (GC) áp lực lên RAM."
    )

    # 3. MOD_02_06
    add_ipo_module_table(
        doc,
        mod_id="MOD_02_06",
        mod_name="Xếp hàng đợi tin nhắn tạm thời phía Server (FIFO + TTL Pending Queue)",
        subsystem="Phân hệ 2: Quản lý Kết nối & Hàng đợi Trung chuyển (TV2)",
        priority="Bắt buộc",
        input_text="• Đối tượng NetworkPacket (Tin nhắn 1-1 hoặc Thông báo) cần gửi tới RecipientId.\n• Trạng thái kết nối của RecipientId được xác định là Offline (không có trong ActiveConnectionMap).",
        process_text="1. Server kiểm tra danh mục hàng đợi của RecipientId trong cấu trúc dữ liệu ConcurrentDictionary<string, ConcurrentQueue<PendingQueueItem>>.\n2. Nếu chưa có hàng đợi cho User này, tạo mới một hàng đợi FIFO luồng an toàn.\n3. Khởi tạo đối tượng PendingQueueItem: gán QueueId = UUID, RecipientId, PacketPayload, PacketType, EnqueuedAt = DateTime.UtcNow, ExpiryTime = DateTime.UtcNow.AddDays(7).\n4. Đưa gói tin vào cuối hàng đợi: userQueue.Enqueue(item).\n5. Khi người nhận kết nối và đăng nhập thành công: Tiến trình xả hàng đợi kiểm tra, lấy tuần tự từng gói tin (userQueue.TryDequeue) và đẩy thẳng qua Socket Stream của người nhận.\n6. Nhận được gói tin xác nhận PacketAck từ Client: Xóa bỏ bản ghi hoàn toàn khỏi bộ nhớ RAM của Server.",
        output_text="• Gói tin được bảo lưu an toàn trong RAM của Server khi người nhận chưa Online.\n• Gói tin được chuyển phát tức thì ngay thời điểm người nhận vừa kết nối mạng trở lại.",
        exception_text="• Hàng đợi của một người dùng vượt quá 10.000 tin nhắn: Tự động loại bỏ các tin nhắn cũ nhất (FIFO Drop-oldest) và ghi log cảnh báo.\n• Tin nhắn tồn tại quá 7 ngày mà người nhận không online: Tiến trình nền định kỳ quét và giải phóng bộ nhớ RAM.",
        note_text="• Tuân thủ nghiêm ngặt nguyên tắc Kiến trúc Zalo: KHÔNG lưu tin nhắn vào ổ cứng Server, chỉ lưu tạm trên RAM với TTL."
    )

    # 4. MOD_03_01
    add_ipo_module_table(
        doc,
        mod_id="MOD_03_01",
        mod_name="Gửi tin nhắn văn bản 1-1 & Ghi nối tiếp Append-only Log (.jsonl)",
        subsystem="Phân hệ 3: Chat Cá nhân 1-1 (TV4 Engine + TV1 UI)",
        priority="Bắt buộc",
        input_text="• Nội dung tin nhắn văn bản do người dùng nhập tại TextBox chat trên giao diện.\n• PartnerId (UserId của người nhận trong cuộc trò chuyện 1-1).\n• CurrentUserId (UserId của người gửi hiện tại lấy từ AppSession).",
        process_text="1. Tạo đối tượng MessageDto: MessageId = Guid.NewGuid(), RoomId = PartnerId, SenderId = CurrentUserId, Content = text, Timestamp = DateTime.UtcNow, Status = 'Sending'.\n2. Ghi cục bộ (Local-First Append): Mở luồng ghi tệp FileMode.Append tại đường dẫn: LocalData/{CurrentUserId}/Messages/{PartnerId}/YYYY-MM.jsonl.\n3. Ghi dòng JSON vào cuối tệp kèm ký tự xuống dòng '\\n'. Lấy vị trí byte offset vừa ghi để cập nhật vào tệp chỉ mục nhị phân .idx.\n4. Đóng gói MessagePacket (PacketType 200) và đẩy vào hàng đợi gửi của NetworkClient qua Socket.\n5. Cập nhật ObservableCollection<ChatMessageModel> trên ViewModel để hiển thị bong bóng chat ngay lập tức trên UI với icon 'Đang gửi'.\n6. Khi nhận được phản hồi gói tin PacketAck từ Server: Cập nhật trạng thái tin nhắn thành 'Đã gửi' (Sent).",
        output_text="• Dòng dữ liệu JSON tin nhắn được ghi an toàn xuống ổ đĩa cục bộ của người gửi.\n• Gói tin mạng được phát đi qua hạ tầng TCP Socket.\n• Giao diện hiển thị bong bóng chat mượt mà, phản hồi tức thời không có độ trễ.",
        exception_text="• Ổ cứng đầy hoặc quyền ghi file bị từ chối: Bắt UnauthorizedAccessException/IOException, hiển thị icon dấu chấm than đỏ bên cạnh tin nhắn và lưu vào hàng đợi thử lại sau.\n• Socket mất kết nối: Tin nhắn vẫn lưu trong file .jsonl với trạng thái 'Failed', cho phép nhấn nút 'Gửi lại'.",
        note_text="• Hiệu năng: Kỹ thuật Append-only đảm bảo tốc độ ghi đĩa đạt hàng chục nghìn tin/giây, không phải ghi đè toàn bộ tệp."
    )

    # 5. MOD_05_01
    add_ipo_module_table(
        doc,
        mod_id="MOD_05_01",
        mod_name="Phân mảnh tệp tin (Chunking 64KB) & Tính toán mã băm gốc SHA-256",
        subsystem="Phân hệ 5: Truyền Tệp Socket Stream (TV5 Engine + TV1 UI)",
        priority="Bắt buộc (Kỹ thuật cao)",
        input_text="• Đường dẫn tệp tin gốc trên máy tính người gửi (ví dụ: DoAnTotNghiep_KienTruc.dwg dung lượng 250MB).\n• Định danh cuộc trò chuyện RoomId và RecipientId.",
        process_text="1. Đọc tệp và tính mã băm gốc: Mở FileStream chế độ chia sẻ FileShare.Read, sử dụng lớp băm nhị phân SHA256.Create() để tính toán mã SHA-256 Checksum của toàn bộ tệp tin.\n2. Tính toán tổng số phân mảnh (TotalChunks): totalChunks = (int)Math.Ceiling((double)fileLength / 65536) (mỗi chunk 64KB = 65,536 bytes).\n3. Gửi gói tin khởi tạo FileTransferRequest (PacketType 301): Chứa FileId = Guid.NewGuid(), FileName, FileLength, TotalChunks, Sha256Hash.\n4. Chờ nhận phản hồi FileTransferResponse chấp thuận từ bên nhận qua Server.\n5. Bắt đầu vòng lặp đọc và truyền: Dùng vòng lặp đọc từng mảng 64KB byte từ FileStream, đóng gói vào FileChunkPacket (PacketType 302) gồm [FileId, ChunkIndex, ChunkSize, DataBytes].\n6. Bơm từng chunk qua NetworkClient và cập nhật thanh tiến trình ProgressBar trên giao diện UI.",
        output_text="• Chuỗi các gói tin FileChunkPacket kích thước chuẩn 64KB được truyền đều đặn qua luồng Socket.\n• Mã băm gốc SHA-256 được lưu vết để đối soát tính toàn vẹn.",
        exception_text="• Tệp bị khóa bởi tiến trình khác (ví dụ AutoCAD đang mở tệp): Hiển thị cảnh báo 'Tệp đang được sử dụng bởi ứng dụng khác, vui lòng đóng tệp trước khi gửi'.\n• Đứt kết nối mạng giữa chừng: Tạm dừng gửi, lưu vị trí ChunkIndex cuối cùng để hỗ trợ tiếp tục truyền khi có mạng.",
        note_text="• Quản lý RAM: Mỗi lần chỉ nạp đúng 64KB vào RAM, giải phóng ngay sau khi ghi vào Socket, không bao giờ đọc toàn bộ tệp 250MB vào bộ nhớ, đảm bảo Client chạy mượt mà."
    )

    # 6. MOD_05_04
    add_ipo_module_table(
        doc,
        mod_id="MOD_05_04",
        mod_name="Tái cấu trúc tệp .part & Xác thực tính toàn vẹn SHA-256",
        subsystem="Phân hệ 5: Truyền Tệp Socket Stream (TV5)",
        priority="Bắt buộc",
        input_text="• Luồng các gói tin FileChunkPacket (PacketType 302) nhận được từ Socket.\n• Thông tin khởi tạo tệp từ FileTransferRequest (FileName, FileLength, TotalChunks, OriginalSha256).",
        process_text="1. Khởi tạo tệp tạm: Tạo đường dẫn tệp tạm tại LocalData/{UserId}/Files/{RoomId}/filename.part.\n2. Khi nhận từng FileChunkPacket:\n   a. Kiểm tra khớp FileId và ChunkIndex tuần tự.\n   b. Ghi mảng byte của chunk vào vị trí offset tương ứng trong FileStream của tệp .part: stream.Write(chunk.Data, 0, chunk.Data.Length).\n   c. Cập nhật phần trăm tiến trình nhận (ChunkIndex / TotalChunks * 100%).\n3. Khi nhận đủ 100% các chunk (ChunkIndex == TotalChunks - 1):\n   a. Đóng FileStream của tệp .part.\n   b. Mở lại tệp và tính toán mã băm SHA-256 thực tế của toàn bộ tệp vừa ghép.\n   c. So sánh: actualHash.Equals(originalSha256, StringComparison.OrdinalIgnoreCase).\n   d. Nếu khớp 100%: Đổi tên tệp (File.Move) từ filename.part thành filename chính thức và ghi vào file_manifest.jsonl.\n   e. Nếu không khớp: Xóa tệp .part, gửi thông báo FileTransferFailed và yêu cầu truyền lại.",
        output_text="• Tệp đồ án hoàn chỉnh, chính xác tuyệt đối từng bit được lưu vào thư mục cục bộ.\n• Một dòng mới được cập nhật vào tệp mục lục file_manifest.jsonl.",
        exception_text="• Ổ đĩa hết dung lượng khi đang ghi: Bắt IOException, xóa tệp .part và gửi cảnh báo dung lượng đĩa cho người dùng.\n• Mất gói tin giữa chừng: Sau 30 giây không nhận được chunk tiếp theo, kích hoạt timeout và cảnh báo lỗi truyền tải.",
        note_text="• An toàn dữ liệu: Ngăn chặn triệt để tình trạng tệp đồ án CAD/BIM bị hỏng cấu trúc do mất mát gói tin trên đường truyền mạng LAN."
    )

    # 7. MOD_05_06
    add_ipo_module_table(
        doc,
        mod_id="MOD_05_06",
        mod_name="Xử lý thao tác nhận kép: 'Mở file' (Quick View) & 'Lưu về máy' (SaveFileDialog)",
        subsystem="Phân hệ 5: Truyền Tệp Socket Stream (TV1 UI + TV5 Core)",
        priority="Bắt buộc (Đặc sắc)",
        input_text="• Người dùng nhấn một trong hai nút bấm trên bóng chat tệp nhận được: 'Mở file' hoặc 'Lưu về máy'.\n• Thông tin tệp tin từ bản ghi tin nhắn: LocalPath, FileName, FileExtension.",
        process_text="1. Xử lý Thao tác 'MỞ FILE' (Quick View / Direct Launch):\n   a. Kiểm tra sự tồn tại của tệp tại đường dẫn cục bộ File.Exists(localPath).\n   b. Nếu tệp tồn tại: Khởi tạo đối tượng ProcessStartInfo với UseShellExecute = true, FileName = localPath.\n   c. Gọi hệ điều hành Windows khởi chạy ứng dụng mặc định: Process.Start(startInfo) (ví dụ: mở tệp .dwg bằng AutoCAD, .pdf bằng Adobe Acrobat, .docx bằng MS Word).\n2. Xử lý Thao tác 'LƯU VỀ MÁY' (Save As Dialog):\n   a. Khởi tạo hộp thoại chuẩn Windows Microsoft.Win32.SaveFileDialog.\n   b. Thiết lập FileName mặc định và bộ lọc định dạng tương ứng với phần mở rộng tệp.\n   c. Hiển thị hộp thoại cho người dùng lựa chọn thư mục đích trên máy tính (Desktop, USB, Thư mục bài tập).\n   d. Khi người dùng nhấn Save: Sao chép tệp từ thư mục ứng dụng sang đường dẫn đích được chọn: File.Copy(localPath, dialog.FileName, overwrite: true).\n   e. Hiển thị thông báo Toast Notification 'Đã lưu tệp thành công'.",
        output_text="• Nút 'Mở file': Ứng dụng chuyên ngành tương ứng trên máy tính được kích hoạt và mở tệp ngay lập tức.\n• Nút 'Lưu về máy': Tệp tin được xuất ra vị trí bất kỳ trên hệ thống tệp của Windows theo ý muốn người dùng.",
        exception_text="• Máy tính chưa cài đặt phần mềm đọc định dạng tệp: Bắt Win32Exception, hiển thị hộp thoại 'Mở bằng...' (Open with...) của Windows.\n• Tệp cục bộ bị người dùng vô tình xóa mất: Hiển thị thông báo 'Tệp nguồn không còn tồn tại trên máy trạm này'.",
        note_text="• Trải nghiệm người dùng: Đem lại sự thuận tiện tối đa cho sinh viên và giảng viên, vừa xem nhanh vừa chủ động lưu trữ tệp vào đồ án cá nhân."
    )

    # 8. MOD_07_04
    add_ipo_module_table(
        doc,
        mod_id="MOD_07_04",
        mod_name="Đẩy bản sao lưu đồng bộ lên kho vùng đệm Server Vault (LAN Socket Sync Push)",
        subsystem="Phân hệ 7: Quản lý Dữ liệu Local-First & Đồng bộ LAN Vault (TV5 Engine + Server)",
        priority="Bắt buộc (Chiến lược)",
        input_text="• Yêu cầu đồng bộ kích hoạt từ người dùng (nút 'Đẩy lên LAN Server' trong màn hình Cài đặt) hoặc tự động kích hoạt khi đăng xuất tại Chế độ Lab PC Mode.\n• Dữ liệu thư mục cục bộ của tài khoản: LocalData/{UserId}/ (bao gồm toàn bộ Messages và Files).",
        process_text="1. Nén dữ liệu: Sử dụng thư viện ZipFile.CreateFromDirectory để nén toàn bộ thư mục LocalData/{UserId}/ thành một tệp nhị phân duy nhất tại bộ nhớ đệm tạm thời.\n2. Tính toán mã băm SHA-256 của tệp nén và kiểm tra kích thước.\n3. Nếu kích thước vượt quá hạn ngạch tối đa 1GB: Cảnh báo người dùng cần dọn bớt tệp trước khi đẩy.\n4. Đóng gói gói tin VaultPushRequest (PacketType 701): Chứa UserId, FileSize, Sha256Hash và luồng byte nén.\n5. Truyền luồng byte qua Socket lên Server.\n6. Phía Server tiếp nhận:\n   a. Kiểm tra dung lượng kho đệm nóng Server Vault; nếu đầy, kích hoạt giải phóng các bản ghi cũ nhất (LRU Eviction).\n   b. Lưu tệp vào đường dẫn ServerData/Vaults/{UserId}/latest_snapshot.bin.\n   c. Ghi nhận thời gian LastPushedAt và phản hồi gói tin VaultPushAck (PacketType 702) thành công.\n7. Client nhận Ack, cập nhật nhãn 'Đã đồng bộ mạng LAN lúc HH:mm:ss' trên giao diện.",
        output_text="• Bản sao lưu toàn vẹn mới nhất của sinh viên được lưu trữ an toàn trong kho đệm nóng của Server mạng LAN.\n• Người dùng hoàn toàn yên tâm có thể khôi phục lại dữ liệu này trên bất kỳ máy trạm nào khác trong trường.",
        exception_text="• Kết nối mạng LAN bị ngắt giữa quá trình đẩy: Tự động hủy tệp đang nhận dở trên Server và thông báo lỗi cho Client.\n• Hạn ngạch Server Vault bị tràn: Server tự động dọn dẹp các bản sao lưu của sinh viên đã lâu không hoạt động (> 30 ngày).",
        note_text="• Bản chất kỹ thuật: Đây là vùng đệm nóng (Hot-Cache) tạm thời, không biến Server thành kho lưu trữ vĩnh viễn, giải quyết triệt để vấn đề quá tải hạ tầng."
    )

    # 9. MOD_07_05
    add_ipo_module_table(
        doc,
        mod_id="MOD_07_05",
        mod_name="Khôi phục phiên làm việc từ Server Vault & Chế độ bảo mật Lab PC Mode",
        subsystem="Phân hệ 7: Quản lý Dữ liệu Local-First & Đồng bộ LAN Vault (TV1 UI + TV5 Core)",
        priority="Bắt buộc",
        input_text="• Sự kiện 1: Sinh viên vừa đăng nhập thành công trên một máy tính mới (chưa có thư mục LocalData cục bộ).\n• Sự kiện 2: Sinh viên nhấn nút 'Đăng xuất' tại phòng máy tính thực hành của nhà trường.",
        process_text="1. Quy trình Khôi phục khi Đăng nhập máy mới:\n   a. Ngay sau khi Login thành công, Client tự động gửi gói tin VaultStatusQuery (PacketType 705) lên Server.\n   b. Server kiểm tra tệp latest_snapshot.bin của User; nếu tồn tại, phản hồi gói tin VaultStatusReply (PacketType 706) kèm dung lượng và thời gian sao lưu.\n   c. Client hiển thị hộp thoại Modal trên giao diện: 'Hệ thống phát hiện bản sao lưu của bạn trên Server Vault mạng LAN. Bạn có muốn đồng bộ ngay không?'.\n   d. Nếu người dùng chọn 'Đồng bộ ngay': Client gửi VaultPullRequest (PacketType 703).\n   e. Server truyền luồng tệp nén (VaultPullResponse 704). Client nhận, giải nén trực tiếp vào LocalData/{UserId}/ và nạp lại toàn bộ lịch sử trò chuyện tức thì.\n2. Quy trình Thoát an toàn tại Phòng máy Lab (Lab PC Mode):\n   a. Khi nhấn Đăng xuất, hộp thoại hỏi: 'Bạn có đang sử dụng máy tính phòng thực hành công cộng không?'.\n   b. Nếu chọn 'Bật chế độ máy Lab': Client tự động thực thi đẩy bản sao lưu lên Server Vault (MOD_07_04).\n   c. Sau khi Server xác nhận lưu thành công: Client gọi Directory.Delete(localDataPath, recursive: true) để xóa trắng toàn bộ lịch sử chat và tài liệu đồ án trên máy phòng Lab.\n   d. Đóng phiên kết nối và quay trở về màn hình đăng nhập ban đầu.",
        output_text="• Khi đăng nhập máy mới: Toàn bộ dữ liệu chat và file có mặt đầy đủ chỉ sau vài giây.\n• Khi đăng xuất máy Lab: Dữ liệu cá nhân trên máy công cộng được xóa trắng 100%, bảo vệ an toàn quyền riêng tư.",
        exception_text="• Lỗi mạng khi đang kéo dữ liệu: Giữ nguyên trạng thái cũ và cho phép người dùng ấn nút 'Thử lại' trong phần Cài đặt.\n• Xóa file cục bộ bị khóa do ứng dụng khác: Client thử lại 3 lần và xóa triệt để khi thoát tiến trình.",
        note_text="• Giá trị thực tiễn: Đây là tính năng đột phá giải quyết hoàn hảo bài toán học tập xoay tua phòng máy của sinh viên Đại học Kiến trúc Đà Nẵng."
    )

    doc.add_heading("4.3. Bảng tổng hợp thông số kỹ thuật toàn bộ 63 Module chức năng", level=2)
    p_sum_intro = doc.add_paragraph(
        "Bảng 4.1: Ma trận tổng hợp thông số kỹ thuật Input - Process - Output cho toàn bộ 63 module đơn nguyên trong hệ thống DAU Chat:"
    )
    p_sum_intro.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p_sum_intro.paragraph_format.space_after = Pt(2)

    tbl_all = doc.add_table(rows=1, cols=6)
    set_table_borders(tbl_all)
    tbl_all.alignment = WD_TABLE_ALIGNMENT.CENTER
    all_headers = ["STT", "Mã Module", "Tên Module Đơn nguyên", "Dữ liệu Đầu vào (Input)", "Thuật toán / Xử lý Chính (Process)", "Kết quả Đầu ra (Output)"]
    all_widths = [Inches(0.5), Inches(1.1), Inches(1.8), Inches(1.2), Inches(1.8), Inches(1.0)]
    add_header_styled_row(tbl_all, all_headers, all_widths)

    all_mods_data = [
        # Phân hệ 1
        ("1", "MOD_01_01", "Đăng nhập hệ thống", "MSSV/Username, Password", "Băm mật khẩu, tra cứu Users SQLite, cấp Token", "UserDto, AppSession"),
        ("2", "MOD_01_02", "Đăng xuất tài khoản", "UserId, SessionToken", "Đóng Socket kết nối, xóa Session, dọn RAM", "Trở về LoginView"),
        ("3", "MOD_01_03", "Đổi mật khẩu", "OldPassword, NewPassword", "Kiểm tra mật khẩu cũ, cập nhật PasswordHash", "Thông báo thành công"),
        ("4", "MOD_01_04", "Xem thông tin cá nhân", "UserId hiện tại", "Tra cứu bảng Users, nạp Profile và Avatar", "ProfileViewModel"),
        ("5", "MOD_01_05", "Cập nhật hồ sơ cá nhân", "Phone, Email, AvatarByte", "Kiểm tra định dạng, cập nhật SQLite và thư mục", "Hồ sơ cập nhật"),
        ("6", "MOD_01_06", "Thêm tài khoản (Admin)", "Username, Password, Role", "Kiểm tra trùng lặp, thêm bản ghi vào bảng Users", "Bản ghi User mới"),
        ("7", "MOD_01_07", "Sửa tài khoản (Admin)", "UserId, UpdatedFields", "Cập nhật thông tin chi tiết người dùng", "Dữ liệu User mới"),
        ("8", "MOD_01_08", "Khóa tài khoản (Admin)", "UserId cần khóa, Lý do", "Cập nhật IsActive = 0, ngắt kết nối Socket", "User bị ngắt phiên"),
        ("9", "MOD_01_09", "Mở khóa tài khoản (A)", "UserId đang khóa", "Cập nhật IsActive = 1 trong CSDL SQLite", "Tài khoản mở khóa"),
        ("10", "MOD_01_10", "Cấp lại mật khẩu mặc định", "UserId cần reset", "Đặt lại PasswordHash = Hash('123456')", "Mật khẩu đặt lại"),
        ("11", "MOD_01_11", "Phân quyền Quản trị viên", "UserId được phân quyền", "Cập nhật RoleId = 1 trong bảng Users", "Kích hoạt Menu Admin"),
        ("12", "MOD_01_12", "Phân quyền Giảng viên", "UserId được phân quyền", "Cập nhật RoleId = 2 trong bảng Users", "Kích hoạt Quản lý lớp"),
        ("13", "MOD_01_13", "Phân quyền Sinh viên", "UserId được phân quyền", "Cập nhật RoleId = 3 trong bảng Users", "Giao diện Sinh viên"),

        # Phân hệ 2
        ("14", "MOD_02_01", "Lắng nghe kết nối TCP", "Cổng Port (ví dụ: 5000)", "TcpListener.AcceptTcpClientAsync đa luồng", "TcpClient phiên mới"),
        ("15", "MOD_02_02", "Xử lý ngắt kết nối", "Sự kiện Socket đóng", "Hủy phiên, cập nhật Offline, broadcast mạng", "Client ngắt kết nối"),
        ("16", "MOD_02_03", "Quản lý đa kết nối", "N kết nối TcpClient", "ConcurrentDictionary lưu trữ kết nối luồng an toàn", "Bảng kết nối Server"),
        ("17", "MOD_02_04", "Phát tín hiệu Heartbeat", "Chu kỳ 15 giây/lần", "Gửi gói tin Ping/Pong qua lại giữa Client/Server", "Duy trì mạch TCP"),
        ("18", "MOD_02_05", "Phát hiện mất kết nối", "Quá 3 chu kỳ Ping", "Đánh dấu trạng thái Offline, đóng socket rác", "Danh bạ đổi màu xám"),
        ("19", "MOD_02_06", "Xếp hàng đợi tin Offline", "MessagePacket, RecipientId", "Đưa vào PendingQueue FIFO trên bộ nhớ RAM", "Bảo lưu tin nhắn"),
        ("20", "MOD_02_07", "Tự xả hàng đợi khi Login", "Sự kiện User Online", "Rút gói tin từ FIFO và đẩy qua Socket Stream", "Chuyển phát toàn bộ"),
        ("21", "MOD_02_08", "Xóa tin sau PacketAck", "Mã PacketAck trả về", "Xóa hoàn toàn gói tin tương ứng khỏi RAM Server", "Giải phóng RAM O(1)"),
        ("22", "MOD_02_09", "Tự hủy tin quá hạn TTL", "Tin nhắn nằm quá 7 ngày", "Tiến trình quét nền tự hủy bản ghi quá hạn", "Tránh rò rỉ RAM"),

        # Phân hệ 3
        ("23", "MOD_03_01", "Soạn thảo tin nhắn văn bản", "Nội dung ký tự từ UI", "Kiểm tra chuỗi rỗng, ràng buộc độ dài (< 4000 ký tự)", "Văn bản sẵn sàng"),
        ("24", "MOD_03_02", "Đóng gói MessagePacket", "MessageDto, Sender, Recipient", "Đóng gói 4B Length + PacketType 200 + Payload", "NetworkPacket nhị phân"),
        ("25", "MOD_03_03", "Ghi nối tiếp tệp .jsonl", "Dòng JSON bản tin", "FileMode.Append vào Messages/{PartnerId}/YYYY-MM.jsonl", "Dữ liệu lưu an toàn"),
        ("26", "MOD_03_04", "Giải mã tin nhắn nhận", "Mảng byte từ Socket", "Chuyển đổi UTF-8 thành đối tượng MessageDto", "MessageDto nhận"),
        ("27", "MOD_03_05", "Sinh chỉ mục nhị phân .idx", "Vị trí byte offset vừa ghi", "Ghi cặp số nguyên 8B Offset + 8B Timestamp vào file .idx", "Chỉ mục tìm nhanh"),
        ("28", "MOD_03_06", "Gửi phản hồi PacketAck", "MessageId đã nhận", "Gửi gói tin Ack (PacketType 204) cho người gửi", "Xác nhận nhận tin"),
        ("29", "MOD_03_07", "Hiển thị bóng chat lên UI", "ChatMessageModel", "Dispatcher.Invoke thêm vào ChatMessages collection", "Bóng chat hiện ra"),
        ("30", "MOD_03_08", "Cập nhật trạng thái tin", "PacketAck phản hồi", "Đổi icon từ 'Đang gửi' sang 'Đã nhận'", "Icon trạng thái mới"),
        ("31", "MOD_03_09", "Tải phân trang lịch sử", "PageSize (ví dụ: 50 tin)", "Dùng file .idx đọc ngược từ cuối file .jsonl O(1)", "50 tin nhắn cũ nhất"),
        ("32", "MOD_03_10", "Tìm kiếm tin nhắn", "Từ khóa tìm kiếm", "Duyệt nhanh file .jsonl tìm dòng chứa từ khóa", "Danh sách tin khớp"),

        # Phân hệ 4
        ("33", "MOD_04_01", "Tạo phòng chat nhóm", "RoomName, ClassId, Creator", "Tạo RoomId UUID, ghi bản ghi vào bảng Rooms SQLite", "Phòng chat mới"),
        ("34", "MOD_04_02", "Sửa thông tin phòng", "RoomId, NewName", "Cập nhật trường RoomName trong CSDL SQLite", "Tên phòng mới"),
        ("35", "MOD_04_03", "Giải tán phòng chat", "RoomId", "Xóa liên kết trong RoomMembers và Rooms", "Phòng bị đóng"),
        ("36", "MOD_04_04", "Mời thành viên vào phòng", "RoomId, List<UserId>", "Thêm các bản ghi vào bảng RoomMembers", "Thành viên gia nhập"),
        ("37", "MOD_04_05", "Xóa thành viên khỏi phòng", "RoomId, TargetUserId", "Xóa bản ghi khỏi bảng liên kết RoomMembers", "Thành viên bị kích"),
        ("38", "MOD_04_06", "Rời khỏi phòng chat", "RoomId, CurrentUserId", "Tự xóa tên khỏi danh sách RoomMembers", "Rời nhóm an toàn"),
        ("39", "MOD_04_07", "Gửi tin nhắn nhóm", "Content, RoomId", "Đóng gói GroupMessagePacket (PacketType 203)", "Gói tin nhóm phát đi"),
        ("40", "MOD_04_08", "Broadcast tin nhắn nhóm", "GroupMessagePacket", "Server tra cứu danh sách RoomMembers, gửi từng socket", "Đa phát tới các máy"),
        ("41", "MOD_04_09", "Ghi nhật ký nhóm máy trạm", "GroupMessageDto", "Ghi nối tiếp vào LocalData/.../Messages/{RoomId}/", "Lưu trữ độc lập"),

        # Phân hệ 5
        ("42", "MOD_05_01", "Khay tệp chờ (Staged Tray)", "Danh sách File kéo thả", "Lấy icon, dung lượng, nạp vào khay chuẩn bị gửi", "StagedFileCollection"),
        ("43", "MOD_05_02", "Tính mã băm gốc SHA-256", "Đường dẫn tệp gốc", "Chạy giải thuật băm nhị phân SHA256", "Chuỗi Hash 64 ký tự"),
        ("44", "MOD_05_03", "Gửi FileTransferRequest", "Tên file, kích thước, hash", "Gửi gói tin 301 xin phép truyền tệp", "Yêu cầu truyền tệp"),
        ("45", "MOD_05_04", "Cắt và truyền Chunk 64KB", "FileStream đọc tuần tự", "Chia nhỏ 65,536 bytes/chunk, đóng gói và gửi", "Luồng chunk Socket"),
        ("46", "MOD_05_05", "Server chuyển tiếp RAM", "FileChunkPacket nhận", "Chuyển tiếp RAM-to-RAM ngay sang bên nhận", "Tải Server O(1)"),
        ("47", "MOD_05_06", "Ghép tệp tạm filename.part", "Chunk nhận được", "Ghi nối tiếp mảng byte vào FileStream của tệp .part", "Tệp .part lớn dần"),
        ("48", "MOD_05_07", "Kiểm tra toàn vẹn SHA-256", "Tệp .part đã đủ 100%", "Băm lại tệp ghép, so sánh với mã gốc bên gửi", "Xác thực 100% khớp"),
        ("49", "MOD_05_08", "Đổi tên & Ghi Manifest", "Tệp .part hoàn chỉnh", "File.Move sang tệp chính, ghi file_manifest.jsonl", "Tệp sẵn sàng sử dụng"),
        ("50", "MOD_05_09", "Thao tác 'Mở file'", "Nút bấm 'Mở file' từ UI", "Gọi Process.Start(filePath) mở bằng app Windows", "AutoCAD/Word mở tệp"),
        ("51", "MOD_05_10", "Thao tác 'Lưu về máy'", "Nút 'Lưu về máy' từ UI", "Mở SaveFileDialog cho người dùng chọn nơi lưu", "Sao chép ra USB/đĩa"),

        # Phân hệ 6
        ("52", "MOD_06_01", "Tạo lớp học phần mới", "ClassName, LecturerId", "Thêm bản ghi vào bảng Classes SQLite", "Lớp học mới"),
        ("53", "MOD_06_02", "Cập nhật thông tin lớp", "ClassId, NewInfo", "Cập nhật thông tin trong bảng Classes", "Thông tin lớp mới"),
        ("54", "MOD_06_03", "Đóng lớp học phần", "ClassId", "Cập nhật trạng thái kết thúc niên khóa của lớp", "Lớp được lưu trữ"),
        ("55", "MOD_06_04", "Thêm sinh viên vào lớp", "ClassId, MSSV", "Tra cứu UserId, thêm vào bảng ClassMembers", "Sinh viên vào lớp"),
        ("56", "MOD_06_05", "Nhập danh sách SV hàng loạt", "Danh sách text MSSV", "Vòng lặp thêm nhiều bản ghi ClassMembers", "Danh sách lớp nạp đủ"),
        ("57", "MOD_06_06", "Xóa sinh viên khỏi lớp", "ClassId, UserId", "Xóa bản ghi liên kết trong bảng ClassMembers", "Loại khỏi lớp"),

        # Phân hệ 7
        ("58", "MOD_07_01", "Tổ chức thư mục LocalData", "UserId khi Login", "Tạo cây thư mục Messages/ và Files/ nếu chưa có", "Cấu trúc đĩa cục bộ"),
        ("59", "MOD_07_02", "Thống kê dung lượng chiếm", "Thư mục LocalData", "Quét tính tổng số byte của Messages và Files", "Dung lượng MB hiển thị"),
        ("60", "MOD_07_03", "Dọn dẹp tệp quá hạn", "Số ngày lưu (vd: 30 ngày)", "Duyệt file_manifest.jsonl, xóa tệp cũ hơn 30 ngày", "Giải phóng dung lượng"),
        ("61", "MOD_07_04", "Sao lưu ngoại tuyến .zip", "Thư mục đích lưu USB", "ZipFile.CreateFromDirectory đóng gói LocalData", "Tệp Backup.zip"),
        ("62", "MOD_07_05", "Khôi phục từ tệp .zip", "Đường dẫn tệp .zip", "Giải nén tệp zip đè vào thư mục LocalData", "Phục hồi đầy đủ chat"),
        ("63", "MOD_07_06", "Đẩy dữ liệu lên Server Vault", "Nút 'Đẩy lên LAN Server'", "Nén và gửi VaultPushRequest (701) qua Socket", "Lưu vào Server Vault"),
        ("64", "MOD_07_07", "Server quản lý hạn ngạch 1GB", "Snapshot byte nhận", "Lưu latest_snapshot.bin, thực thi dọn dẹp LRU", "Vùng đệm cập nhật"),
        ("65", "MOD_07_08", "Kéo dữ liệu từ Server Vault", "VaultPullRequest (703)", "Server gửi stream bản sao lưu, Client giải nén", "Đồng bộ đa máy xong"),
        ("66", "MOD_07_09", "Chế độ Lab PC Mode", "Xác nhận Đăng xuất máy Lab", "Tự động Push Vault -> Ack -> Xóa sạch LocalData", "Xóa sạch bảo mật"),

        # Phân hệ 8
        ("67", "MOD_08_01", "Thống kê kết nối thời gian thực", "Server Connection Map", "Đếm số Client đang giữ Socket kết nối", "Dashboard số Online"),
        ("68", "MOD_08_02", "Giám sát phiên làm việc", "Active Sockets", "Hiển thị bảng IP, Port, Username, Thời gian kết nối", "Bảng giám sát mạng"),
        ("69", "MOD_08_03", "Kiểm soát dung lượng Vault", "Thư mục ServerData/Vaults/", "Tính tổng dung lượng đĩa đệm nóng, kích hoạt LRU", "Đảm bảo đĩa < 50GB"),
        ("70", "MOD_08_04", "Dọn dẹp Vault quá hạn", "Snapshot > 30 ngày", "Tự động xóa các file latest_snapshot.bin cũ", "Giải phóng đĩa Server"),
        ("71", "MOD_08_05", "Nhật ký kiểm toán hệ thống", "Sự kiện hệ thống phát sinh", "Ghi vào bảng SystemAuditLogs trong SQLite", "Log điều tra sự cố")
    ]

    for idx, row in enumerate(all_mods_data):
        add_data_row(tbl_all, row, is_even=(idx % 2 == 1), col_widths=all_widths)

def add_section_5(doc):
    doc.add_heading("5. LUỒNG DỮ LIỆU LIÊN QUAN ĐẾN ĐỐI TƯỢNG & CHỨC NĂNG", level=1)

    doc.add_heading("5.1. Ma trận Đối tượng – Chức năng (CRUD Matrix) & Ma trận Phân quyền RBAC", level=2)
    p_crud_intro = doc.add_paragraph(
        "Bảng 5.1: Ma trận Đối tượng – Chức năng (CRUD Matrix) thể hiện mối tương tác giữa các thực thể và chức năng trong hệ thống (C: Create, R: Read, U: Update, D: Delete):"
    )
    p_crud_intro.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p_crud_intro.paragraph_format.space_after = Pt(2)

    # CRUD Table
    tbl_crud = doc.add_table(rows=1, cols=6)
    set_table_borders(tbl_crud)
    tbl_crud.alignment = WD_TABLE_ALIGNMENT.CENTER
    crud_headers = ["Đối tượng Dữ liệu (Entity)", "Tạo mới (Create - C)", "Truy vấn (Read - R)", "Cập nhật (Update - U)", "Xóa bỏ (Delete - D)", "Ghi chú Phân vùng Lưu trữ"]
    crud_widths = [Inches(1.5), Inches(1.3), Inches(1.3), Inches(1.3), Inches(1.3), Inches(1.5)]
    add_header_styled_row(tbl_crud, crud_headers, crud_widths)

    crud_data = [
        ("ENT-01: User", "Admin tạo tài khoản (MOD_01_06)", "Đăng nhập, xem profile, danh bạ", "Đổi pass, sửa thông tin, khóa tài khoản", "Xóa tài khoản (chuyển IsActive=0)", "Server Metadata (SQLite)"),
        ("ENT-02: Role", "Hệ thống khởi tạo mặc định", "Tra cứu quyền khi thực thi tác vụ", "Admin điều chỉnh phân quyền", "Không cho phép xóa", "Server Metadata (SQLite)"),
        ("ENT-03: Class", "Admin/GV tạo lớp học (MOD_06_01)", "Hiển thị danh sách lớp", "Cập nhật tên lớp, giảng viên", "Khóa lớp hoặc lưu trữ", "Server Metadata (SQLite)"),
        ("ENT-04: ClassMember", "Thêm SV vào lớp (MOD_06_04)", "Xem danh sách sinh viên lớp", "Cập nhật trạng thái học tập", "Xóa sinh viên khỏi lớp", "Server Metadata (SQLite)"),
        ("ENT-05: Room", "Tạo phòng chat lớp/nhóm (MOD_04_01)", "Tải danh sách phòng chat", "Đổi tên phòng, đổi chủ phòng", "Giải tán phòng chat", "Server Metadata (SQLite)"),
        ("ENT-06: RoomMember", "Mời thành viên vào phòng (MOD_04_04)", "Xem thành viên trong phòng", "Phân quyền quản trị nhóm", "Kích thành viên khỏi phòng", "Server Metadata (SQLite)"),
        ("ENT-07: Message", "Gửi tin nhắn 1-1 / Nhóm (MOD_03_01)", "Đọc tin phân trang qua file .idx", "Cập nhật trạng thái Đã gửi/Đã nhận", "Xóa tin nhắn trên máy cục bộ", "Client Local (.jsonl + .idx)"),
        ("ENT-08: FileAttachment", "Kéo thả gửi tệp đồ án (MOD_05_01)", "Tải tệp, xem trước hình ảnh", "Đổi tên tệp sau khi xác thực SHA-256", "Dọn dẹp tệp tải về quá hạn", "Client Local (Files/ Store)"),
        ("ENT-09: PendingQueueItem", "Xếp hàng khi nhận Offline (MOD_02_06)", "Kiểm tra gói tin khi User Login", "Cập nhật trạng thái đang chuyển tiếp", "Xóa khỏi RAM sau khi nhận PacketAck", "Server Transient RAM Queue"),
        ("ENT-10: LocalManifestItem", "Ghi khi tải xong tệp (MOD_05_08)", "Tra cứu thư mục tệp cục bộ", "Cập nhật đường dẫn khi đổi vị trí", "Xóa bản ghi khi dọn dẹp đĩa", "Client Local (file_manifest)"),
        ("ENT-11: VaultSnapshot", "Đẩy nén lên Server Vault (MOD_07_06)", "Kiểm tra tình trạng sao lưu LAN", "Ghi đè bản snapshot mới nhất", "Tự hủy khi quá hạn hoặc LRU", "Server Hot-Cache Vault"),
        ("ENT-12: SystemAuditLog", "Tự động ghi khi có sự kiện (MOD_08_05)", "Admin tra cứu điều tra sự cố", "Không cho phép chỉnh sửa", "Tự động xoay vòng log định kỳ", "Server SQLite Logs")
    ]

    for idx, row in enumerate(crud_data):
        add_data_row(tbl_crud, row, is_even=(idx % 2 == 1), col_widths=crud_widths)

    p_rbac = doc.add_paragraph()
    p_rbac.paragraph_format.space_before = Pt(8)
    p_rbac.paragraph_format.space_after = Pt(2)
    r_rb = p_rbac.add_run("Bảng 5.2: Ma trận phân quyền theo vai trò (Role-Based Access Control - RBAC Matrix):")
    r_rb.font.name = 'Times New Roman'
    r_rb.font.bold = True
    r_rb.font.size = Pt(11)
    r_rb.font.color.rgb = RGBColor(0, 0, 0)

    tbl_rbac = doc.add_table(rows=1, cols=5)
    set_table_borders(tbl_rbac)
    tbl_rbac.alignment = WD_TABLE_ALIGNMENT.CENTER
    rbac_headers = ["Nhóm Quyền Hạn Nghiệp Vụ", "Quản trị viên (Admin)", "Giảng viên (Teacher)", "Sinh viên (Student)", "Hệ thống Nền (System)"]
    rbac_widths = [Inches(2.5), Inches(1.4), Inches(1.4), Inches(1.4), Inches(1.5)]
    add_header_styled_row(tbl_rbac, rbac_headers, rbac_widths)

    rbac_data = [
        ("Quản trị Tài khoản người dùng (Thêm/Sửa/Khóa)", "Toàn quyền [A]", "Không có quyền", "Không có quyền", "Tự động khóa vi phạm"),
        ("Quản trị Lớp học phần & Lớp sinh hoạt", "Toàn quyền [A]", "Quản lý lớp được phân công", "Xem lớp mình học", "Không"),
        ("Tạo & Quản trị Phòng chat môn học / Nhóm", "Toàn quyền [A]", "Toàn quyền lớp mình", "Tạo nhóm học tập SV", "Không"),
        ("Gửi nhận tin nhắn 1-1 & Tin nhắn nhóm", "Có quyền", "Có quyền", "Có quyền", "Định tuyến & Relay"),
        ("Truyền tệp tài liệu đồ án CAD/BIM (Chunking)", "Có quyền", "Có quyền", "Có quyền", "Relay RAM-to-RAM"),
        ("Sao lưu cục bộ (.zip) & Đồng bộ Server Vault", "Có quyền", "Có quyền", "Có quyền", "Quản lý Quota 1GB & LRU"),
        ("Chế độ bảo mật phòng máy Lab (Auto-Wipe)", "Có quyền", "Có quyền", "Có quyền", "Nhận Push & Trả Ack"),
        ("Giám sát Server, Dashboard kết nối Socket", "Toàn quyền [A]", "Không có quyền", "Không có quyền", "Cập nhật dữ liệu thời gian thực")
    ]

    for idx, row in enumerate(rbac_data):
        add_data_row(tbl_rbac, row, is_even=(idx % 2 == 1), col_widths=rbac_widths)

    doc.add_heading("5.2. Danh mục các Luồng dữ liệu chính (Data Flows)", level=2)
    p_df = doc.add_paragraph(
        "Bảng 5.3: Danh mục 14 luồng trao đổi dữ liệu kỹ thuật trong hệ thống (DF-01 đến DF-14):"
    )
    p_df.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p_df.paragraph_format.space_after = Pt(2)

    tbl_df = doc.add_table(rows=1, cols=6)
    set_table_borders(tbl_df)
    tbl_df.alignment = WD_TABLE_ALIGNMENT.CENTER
    df_headers = ["Mã Luồng", "Tên Luồng Dữ liệu", "Thành phần Bắt đầu", "Thành phần Đích", "Dữ liệu Trao đổi Chính", "Điều kiện Tiên quyết"]
    df_widths = [Inches(0.8), Inches(1.5), Inches(1.3), Inches(1.3), Inches(1.7), Inches(1.6)]
    add_header_styled_row(tbl_df, df_headers, df_widths)

    df_data = [
        ("DF-01", "Xác thực đăng nhập", "Client LoginView", "Server AuthHandler", "MSSV/Username, Hash Password", "Kết nối Socket mở"),
        ("DF-02", "Duy trì mạch Heartbeat", "Client NetworkClient", "Server ConnectionMgr", "Ping/Pong byte packet (0x05)", "Đã đăng nhập"),
        ("DF-03", "Gửi tin nhắn trực tiếp 1-1", "Client Sender", "Server Dispatcher", "MessagePacket (PacketType 200)", "Người nhận Online"),
        ("DF-04", "Relay tin nhắn tức thời", "Server Dispatcher", "Client Recipient", "MessagePacket chuyển tiếp RAM-to-RAM", "Người nhận Online"),
        ("DF-05", "Lưu trữ tin vào PendingQueue", "Server Dispatcher", "Server PendingQueue", "NetworkPacket Payload + TTL (7 ngày)", "Người nhận Offline"),
        ("DF-06", "Xả hàng đợi khi Login", "Server PendingQueue", "Client Recipient", "Tập hợp các gói tin tồn đọng", "Vừa xác thực thành công"),
        ("DF-07", "Xác nhận nhận tin (PacketAck)", "Client Recipient", "Server Dispatcher", "PacketAck (PacketType 204)", "Đã ghi file .jsonl xong"),
        ("DF-08", "Gửi tin nhắn nhóm Broadcast", "Client Sender", "Server GroupDispatcher", "GroupMessagePacket (PacketType 203)", "Là thành viên phòng"),
        ("DF-09", "Đa phát tin nhóm tới các máy", "Server GroupDispatcher", "N máy trạm thành viên", "GroupMessagePacket qua Socket riêng", "Thành viên đang Online"),
        ("DF-10", "Yêu cầu truyền tệp đồ án", "Client Sender", "Server FileHandler", "FileTransferRequest (Tên, Size, SHA256)", "Tệp đã nạp vào Staged Tray"),
        ("DF-11", "Truyền luồng phân mảnh 64KB", "Client Sender", "Client Recipient", "Chuỗi FileChunkPacket (qua Relay Server)", "Bên nhận chấp nhận"),
        ("DF-12", "Đẩy sao lưu lên Server Vault", "Client BackupAgent", "Server VaultHandler", "VaultPushRequest (701) + Tệp nén .bin", "Kích hoạt sao lưu / Lab PC"),
        ("DF-13", "Kéo sao lưu từ Server Vault", "Server VaultHandler", "Client BackupAgent", "VaultPullResponse (704) + Dữ liệu nén", "Đăng nhập máy mới"),
        ("DF-14", "Ghi nhật ký kiểm toán hệ thống", "Các phân hệ nghiệp vụ", "Server AuditLog Store", "UserId, ActionType, IP, Timestamp", "Sự kiện nhạy cảm phát sinh")
    ]

    for idx, row in enumerate(df_data):
        add_data_row(tbl_df, row, is_even=(idx % 2 == 1), col_widths=df_widths)

    doc.add_heading("5.3. Sơ đồ kiến trúc & luồng dữ liệu chuyên sâu", level=2)
    p_diag_intro = doc.add_paragraph(
        "Nhằm làm sáng tỏ tường minh cơ chế vận hành ở tầng mạng và lưu trữ, 4 sơ đồ kiến trúc và luồng dữ liệu kỹ thuật được xây dựng chi tiết dưới đây theo đúng chuẩn kỹ thuật phần mềm:"
    )
    p_diag_intro.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY

    # Sơ đồ 1: Tổng thể
    add_figure(doc, 'e:/DAU-Chat-Team5/diagrams/diagram_system_architecture.png', 
               "Hình 5.1: Sơ đồ kiến trúc tổng thể hệ thống DAU Chat (C4 Component Architecture & Network Protocol Specification)")

    p_arch_desc = doc.add_paragraph(
        "Giải thích cơ chế vận hành kiến trúc (Hình 5.1):\n"
        "• Tầng Ứng dụng Client (WPF Native .NET 10): Hoạt động theo mô hình Local-First độc lập. Mọi tin nhắn văn bản gửi hoặc nhận đều được ghi nối tiếp vào tệp Append-only log (.jsonl) và đánh chỉ mục nhị phân (.idx) trực tiếp trên ổ đĩa máy trạm. Tệp tài liệu nhận về được lưu tại thư mục Files/ và quản lý bởi file_manifest.jsonl. Nhờ đó, Client hoàn toàn tự chủ về dữ liệu mà không cần tải lại từ máy chủ.\n"
        "• Kênh Mạng TCP Socket Stream: Sử dụng kết nối TCP thuần hướng luồng (Stream-oriented). Cơ chế đóng gói nhị phân Length-prefix (4 byte Big-Endian xác định độ dài gói tin, 1 byte xác định PacketType và mảng byte Payload) giúp loại bỏ triệt để hiện tượng vỡ gói hoặc dính gói trên đường truyền mạng LAN trường học.\n"
        "• Tầng Máy chủ Server: Chia làm 3 khối chức năng độc lập:\n"
        "  1. Dispatcher & Connection Manager: Điều phối các luồng kết nối đa nhiệm (Async/Await), theo dõi nhịp tim Heartbeat.\n"
        "  2. Transient Relay & Pending Queue: Cầu nối RAM-to-RAM trực tiếp khi hai máy cùng Online; lưu trữ tạm thời trên bộ nhớ RAM (FIFO + TTL 7 ngày) khi người nhận Offline và xóa ngay khi nhận được xác nhận PacketAck.\n"
        "  3. Server Vault (Kho đệm nóng LAN): Vùng đệm sao lưu có kiểm soát hạn ngạch 1GB/user và cơ chế dọn dẹp LRU, hỗ trợ học tập xoay tua phòng máy."
    )
    p_arch_desc.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY

    # Sơ đồ 2: Luồng tin nhắn
    add_figure(doc, 'e:/DAU-Chat-Team5/diagrams/diagram_message_flow.png', 
               "Hình 5.2: Biểu đồ tuần tự định tuyến tin nhắn và vòng đời hàng đợi tạm thời (UML Sequence Diagram)")

    p_msg_desc = doc.add_paragraph(
        "Giải thích luồng tin nhắn (Hình 5.2):\n"
        "1. Phía Bên gửi: Khi người dùng nhấn gửi, động cơ Messaging tạo mã định danh UUID v4 và nhãn thời gian UTC. Tin nhắn lập tức được ghi nối tiếp vào tệp .jsonl cục bộ của người gửi (đảm bảo không mất dữ liệu ngay cả khi mạng sập ngay tức khắc) và đóng gói vào MessagePacket gửi qua Socket.\n"
        "2. Phía Máy chủ: Server đọc 4 byte Header để bóc tách gói tin. Tra cứu bảng ActiveConnectionMap:\n"
        "   - Nhánh Online (Direct Stream): Nếu người nhận đang kết nối, Server bơm gói tin RAM-to-RAM thẳng sang Socket của người nhận. Độ trễ đạt < 50ms.\n"
        "   - Nhánh Offline (Pending Queue): Nếu người nhận ngắt kết nối, gói tin được đưa vào hàng đợi FIFO trên RAM kèm thời gian sống TTL 7 ngày. Khi người dùng đăng nhập lại, hệ thống tự động xả toàn bộ tin nhắn tồn đọng.\n"
        "3. Phía Bên nhận: Nhận gói tin từ Socket, ghi nối tiếp vào file .jsonl của mình, cập nhật file chỉ mục .idx, kích hoạt sự kiện UI hiển thị bóng chat và gửi gói tin xác nhận PacketAck (204) ngược lại để Server xóa tin khỏi RAM."
    )
    p_msg_desc.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY

    # Sơ đồ 3: Luồng truyền file
    add_figure(doc, 'e:/DAU-Chat-Team5/diagrams/diagram_file_streaming_flow.png', 
               "Hình 5.3: Quy trình truyền tệp phân mảnh (Chunking 64KB), xác thực SHA-256 và cơ chế phản hồi kép")

    p_file_desc = doc.add_paragraph(
        "Giải thích luồng truyền tệp phân mảnh và cơ chế phản hồi kép (Hình 5.3):\n"
        "1. Giai đoạn Chuẩn bị: Người dùng kéo thả các tệp đồ án nặng vào khay soạn thảo Staged File Tray. Hệ thống tính toán mã băm gốc SHA-256 và gửi gói tin FileTransferRequest (301) xin phép truyền tệp.\n"
        "2. Giai đoạn Phân mảnh & Bơm luồng: Tệp được đọc tuần tự từng khối 64KB (65,536 bytes) và đóng gói thành FileChunkPacket (302). Server đóng vai trò cầu nối RAM-to-RAM, chuyển tiếp tức thì từng chunk sang bên nhận mà không ghi đĩa Server.\n"
        "3. Giai đoạn Tái cấu trúc & Xác thực Toàn vẹn: Bên nhận ghi nối tiếp các chunk vào tệp tạm thời filename.part. Khi nhận đủ 100% các chunk, bên nhận tính toán lại mã SHA-256 của toàn bộ tệp ghép; nếu trùng khớp tuyệt đối mới đổi tên thành tệp chính thức và ghi vào file_manifest.jsonl.\n"
        "4. Cơ chế Phản hồi Kép (Dual Receiver Actions):\n"
        "   - Thao tác 1 - 'MỞ FILE' (Quick View): Gọi Process.Start để kích hoạt ngay ứng dụng chuyên ngành mặc định trên Windows (AutoCAD mở .dwg, Adobe Photoshop mở .psd, Word mở .docx).\n"
        "   - Thao tác 2 - 'LƯU VỀ MÁY' (Save As): Mở SaveFileDialog cho phép người dùng tự do lưu tệp ra Desktop, USB hoặc ổ đĩa cá nhân ngoài phân vùng của ứng dụng."
    )
    p_file_desc.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY

    # Sơ đồ 4: Luồng LAN Vault
    add_figure(doc, 'e:/DAU-Chat-Team5/diagrams/diagram_lan_vault_sync_flow.png', 
               "Hình 5.4: Sơ đồ cơ chế đồng bộ mạng LAN Socket Sync & Chế độ phòng máy thực hành (Lab PC Mode)")

    p_vault_desc = doc.add_paragraph(
        "Giải thích cơ chế đồng bộ đa máy và bảo mật phòng thực hành (Hình 5.4):\n"
        "• Tình huống 1 - Rời phòng máy Lab (Lab PC Mode - Auto Push & Wipe): Khi sinh viên kết thúc buổi thực hành và nhấn Đăng xuất trên máy tính trường, hộp thoại cảnh báo bảo mật hỏi sinh viên có muốn kích hoạt chế độ phòng máy công cộng. Khi xác nhận, Client tự động nén toàn bộ thư mục LocalData (Messages và Files) thành tệp sao lưu nén, gửi gói tin VaultPushRequest (701) qua Socket lên Server Vault. Sau khi Server lưu trữ thành công và trả về VaultPushAck (702), Client lập tức xóa sạch thư mục LocalData trên máy phòng máy, triệt tiêu hoàn toàn nguy cơ bị lộ bài tập và lịch sử chat cá nhân.\n"
        "• Tình huống 2 - Đăng nhập máy mới (Auto Detect & Restore): Khi sinh viên về phòng trọ hoặc chuyển sang phòng máy khác và đăng nhập vào DAU Chat, Client tự động truy vấn trạng thái sao lưu qua VaultStatusQuery (705). Server phát hiện có bản sao lưu trên Server Vault và thông báo cho sinh viên. Khi sinh viên đồng ý, tệp sao lưu được tải về qua mạng LAN với tốc độ cao (VaultPullRequest 703 / 704) và giải nén trực tiếp vào thư mục LocalData. Toàn bộ lịch sử trò chuyện và tệp đính kèm được phục hồi nguyên vẹn như đang ngồi tại máy cũ."
    )
    p_vault_desc.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY

print("Section 4 and 5 builder functions updated.")
