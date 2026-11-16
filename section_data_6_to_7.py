# Section 6 and 7 Data and Builder Functions for DAU Chat Report (Academic Tech Dev Edition)

import docx
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx_helpers import set_table_borders, add_header_styled_row, add_data_row, add_tech_box, add_figure, add_body_p, add_bullet_p

def add_table_dictionary(doc, tbl_name, tbl_desc, columns_data):
    p_h = doc.add_paragraph()
    p_h.paragraph_format.space_before = Pt(8)
    p_h.paragraph_format.space_after = Pt(2)
    p_h.paragraph_format.keep_with_next = True
    r_n = p_h.add_run(f"• Bảng CSDL: [{tbl_name}] – {tbl_desc}")
    r_n.font.name = 'Times New Roman'
    r_n.font.size = Pt(11.5)
    r_n.font.bold = True
    r_n.font.color.rgb = RGBColor(0, 0, 0)

    tbl = doc.add_table(rows=1, cols=6)
    set_table_borders(tbl)
    tbl.alignment = WD_TABLE_ALIGNMENT.CENTER
    widths = [Inches(1.5), Inches(1.1), Inches(0.7), Inches(0.9), Inches(1.7), Inches(1.3)]
    add_header_styled_row(tbl, ["Tên Cột", "Kiểu Dữ liệu", "Null", "Default", "Mô tả Thuộc tính", "Ràng buộc / Khóa"], widths)

    for idx, row in enumerate(columns_data):
        add_data_row(tbl, row, is_even=(idx % 2 == 1), col_widths=widths)

    p_after = doc.add_paragraph()
    p_after.paragraph_format.space_before = Pt(0)
    p_after.paragraph_format.space_after = Pt(4)

def add_section_6(doc):
    doc.add_heading("6. THIẾT KẾ CƠ SỞ DỮ LIỆU & KIẾN TRÚC LƯU TRỮ LAI", level=1)

    doc.add_heading("6.1. Danh mục Bảng CSDL Server (SQLite) & Cấu trúc lưu trữ Client Local-First", level=2)
    add_body_p(doc, "Khác biệt hoàn toàn với các đồ án mạng truyền thống (thường sử dụng SQL Server để lưu toàn bộ dữ liệu tin nhắn và file dẫn đến quá tải hệ thống), DAU Chat hiện thực hóa Kiến trúc Lưu trữ Lai (Hybrid Storage Architecture) phân tách 3 vùng lưu trữ độc lập:")
    add_bullet_p(doc, "Lưu trữ trong tệp dauchat-metadata.db sử dụng thư viện siêu nhẹ Microsoft.Data.Sqlite. Kho này chỉ quản lý danh mục tài khoản, vai trò, lớp học và phòng chat; tuyệt đối không lưu nội dung chat và file đính kèm.", bold_prefix="1. Kho Metadata Máy chủ (Server SQLite Store):")
    add_bullet_p(doc, "Mỗi máy trạm quản lý độc lập toàn bộ lịch sử trò chuyện và tài liệu tải về trong thư mục LocalData/{UserId}/, đảm bảo tính bảo mật, tốc độ đọc ghi tức thì và không làm hao tổn tài nguyên máy chủ.", bold_prefix="2. Kho Lưu trữ Cục bộ Máy trạm (Client Local-First Store):")
    add_bullet_p(doc, "Lưu trữ các bản snapshot nén của người dùng tại ServerData/Vaults/{UserId}/latest_snapshot.bin với hạn ngạch nghiêm ngặt 1GB/user và cơ chế dọn dẹp LRU.", bold_prefix="3. Kho Vùng đệm Nóng Mạng LAN (Server Vault Hot-Cache):")

    # Table List
    p_tlist = doc.add_paragraph("Bảng 6.1: Danh mục các bảng CSDL Metadata Server và Cấu trúc tệp Client Local:")
    p_tlist.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p_tlist.paragraph_format.space_after = Pt(2)

    tbl_list = doc.add_table(rows=1, cols=5)
    set_table_borders(tbl_list)
    tbl_list.alignment = WD_TABLE_ALIGNMENT.CENTER
    list_headers = ["Tên Bảng / Cấu trúc", "Phân vùng Lưu trữ", "Khóa Chính (PK)", "Khóa Ngoại (FK)", "Mục đích & Ghi chú"]
    list_widths = [Inches(1.8), Inches(1.5), Inches(1.1), Inches(1.2), Inches(1.6)]
    add_header_styled_row(tbl_list, list_headers, list_widths)

    tables_summary = [
        ("Roles", "Server SQLite", "RoleId", "Không", "Danh mục vai trò: Admin, Teacher, Student"),
        ("Users", "Server SQLite", "UserId", "RoleId", "Danh mục tài khoản sinh viên và giảng viên DAU"),
        ("Classes", "Server SQLite", "ClassId", "LecturerId", "Danh mục lớp học phần và lớp sinh hoạt Khoa CNTT"),
        ("ClassMembers", "Server SQLite", "ClassId + UserId", "ClassId, UserId", "Bảng liên kết quan hệ N - M giữa Sinh viên và Lớp học"),
        ("Rooms", "Server SQLite", "RoomId", "ClassId, CreatedBy", "Danh mục phòng chat môn học, nhóm đồ án"),
        ("RoomMembers", "Server SQLite", "RoomId + UserId", "RoomId, UserId", "Bảng liên kết N - M giữa Người dùng và Phòng chat"),
        ("SystemAuditLogs", "Server SQLite", "LogId", "UserId", "Nhật ký kiểm toán sự kiện hệ thống và cảnh báo mạng"),
        ("Messages Log (.jsonl)", "Client Local-First", "MessageId (UUID)", "RoomId, SenderId", "Lịch sử tin nhắn văn bản ghi nối tiếp theo tháng"),
        ("Binary Index (.idx)", "Client Local-First", "Vị trí trong mảng", "Byte Offset", "Chỉ mục nhị phân định vị byte offset đọc nhanh O(1)"),
        ("File Manifest (.jsonl)", "Client Local-First", "FileId (UUID)", "RoomId", "Mục lục quản lý tệp tải về và mã băm SHA-256"),
        ("Server Vault Snapshot", "Server Hot-Cache", "UserId", "UserId", "Bản nén snapshot phiên làm việc, hạn ngạch 1GB")
    ]

    for idx, row in enumerate(tables_summary):
        add_data_row(tbl_list, row, is_even=(idx % 2 == 1), col_widths=list_widths)

    doc.add_heading("6.2. Chi tiết thuộc tính từng bảng (Data Dictionary)", level=2)
    p_dict_intro = doc.add_paragraph(
        "Đặc tả từ điển dữ liệu chi tiết cho toàn bộ các bảng trong CSDL Metadata Server (SQLite) và các tệp cấu trúc cục bộ của Client:"
    )
    p_dict_intro.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY

    # Bảng 1: Roles
    roles_cols = [
        ("RoleId", "INTEGER", "No", "None", "Mã định danh vai trò duy nhất", "PRIMARY KEY AUTOINCREMENT"),
        ("RoleName", "TEXT", "No", "None", "Tên vai trò (Admin, Teacher, Student)", "UNIQUE, NOT NULL"),
        ("Description", "TEXT", "Yes", "NULL", "Mô tả chi tiết quyền hạn của vai trò", "None"),
        ("PermissionsJson", "TEXT", "Yes", "'{}'", "Chuỗi JSON quy định quyền chi tiết", "None")
    ]
    add_table_dictionary(doc, "Roles", "Bảng danh mục vai trò người dùng (Server SQLite)", roles_cols)

    # Bảng 2: Users
    users_cols = [
        ("UserId", "TEXT", "No", "None", "Mã định danh người dùng (UUID chuỗi)", "PRIMARY KEY"),
        ("Username", "TEXT", "No", "None", "Tên đăng nhập (MSSV 10 chữ số hoặc mã GV)", "UNIQUE, NOT NULL, INDEX"),
        ("PasswordHash", "TEXT", "No", "None", "Chuỗi băm mật khẩu an toàn (PBKDF2/SHA-256)", "NOT NULL"),
        ("FullName", "TEXT", "No", "None", "Họ và tên đầy đủ của sinh viên/giảng viên", "NOT NULL"),
        ("Email", "TEXT", "Yes", "NULL", "Địa chỉ email trường cấp (@dau.edu.vn)", "None"),
        ("PhoneNumber", "TEXT", "Yes", "NULL", "Số điện thoại liên lạc", "None"),
        ("AvatarPath", "TEXT", "Yes", "NULL", "Đường dẫn tệp ảnh đại diện", "None"),
        ("RoleId", "INTEGER", "No", "3", "Mã vai trò của người dùng", "FOREIGN KEY -> Roles(RoleId)"),
        ("IsActive", "INTEGER", "No", "1", "Trạng thái hoạt động (1: Active, 0: Khóa)", "CHECK (IsActive IN (0,1))"),
        ("CreatedAt", "TEXT", "No", "CURRENT_TIMESTAMP", "Thời điểm tạo tài khoản (UTC)", "NOT NULL")
    ]
    add_table_dictionary(doc, "Users", "Bảng tài khoản người dùng và xác thực (Server SQLite)", users_cols)

    # Bảng 3: Classes
    classes_cols = [
        ("ClassId", "TEXT", "No", "None", "Mã định danh lớp học (ví dụ: 23CT1, K23_LTM)", "PRIMARY KEY"),
        ("ClassName", "TEXT", "No", "None", "Tên đầy đủ của lớp sinh hoạt / học phần", "NOT NULL"),
        ("AcademicYear", "TEXT", "No", "'2023-2027'", "Niên khóa đào tạo", "NOT NULL"),
        ("Faculty", "TEXT", "No", "'Khoa CNTT'", "Khoa quản lý chuyên môn", "NOT NULL"),
        ("LecturerId", "TEXT", "Yes", "NULL", "Mã định danh Giảng viên phụ trách/chủ nhiệm", "FOREIGN KEY -> Users(UserId)"),
        ("CreatedAt", "TEXT", "No", "CURRENT_TIMESTAMP", "Thời điểm tạo lớp", "NOT NULL")
    ]
    add_table_dictionary(doc, "Classes", "Bảng danh mục lớp sinh hoạt và lớp học phần (Server SQLite)", classes_cols)

    # Bảng 4: ClassMembers
    class_members_cols = [
        ("ClassId", "TEXT", "No", "None", "Mã lớp học", "PRIMARY KEY, FK -> Classes(ClassId)"),
        ("UserId", "TEXT", "No", "None", "Mã sinh viên tham gia lớp", "PRIMARY KEY, FK -> Users(UserId)"),
        ("JoinedAt", "TEXT", "No", "CURRENT_TIMESTAMP", "Thời điểm tham gia lớp học", "NOT NULL"),
        ("Status", "TEXT", "No", "'Active'", "Trạng thái học tập (Active, Suspended)", "None")
    ]
    add_table_dictionary(doc, "ClassMembers", "Bảng liên kết sinh viên thuộc lớp học (Server SQLite)", class_members_cols)

    # Bảng 5: Rooms
    rooms_cols = [
        ("RoomId", "TEXT", "No", "None", "Mã định danh phòng chat (UUID)", "PRIMARY KEY"),
        ("RoomName", "TEXT", "No", "None", "Tên phòng chat hoặc tiêu đề nhóm đồ án", "NOT NULL"),
        ("RoomType", "TEXT", "No", "'Group'", "Loại phòng (Direct: 1-1, Group: Nhóm)", "CHECK (RoomType IN ('Direct','Group'))"),
        ("ClassId", "TEXT", "Yes", "NULL", "Lớp học liên kết (nếu là phòng chat lớp)", "FOREIGN KEY -> Classes(ClassId)"),
        ("CreatedBy", "TEXT", "No", "None", "Mã người dùng tạo phòng chat", "FOREIGN KEY -> Users(UserId)"),
        ("CreatedAt", "TEXT", "No", "CURRENT_TIMESTAMP", "Thời điểm khởi tạo phòng", "NOT NULL")
    ]
    add_table_dictionary(doc, "Rooms", "Bảng danh mục phòng chat và nhóm học tập (Server SQLite)", rooms_cols)

    # Bảng 6: RoomMembers
    room_members_cols = [
        ("RoomId", "TEXT", "No", "None", "Mã phòng chat", "PRIMARY KEY, FK -> Rooms(RoomId)"),
        ("UserId", "TEXT", "No", "None", "Mã thành viên tham gia phòng", "PRIMARY KEY, FK -> Users(UserId)"),
        ("RoleInRoom", "TEXT", "No", "'Member'", "Vai trò trong phòng (Owner, Admin, Member)", "None"),
        ("JoinedAt", "TEXT", "No", "CURRENT_TIMESTAMP", "Thời điểm tham gia phòng", "NOT NULL")
    ]
    add_table_dictionary(doc, "RoomMembers", "Bảng liên kết thành viên trong phòng chat (Server SQLite)", room_members_cols)

    # Bảng 7: SystemAuditLogs
    audit_cols = [
        ("LogId", "INTEGER", "No", "None", "Mã nhật ký tự tăng", "PRIMARY KEY AUTOINCREMENT"),
        ("UserId", "TEXT", "Yes", "NULL", "Mã người dùng phát sinh sự kiện", "FOREIGN KEY -> Users(UserId)"),
        ("ActionType", "TEXT", "No", "None", "Loại hành động (LOGIN, LOGOUT, VAULT_PUSH...)", "NOT NULL"),
        ("IpAddress", "TEXT", "Yes", "NULL", "Địa chỉ IP máy trạm thực hiện", "None"),
        ("Details", "TEXT", "Yes", "NULL", "Thông tin chi tiết gói tin hoặc thông báo lỗi", "None"),
        ("Timestamp", "TEXT", "No", "CURRENT_TIMESTAMP", "Thời điểm phát sinh sự kiện", "NOT NULL")
    ]
    add_table_dictionary(doc, "SystemAuditLogs", "Bảng nhật ký kiểm toán hệ thống máy chủ (Server SQLite)", audit_cols)

    # Cấu trúc tệp Client Local
    p_client_store = doc.add_paragraph()
    p_client_store.paragraph_format.space_before = Pt(8)
    p_client_store.paragraph_format.space_after = Pt(2)
    r_cs = p_client_store.add_run("• Cấu trúc Lưu trữ Cục bộ Máy trạm (Client Local-First Files):")
    r_cs.font.name = 'Times New Roman'
    r_cs.font.size = Pt(11.5)
    r_cs.font.bold = True
    r_cs.font.color.rgb = RGBColor(0, 0, 0)

    add_tech_box(doc, "CẤU TRÚC ĐỊNH DẠNG TỆP LOCAL-FIRST TRÊN Ổ ĐĨA MÁY TRẠM CLIENT",
                 "1. Tệp Tin nhắn Append-only (Messages/{RoomId}/YYYY-MM.jsonl):\n"
                 "   Mỗi dòng là một chuỗi JSON độc lập đại diện cho 1 bản tin nhắn:\n"
                 "   {\"id\":\"uuid-v4\",\"room_id\":\"room-uuid\",\"sender_id\":\"mssv\",\"timestamp\":\"2026-11-16T08:30:00Z\",\"content\":\"Nội dung tin nhắn\",\"type\":\"text\",\"attachments\":[],\"status\":\"Sent\"}\n\n"
                 "2. Tệp Chỉ mục Nhị phân Truy xuất Nhanh (Messages/{RoomId}/YYYY-MM.idx):\n"
                 "   Định dạng nhị phân thuần túy, mỗi bản ghi cố định đúng 16 bytes:\n"
                 "   [Byte 0-7: Int64 Byte-Offset vị trí dòng trong file .jsonl] + [Byte 8-15: Int64 Unix-Timestamp miligiây].\n"
                 "   -> Cho phép thuật toán dịch con trỏ FileStream.Seek đọc ngược từ cuối file O(1) mà không phải nạp toàn bộ file vào RAM.\n\n"
                 "3. Tệp Mục lục Quản lý Tệp Đính kèm (file_manifest.jsonl):\n"
                 "   {\"file_id\":\"uuid\",\"file_name\":\"DoAn.dwg\",\"file_size\":104857600,\"sha256\":\"a1b2c3...\",\"saved_path\":\"LocalData/.../Files/DoAn.dwg\",\"downloaded_at\":\"2026-11-16T08:32:00Z\",\"room_id\":\"r1\"}\n\n"
                 "4. Tệp Cấu hình Cục bộ (LocalSettings.json):\n"
                 "   Lưu trữ địa chỉ IP Server LAN đã kết nối lần cuối, tài khoản ghi nhớ, cấu hình tự động đẩy Server Vault khi đăng xuất.")

    doc.add_heading("6.3. Sơ đồ quan hệ thực thể (ERD) & Quan hệ lưu trữ Hybrid", level=2)
    p_erd_desc = doc.add_paragraph(
        "Sơ đồ quan hệ thực thể CSDL (Hình 6.1) biểu diễn mối liên kết chặt chẽ giữa các bảng trong CSDL Metadata Server SQLite, đồng thời minh họa mối liên kết phi tập trung hướng tới kho lưu trữ Local-First của Client:"
    )
    p_erd_desc.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY

    # Sơ đồ ERD
    add_figure(doc, 'e:/DAU-Chat-Team5/diagrams/diagram_erd.png', 
               "Hình 6.1: Sơ đồ quan hệ thực thể Cơ sở dữ liệu (ERD) & Liên kết lưu trữ Hybrid Local-First")

    add_body_p(doc, "Các mối quan hệ thực thể cốt lõi trong sơ đồ quan hệ CSDL (Hình 6.1):")
    add_bullet_p(doc, "Một vai trò (Admin, Teacher, Student) có thể gán cho nhiều tài khoản người dùng; mỗi tài khoản chỉ thuộc duy nhất một vai trò chính.", bold_prefix="• Quan hệ 1 – N (Roles – Users):")
    add_bullet_p(doc, "Một sinh viên có thể tham gia nhiều lớp học phần khác nhau; mỗi lớp học phần tập hợp danh sách nhiều sinh viên.", bold_prefix="• Quan hệ N – M (Users – Classes qua ClassMembers):")
    add_bullet_p(doc, "Một Giảng viên có thể phụ trách giảng dạy và làm chủ nhiệm của nhiều lớp học phần.", bold_prefix="• Quan hệ 1 – N (Users – Classes qua LecturerId):")
    add_bullet_p(doc, "Một lớp học có thể sở hữu nhiều phòng chat (Phòng thảo luận chung của lớp, các phòng làm đồ án nhóm của từng đề tài).", bold_prefix="• Quan hệ 1 – N (Classes – Rooms):")
    add_bullet_p(doc, "Một người dùng có thể tham gia vào nhiều phòng chat; mỗi phòng chat chứa nhiều thành viên với vai trò Owner hoặc Member.", bold_prefix="• Quan hệ N – M (Users – Rooms qua RoomMembers):")
    add_bullet_p(doc, "Một tài khoản phát sinh nhiều bản ghi nhật ký sự kiện kiểm toán hệ thống phục vụ công tác giám sát an ninh.", bold_prefix="• Quan hệ 1 – N (Users – SystemAuditLogs):")
    add_bullet_p(doc, "Bảng Users và Rooms liên kết với các tệp tin nhắn .jsonl và kho tệp Files/ của Client thông qua các trường khóa ngoại mềm RoomId và UserId mà không cần thực hiện truy vấn SQL JOIN qua mạng, giúp hệ thống đạt hiệu năng vô hạn.", bold_prefix="• Liên kết Lai (Hybrid Linkage):")

def add_section_7(doc):
    doc.add_heading("7. CHIA & QUẢN LÝ CÔNG VIỆC THEO NHÓM (TEAM 5)", level=1)

    doc.add_heading("7.1. Phân công vai trò trong nhóm (5 Thành viên) & Quy tắc Kỹ thuật", level=2)
    p_role_intro = doc.add_paragraph(
        "Bảng 7.1: Cơ cấu phân công nhiệm vụ và ranh giới kỹ thuật theo tài liệu kiến trúc GEMINI.md:"
    )
    p_role_intro.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p_role_intro.paragraph_format.space_after = Pt(2)

    # Role Table
    tbl_role = doc.add_table(rows=1, cols=4)
    set_table_borders(tbl_role)
    tbl_role.alignment = WD_TABLE_ALIGNMENT.CENTER
    role_headers = ["STT", "Họ và Tên Thành viên", "Vai trò & Ranh giới Kỹ thuật", "Trách nhiệm Cụ thể & Nhánh Git Sở hữu"]
    role_widths = [Inches(0.6), Inches(1.8), Inches(2.2), Inches(2.6)]
    add_header_styled_row(tbl_role, role_headers, role_widths)

    role_data = [
        ("1", "Bùi Trần Nhật Hưng (Trưởng nhóm - TV1)", "Kiến trúc sư Trưởng (Lead) & Phụ trách Giao diện Client UI\n(Nhánh: feature/tv1-client-ui)", "Điều phối tổng thể, thiết kế UI/UX WPF MVVM (MainShell, Login, ChatView, ModalsOverlay, Khay Staged File Tray, Lab PC Mode). Độc quyền chỉnh sửa DauChat.Client.UI. Tổng hợp báo cáo kỹ thuật."),
        ("2", "Phan Trung Nghĩa (Thành viên 2 - TV2)", "Kỹ sư Mạng TCP & Hạ tầng Máy chủ Server\n(Nhánh: feature/tv2-network-server)", "Phụ trách tầng mạng TCP Socket, cơ chế đóng gói Length-prefix 4 byte Big-Endian, PacketDispatcher, Transient Relay RAM-to-RAM, hàng đợi PendingQueue (FIFO + TTL). Độc quyền code DauChat.Core/Networking và DauChat.Server."),
        ("3", "Nguyễn Thành Long (Thành viên 3 - TV3)", "Kỹ sư Xác thực, CSDL Metadata & Quản lý Lớp/Phòng\n(Nhánh: feature/tv3-auth-room-class)", "Phụ trách CSDL SQLite (dauchat-metadata.db), logic xác thực tài khoản MSSV/GV, phân quyền RBAC, quản lý danh mục Lớp học và Phòng chat. Code trong DauChat.Core/AuthAndRooms và Metadata Server."),
        ("4", "Dương Thị Yến Nhi (Thành viên 4 - TV4)", "Kỹ sư Động cơ Tin nhắn & Đánh chỉ mục Local-First\n(Nhánh: feature/tv4-messaging-history)", "Phụ trách động cơ tin nhắn cục bộ, cơ chế ghi nối tiếp Append-only Log (.jsonl), xây dựng cấu trúc chỉ mục nhị phân (.idx), thuật toán tải phân trang O(1) và tìm kiếm lịch sử chat. Code trong DauChat.Core/Messaging."),
        ("5", "Hồng Quốc Tuấn (Thành viên 5 - TV5)", "Kỹ sư Truyền Tệp Phân mảnh & Kho Lưu trữ Đa máy LAN\n(Nhánh: feature/tv5-file-storage-backup)", "Phụ trách truyền file theo luồng phân mảnh 64KB/chunk, xác thực mã băm SHA-256, quản lý kho tệp Local và file_manifest.jsonl, nén sao lưu .zip, hiện thực hóa LAN Socket Sync và Server Vault. Code trong DauChat.Core/FileAndStorage.")
    ]

    for idx, row in enumerate(role_data):
        add_data_row(tbl_role, row, is_even=(idx % 2 == 1), col_widths=role_widths)

    add_tech_box(doc, "QUY TẮC PHỐI HỢP & RANH GIỚI KỸ THUẬT THEO GEMINI.MD",
                 "1. Nguyên tắc Read-Only đối với DauChat.Shared: Dự án chung DauChat.Shared là READ-ONLY đối với TV2, TV3, TV4, TV5. Tuyệt đối không tự ý đổi tên Interface, không sửa NetworkPacket hay PacketType nếu chưa có sự thống nhất và phê duyệt từ Trưởng nhóm (TV1).\n"
                 "2. Độc quyền thư mục: Mỗi thành viên chỉ được commit mã nguồn vào đúng thư mục phụ trách trên nhánh Git của mình. Nghiêm cấm sửa chéo mã nguồn của thành viên khác.\n"
                 "3. Kiểm thử qua DauChat.SandboxTest: TV2 đến TV5 không được tự ý tạo cửa sổ XAML riêng để test; mọi thử nghiệm thuật toán và kịch bản mạng đều được viết dưới dạng Unit/Integration Test trong project DauChat.SandboxTest.")

    doc.add_heading("7.2. Cách thức chia công việc theo 7 giai đoạn phát triển", level=2)
    p_stage_intro = doc.add_paragraph(
        "Bảng 7.2: Kế hoạch triển khai dự án DAU Chat chia làm 7 giai đoạn nối tiếp theo mô hình Agile/Scrum rút gọn:"
    )
    p_stage_intro.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p_stage_intro.paragraph_format.space_after = Pt(2)

    # Stage Table
    tbl_stage = doc.add_table(rows=1, cols=5)
    set_table_borders(tbl_stage)
    tbl_stage.alignment = WD_TABLE_ALIGNMENT.CENTER
    stage_headers = ["Giai đoạn", "Công việc Chính & Sản phẩm Đầu ra", "Người Phụ trách Chính", "Người Hỗ trợ", "Thời gian Ước tính"]
    stage_widths = [Inches(1.2), Inches(2.6), Inches(1.3), Inches(1.1), Inches(1.0)]
    add_header_styled_row(tbl_stage, stage_headers, stage_widths)

    stage_data = [
        ("GĐ 1: Khảo sát & Đặt tên đề tài", "Khảo sát thực tế nhu cầu sinh viên kiến trúc tại DAU, phân tích 5 phần mềm tương đương, xác lập cấu trúc tên đề tài 3 thành phần.", "TV1 (Hưng)", "Toàn nhóm Team 5", "Tuần 1"),
        ("GĐ 2: Đặc tả Yêu cầu Hệ thống", "Xác định 25 yêu cầu chức năng (FR-01..25), 16 yêu cầu phi chức năng (NFR), phạm vi và ràng buộc kỹ thuật của đề tài.", "TV1 (Hưng) + TV2 (Nghĩa)", "TV3, TV4, TV5", "Tuần 2"),
        ("GĐ 3: Phân tích Thực thể & Phân hệ", "Định nghĩa 12 đối tượng nghiệp vụ (ENT-01..12), quy hoạch 8 phân hệ chức năng (MOD-01..08), xác lập kiến trúc Hybrid Local-First.", "TV3 (Long) + TV1 (Hưng)", "TV2, TV4, TV5", "Tuần 3"),
        ("GĐ 4: Phân rã Chức năng & IPO", "Xây dựng cây phân rã 63 modules, viết đặc tả IPO chi tiết cho 9 module trọng điểm và bảng tổng hợp 63 module.", "Toàn nhóm Team 5", "TV1 (Tổng hợp)", "Tuần 4"),
        ("GĐ 5: Thiết kế Luồng Dữ liệu & Mạng", "Xây dựng Ma trận CRUD, RBAC, danh mục 14 luồng dữ liệu (DF-01..14), thiết kế giao thức Length-prefix và luồng Socket.", "TV2 (Nghĩa) + TV5 (Tuấn)", "TV3, TV4", "Tuần 5"),
        ("GĐ 6: Thiết kế CSDL & Lưu trữ Lai", "Thiết kế CSDL SQLite 7 bảng, cấu trúc tệp Client Local (.jsonl + .idx), kho Server Vault và vẽ sơ đồ quan hệ ERD.", "TV3 (Long) + TV4 (Nhi)", "TV1, TV5", "Tuần 6"),
        ("GĐ 7: Hiện thực Hóa & Kiểm thử Demo", "Hoàn thiện mã nguồn .NET 10 LTS, đồng bộ mạng LAN Socket Sync, kiểm thử tự động 100% pass trên DauChat.SandboxTest.", "Toàn nhóm Team 5", "TV1 (Demo Host)", "Tuần 7 - 8")
    ]

    for idx, row in enumerate(stage_data):
        add_data_row(tbl_stage, row, is_even=(idx % 2 == 1), col_widths=stage_widths)

    doc.add_heading("7.3. Nhật ký theo dõi công việc (Task Tracking Log)", level=2)
    p_log_intro = doc.add_paragraph(
        "Bảng 7.3: Nhật ký theo dõi tiến độ công việc thực tế đối soát 100% với commit và mã nguồn hiện hành trong kho chứa Git của dự án DAU Chat:"
    )
    p_log_intro.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p_log_intro.paragraph_format.space_after = Pt(2)

    # Task Tracking Table
    tbl_log = doc.add_table(rows=1, cols=6)
    set_table_borders(tbl_log)
    tbl_log.alignment = WD_TABLE_ALIGNMENT.CENTER
    log_headers = ["Ngày", "Thành viên", "Hạng mục Công việc Thực tế", "Kết quả / Mã nguồn Đối soát", "% Hoàn thành", "Ghi chú"]
    log_widths = [Inches(0.9), Inches(1.2), Inches(2.2), Inches(1.5), Inches(0.7), Inches(0.7)]
    add_header_styled_row(tbl_log, log_headers, log_widths)

    log_data = [
        ("01/11/2026", "TV1 (Hưng)", "Thiết lập cấu trúc Solution và dự án chung DauChat.Shared", "DauChat.sln, PacketType.cs", "100%", "Kịp hạn"),
        ("03/11/2026", "TV2 (Nghĩa)", "Xây dựng khung mạng TcpListener và cơ chế Length-prefix", "TcpNetworkService.cs", "100%", "Kịp hạn"),
        ("05/11/2026", "TV3 (Long)", "Thiết kế CSDL SQLite Metadata và dịch vụ Auth/Room", "dauchat-metadata.db, SqliteRepo", "100%", "Kịp hạn"),
        ("07/11/2026", "TV4 (Nhi)", "Hiện thực hóa ghi file .jsonl Append-only và chỉ mục .idx", "JsonlMessageStorage.cs", "100%", "Kịp hạn"),
        ("09/11/2026", "TV5 (Tuấn)", "Hiện thực hóa cắt/ghép chunk 64KB và kiểm tra SHA-256", "ChunkFileTransferService.cs", "100%", "Kịp hạn"),
        ("11/11/2026", "TV1 (Hưng)", "Thiết kế giao diện WPF MVVM, ChatView, Staged File Tray", "MainShellView.xaml, ChatView", "100%", "Kịp hạn"),
        ("13/11/2026", "TV2 (Nghĩa)", "Xây dựng hàng đợi PendingQueue FIFO + TTL trên RAM", "PendingQueueManager.cs", "100%", "Kịp hạn"),
        ("14/11/2026", "TV5 (Tuấn)", "Hiện thực cơ chế sao lưu nén .zip và quản lý manifest", "LocalStorageBackupService.cs", "100%", "Kịp hạn"),
        ("15/11/2026", "TV1 + TV5", "Nâng cấp LAN Socket Sync, Server Vault Hot-Cache & Lab PC", "ServerVaultHandler, StorageSync", "100%", "Vượt tiến độ"),
        ("16/11/2026", "Team 5", "Kiểm thử tự động DauChat.SandboxTest (100% Pass) & Báo cáo", "SandboxTest.cs, BaoCao.docx", "100%", "Hoàn tất")
    ]

    for idx, row in enumerate(log_data):
        add_data_row(tbl_log, row, is_even=(idx % 2 == 1), col_widths=log_widths)

    p_conc_h = doc.add_paragraph()
    p_conc_h.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p_conc_h.paragraph_format.space_before = Pt(14)
    p_conc_h.paragraph_format.space_after = Pt(2)
    p_conc_h.paragraph_format.keep_with_next = True
    r_cch = p_conc_h.add_run("KẾT LUẬN ĐÁNH GIÁ MỐC GIỮA KỲ & HƯỚNG PHÁT TRIỂN GIAI ĐOẠN 2")
    r_cch.font.name = 'Times New Roman'
    r_cch.font.size = Pt(13)
    r_cch.font.bold = True
    r_cch.font.color.rgb = RGBColor(0, 0, 0)

    add_body_p(doc, "Báo cáo phân tích hệ thống thông tin đề tài \"DAU Chat\" ghi nhận tiến độ thực tế của nhóm Team 5 tại mốc đánh giá giữa kỳ học phần Lập trình mạng (IT311):")

    add_bullet_p(doc, "Nhóm đã hoàn thành toàn diện công tác phân tích đặc tả yêu cầu (25 FR, 16 NFR), mô hình hóa 12 đối tượng nghiệp vụ, phân rã 8 phân hệ thành 63 module đơn nguyên và thiết kế chi tiết các luồng dữ liệu (C4 Component, UML Sequence, Data Flow, ERD 7 bảng SQLite kết hợp liên kết lai Local-First).", bold_prefix="1. Về công tác Phân tích & Thiết kế:")

    add_bullet_p(doc, "Đã xây dựng thành công bộ khung kiến trúc cốt lõi bằng C# .NET 10.0 LTS: Giao thức mạng TCP Socket thuần với cơ chế đóng gói Length-prefix 4 byte Big-Endian, động cơ tin nhắn cục bộ Append-only (.jsonl) kèm chỉ mục nhị phân (.idx), dịch vụ truyền luồng tệp phân mảnh 64KB xác thực SHA-256, và cơ chế đồng bộ Server Vault Hot-Cache kèm chế độ Lab PC Mode. Các thuật toán nền tảng đã vượt qua các ca kiểm thử tự động trong project DauChat.SandboxTest.", bold_prefix="2. Về hiện thực hóa Mã nguồn Core:")

    add_bullet_p(doc, "Do điều kiện kiểm thử giữa kỳ chủ yếu thực hiện qua môi trường máy cá nhân và kịch bản Sandbox Test giả lập, nhóm cần tiếp tục tổ chức thử nghiệm chịu tải thực tế trên mạng LAN phòng máy với 2 - 3 máy trạm đồng thời để đánh giá chính xác độ trễ, thông lượng truyền tệp CAD/BIM nặng và xử lý triệt để hiện tượng nghẽn mạch khi ngắt kết nối đột ngột.", bold_prefix="3. Các hạn chế và thách thức kỹ thuật còn tồn đọng:")

    add_bullet_p(doc, "Trong nửa cuối học kỳ, nhóm Team 5 tập trung hoàn thiện toàn diện giao diện người dùng WPF (Client UI), tích hợp đầy đủ các chức năng quản trị lớp học/phòng chat môn học, tối ưu hóa cơ chế tự kết nối lại (Auto Reconnect) và chuẩn bị kịch bản thực nghiệm trực tiếp (Live Demo) phục vụ buổi báo cáo nghiệm thu cuối kỳ trước Giảng viên hướng dẫn TS. Đặng Hùng Vĩ.", bold_prefix="4. Kế hoạch triển khai giai đoạn 2 (Cuối kỳ):")

    # PHỤ LỤC. BẢNG ĐỐI CHIẾU DANH MỤC TỆP NGUỒN C# .NET 10 THỰC TẾ
    doc.add_page_break()
    p_app = doc.add_heading("PHỤ LỤC. BẢNG ĐỐI CHIẾU DANH MỤC TỆP NGUỒN C# .NET 10 THỰC TẾ", level=1)
    p_app_desc = doc.add_paragraph(
        "Bảng Phụ lục: Danh mục tệp nguồn C# .NET 10 và vai trò kỹ thuật đối soát trực tiếp với Solution DauChat.sln trong kho chứa Git:"
    )
    p_app_desc.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p_app_desc.paragraph_format.space_after = Pt(2)

    tbl_src = doc.add_table(rows=1, cols=4)
    set_table_borders(tbl_src)
    tbl_src.alignment = WD_TABLE_ALIGNMENT.CENTER
    src_headers = ["Phân hệ / Nhánh Git", "Đường dẫn Tệp nguồn trong Repository", "Lớp / Interface Chính", "Nhiệm vụ Kỹ thuật Cốt lõi"]
    src_widths = [Inches(1.5), Inches(2.3), Inches(1.8), Inches(1.6)]
    add_header_styled_row(tbl_src, src_headers, src_widths)

    src_data = [
        ("Shared (Dùng chung)", "DauChat.Shared/Networking/NetworkPacket.cs", "NetworkPacket", "Định nghĩa cấu trúc gói tin mạng nhị phân chuẩn 4-byte Length-prefix"),
        ("Shared (Dùng chung)", "DauChat.Shared/Enums/PacketType.cs", "PacketType (Enum)", "Danh mục mã gói tin từ 101 (Login) đến 706 (VaultStatusReply)"),
        ("Shared (Dùng chung)", "DauChat.Shared/Models/StorageSyncDtos.cs", "VaultPushRequest, VaultPull...", "DTOs phục vụ đồng bộ dữ liệu đa máy và Server Vault LAN"),
        ("TV1: Client UI", "DauChat.Client.UI/Views/MainShellView.xaml", "MainShellView, MainShellViewModel", "Giao diện chính WPF MVVM, quản lý danh bạ, chuyển tab và thanh trạng thái"),
        ("TV1: Client UI", "DauChat.Client.UI/Views/ChatWorkspaceView.xaml", "ChatWorkspaceView, ChatViewModel", "Không gian trò chuyện, bong bóng tin nhắn, khay tệp chờ Staged Tray"),
        ("TV1: Client UI", "DauChat.Client.UI/Views/ModalsOverlay.xaml", "ModalsOverlay, ModalDialogService", "Cửa sổ Modal đồng bộ LAN Vault và xác nhận Lab PC Mode khi đăng xuất"),
        ("TV1: Client UI", "DauChat.Client.UI/Views/StorageBackupView.xaml", "StorageBackupView, StorageViewModel", "Màn hình quản lý dung lượng, sao lưu .zip và đẩy/kéo LAN Server Vault"),
        ("TV2: Network & Server", "DauChat.Server/Program.cs", "ServerProgram, TcpListener", "Điểm khởi động máy chủ TCP Socket, quản lý đa luồng Async/Await"),
        ("TV2: Network & Server", "DauChat.Core/Networking/TcpNetworkService.cs", "TcpNetworkService, INetworkService", "Bộ điều hợp mạng Client: đóng gói 4B Big-Endian, phát Heartbeat"),
        ("TV2: Network & Server", "DauChat.Server/Vault/ServerVaultHandler.cs", "ServerVaultHandler", "Xử lý kho đệm nóng LAN: lưu snapshot, hạn ngạch 1GB và dọn dẹp LRU"),
        ("TV3: Auth & Metadata", "DauChat.Core/AuthAndRooms/SqliteStore.cs", "SqliteMetadataStore, IAuthRoom", "Quản trị CSDL SQLite: Users, Roles, Classes, Rooms, AuditLogs"),
        ("TV3: Auth & Metadata", "DauChat.Server/dauchat-metadata.db", "SQLite Database File", "Tệp CSDL cục bộ phía Server lưu danh mục người dùng và phân quyền"),
        ("TV4: Messaging", "DauChat.Core/Messaging/JsonlMessageStorage.cs", "JsonlMessageStorage, IMessaging", "Động cơ Local-First ghi nối tiếp Append-only tệp YYYY-MM.jsonl"),
        ("TV4: Messaging", "DauChat.Core/Messaging/BinaryIndexManager.cs", "BinaryIndexManager", "Quản lý file chỉ mục nhị phân .idx (offset 8B + time 8B) đọc nhanh O(1)"),
        ("TV5: File & Storage", "DauChat.Core/FileAndStorage/ChunkTransfer.cs", "ChunkFileTransferService, IFile", "Cắt/ghép chunk 64KB, kiểm tra SHA-256, ghi file_manifest.jsonl"),
        ("TV5: File & Storage", "DauChat.Core/FileAndStorage/BackupService.cs", "LocalStorageBackupService", "Nén/giải nén .zip, dọn dẹp dung lượng đĩa cũ quá hạn"),
        ("Kiểm thử Tự động", "DauChat.SandboxTest/Program.cs", "SandboxTestRunner", "Bộ kiểm thử tự động toàn diện cho TV2, TV3, TV4, TV5 (100% Pass)")
    ]

    for idx, row in enumerate(src_data):
        add_data_row(tbl_src, row, is_even=(idx % 2 == 1), col_widths=src_widths)

print("Section 6 and 7 builder functions updated.")
