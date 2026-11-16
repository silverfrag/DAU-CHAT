import os
import sys

# Ensure local directory in python path
sys.path.insert(0, 'e:/DAU-Chat-Team5')

from scratch_test_setup import create_report
from section_data_1_to_3 import add_cover_page, add_table_of_contents, add_section_1, add_section_2, add_section_3
from section_data_4_to_5 import add_section_4, add_section_5
from section_data_6_to_7 import add_section_6, add_section_7

def build_full_report():
    print("=== STARTING DAU CHAT REPORT COMPILATION (ACADEMIC TECH DEV EDITION) ===")
    doc = create_report()

    print("1. Adding Cover Page...")
    add_cover_page(doc)

    print("2. Adding Table of Contents...")
    add_table_of_contents(doc)

    print("3. Adding Section 1: Đặt tên đề tài...")
    add_section_1(doc)

    print("4. Adding Section 2: Đặc tả yêu cầu chức năng (FR & NFR)...")
    add_section_2(doc)

    print("5. Adding Section 3: Phân tích đối tượng & Phân hệ...")
    add_section_3(doc)

    print("6. Adding Section 4: Phân tích chức năng đến tận cùng (63 Modules & IPO)...")
    add_section_4(doc)

    print("7. Adding Section 5: Luồng dữ liệu, CRUD, RBAC & Sơ đồ kiến trúc...")
    add_section_5(doc)

    print("8. Adding Section 6: Thiết kế CSDL & Lưu trữ Lai (Data Dictionary & ERD)...")
    add_section_6(doc)

    print("9. Adding Section 7: Chia & Quản lý công việc nhóm Team 5 (7 Giai đoạn & Log)...")
    add_section_7(doc)

    # Primary targets
    primary_path = 'e:/DAU-Chat-Team5/BAO_CAO_PHAN_TICH_HTTT_DAU_CHAT_Chuan_Khoa_CNTT.docx'
    print(f"10. Saving document to: {primary_path} ...")
    doc.save(primary_path)
    file_size_kb = os.path.getsize(primary_path) / 1024
    print(f"=== COMPILATION SUCCESSFUL! File size: {file_size_kb:.2f} KB ===")

    # Also try to update the original file if unlocked
    orig_path = 'e:/DAU-Chat-Team5/BAO_CAO_PHAN_TICH_HTTT_DAU_CHAT.docx'
    try:
        doc.save(orig_path)
        print(f"Successfully updated original file: {orig_path}")
    except PermissionError:
        print(f"Notice: {orig_path} is currently locked by Word. The new version is available at {primary_path}.")

if __name__ == '__main__':
    build_full_report()
