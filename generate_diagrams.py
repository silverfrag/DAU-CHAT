import matplotlib.pyplot as plt
import matplotlib.patches as patches
import os

os.makedirs('e:/DAU-Chat-Team5/diagrams', exist_ok=True)

plt.rcParams['font.sans-serif'] = 'Arial'
plt.rcParams['font.family'] = 'sans-serif'

# 1. System Architecture Diagram
def create_architecture_diagram():
    fig, ax = plt.subplots(figsize=(12, 7.5), dpi=300)
    ax.set_xlim(0, 100)
    ax.set_ylim(0, 100)
    ax.axis('off')

    # Background canvas
    fig.patch.set_facecolor('#FFFFFF')

    # Title
    ax.text(50, 96, 'SƠ ĐỒ KIẾN TRÚC TỔNG THỂ HỆ THỐNG DAU CHAT (HYBRID LOCAL-FIRST)', 
            fontsize=14, fontweight='bold', ha='center', va='center', color='#0F2D59')

    # CLIENT CONTAINER (Left)
    rect_client = patches.FancyBboxPatch((4, 8), 38, 82, boxstyle="round,pad=1,rounding_size=2",
                                         facecolor='#F0F4F8', edgecolor='#1D4ED8', linewidth=2)
    ax.add_patch(rect_client)
    ax.text(23, 86, 'MÁY TRẠM CLIENT (WPF .NET 10)', fontsize=12, fontweight='bold', ha='center', color='#1D4ED8')

    # Client Subcomponents
    boxes_client = [
        (6, 73, 34, 9, 'Giao diện Người dùng (WPF MVVM)', 'MainShell, ChatView, Staged File Tray, Modals\nAuto LAN Sync Prompt & Lab PC Mode', '#DBEAFE', '#1E40AF'),
        (6, 59, 34, 10, 'Động cơ Tin nhắn & Đánh chỉ mục', 'Local-First Append-only (.jsonl) + Binary Index (.idx)\nTruy xuất nhanh O(1), Khôi phục trạng thái offline', '#E0E7FF', '#3730A3'),
        (6, 44, 34, 11, 'Bộ Truyền tệp & Kiểm tra Tính toàn vẹn', 'Streaming 64KB/chunk, Tái tạo tệp .part\nKiểm tra mã băm SHA-256, Ghi file_manifest.jsonl\nDual Actions: "Mở file" vs "Lưu về máy"', '#FEF3C7', '#92400E'),
        (6, 29, 34, 11, 'Mô-đun Đồng bộ & Sao lưu Mạng LAN', 'Nén dữ liệu .zip (Messages + Files)\nSocket LAN Sync Agent (PacketType 701-706)\nTự động sao lưu và xóa trắng trên máy Lab PC', '#DCFCE7', '#166534'),
        (6, 14, 34, 11, 'Bộ điều hợp Mạng TCP (Network Client)', 'TcpClient, Big-Endian 4-byte Length-prefix\nQuản lý Buffer nhận/gửi, Heartbeat định kỳ', '#FCE7F3', '#9D174D')
    ]
    for x, y, w, h, t1, t2, bg, fg in boxes_client:
        r = patches.FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0.5,rounding_size=1", facecolor=bg, edgecolor=fg, linewidth=1.2)
        ax.add_patch(r)
        ax.text(x + w/2, y + h - 2.8, t1, fontsize=9.5, fontweight='bold', ha='center', va='center', color=fg)
        ax.text(x + w/2, y + (h - 3)/2, t2, fontsize=7.8, ha='center', va='center', color='#334155')

    # SERVER CONTAINER (Right)
    rect_server = patches.FancyBboxPatch((58, 8), 38, 82, boxstyle="round,pad=1,rounding_size=2",
                                         facecolor='#F8FAFC', edgecolor='#0F2D59', linewidth=2)
    ax.add_patch(rect_server)
    ax.text(77, 86, 'MÁY CHỦ DAU CHAT SERVER (.NET 10)', fontsize=12, fontweight='bold', ha='center', color='#0F2D59')

    # Server Subcomponents
    boxes_server = [
        (60, 71, 34, 11, 'Lắng nghe & Điều phối Kết nối (Dispatcher)', 'TcpListener Socket, Xử lý đa luồng Async/Await\nKiểm tra Header 4-byte, Giải mã PacketType\nQuản lý bảng định tuyến Active Connection Map', '#DBEAFE', '#1E40AF'),
        (60, 56, 34, 11, 'Bộ Trung chuyển Tạm thời (Transient Relay)', 'RAM-to-RAM Stream khi người nhận Online\nHàng đợi PendingQueue (FIFO + TTL) khi Offline\nXóa RAM ngay sau PacketAck hoặc hết TTL', '#FEE2E2', '#991B1B'),
        (60, 39, 34, 13, 'Kho Metadata CSDL SQLite (Server)', 'dauchat-metadata.db (Microsoft.Data.Sqlite)\nBảng: Roles, Users, Classes, ClassMembers,\nRooms, RoomMembers, SystemAuditLogs\n(KHÔNG lưu tin nhắn & KHÔNG lưu tệp nặng)', '#E0E7FF', '#3730A3'),
        (60, 20, 34, 15, 'Kho Đồng bộ Đa máy LAN (Server Vault)', 'Lưu Snapshot đệm nóng: ServerData/Vaults/{UserId}/\nHạn ngạch đĩa: 1GB/user, Tự động dọn dẹp LRU\nTiếp nhận Push (701) và cấp phát Pull (703)\nPhục vụ sinh viên xoay tua giữa các phòng máy', '#DCFCE7', '#166534')
    ]
    for x, y, w, h, t1, t2, bg, fg in boxes_server:
        r = patches.FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0.5,rounding_size=1", facecolor=bg, edgecolor=fg, linewidth=1.2)
        ax.add_patch(r)
        ax.text(x + w/2, y + h - 2.8, t1, fontsize=9.5, fontweight='bold', ha='center', va='center', color=fg)
        ax.text(x + w/2, y + (h - 3)/2, t2, fontsize=7.8, ha='center', va='center', color='#334155')

    # NETWORK PROTOCOL CHANNEL (Center)
    rect_channel = patches.FancyBboxPatch((43.5, 25), 13, 50, boxstyle="round,pad=0.5,rounding_size=1.5",
                                          facecolor='#FEF3C7', edgecolor='#D97706', linewidth=1.5)
    ax.add_patch(rect_channel)
    ax.text(50, 71, 'KÊNH MẠNG TCP', fontsize=9.5, fontweight='bold', ha='center', color='#B45309')
    ax.text(50, 66, 'Mạng LAN Trường', fontsize=8, ha='center', color='#78350F')

    channel_notes = [
        'Raw TCP Socket',
        'Cổng: 5000 / 8888',
        '4-byte Length',
        'Big-Endian',
        'PacketType (1B)',
        'Payload (N Bytes)',
        'Zero-Overhead'
    ]
    for i, cn in enumerate(channel_notes):
        ax.text(50, 59 - i * 4.8, cn, fontsize=7.5, ha='center', color='#92400E', fontweight='bold' if i in [0, 2] else 'normal')

    # Connecting Arrows
    ax.annotate('', xy=(43.5, 60), xytext=(40, 60), arrowprops=dict(arrowstyle="->", color="#1D4ED8", lw=2))
    ax.annotate('', xy=(40, 52), xytext=(43.5, 52), arrowprops=dict(arrowstyle="->", color="#0F2D59", lw=2))
    ax.annotate('', xy=(58, 60), xytext=(56.5, 60), arrowprops=dict(arrowstyle="->", color="#1D4ED8", lw=2))
    ax.annotate('', xy=(56.5, 52), xytext=(58, 52), arrowprops=dict(arrowstyle="->", color="#0F2D59", lw=2))

    plt.tight_layout()
    plt.savefig('e:/DAU-Chat-Team5/diagrams/diagram_system_architecture.png', dpi=300)
    plt.close()
    print('1. Generated architecture diagram')

# 2. Message Flow Diagram
def create_message_flow_diagram():
    fig, ax = plt.subplots(figsize=(11, 7), dpi=300)
    ax.set_xlim(0, 100)
    ax.set_ylim(0, 100)
    ax.axis('off')
    fig.patch.set_facecolor('#FFFFFF')

    ax.text(50, 96, 'SƠ ĐỒ ĐỊNH TUYẾN TIN NHẮN & HÀNG ĐỢI TRUNG CHUYỂN TẠM THỜI', 
            fontsize=13, fontweight='bold', ha='center', va='center', color='#0F2D59')

    # Step boxes
    steps = [
        (6, 68, 24, 18, '1. CLIENT GỬI\n(SENDER)', '• Tạo UUID & Timestamp\n• Đóng gói Length-prefix\n• Ghi nối tiếp Append-only\n  vào Local .jsonl & .idx\n• Gửi PacketType 200/203', '#DBEAFE', '#1E40AF'),
        (38, 68, 24, 18, '2. SERVER DISPATCHER\n(TRUNG CHUYỂN)', '• Nhận 4-byte độ dài\n• Đọc Payload & giải mã\n• Tra cứu Active Map:\n  Người nhận Online/Offline?', '#FEF3C7', '#B45309'),
        (70, 74, 24, 16, '3A. NHÁNH ONLINE\n(DIRECT STREAM)', '• Người nhận đang kết nối\n• Server đẩy gói tin RAM-to-RAM\n• Recipient nhận tức thì\n• Gửi PacketAck (204)', '#DCFCE7', '#166534'),
        (70, 48, 24, 18, '3B. NHÁNH OFFLINE\n(PENDING QUEUE)', '• Người nhận đang Offline\n• Đưa vào RAM FIFO Queue\n• Gắn thời gian sống (TTL=7 ngày)\n• Khi User Login -> Tự xả\n• Nhận Ack -> Xóa khỏi RAM', '#FEE2E2', '#991B1B'),
        (38, 18, 56, 20, '4. CLIENT NHẬN (RECIPIENT) - CẬP NHẬT DỮ LIỆU CỤC BỘ', 
         '• Nhận gói tin từ Socket -> Giải mã JSON MessageDto\n• Ghi nối tiếp vào file LocalData/{UserId}/Messages/{RoomId}/YYYY-MM.jsonl\n• Cập nhật file chỉ mục nhị phân .idx (Byte Offset + Timestamp) để đọc tức thì O(1)\n• Kích hoạt sự kiện UI (CollectionChanged) hiển thị bóng chat lên màn hình', '#EDE9FE', '#5B21B6')
    ]

    for x, y, w, h, t1, t2, bg, fg in steps:
        r = patches.FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0.5,rounding_size=1", facecolor=bg, edgecolor=fg, linewidth=1.3)
        ax.add_patch(r)
        ax.text(x + w/2, y + h - 3.5, t1, fontsize=9.5, fontweight='bold', ha='center', va='center', color=fg)
        ax.text(x + w/2, y + (h - 4)/2, t2, fontsize=8, ha='center', va='center', color='#1E293B')

    # Arrows
    ax.annotate('', xy=(38, 77), xytext=(30, 77), arrowprops=dict(arrowstyle="->", color="#1E40AF", lw=2))
    ax.text(34, 80, 'Gửi TCP', fontsize=8, ha='center', color='#1E40AF', fontweight='bold')

    ax.annotate('', xy=(70, 82), xytext=(62, 79), arrowprops=dict(arrowstyle="->", color="#166534", lw=2))
    ax.text(66, 83, 'Online', fontsize=8.5, ha='center', color='#166534', fontweight='bold')

    ax.annotate('', xy=(70, 58), xytext=(62, 73), arrowprops=dict(arrowstyle="->", color="#991B1B", lw=2))
    ax.text(66, 68, 'Offline', fontsize=8.5, ha='center', color='#991B1B', fontweight='bold')

    ax.annotate('', xy=(75, 38), xytext=(82, 48), arrowprops=dict(arrowstyle="->", color="#5B21B6", lw=1.8, linestyle='--'))
    ax.text(82, 42, 'Xả hàng đợi khi Login', fontsize=8, ha='center', color='#5B21B6')

    ax.annotate('', xy=(70, 38), xytext=(78, 74), arrowprops=dict(arrowstyle="->", color="#5B21B6", lw=1.8))
    ax.text(77, 60, 'Đẩy gói trực tiếp', fontsize=8, ha='center', color='#5B21B6')

    plt.tight_layout()
    plt.savefig('e:/DAU-Chat-Team5/diagrams/diagram_message_flow.png', dpi=300)
    plt.close()
    print('2. Generated message flow diagram')

# 3. File Streaming Flow Diagram
def create_file_flow_diagram():
    fig, ax = plt.subplots(figsize=(11, 7.5), dpi=300)
    ax.set_xlim(0, 100)
    ax.set_ylim(0, 100)
    ax.axis('off')
    fig.patch.set_facecolor('#FFFFFF')

    ax.text(50, 96, 'SƠ ĐỒ TRUYỀN TỆP PHÂN MẢNH (CHUNKING 64KB), SHA-256 & DUAL RECEIVER ACTIONS', 
            fontsize=12, fontweight='bold', ha='center', va='center', color='#0F2D59')

    boxes = [
        (4, 52, 27, 38, 'A. BÊN GỬI (SENDER)', 
         '1. Kéo thả tệp vào Staged File Tray\n2. Tính toán mã băm gốc SHA-256\n3. Gửi FileTransferRequest (301)\n4. Chia tệp thành các Chunk 64KB\n5. Đóng gói FileChunkPacket (302)\n   [FileId, ChunkIdx, Total, Data]\n6. Bơm liên tục qua Socket Stream\n7. Cập nhật thanh tiến trình gửi UI', 
         '#EFF6FF', '#1D4ED8'),
        
        (36, 52, 27, 38, 'B. MÁY CHỦ (SERVER)', 
         '1. Nhận gói 301 (Request)\n2. Kiểm tra tính sẵn sàng của Bên nhận\n3. Phản hồi FileTransferResponse (303)\n4. Đóng vai trò Cầu nối RAM-to-RAM:\n   • KHÔNG ghi chunk vào đĩa cứng\n   • KHÔNG đọc toàn bộ tệp vào RAM\n   • Chuyển tiếp ngay Chunk sang bên nhận\n5. Tải Server luôn ở mức O(1)', 
         '#FEF3C7', '#B45309'),
        
        (69, 52, 27, 38, 'C. BÊN NHẬN (RECEIVER)', 
         '1. Nhận yêu cầu và chấp nhận truyền\n2. Tạo tệp tạm thời filename.part\n3. Ghi nối tiếp các Chunk 64KB vào đĩa\n4. Khi nhận đủ 100% các Chunk:\n   • Tính mã băm SHA-256 của tệp ghép\n   • So sánh với SHA-256 từ bên gửi\n   • Trùng khớp: Đổi đuôi .part -> gốc\n   • Lưu vào: LocalData/.../Files/\n5. Ghi nhật ký vào file_manifest.jsonl', 
         '#DCFCE7', '#166534'),
        
        (15, 10, 70, 32, 'D. CƠ CHẾ TƯƠNG TÁC KÉP CỦA BÊN NHẬN (DUAL RECEIVER ACTIONS)', 
         'Hệ thống cung cấp 2 nút bấm thao tác trực quan ngay tại bong bóng tin nhắn file:\n\n'
         '• HÀNH ĐỘNG 1: "MỞ FILE" (Quick View / Launch):\n'
         '   Gọi trực tiếp Process.Start qua hệ điều hành Windows để mở tệp ngay từ thư mục lưu trữ cục bộ\n'
         '   bằng ứng dụng mặc định (AutoCAD cho .dwg, Word cho .docx, Adobe Photoshop cho .psd).\n\n'
         '• HÀNH ĐỘNG 2: "LƯU VỀ MÁY" (Save As Dialog):\n'
         '   Mở cửa sổ chuẩn SaveFileDialog cho phép người dùng chủ động lựa chọn thư mục đích ngoài ổ đĩa\n'
         '   (Desktop, USB, Thư mục môn học) mà không bị giới hạn trong phân vùng sandbox của ứng dụng.',
         '#F1F5F9', '#334155')
    ]

    for x, y, w, h, t1, t2, bg, fg in boxes:
        r = patches.FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0.5,rounding_size=1", facecolor=bg, edgecolor=fg, linewidth=1.3)
        ax.add_patch(r)
        ax.text(x + w/2, y + h - 3, t1, fontsize=9.2, fontweight='bold', ha='center', va='center', color=fg)
        ax.text(x + 1.5, y + (h - 4)/2, t2, fontsize=7.8, ha='left', va='center', color='#1E293B')

    # Horizontal arrows between A -> B -> C
    ax.annotate('', xy=(36, 75), xytext=(31, 75), arrowprops=dict(arrowstyle="->", color="#1D4ED8", lw=2))
    ax.text(33.5, 78, 'Chunk 64KB', fontsize=7.5, ha='center', color='#1D4ED8', fontweight='bold')

    ax.annotate('', xy=(69, 75), xytext=(63, 75), arrowprops=dict(arrowstyle="->", color="#B45309", lw=2))
    ax.text(66, 78, 'Chuyển tiếp', fontsize=7.5, ha='center', color='#B45309', fontweight='bold')

    ax.annotate('', xy=(50, 42), xytext=(50, 52), arrowprops=dict(arrowstyle="->", color="#166534", lw=2))
    ax.text(58, 47, 'Tệp sẵn sàng trên đĩa', fontsize=8, ha='left', color='#166534', fontweight='bold')

    plt.tight_layout()
    plt.savefig('e:/DAU-Chat-Team5/diagrams/diagram_file_streaming_flow.png', dpi=300)
    plt.close()
    print('3. Generated file flow diagram')

# 4. LAN Vault Sync Flow Diagram
def create_lan_vault_flow_diagram():
    fig, ax = plt.subplots(figsize=(11, 7.5), dpi=300)
    ax.set_xlim(0, 100)
    ax.set_ylim(0, 100)
    ax.axis('off')
    fig.patch.set_facecolor('#FFFFFF')

    ax.text(50, 96, 'SƠ ĐỒ ĐỒNG BỘ ĐA MÁY LAN SOCKET SYNC & KHO VÙNG ĐỆM SERVER VAULT', 
            fontsize=12, fontweight='bold', ha='center', va='center', color='#0F2D59')

    boxes = [
        (4, 52, 28, 38, 'TÌNH HUỐNG 1: RỜI PHÒNG MÁY LAB\n(LAB PC MODE - AUTO PUSH & WIPE)', 
         '1. Sinh viên nhấn "Đăng xuất" tại máy Lab\n2. Hộp thoại hỏi: "Bật chế độ máy công cộng?"\n3. Khi xác nhận:\n   • Nén LocalData (Messages + Files) -> .zip\n   • Đóng gói VaultPushRequest (701)\n   • Gửi qua TCP Socket lên Server Vault\n4. Chờ nhận gói tin VaultPushAck (702)\n5. Xóa sạch thư mục LocalData trên máy trạm\n=> Bảo mật 100% dữ liệu riêng tư.', 
         '#FEF2F2', '#991B1B'),

        (36, 46, 28, 44, 'SERVER VAULT (HOT-CACHE VÙNG ĐỆM)\nQUẢN LÝ DUNG LƯỢNG & HẠN NGẠCH 1GB', 
         '• Đường dẫn: ServerData/Vaults/{UserId}/\n• Tệp lưu: latest_snapshot.bin\n• Kiểm soát Hạn ngạch (Quota):\n  Tối đa 1GB đệm nóng cho mỗi User\n• Thuật toán dọn dẹp LRU / Oldest-First:\n  Tự động giải phóng bản sao lưu cũ nhất khi\n  tổng dung lượng Server vượt ngưỡng an toàn.\n• Phục vụ Push (701) và Pull (703) độc quyền\n  cho phiên làm việc nội bộ trường.', 
         '#FEF3C7', '#B45309'),

        (68, 52, 28, 38, 'TÌNH HUỐNG 2: ĐĂNG NHẬP MÁY MỚI\n(LAPTOP CÁ NHÂN HOẶC LAB MỚI)', 
         '1. Sinh viên đăng nhập vào máy mới\n2. Client tự động gửi VaultStatusQuery (705)\n3. Server kiểm tra và báo có bản sao lưu (706)\n4. Hiển thị thông báo trên giao diện:\n   "Phát hiện bản sao lưu trên Server Vault,\n    bạn có muốn khôi phục ngay không?"\n5. Khi đồng ý: Gửi VaultPullRequest (703)\n6. Nhận dữ liệu (704) -> Giải nén vào LocalData\n=> Toàn bộ tin nhắn & file có ngay lập tức!', 
         '#ECFDF5', '#065F46'),

        (12, 10, 76, 28, 'GIẢI PHÁP ĐỒNG BỘ ĐA MÁY TOÀN DIỆN CHO MÔI TRƯỜNG ĐH KIẾN TRÚC ĐÀ NẴNG', 
         '• Giải quyết triệt để nhược điểm của mô hình Local-First khi sinh viên phải đổi máy giữa các phòng thực hành.\n'
         '• Không biến Server thành kho đám mây vĩnh viễn gây quá tải đĩa cứng (chỉ đóng vai trò Hot-Cache tạm thời).\n'
         '• Cơ chế kết hợp linh hoạt 2 trong 1: Sao lưu thủ công ra tệp ngoại tuyến .zip (USB) + Sao lưu tự động qua LAN Socket Sync.\n'
         '• Tối ưu tuyệt đối cho đồ án Lập trình mạng: Chứng minh khả năng vận hành giao thức Socket trên mạng vật lý thực tế.',
         '#EFF6FF', '#1E40AF')
    ]

    for x, y, w, h, t1, t2, bg, fg in boxes:
        r = patches.FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0.5,rounding_size=1", facecolor=bg, edgecolor=fg, linewidth=1.3)
        ax.add_patch(r)
        ax.text(x + w/2, y + h - 3, t1, fontsize=8.8, fontweight='bold', ha='center', va='center', color=fg)
        ax.text(x + 1.5, y + (h - 4)/2, t2, fontsize=7.8, ha='left', va='center', color='#1E293B')

    # Arrows
    ax.annotate('', xy=(36, 73), xytext=(32, 73), arrowprops=dict(arrowstyle="->", color="#991B1B", lw=2))
    ax.text(34, 76, 'Push (701)', fontsize=7.5, ha='center', color='#991B1B', fontweight='bold')

    ax.annotate('', xy=(68, 73), xytext=(64, 73), arrowprops=dict(arrowstyle="->", color="#065F46", lw=2))
    ax.text(66, 76, 'Pull (703)', fontsize=7.5, ha='center', color='#065F46', fontweight='bold')

    plt.tight_layout()
    plt.savefig('e:/DAU-Chat-Team5/diagrams/diagram_lan_vault_sync_flow.png', dpi=300)
    plt.close()
    print('4. Generated LAN Vault sync flow diagram')

# 5. ERD Diagram
def create_erd_diagram():
    fig, ax = plt.subplots(figsize=(12, 8), dpi=300)
    ax.set_xlim(0, 120)
    ax.set_ylim(0, 100)
    ax.axis('off')
    fig.patch.set_facecolor('#FFFFFF')

    ax.text(60, 96, 'SƠ ĐỒ QUAN HỆ THỰC THỂ CƠ SỞ DỮ LIỆU (ERD) & LIÊN KẾT LƯU TRỮ LAI', 
            fontsize=13, fontweight='bold', ha='center', va='center', color='#0F2D59')

    # Entities
    tables = [
        # x, y, w, h, name, fields, color
        (6, 68, 22, 22, 'Roles (Vai trò)', 
         'PK  RoleId (INT)\n    RoleName (VARCHAR)\n    Description (TEXT)\n    Permissions (TEXT)', '#EFF6FF', '#1D4ED8'),

        (36, 60, 26, 30, 'Users (Tài khoản)', 
         'PK  UserId (VARCHAR)\nFK  RoleId (INT)\n    Username (VARCHAR)\n    PasswordHash (VARCHAR)\n    FullName (VARCHAR)\n    Email (VARCHAR)\n    IsActive (BOOLEAN)\n    CreatedAt (DATETIME)', '#EFF6FF', '#1D4ED8'),

        (70, 70, 24, 22, 'Classes (Lớp học)', 
         'PK  ClassId (VARCHAR)\n    ClassName (VARCHAR)\nFK  LecturerId (VARCHAR)\n    AcademicYear (VARCHAR)', '#EFF6FF', '#1D4ED8'),

        (100, 68, 18, 24, 'Rooms (Phòng chat)', 
         'PK  RoomId (VARCHAR)\nFK  ClassId (VARCHAR)\n    RoomName (VARCHAR)\n    RoomType (VARCHAR)\n    CreatedBy (VARCHAR)', '#EFF6FF', '#1D4ED8'),

        (70, 40, 24, 20, 'ClassMembers', 
         'PK,FK ClassId (VARCHAR)\nPK,FK UserId (VARCHAR)\n      JoinedAt (DATETIME)', '#FEF3C7', '#B45309'),

        (100, 38, 18, 22, 'RoomMembers', 
         'PK,FK RoomId (VARCHAR)\nPK,FK UserId (VARCHAR)\n      RoleInRoom (VARCHAR)\n      JoinedAt (DATETIME)', '#FEF3C7', '#B45309'),

        (6, 36, 24, 22, 'SystemAuditLogs', 
         'PK  LogId (INT)\nFK  UserId (VARCHAR)\n    Action (VARCHAR)\n    IpAddress (VARCHAR)\n    Timestamp (DATETIME)', '#F1F5F9', '#475569'),

        # Client Local Box
        (25, 6, 70, 24, 'KHO LƯU TRỮ LOCAL-FIRST TRÊN MÁY TRẠM CLIENT (TÁCH BIỆT SERVER)', 
         '• Messages/{RoomId}/YYYY-MM.jsonl: Ghi nối tiếp bản tin nhắn (Id, SenderId, Content, Timestamp, Status)\n'
         '• Messages/{RoomId}/YYYY-MM.idx: Chỉ mục nhị phân định vị byte offset phục vụ tìm kiếm & tải tức thì\n'
         '• Files/{RoomId}/: Lưu trữ các tệp tải về từ mạng nội bộ đã được xác thực toàn vẹn bằng SHA-256\n'
         '• file_manifest.jsonl: Bảng mục lục tệp cục bộ (FileId, OriginalName, FileSize, SHA256, LocalPath)', 
         '#DCFCE7', '#166534')
    ]

    for x, y, w, h, t1, t2, bg, fg in tables:
        r = patches.FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0.4,rounding_size=1", facecolor=bg, edgecolor=fg, linewidth=1.3)
        ax.add_patch(r)
        ax.text(x + w/2, y + h - 2.8, t1, fontsize=9, fontweight='bold', ha='center', va='center', color=fg)
        ax.text(x + 1.2, y + (h - 3.8)/2, t2, fontsize=7.5, ha='left', va='center', color='#1E293B')

    # Relations lines
    # Roles 1 -- N Users
    ax.annotate('', xy=(36, 75), xytext=(28, 75), arrowprops=dict(arrowstyle="->", color="#1D4ED8", lw=1.5))
    ax.text(29, 77, '1', fontsize=8, color='#1D4ED8', fontweight='bold')
    ax.text(34, 77, 'N', fontsize=8, color='#1D4ED8', fontweight='bold')

    # Users 1 -- N ClassMembers
    ax.annotate('', xy=(70, 50), xytext=(62, 65), arrowprops=dict(arrowstyle="->", color="#B45309", lw=1.5))

    # Classes 1 -- N ClassMembers
    ax.annotate('', xy=(78, 60), xytext=(78, 70), arrowprops=dict(arrowstyle="->", color="#B45309", lw=1.5))

    # Classes 1 -- N Rooms
    ax.annotate('', xy=(100, 78), xytext=(94, 78), arrowprops=dict(arrowstyle="->", color="#1D4ED8", lw=1.5))

    # Rooms 1 -- N RoomMembers
    ax.annotate('', xy=(109, 60), xytext=(109, 68), arrowprops=dict(arrowstyle="->", color="#B45309", lw=1.5))

    # Users 1 -- N RoomMembers
    ax.annotate('', xy=(100, 48), xytext=(62, 62), arrowprops=dict(arrowstyle="->", color="#B45309", lw=1.5, linestyle=':'))

    # Users 1 -- N SystemAuditLogs
    ax.annotate('', xy=(20, 58), xytext=(36, 68), arrowprops=dict(arrowstyle="->", color="#475569", lw=1.5))

    # Dotted line to Client Local Storage
    ax.annotate('', xy=(60, 30), xytext=(60, 40), arrowprops=dict(arrowstyle="->", color="#166534", lw=2, linestyle='--'))
    ax.text(61, 34, 'Tham chiếu RoomId & UserId không qua SQL Network', fontsize=8, color='#166534', fontweight='bold')

    plt.tight_layout()
    plt.savefig('e:/DAU-Chat-Team5/diagrams/diagram_erd.png', dpi=300)
    plt.close()
    print('5. Generated ERD diagram')

if __name__ == '__main__':
    create_architecture_diagram()
    create_message_flow_diagram()
    create_file_flow_diagram()
    create_lan_vault_flow_diagram()
    create_erd_diagram()
    print('All 5 diagrams successfully generated!')
