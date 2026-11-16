# Section 1, 2, 3 Data and Builder Functions for DAU Chat Report (Academic Tech Dev Edition)

import docx
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx_helpers import set_table_borders, add_header_styled_row, add_data_row, add_tech_box, add_figure

def add_cover_page(doc):
    p_inst = doc.add_paragraph()
    p_inst.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_inst.paragraph_format.space_before = Pt(0)
    p_inst.paragraph_format.space_after = Pt(2)
    r1 = p_inst.add_run("TRƯỜNG ĐẠI HỌC KIẾN TRÚC ĐÀ NẴNG\n")
    r1.font.name = 'Times New Roman'
    r1.font.size = Pt(13.5)
    r1.font.bold = True
    r1.font.color.rgb = RGBColor(0, 0, 0)

    r2 = p_inst.add_run("KHOA CÔNG NGHỆ THÔNG TIN")
    r2.font.name = 'Times New Roman'
    r2.font.size = Pt(13)
    r2.font.bold = True
    r2.font.color.rgb = RGBColor(0, 0, 0)

    # Logo
    logo_path = 'e:/DAU-Chat-Team5/extracted_logo.png'
    add_figure(doc, logo_path, "", width=Inches(1.6))

    # Title
    p_title = doc.add_paragraph()
    p_title.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_title.paragraph_format.space_before = Pt(10)
    p_title.paragraph_format.space_after = Pt(4)
    r_t1 = p_title.add_run("BÁO CÁO PHÂN TÍCH HỆ THỐNG THÔNG TIN\n")
    r_t1.font.name = 'Times New Roman'
    r_t1.font.size = Pt(18)
    r_t1.font.bold = True
    r_t1.font.color.rgb = RGBColor(0, 0, 0)

    r_sub = p_title.add_run("HỌC PHẦN: LẬP TRÌNH MẠNG (MÃ HỌC PHẦN: IT311)\n")
    r_sub.font.name = 'Times New Roman'
    r_sub.font.size = Pt(13)
    r_sub.font.bold = True
    r_sub.font.color.rgb = RGBColor(0, 0, 0)

    p_proj = doc.add_paragraph()
    p_proj.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_proj.paragraph_format.space_before = Pt(8)
    p_proj.paragraph_format.space_after = Pt(24)
    r_p1 = p_proj.add_run("ĐỀ TÀI:\n")
    r_p1.font.name = 'Times New Roman'
    r_p1.font.size = Pt(13)
    r_p1.font.bold = True
    r_p1.font.color.rgb = RGBColor(0, 0, 0)
    r_p2 = p_proj.add_run("XÂY DỰNG HỆ THỐNG CHAT NỘI BỘ VÀ TRUYỀN TỆP TỐC ĐỘ CAO CHO SINH VIÊN VÀ GIẢNG VIÊN TRƯỜNG ĐẠI HỌC KIẾN TRÚC ĐÀ NẴNG (DAU CHAT)")
    r_p2.font.name = 'Times New Roman'
    r_p2.font.size = Pt(14)
    r_p2.font.bold = True
    r_p2.font.color.rgb = RGBColor(0, 0, 0)

    # Info Block
    p_info = doc.add_paragraph()
    p_info.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p_info.paragraph_format.left_indent = Inches(1.2)
    p_info.paragraph_format.line_spacing = 1.3
    p_info.paragraph_format.space_after = Pt(36)

    def add_line(p, label, val, is_bold=False):
        r_l = p.add_run(label)
        r_l.font.name = 'Times New Roman'
        r_l.font.size = Pt(12)
        r_l.font.bold = True
        r_l.font.color.rgb = RGBColor(0, 0, 0)
        r_v = p.add_run(val + "\n")
        r_v.font.name = 'Times New Roman'
        r_v.font.size = Pt(12)
        r_v.font.bold = is_bold
        r_v.font.color.rgb = RGBColor(0, 0, 0)

    add_line(p_info, "Người hướng dẫn khoa học:  ", "TS. Đặng Hùng Vĩ", is_bold=True)
    add_line(p_info, "Nhóm sinh viên thực hiện:  ", "Nhóm Team 5 (5 Thành viên)")
    add_line(p_info, "  1. Bùi Trần Nhật Hưng  ", "(Trưởng nhóm - TV1) - MSSV: 2351220198", is_bold=True)
    add_line(p_info, "  2. Phan Trung Nghĩa   ", "(Thành viên 2 - TV2)")
    add_line(p_info, "  3. Nguyễn Thành Long  ", "(Thành viên 3 - TV3)")
    add_line(p_info, "  4. Dương Thị Yến Nhi  ", "(Thành viên 4 - TV4)")
    add_line(p_info, "  5. Hồng Quốc Tuấn     ", "(Thành viên 5 - TV5)")
    add_line(p_info, "Lớp sinh hoạt:           ", "23CT1")
    add_line(p_info, "Khóa học:                ", "2023 – 2027")

    p_bot = doc.add_paragraph()
    p_bot.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_bot.paragraph_format.space_before = Pt(20)
    r_bot = p_bot.add_run("ĐÀ NẴNG, NĂM 2026")
    r_bot.font.name = 'Times New Roman'
    r_bot.font.size = Pt(12)
    r_bot.font.bold = True
    r_bot.font.color.rgb = RGBColor(0, 0, 0)

    doc.add_page_break()

def add_table_of_contents(doc):
    p_toc = doc.add_heading("MỤC LỤC TỔNG QUAN BÁO CÁO", level=1)
    p_toc.alignment = WD_ALIGN_PARAGRAPH.CENTER
    
    toc_items = [
        ("1. ĐẶT TÊN ĐỀ TÀI", "1.1. Đề xuất tên ứng dụng theo cấu trúc 3 thành phần\n1.2. Đặt vấn đề & Định hướng kiến trúc lưu trữ thực tế tại ĐH Kiến trúc Đà Nẵng\n1.3. Bảng so sánh 5 phần mềm liên quan & định hình chức năng đề tài"),
        ("2. ĐẶC TẢ YÊU CẦU CHỨC NĂNG (FUNCTIONAL REQUIREMENTS)", "2.1. Bảng đặc tả yêu cầu chức năng (FR-01 đến FR-25)\n2.2. Đặc tả yêu cầu phi chức năng (Non-Functional Requirements – NFR)\n2.3. Phạm vi và ràng buộc kỹ thuật hệ thống"),
        ("3. PHÂN TÍCH ĐỐI TƯỢNG & NHÓM CHỨC NĂNG (PHÂN HỆ)", "3.1. Danh sách Đối tượng hệ thống (Entities ENT-01 đến ENT-12)\n3.2. Phân hệ và Nhóm chức năng (MOD-01 đến MOD-08)"),
        ("4. PHÂN TÍCH CHỨC NĂNG ĐẾN TẬN CÙNG (CHỨC NĂNG CON → MODULE)", "4.1. Cấu trúc phân rã chức năng (Functional Decomposition Tree 63 Modules)\n4.2. Mô tả Module chi tiết (IPO – Input / Process / Output)\n4.3. Bảng tổng hợp thông số kỹ thuật toàn bộ 63 Module"),
        ("5. LUỒNG DỮ LIỆU LIÊN QUAN ĐẾN ĐỐI TƯỢNG & CHỨC NĂNG", "5.1. Ma trận Đối tượng – Chức năng (CRUD Matrix) & Ma trận RBAC\n5.2. Danh mục các Luồng dữ liệu chính (Data Flows DF-01 đến DF-14)\n5.3. Sơ đồ kiến trúc & luồng dữ liệu chuyên sâu (Mạng TCP, Tin nhắn, File Streaming, LAN Vault Sync)"),
        ("6. THIẾT KẾ CƠ SỞ DỮ LIỆU & KIẾN TRÚC LƯU TRỮ LAI", "6.1. Danh mục Bảng CSDL Server (SQLite) & Cấu trúc lưu trữ Client Local-First\n6.2. Chi tiết thuộc tính từng bảng (Data Dictionary đầy đủ 7 bảng SQLite & Client Data)\n6.3. Sơ đồ quan hệ thực thể (ERD) & Quan hệ lưu trữ Hybrid"),
        ("7. CHIA & QUẢN LÝ CÔNG VIỆC THEO NHÓM (TEAM 5)", "7.1. Phân công vai trò trong nhóm (TV1 đến TV5) & Quy tắc GEMINI.md\n7.2. Cách thức chia công việc theo 7 giai đoạn phát triển\n7.3. Nhật ký theo dõi công việc (Task Tracking Log đối soát 100% mã nguồn .NET 10)\nPHỤ LỤC. Bảng đối chiếu danh mục tệp nguồn C# .NET 10 thực tế")
    ]

    for title, subs in toc_items:
        p_t = doc.add_paragraph()
        p_t.paragraph_format.space_before = Pt(6)
        p_t.paragraph_format.space_after = Pt(1)
        r_t = p_t.add_run(title)
        r_t.font.name = 'Times New Roman'
        r_t.font.size = Pt(12)
        r_t.font.bold = True
        r_t.font.color.rgb = RGBColor(0, 0, 0)

        p_s = doc.add_paragraph()
        p_s.paragraph_format.left_indent = Inches(0.4)
        p_s.paragraph_format.space_before = Pt(0)
        p_s.paragraph_format.space_after = Pt(4)
        p_s.paragraph_format.line_spacing = 1.2
        r_s = p_s.add_run(subs)
        r_s.font.name = 'Times New Roman'
        r_s.font.size = Pt(11)
        r_s.font.color.rgb = RGBColor(0, 0, 0)

    doc.add_page_break()

def add_section_1(doc):
    doc.add_heading("1. ĐẶT TÊN ĐỀ TÀI", level=1)

    doc.add_heading("1.1. Đề xuất tên ứng dụng theo cấu trúc 3 thành phần", level=2)
    p1 = doc.add_paragraph(
        "Theo hướng dẫn chuẩn của Bộ môn Công nghệ Phần mềm và Khoa Công nghệ Thông tin - Trường Đại học Kiến trúc Đà Nẵng, tên đề tài khoa học ứng dụng được chuẩn hóa chặt chẽ theo cấu trúc 3 thành phần bắt buộc:"
    )
    p1.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY

    add_tech_box(doc, "CẤU TRÚC 3 THÀNH PHẦN CHUẨN CỦA TÊN ĐỀ TÀI",
                 "• Thành phần 1 (Loại hình / Hành động): Xây dựng hệ thống ứng dụng Desktop Client-Server\n"
                 "• Thành phần 2 (Chức năng chính): Chat nội bộ thời gian thực và Truyền tệp tốc độ cao theo luồng phân mảnh (Chunking 64KB)\n"
                 "• Thành phần 3 (Đối tượng / Lĩnh vực): Sinh viên và Giảng viên Trường Đại học Kiến trúc Đà Nẵng (DAU)\n"
                 "→ Tên chính thức đề tài: \"Xây dựng hệ thống chat nội bộ và truyền tệp tốc độ cao cho sinh viên và giảng viên Trường Đại học Kiến trúc Đà Nẵng (DAU Chat)\"")

    doc.add_heading("1.2. Đặt vấn đề & Định hướng kiến trúc lưu trữ thực tế tại ĐH Kiến trúc Đà Nẵng", level=2)
    p_prob1 = doc.add_paragraph(
        "Trường Đại học Kiến trúc Đà Nẵng (DAU) là cơ sở đào tạo đặc thù với các khối ngành mũi nhọn bao gồm Kiến trúc, Xây dựng, Thiết kế Đồ họa và Công nghệ Thông tin. Trong quá trình học tập và làm đồ án tốt nghiệp, nhu cầu trao đổi học thuật, thảo luận nhóm và đặc biệt là chia sẻ các tệp tài liệu đồ họa kỹ thuật dung lượng lớn diễn ra với tần suất rất cao. Sinh viên liên tục phải gửi nhận các bản vẽ CAD (.dwg), mô hình không gian BIM/Revit (.rvt), khung cảnh 3ds Max (.max), tệp nguồn Photoshop (.psd) hay các bộ ảnh render đồ án chất lượng cao với dung lượng từ hàng chục Megabyte lên tới hàng Gigabyte."
    )
    p_prob1.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY

    p_prob2 = doc.add_paragraph(
        "Tuy nhiên, việc sử dụng các nền tảng chat thương mại công cộng hiện nay (như Zalo, Facebook Messenger, Microsoft Teams, Google Drive) trong môi trường mạng nội bộ nhà trường đang bộc lộ những rào cản và bất cập nghiêm trọng:\n"
        "1. Nghẽn băng thông kết nối ngoại mạng (Internet WAN): Khi hàng trăm sinh viên trong cùng phòng máy cùng tải các tệp đồ án nặng lên hạ tầng đám mây công cộng bên ngoài, đường truyền Internet của trường lập tức bị quá tải, gây nghẽn mạng nghiêm trọng trong khi băng thông mạng cục bộ nội bộ (LAN 1Gbps) lại bị bỏ phí.\n"
        "2. Nén suy giảm chất lượng dữ liệu: Hầu hết các ứng dụng chat công cộng tự động nén ảnh đồ án render hoặc giới hạn kích thước tệp đính kèm (dưới 25MB đến 100MB), gây vỡ nét bản vẽ và cản trở việc chấm bài của giảng viên.\n"
        "3. Nguy cơ mất dữ liệu do xóa tệp tự động: Điển hình như Zalo áp dụng chính sách xóa các tệp tài liệu và ảnh lưu trữ trên máy chủ sau một khoảng thời gian (khoảng 30 ngày), khiến sinh viên bị mất các tài liệu đồ án quan trọng khi cần ôn tập hoặc nộp lại.\n"
        "4. Nguy cơ vỡ trận hạ tầng nếu lưu trữ tập trung phía Server: Nếu ứng dụng đồ án tự xây dựng cố gắng lưu toàn bộ lịch sử tin nhắn và tệp đính kèm của hàng nghìn sinh viên vào một máy chủ duy nhất (như dùng SQL Server để chứa file và chat), máy chủ nội bộ (thường là một máy tính cấu hình văn phòng hoặc laptop của giảng viên đóng vai trò server) sẽ lập tức cạn kiệt dung lượng ổ cứng, tắc nghẽn bộ nhớ RAM và sập dịch vụ Socket ngay khi nhiều phiên truyền file diễn ra đồng thời."
    )
    p_prob2.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY

    p_sol = doc.add_paragraph(
        "Xuất phát từ các bài toán thực tiễn nêu trên, nhóm Team 5 quyết định đề xuất và hiện thực hóa đề tài DAU Chat dựa trên Kiến trúc Lưu trữ Lai (Hybrid Local-First Architecture) kết hợp Giao thức Mạng TCP Socket Stream độc lập. Điểm mấu chốt của giải pháp là:\n"
        "• Máy chủ (Server Transient Relay): Không lưu lịch sử tin nhắn và không lưu giữ tệp đính kèm lâu dài. Máy chủ chỉ đóng vai trò cầu nối trung chuyển dữ liệu RAM-to-RAM trực tiếp giữa hai máy trạm khi cả hai cùng Online, hoặc giữ tạm thời trong hàng đợi PendingQueue (FIFO + TTL) khi người nhận Offline và xóa sạch ngay khi gói tin được xác nhận giao nhận thành công (PacketAck).\n"
        "• Máy trạm (Client Local-First): Mỗi sinh viên tự chịu trách nhiệm lưu trữ lịch sử chat cục bộ dưới dạng tệp Append-only Log (.jsonl) kèm chỉ mục nhị phân (.idx), đồng thời lưu trữ các tệp tải về tại thư mục cục bộ của mình. Điều này giúp giải phóng hoàn toàn gánh nặng lưu trữ cho máy chủ trường học.\n"
        "• Vùng đệm đồng bộ mạng LAN (LAN Socket Sync & Server Vault Hot-Cache): Để giải quyết triệt để vấn đề sinh viên phải di chuyển giữa các phòng máy thực hành (Lab PC) và máy tính xách tay cá nhân, hệ thống tích hợp kho đệm nóng Server Vault với hạn ngạch kiểm soát 1GB/user và thuật toán dọn dẹp LRU (Least Recently Used). Khi rời phòng máy, sinh viên kích hoạt chế độ \"Lab PC Mode\" để đẩy bản sao lưu nén lên Server Vault rồi tự động xóa sạch dữ liệu trên máy công cộng; khi đăng nhập máy mới, hệ thống tự động phát hiện và khôi phục toàn vẹn dữ liệu chỉ sau vài giây."
    )
    p_sol.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY

    doc.add_heading("1.3. Bảng so sánh 5 phần mềm liên quan & định hình chức năng đề tài", level=2)
    p_cmp = doc.add_paragraph(
        "Bảng 1.1: Bảng đối sánh kỹ thuật chuyên sâu giữa DAU Chat và 4 nền tảng ứng dụng truyền thông phổ biến:"
    )
    p_cmp.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p_cmp.paragraph_format.space_after = Pt(2)

    # Comparison Table
    tbl_cmp = doc.add_table(rows=1, cols=6)
    set_table_borders(tbl_cmp)
    tbl_cmp.alignment = WD_TABLE_ALIGNMENT.CENTER
    headers = ["Tiêu chí Kỹ thuật", "Zalo Desktop", "Telegram", "MS Teams", "Discord", "DAU Chat (Team 5)"]
    widths = [Inches(1.5), Inches(1.1), Inches(1.1), Inches(1.1), Inches(1.1), Inches(1.3)]
    add_header_styled_row(tbl_cmp, headers, widths)

    cmp_data = [
        ("Nền tảng & Ngôn ngữ", "Electron / C++", "Qt / C++", "Electron / React", "Electron / React", "C# .NET 10 LTS / WPF Native"),
        ("Giao thức Truyền thông", "HTTPS / WebSocket", "MTProto / TCP", "HTTPS / Azure SignalR", "WebRTC / WebSocket", "Raw TCP Socket (Length-prefix 4B)"),
        ("Kiến trúc Lưu trữ Tin nhắn", "Local-First (SQLite Client)", "Cloud-First (Server Cloud)", "Cloud-First (Exchange/SharePoint)", "Cloud-First (Cassandra/Scion)", "Local-First (.jsonl + .idx) + LAN Sync"),
        ("Cơ chế Lưu trữ File", "Tải về máy trạm, xóa trên Cloud sau 30 ngày", "Lưu vĩnh viễn trên Telegram Cloud", "Lưu tập trung trên SharePoint/OneDrive", "Lưu trên Cloud CDN", "Client Local Store + Transient Relay Stream"),
        ("Giới hạn Dung lượng File", "Tối đa 1GB (bị bóp tốc độ ngoại mạng)", "Tối đa 2GB (4GB bản Premium)", "Tối đa 250GB (phụ thuộc gói thuê bao)", "Tối đa 25MB (bản miễn phí)", "Không giới hạn (Stream 64KB chunk qua LAN)"),
        ("Tối ưu Mạng LAN Nội bộ", "Không (phải đi vòng qua Server VNG)", "Không (đi vòng qua Server Telegram)", "Không (đi qua Cloud Microsoft)", "Không (đi qua Cloud Discord)", "Tối ưu tuyệt đối (Chạy độc lập LAN 1Gbps)"),
        ("Hoạt động khi mất Internet", "Không thể gửi nhận tin nhắn mới", "Không hoạt động", "Không hoạt động", "Không hoạt động", "Vẫn gửi tin và truyền file bình thường qua LAN"),
        ("Tải trọng Phần cứng Server", "Khổng lồ (cần cụm Data Center)", "Khổng lồ (cần hạ tầng phân tán)", "Khổng lồ (hạ tầng Azure)", "Khổng lồ (hạ tầng Cloud)", "Cực nhẹ: RAM O(1), Server chạy tốt trên Laptop"),
        ("Bảo mật Phòng máy Lab", "Kém (lưu cache dễ bị người sau xem)", "Khá (hỗ trợ xóa phiên từ xa)", "Trung bình (cần đăng xuất Office 365)", "Trung bình (lưu session)", "Tuyệt đối (Lab PC Mode: Push Vault & Auto-Wipe)"),
        ("Chi phí Bản quyền & Duy trì", "Miễn phí cơ bản / Giới hạn dung lượng", "Miễn phí / Trả phí gói Premium", "Rất đắt (Thuê bao Office 365 A3/A5)", "Miễn phí / Trả phí gói Nitro", "Hoàn toàn miễn phí, Mã nguồn mở DAU")
    ]

    for idx, row in enumerate(cmp_data):
        add_data_row(tbl_cmp, row, is_even=(idx % 2 == 1), col_widths=widths)

    p_post_cmp = doc.add_paragraph()
    p_post_cmp.paragraph_format.space_before = Pt(4)
    p_post_cmp.paragraph_format.space_after = Pt(8)
    r_c = p_post_cmp.add_run(
        "Kết luận khảo sát: Các phần mềm hiện hành đều phụ thuộc vào đường truyền Internet bên ngoài và cơ sở hạ tầng đám mây đắt đỏ, không giải quyết được bài toán chia sẻ tệp đồ án CAD/BIM dung lượng lớn trong mạng LAN trường học và gây rủi ro lộ lọt dữ liệu tại các phòng máy thực hành công cộng. DAU Chat là sự kết hợp hoàn hảo giữa mô hình Local-First tối ưu tài nguyên và Giao thức Socket thuần hướng LAN, đáp ứng trọn vẹn yêu cầu học thuật của môn Lập trình mạng."
    )
    r_c.font.name = 'Times New Roman'
    r_c.font.size = Pt(11)
    r_c.font.italic = True

def add_section_2(doc):
    doc.add_heading("2. ĐẶC TẢ YÊU CẦU CHỨC NĂNG (FUNCTIONAL REQUIREMENTS)", level=1)

    doc.add_heading("2.1. Bảng đặc tả yêu cầu chức năng (Functional Requirements – FR)", level=2)
    p_fr = doc.add_paragraph(
        "Bảng 2.1: Danh mục đặc tả 25 yêu cầu chức năng hệ thống (FR-01 đến FR-25):"
    )
    p_fr.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p_fr.paragraph_format.space_after = Pt(2)

    tbl_fr = doc.add_table(rows=1, cols=5)
    set_table_borders(tbl_fr)
    tbl_fr.alignment = WD_TABLE_ALIGNMENT.CENTER
    headers = ["Mã YC", "Tên Yêu cầu", "Mô tả Chi tiết Nghiệp vụ & Kỹ thuật", "Mức Ưu tiên", "Nguồn / Vai trò"]
    widths = [Inches(0.8), Inches(1.5), Inches(3.2), Inches(0.9), Inches(1.1)]
    add_header_styled_row(tbl_fr, headers, widths)

    fr_data = [
        ("FR-01", "Đăng nhập hệ thống", "Người dùng nhập Tên đăng nhập (MSSV chuẩn 10 chữ số hoặc mã GV) và Mật khẩu. Client băm mật khẩu và gửi PacketLogin. Server tra cứu SQLite, phản hồi Token đăng nhập và Profile.", "Bắt buộc", "Mọi người dùng"),
        ("FR-02", "Xem & Cập nhật Hồ sơ", "Xem thông tin cá nhân (MSSV, Họ tên, Email, Lớp sinh hoạt, Vai trò). Cho phép cập nhật số điện thoại, ảnh đại diện Avatar cục bộ.", "Trung bình", "Mọi người dùng"),
        ("FR-03", "Đổi mật khẩu", "Xác thực mật khẩu cũ, kiểm tra ràng buộc độ mạnh của mật khẩu mới (tối thiểu 6 ký tự) và ghi đè PasswordHash vào CSDL SQLite.", "Cao", "Mọi người dùng"),
        ("FR-04", "Quản lý Tài khoản (Admin)", "Quản trị viên thêm mới, sửa thông tin, khóa tài khoản hoặc mở khóa người dùng; cấp lại mật khẩu mặc định (123456) khi sinh viên quên mật khẩu.", "Bắt buộc", "Admin"),
        ("FR-05", "Phân quyền theo vai trò (RBAC)", "Hệ thống tự động kích hoạt giao diện và chức năng tương ứng với vai trò trong CSDL: Quản trị viên (Admin), Giảng viên (Teacher), Sinh viên (Student).", "Bắt buộc", "Hệ thống"),
        ("FR-06", "Quản lý Lớp học", "Admin và Giảng viên tạo mới lớp sinh hoạt hoặc lớp học phần, chỉnh sửa tên lớp, gán Giảng viên chủ nhiệm/phụ trách.", "Bắt buộc", "Admin / Giảng viên"),
        ("FR-07", "Quản lý Thành viên Lớp", "Thêm danh sách sinh viên vào lớp học (nhập từng tài khoản hoặc import hàng loạt từ danh sách MSSV), xóa sinh viên khỏi lớp.", "Cao", "Admin / Giảng viên"),
        ("FR-08", "Quản lý Phòng chat / Nhóm", "Tạo phòng chat môn học, phòng đồ án nhóm hoặc kênh thông báo chung của lớp; chỉnh sửa thông tin phòng chat.", "Bắt buộc", "Admin / Giảng viên"),
        ("FR-09", "Quản lý Thành viên Phòng", "Chủ phòng (Owner) mời thêm thành viên vào phòng chat nhóm, phân quyền Admin nhóm, kích thành viên vi phạm ra khỏi phòng.", "Cao", "Chủ phòng"),
        ("FR-10", "Nhắn tin Cá nhân 1-1", "Gửi nhận tin nhắn văn bản tức thời giữa 2 người dùng qua TCP Socket. Relay trực tiếp nếu người nhận Online; lưu PendingQueue nếu Offline.", "Bắt buộc", "Mọi người dùng"),
        ("FR-11", "Nhắn tin Nhóm (Broadcast)", "Gửi tin nhắn vào phòng chat. Server tra cứu danh sách thành viên trong phòng và broadcast gói tin tới toàn bộ thành viên đang Online.", "Bắt buộc", "Mọi người dùng"),
        ("FR-12", "Lưu trữ Tin nhắn Local-First", "Client ghi nối tiếp tin nhắn vào tệp LocalData/{UserId}/Messages/{RoomId}/YYYY-MM.jsonl ngay khi gửi hoặc nhận, không phụ thuộc vào Server.", "Bắt buộc", "Client TV4"),
        ("FR-13", "Đánh chỉ mục Tin nhắn (.idx)", "Tạo file chỉ mục nhị phân .idx ghi lại Byte Offset và Timestamp của từng dòng tin nhắn để tải nhanh lịch sử chat với độ phức tạp O(1).", "Cao", "Client TV4"),
        ("FR-14", "Truyền tệp theo luồng (Chunking)", "Cắt tệp tin thành các khối nhị phân 64KB (FileChunkPacket). Bên gửi truyền luồng byte qua Socket; bên nhận ghép tuần tự thành file .part.", "Bắt buộc", "Client TV5"),
        ("FR-15", "Kiểm tra Tính toàn vẹn SHA-256", "Bên gửi tính toán mã SHA-256 của tệp gốc. Sau khi nhận đủ 100% các chunk, bên nhận tính lại mã SHA-256; nếu khớp mới đổi tên thành tệp chính thức.", "Bắt buộc", "Client TV5"),
        ("FR-16", "Khay soạn thảo tệp (Staged Tray)", "Cho phép kéo thả nhiều tệp đồ án vào khay đệm trước khi gửi, hiển thị dung lượng, định dạng và nút xóa bỏ từng tệp trước khi ấn gửi chính thức.", "Trung bình", "Client TV1"),
        ("FR-17", "Cơ chế Phản hồi Kép (Dual Actions)", "Cung cấp 2 lựa chọn thao tác trên bong bóng tệp nhận được: 'Mở file' (Quick View qua app mặc định Windows) hoặc 'Lưu về máy' (SaveFileDialog).", "Bắt buộc", "Client TV1 / TV5"),
        ("FR-18", "Quản lý Thư mục Tệp Cục bộ", "Theo dõi và hiển thị danh mục tệp đã nhận trong LocalData/{UserId}/Files/{RoomId}/; lưu vết chi tiết trong tệp file_manifest.jsonl.", "Cao", "Client TV5"),
        ("FR-19", "Dọn dẹp Dung lượng Đĩa Cục bộ", "Thống kê dung lượng chiếm dụng của thư mục LocalData; cho phép xóa tệp tạm, dọn dẹp các tệp tải về quá 30 ngày theo cấu hình người dùng.", "Trung bình", "Client TV5"),
        ("FR-20", "Sao lưu Cục bộ Ngoại tuyến (.zip)", "Đóng gói toàn bộ thư mục Messages (.jsonl) và Files thành một tệp nén duy nhất LocalBackup_YYYYMMDD.zip để người dùng lưu vào USB/ổ cứng ngoài.", "Cao", "Client TV5"),
        ("FR-21", "Khôi phục từ Tệp Ngoại tuyến (.zip)", "Giải nén tệp sao lưu .zip và tái cấu trúc dữ liệu vào đúng thư mục LocalData/{UserId}/, nạp lại toàn bộ lịch sử trò chuyện trên máy mới.", "Cao", "Client TV5"),
        ("FR-22", "Đẩy Sao lưu lên LAN Server Vault", "Client gửi gói tin VaultPushRequest (701) chứa dữ liệu sao lưu nén qua TCP Socket lên vùng đệm nóng ServerData/Vaults/{UserId}/latest_snapshot.bin.", "Bắt buộc", "Client TV5 / Server"),
        ("FR-23", "Kéo Sao lưu từ LAN Server Vault", "Client gửi VaultPullRequest (703) lên Server để tải về bản sao lưu mới nhất trong mạng LAN khi đăng nhập trên máy tính mới.", "Bắt buộc", "Client TV5 / Server"),
        ("FR-24", "Chế độ Phòng máy Lab (Lab PC Mode)", "Khi đăng xuất trên máy công cộng: tự động nén dữ liệu đẩy lên Server Vault, chờ Ack thành công rồi xóa sạch thư mục LocalData bảo vệ quyền riêng tư.", "Bắt buộc", "Client TV1 / TV5"),
        ("FR-25", "Giám sát & Quản trị Server", "Dashboard máy chủ hiển thị số kết nối TCP hoạt động, trạng thái Online của sinh viên/giảng viên, dung lượng Server Vault và nhật ký kết nối.", "Cao", "Admin / Server TV2")
    ]

    for idx, row in enumerate(fr_data):
        add_data_row(tbl_fr, row, is_even=(idx % 2 == 1), col_widths=widths)

    doc.add_heading("2.2. Đặc tả yêu cầu phi chức năng (Non-Functional Requirements – NFR)", level=2)
    p_nfr = doc.add_paragraph(
        "Bảng 2.2: Danh mục 16 yêu cầu phi chức năng (NFR-01 đến NFR-16):"
    )
    p_nfr.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p_nfr.paragraph_format.space_after = Pt(2)

    tbl_nfr = doc.add_table(rows=1, cols=4)
    set_table_borders(tbl_nfr)
    tbl_nfr.alignment = WD_TABLE_ALIGNMENT.CENTER
    nfr_headers = ["Mã YC", "Nhóm Thuộc tính", "Tiêu chuẩn & Chỉ số Đo lường", "Giải pháp Kỹ thuật Đáp ứng"]
    nfr_widths = [Inches(0.9), Inches(1.6), Inches(2.7), Inches(2.3)]
    add_header_styled_row(tbl_nfr, nfr_headers, nfr_widths)

    nfr_data = [
        ("NFR-01", "Độ trễ Mạng (Latency)", "Thời gian gửi - nhận tin nhắn văn bản trong mạng LAN trường học dưới 50ms khi có 500 kết nối đồng thời.", "Giao thức TCP Socket thuần, giải mã nhị phân trực tiếp không qua tầng HTTP."),
        ("NFR-02", "Tốc độ Truyền tệp (Throughput)", "Tận dụng trên 90% băng thông mạng LAN vật lý (đạt 10 - 12 MB/s trên mạng 100Mbps; 80 - 110 MB/s trên mạng Gigabit 1Gbps).", "Phân mảnh 64KB/chunk, kỹ thuật Socket Stream Buffer bất đồng bộ (async/await)."),
        ("NFR-03", "Tiêu hao Bộ nhớ Server", "RAM của tiến trình DauChat.Server không vượt quá 150MB ngay cả khi đang chuyển tiếp đồng thời nhiều luồng tệp dung lượng hàng GB.", "Kiến trúc Transient Relay chuyển tiếp RAM-to-RAM trực tiếp, không đệm toàn bộ file vào RAM."),
        ("NFR-04", "Tải trọng Đĩa cứng Server", "Dung lượng đĩa máy chủ không tăng theo dung lượng file chat của sinh viên; giới hạn tổng kho Server Vault không quá 50GB.", "Chính sách hạn ngạch 1GB đệm nóng cho mỗi User kèm thuật toán dọn dẹp LRU / Oldest-First."),
        ("NFR-05", "Tốc độ Tải Lịch sử Chat", "Thời gian mở và hiển thị 1.000 tin nhắn lịch sử của một phòng chat trên giao diện Client dưới 100ms.", "Đọc tệp Append-only .jsonl từ cuối lên thông qua bảng chỉ mục byte offset .idx với độ phức tạp O(1)."),
        ("NFR-06", "Độ Toàn vẹn Dữ liệu", "Tỷ lệ lỗi hoặc hỏng tệp khi truyền tải qua mạng LAN bằng 0%.", "Mã băm SHA-256 256-bit được kiểm tra bắt buộc trước khi chuyển đổi tệp .part thành tệp chính thức."),
        ("NFR-07", "Độ Tin cậy Chuyển phát Tin", "100% tin nhắn gửi tới người nhận Offline được chuyển phát ngay khi người dùng đăng nhập trở lại.", "Hàng đợi PendingQueue (FIFO) phía Server với thời gian sống TTL 7 ngày và cơ chế PacketAck."),
        ("NFR-08", "Khả năng Tự phục hồi Kết nối", "Client tự động thử kết nối lại (Auto Reconnect) với chu kỳ tăng dần (1s, 2s, 5s) khi dây mạng bị rút hoặc Switch khởi động lại.", "Tiến trình giám sát NetworkClient kiểm tra cờ IsConnected và phát hiện ngắt mạch qua Socket Heartbeat."),
        ("NFR-09", "Khả năng Chống rò rỉ Bộ nhớ", "Tiến trình Client WPF duy trì mức tiêu thụ RAM ổn định dưới 120MB trong suốt phiên làm việc 8 tiếng liên tục.", "Giải phóng triệt để BitmapCache, Unsubscribe sự kiện ViewModel và dọn dẹp FileStream sau khi truyền."),
        ("NFR-10", "Bảo mật Xác thực Người dùng", "100% mật khẩu được băm trước khi lưu trữ vào SQLite; không lưu mật khẩu dạng bản rõ (plaintext).", "Thuật toán băm một chiều an toàn PBKDF2 / SHA-256 kèm chuỗi Salt ngẫu nhiên."),
        ("NFR-11", "Quyền Riêng tư tại Phòng máy", "Không để sót bất kỳ lịch sử chat hoặc tệp đồ án cá nhân nào trên ổ cứng máy tính phòng Lab sau khi đăng xuất.", "Chế độ Lab PC Mode tự động nén đẩy lên Server Vault và kích hoạt xóa trắng thư mục LocalData."),
        ("NFR-12", "Kiểm soát Phân quyền (RBAC)", "Sinh viên tuyệt đối không thể gọi các lệnh quản trị của Giảng viên/Admin (tạo lớp, sửa điểm, xóa tài khoản).", "Server kiểm tra trường RoleId trong phiên kết nối TCP trước khi thực thi bất kỳ gói tin quản trị nào."),
        ("NFR-13", "Cô lập Dữ liệu Cục bộ", "Mỗi tài khoản sinh viên có một không gian thư mục riêng biệt tại LocalData/{UserId}/, không bị lẫn lộn dữ liệu.", "Cấu trúc định danh UserId làm Root folder cho toàn bộ cây tệp tin nhắn và tài liệu."),
        ("NFR-14", "Tính Thân thiện Giao diện", "Giao diện WPF hiện đại, trực quan, hỗ trợ kéo thả tệp, hiển thị tiến trình phần trăm (%) khi truyền file dung lượng lớn.", "Thiết kế chuẩn UX Zalo/Slack, khay Staged File Tray tiện lợi và thanh ProgressBar thời gian thực."),
        ("NFR-15", "Tính Tương thích Hệ điều hành", "Chạy mượt mà trên hệ điều hành Windows 10 và Windows 11 (64-bit) phổ biến tại các phòng máy thực hành của trường.", "Biên dịch trên nền tảng .NET 10.0 LTS (Long Term Support), hỗ trợ Self-contained executable."),
        ("NFR-16", "Độc lập Môi trường Mạng", "Hệ thống hoạt động hoàn hảo trong môi trường mạng LAN cô lập hoàn toàn không có kết nối ra Internet.", "Không phụ thuộc vào bất kỳ dịch vụ Cloud, CDN, Firebase hay DNS của bên thứ ba.")
    ]

    for idx, row in enumerate(nfr_data):
        add_data_row(tbl_nfr, row, is_even=(idx % 2 == 1), col_widths=nfr_widths)

    doc.add_heading("2.3. Phạm vi và ràng buộc kỹ thuật hệ thống", level=2)
    p_scope = doc.add_paragraph(
        "Nhằm đảm bảo tính khả thi cao nhất trong khuôn khổ đồ án môn học và đáp ứng quy chế ranh giới kỹ thuật theo tài liệu GEMINI.md, dự án thiết lập các ràng buộc sau:\n"
        "• Phạm vi triển khai: Hệ thống được thiết kế và kiểm thử tối ưu cho hạ tầng mạng cục bộ (LAN) của Trường Đại học Kiến trúc Đà Nẵng, bao gồm các phòng máy thực hành và các dải địa chỉ IP nội bộ (192.168.x.x / 10.x.x.x).\n"
        "• Nền tảng công nghệ: Ngôn ngữ C# trên nền .NET 10.0 LTS; Giao diện máy trạm xây dựng bằng WPF (Windows Presentation Foundation) áp dụng kiến trúc MVVM (Model-View-ViewModel).\n"
        "• Giao thức truyền thông: Kết nối Raw TCP Socket sử dụng cơ chế đóng gói nhị phân Length-prefix (4 byte Big-Endian xác định độ dài gói tin, 1 byte xác định PacketType và mảng byte Payload). Nghiêm cấm sử dụng các framework trung gian như SignalR hay WCF để thể hiện rõ bản chất lập trình mạng tầng Socket.\n"
        "• Ràng buộc lưu trữ CSDL Server: Phía Server tuyệt đối không sử dụng SQL Server để lưu tin nhắn và file. Server chỉ sử dụng thư viện siêu nhẹ Microsoft.Data.Sqlite để lưu trữ thông tin danh mục người dùng, lớp học, phòng chat và vai trò."
    )
    p_scope.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY

def add_section_3(doc):
    doc.add_heading("3. PHÂN TÍCH ĐỐI TƯỢNG & NHÓM CHỨC NĂNG (PHÂN HỆ)", level=1)

    doc.add_heading("3.1. Danh sách Đối tượng hệ thống (Entities)", level=2)
    p_ent = doc.add_paragraph(
        "Bảng 3.1: Danh mục 12 thực thể đối tượng nghiệp vụ trong hệ thống DAU Chat (ENT-01 đến ENT-12):"
    )
    p_ent.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p_ent.paragraph_format.space_after = Pt(2)

    tbl_ent = doc.add_table(rows=1, cols=5)
    set_table_borders(tbl_ent)
    tbl_ent.alignment = WD_TABLE_ALIGNMENT.CENTER
    headers = ["Mã ĐT", "Tên Đối tượng", "Mô tả Bản chất Nghiệp vụ", "Thuộc tính Chính", "Quan hệ với Đối tượng khác"]
    widths = [Inches(0.8), Inches(1.4), Inches(2.5), Inches(1.8), Inches(1.0)]
    add_header_styled_row(tbl_ent, headers, widths)

    ent_data = [
        ("ENT-01", "User (Người dùng)", "Tài khoản đăng nhập của sinh viên, giảng viên hoặc quản trị viên nhà trường.", "UserId, Username, PasswordHash, FullName, Email, RoleId, IsActive, CreatedAt", "N - 1 với ENT-02; N - M với ENT-03, ENT-05"),
        ("ENT-02", "Role (Vai trò)", "Vai trò phân quyền trong hệ thống: Admin, Teacher (Giảng viên), Student (Sinh viên).", "RoleId, RoleName, Description, PermissionsJson", "1 - N với ENT-01 (User)"),
        ("ENT-03", "Class (Lớp học)", "Lớp học phần hoặc lớp sinh hoạt trực thuộc Khoa CNTT / Trường ĐH Kiến trúc Đà Nẵng.", "ClassId, ClassName, AcademicYear, Faculty, LecturerId, CreatedAt", "1 - N với ENT-04, ENT-05; N - 1 với ENT-01"),
        ("ENT-04", "ClassMember", "Thực thể liên kết biểu diễn danh sách sinh viên thuộc về một lớp học cụ thể.", "ClassId, UserId, JoinedAt, Status", "N - 1 với ENT-03; N - 1 với ENT-01"),
        ("ENT-05", "Room (Phòng chat)", "Không gian trò chuyện chung của một lớp, nhóm học tập đồ án hoặc hội thoại 1-1.", "RoomId, RoomName, RoomType (Direct/Group), ClassId, CreatedBy, CreatedAt", "N - 1 với ENT-03; 1 - N với ENT-06, ENT-07"),
        ("ENT-06", "RoomMember", "Thành viên tham gia vào một phòng chat nhóm hoặc phòng chat lớp.", "RoomId, UserId, RoleInRoom (Owner/Member), JoinedAt", "N - 1 với ENT-05; N - 1 với ENT-01"),
        ("ENT-07", "Message (Tin nhắn)", "Bản tin nhắn văn bản hoặc thông điệp kèm file lưu trữ cục bộ tại Client (.jsonl).", "MessageId (UUID), RoomId, SenderId, Content, MessageType, Timestamp, Status", "N - 1 với ENT-05; N - 1 với ENT-01; 1 - N với ENT-08"),
        ("ENT-08", "FileAttachment (Tệp đính kèm)", "Tệp tài liệu, đồ án CAD, BIM, ảnh gửi kèm qua cơ chế phân mảnh 64KB.", "FileId (UUID), OriginalName, FileSize, Sha256Hash, TotalChunks, LocalPath", "N - 1 với ENT-07; 1 - 1 với ENT-10"),
        ("ENT-09", "PendingQueueItem", "Gói tin nhắn hoặc thông báo được máy chủ lưu tạm trong RAM khi người nhận Offline.", "QueueId, RecipientId, PacketPayload, PacketType, EnqueuedAt, ExpiryTime (TTL)", "N - 1 với ENT-01 (Recipient)"),
        ("ENT-10", "LocalManifestItem", "Bản ghi mục lục tệp tin trong file_manifest.jsonl quản lý tệp tải về tại máy trạm.", "FileId, FileName, FileSize, Sha256Hash, DownloadedAt, SavedPath, RoomId", "1 - 1 với ENT-08; N - 1 với ENT-05"),
        ("ENT-11", "VaultSnapshot", "Tệp nén .bin/.zip chứa bản sao lưu toàn bộ phiên làm việc của User trên Server Vault.", "SnapshotId, UserId, FileSize, Sha256Hash, LastPushedAt, HotCachePath", "1 - 1 với ENT-01 (User)"),
        ("ENT-12", "SystemAuditLog", "Nhật ký kiểm toán hệ thống ghi nhận các sự kiện đăng nhập, ngắt kết nối, lỗi Socket.", "LogId, UserId, ActionType, IpAddress, Timestamp, Details", "N - 1 với ENT-01 (User)")
    ]

    for idx, row in enumerate(ent_data):
        add_data_row(tbl_ent, row, is_even=(idx % 2 == 1), col_widths=widths)

    doc.add_heading("3.2. Phân hệ và Nhóm chức năng (Subsystems)", level=2)
    p_mod = doc.add_paragraph(
        "Bảng 3.2: Danh mục 8 phân hệ chức năng và phân công trách nhiệm nhóm Team 5 (MOD-01 đến MOD-08):"
    )
    p_mod.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p_mod.paragraph_format.space_after = Pt(2)

    tbl_mod = doc.add_table(rows=1, cols=5)
    set_table_borders(tbl_mod)
    tbl_mod.alignment = WD_TABLE_ALIGNMENT.CENTER
    headers = ["Mã Phân hệ", "Tên Phân hệ Chức năng", "Mục đích & Trách nhiệm Chính", "Đối tượng Liên quan", "Thành viên Phụ trách"]
    widths = [Inches(1.0), Inches(1.6), Inches(2.6), Inches(1.2), Inches(1.1)]
    add_header_styled_row(tbl_mod, headers, widths)

    mod_data = [
        ("MOD-01", "Xác thực & Quản lý Tài khoản", "Xác thực tài khoản MSSV/GV, bảo mật phiên làm việc, quản lý thông tin cá nhân và quản trị người dùng.", "ENT-01, ENT-02, ENT-12", "TV3 (Long) + TV1 (UI)"),
        ("MOD-02", "Quản lý Kết nối & Hàng đợi", "Lắng nghe TcpListener, đóng gói Length-prefix 4B, quản lý bảng kết nối Online, chuyển tiếp và hàng đợi FIFO Offline.", "ENT-01, ENT-09, ENT-12", "TV2 (Nghĩa)"),
        ("MOD-03", "Chat Cá nhân 1-1", "Gửi nhận tin nhắn văn bản tức thời giữa 2 người dùng, ghi nối tiếp Append-only log .jsonl, chỉ mục .idx, tải lịch sử.", "ENT-01, ENT-05, ENT-07", "TV4 (Nhi) + TV1 (UI)"),
        ("MOD-04", "Chat Nhóm / Phòng Học tập", "Quản lý danh sách phòng học, phát tán tin nhắn Broadcast tới toàn bộ thành viên trong phòng, phân quyền trưởng nhóm.", "ENT-01, ENT-05, ENT-06, ENT-07", "TV3 (Phòng) + TV4 (Tin)"),
        ("MOD-05", "Truyền Tệp Socket Stream", "Phân mảnh 64KB/chunk, mã băm SHA-256, tái cấu trúc tệp .part, khay tệp chờ gửi Staged Tray và phản hồi kép Mở/Lưu.", "ENT-01, ENT-05, ENT-07, ENT-08, ENT-10", "TV5 (Tuấn) + TV1 (UI)"),
        ("MOD-06", "Quản lý Lớp & Phân bổ SV", "Quản lý danh mục lớp học phần/sinh hoạt, phân bổ danh sách sinh viên theo MSSV, gán giảng viên chủ nhiệm.", "ENT-01, ENT-03, ENT-04, ENT-05", "TV3 (Long) + TV1 (UI)"),
        ("MOD-07", "Lưu trữ Local-First & LAN Vault", "Quản lý tệp cục bộ (.jsonl, .idx, manifest), dọn dẹp dung lượng đĩa, sao lưu nén .zip, đồng bộ Server Vault và chế độ Lab PC.", "ENT-07, ENT-08, ENT-10, ENT-11", "TV4 (Chat) + TV5 (File)"),
        ("MOD-08", "Quản trị Server & Giám sát", "Giám sát hiệu năng máy chủ, thống kê số lượng kết nối đồng thời, kiểm soát hạn ngạch 1GB Server Vault, nhật ký kiểm toán.", "ENT-01, ENT-09, ENT-11, ENT-12", "TV2 (Server) + TV3 (Admin)")
    ]

    for idx, row in enumerate(mod_data):
        add_data_row(tbl_mod, row, is_even=(idx % 2 == 1), col_widths=widths)

print("Section 1, 2, 3 builder functions updated.")
