import matplotlib.pyplot as plt
import matplotlib.patches as patches
import os

os.makedirs('e:/DAU-Chat-Team5/diagrams', exist_ok=True)

# Technical Dev Styling - Clean, Monochromatic / Engineering Slate
# Fonts and parameters
plt.rcParams['font.sans-serif'] = 'Arial'
plt.rcParams['font.family'] = 'sans-serif'
plt.rcParams['figure.autolayout'] = False

# Colors - Tech Blueprint Palette
COLOR_BG = '#FFFFFF'
COLOR_BORDER = '#0F172A'       # Slate 900
COLOR_PANEL_BG = '#F8FAFC'     # Slate 50
COLOR_BOX_BG = '#FFFFFF'       # Pure White
COLOR_HEADER_BG = '#1E293B'    # Slate 800 (Dark tech header)
COLOR_TEXT_MAIN = '#0F172A'    # Slate 900
COLOR_TEXT_MUTED = '#475569'   # Slate 600
COLOR_LINE = '#334155'         # Slate 700
COLOR_ACCENT_BLUE = '#0284C7'  # Sky 600 (Tech Accent)
COLOR_ACCENT_GREEN = '#16A34A' # Emerald 600
COLOR_ACCENT_RED = '#DC2626'   # Red 600

# 1. System Architecture - C4 Component Blueprint
def create_tech_architecture_diagram():
    fig, ax = plt.subplots(figsize=(13, 8.5), dpi=300)
    ax.set_xlim(0, 130)
    ax.set_ylim(0, 95)
    ax.axis('off')
    fig.patch.set_facecolor(COLOR_BG)

    # Title Banner
    ax.text(65, 91.5, 'SƠ ĐỒ KIẾN TRÚC TỔNG THỂ HỆ THỐNG DAU CHAT (HYBRID LOCAL-FIRST)', 
            fontsize=13, fontweight='bold', ha='center', va='center', color=COLOR_TEXT_MAIN)
    ax.text(65, 88.5, 'C4 Component Architecture & Network Protocol Specification (.NET 10 LTS)', 
            fontsize=9.5, style='italic', ha='center', va='center', color=COLOR_TEXT_MUTED)

    # CLIENT CONTAINER (Left)
    r_client = patches.Rectangle((4, 6), 54, 79, facecolor='#F8FAFC', edgecolor='#0F172A', linewidth=1.5, linestyle='-')
    ax.add_patch(r_client)
    # Header tag
    r_c_tag = patches.Rectangle((4, 81), 54, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.5)
    ax.add_patch(r_c_tag)
    ax.text(31, 83, 'CLIENT WORKSTATION (WPF .NET 10 LTS - MVVM)', fontsize=10.5, fontweight='bold', ha='center', va='center', color='#FFFFFF')

    # Client subcomponents (Clean rectangular boxes with exact signatures)
    c_boxes = [
        (6, 68, 50, 11, 'Presentation Layer (WPF XAML / MVVM)', 
         'MainShellView, ChatWorkspaceView, StorageBackupView\nModalsOverlay (LAN Sync Prompt & Lab PC Mode)\nStagedFileTray (Zalo-style Pre-send File Buffer)', '#FFFFFF'),

        (6, 54, 50, 12, 'Application & Storage Engine (Local-First)', 
         'MessagingEngine: Append-only Write to YYYY-MM.jsonl\nBinaryIndexManager: Binary Index .idx (Offset + Timestamp O(1))\nChunkFileTransferService: 64KB Chunking & SHA-256 Engine\nLocalStorageBackupService: .zip Archive & Manifest Tracker', '#FFFFFF'),

        (6, 40, 50, 12, 'Local Data Store (Isolated Client Sandbox)', 
         'LocalData/{UserId}/Messages/{RoomId}/YYYY-MM.jsonl (Append Log)\nLocalData/{UserId}/Messages/{RoomId}/YYYY-MM.idx (Binary Index)\nLocalData/{UserId}/Files/{RoomId}/* (Downloaded Artifacts)\nfile_manifest.jsonl (File Hash & Metadata Registry)', '#F1F5F9'),

        (6, 26, 50, 12, 'Dual Receiver Actions Handler', 
         'Action 1 - "Mở file": Process.Start(path) -> OS Default Shell Launch\nAction 2 - "Lưu về máy": SaveFileDialog -> File.Copy() Target Folder\nNo Sandbox Restriction; Full OS Native Interop', '#FFFFFF'),

        (6, 9, 50, 15, 'Transport Adapter (Raw TCP Network Client)', 
         'TcpClient NetworkStream, Async/Await Non-blocking I/O\nBig-Endian 4-Byte Length-Prefix Framing: [Len: 4B][Type: 1B][Payload]\nHeartbeat Keeper (Ping/Pong 15s) & Auto-Reconnect State Machine', '#FFFFFF')
    ]

    for x, y, w, h, title, details, bg in c_boxes:
        rect = patches.Rectangle((x, y), w, h, facecolor=bg, edgecolor='#334155', linewidth=1.2)
        ax.add_patch(rect)
        ax.text(x + 2, y + h - 2.8, title, fontsize=9.2, fontweight='bold', color='#0F172A')
        ax.text(x + 2, y + (h - 3)/2, details, fontsize=7.8, color='#334155', fontfamily='monospace' if 'LocalData' in details else 'sans-serif')

    # SERVER CONTAINER (Right)
    r_server = patches.Rectangle((72, 6), 54, 79, facecolor='#F8FAFC', edgecolor='#0F172A', linewidth=1.5, linestyle='-')
    ax.add_patch(r_server)
    r_s_tag = patches.Rectangle((72, 81), 54, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.5)
    ax.add_patch(r_s_tag)
    ax.text(99, 83, 'SERVER DAU CHAT HOST (.NET 10 LTS DAEMON)', fontsize=10.5, fontweight='bold', ha='center', va='center', color='#FFFFFF')

    s_boxes = [
        (74, 68, 50, 11, 'Socket Listener & Packet Dispatcher', 
         'TcpListener (Port 5000 / 8888), ThreadPool Async Dispatch\nReadExactAsync(4) Header Decoder -> Length Verification\nActiveConnectionMap: ConcurrentDictionary<string, TcpClient>', '#FFFFFF'),

        (74, 50, 50, 16, 'Transient Relay Engine (RAM-to-RAM Bridge)', 
         'Online Routing: Direct Socket Stream Pipe (RAM-to-RAM, Zero Disk)\nOffline Routing: PendingQueue (ConcurrentQueue FIFO, TTL = 7d)\nPacketAck Handler: Instant RAM Buffer Release upon Delivery\nZero File Stored Permanently; Server RAM Consumption = O(1)', '#FFFFFF'),

        (74, 30, 50, 18, 'Server Metadata Store (SQLite Engine)', 
         'dauchat-metadata.db (Microsoft.Data.Sqlite)\nTables: Roles, Users, Classes, ClassMembers, Rooms,\n        RoomMembers, SystemAuditLogs\nStrict Constraint: NO Messages and NO Files in SQL Database\nPure Metadata Registry for Auth, Class Roster & RBAC', '#F1F5F9'),

        (74, 9, 50, 19, 'Server Vault Hot-Cache (LAN Multi-Device Sync)', 
         'Storage Path: ServerData/Vaults/{UserId}/latest_snapshot.bin\nStrict Quota Enforcement: Max 1GB Hot-Cache per Student User\nLRU / Oldest-First Automated Eviction Engine\nHandles VaultPushRequest (701) & VaultPullRequest (703)\nEnables Seamless PC-to-PC Mobility across Lab Workstations', '#FFFFFF')
    ]

    for x, y, w, h, title, details, bg in s_boxes:
        rect = patches.Rectangle((x, y), w, h, facecolor=bg, edgecolor='#334155', linewidth=1.2)
        ax.add_patch(rect)
        ax.text(x + 2, y + h - 2.8, title, fontsize=9.2, fontweight='bold', color='#0F172A')
        ax.text(x + 2, y + (h - 3)/2, details, fontsize=7.8, color='#334155', fontfamily='monospace' if 'dauchat-metadata' in details else 'sans-serif')

    # CHANNEL PROTOCOL (Center)
    r_chan = patches.Rectangle((58.5, 22), 13, 48, facecolor='#FFFFFF', edgecolor='#0284C7', linewidth=1.5, linestyle='--')
    ax.add_patch(r_chan)
    ax.text(65, 66.5, 'NETWORK\nCHANNEL', fontsize=8.5, fontweight='bold', ha='center', va='center', color='#0284C7')
    ax.text(65, 59.5, 'LAN 1Gbps\nTCP Socket\nPort 5000', fontsize=7.5, ha='center', va='center', color='#0F172A')
    
    # Framing packet box inside channel
    ax.text(65, 52, 'PACKET FRAME:', fontsize=7, fontweight='bold', ha='center', color='#334155')
    r_f1 = patches.Rectangle((59.2, 45), 11.6, 5, facecolor='#E2E8F0', edgecolor='#475569', linewidth=1)
    ax.add_patch(r_f1)
    ax.text(65, 47.5, '[4B Length Big-Endian]', fontsize=6.5, fontfamily='monospace', ha='center', va='center', color='#0F172A')

    r_f2 = patches.Rectangle((59.2, 38), 11.6, 5, facecolor='#E2E8F0', edgecolor='#475569', linewidth=1)
    ax.add_patch(r_f2)
    ax.text(65, 40.5, '[1B PacketType 1..7]', fontsize=6.5, fontfamily='monospace', ha='center', va='center', color='#0F172A')

    r_f3 = patches.Rectangle((59.2, 27), 11.6, 9, facecolor='#E2E8F0', edgecolor='#475569', linewidth=1)
    ax.add_patch(r_f3)
    ax.text(65, 32.5, '[N Bytes Payload:\nJSON / 64KB Chunk]', fontsize=6.5, fontfamily='monospace', ha='center', va='center', color='#0F172A')

    # Arrows
    ax.annotate('', xy=(58.5, 58), xytext=(56, 58), arrowprops=dict(arrowstyle="->", color="#0284C7", lw=1.5))
    ax.annotate('', xy=(72, 58), xytext=(69.5, 58), arrowprops=dict(arrowstyle="->", color="#0284C7", lw=1.5))
    ax.annotate('', xy=(56, 50), xytext=(58.5, 50), arrowprops=dict(arrowstyle="->", color="#0F172A", lw=1.5))
    ax.annotate('', xy=(69.5, 50), xytext=(72, 50), arrowprops=dict(arrowstyle="->", color="#0F172A", lw=1.5))

    plt.tight_layout()
    plt.savefig('e:/DAU-Chat-Team5/diagrams/diagram_system_architecture.png', dpi=300)
    plt.close()
    print('1. Generated tech architecture diagram')

# 2. Message Flow - UML Sequence Diagram
def create_tech_sequence_diagram():
    fig, ax = plt.subplots(figsize=(13, 8.5), dpi=300)
    ax.set_xlim(0, 130)
    ax.set_ylim(0, 100)
    ax.axis('off')
    fig.patch.set_facecolor(COLOR_BG)

    # Title
    ax.text(65, 96, 'BIỂU ĐỒ TUẦN TỰ ĐỊNH TUYẾN TIN NHẮN (UML SEQUENCE DIAGRAM)', 
            fontsize=13, fontweight='bold', ha='center', va='center', color=COLOR_TEXT_MAIN)
    ax.text(65, 93, 'TCP Socket Length-prefix, RAM Transient Relay & Offline PendingQueue Lifecycle', 
            fontsize=9.5, style='italic', ha='center', va='center', color=COLOR_TEXT_MUTED)

    # Lifeline actors
    actors = [
        (22, 'Client A (Sender)\n[User A]', 22),
        (65, 'DAU Chat Server\n[Relay & FIFO Queue]', 65),
        (108, 'Client B (Recipient)\n[User B]', 108)
    ]

    for x, label, cx in actors:
        r = patches.Rectangle((cx - 15, 84), 30, 6.5, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.2)
        ax.add_patch(r)
        ax.text(cx, 87.2, label, fontsize=8.8, fontweight='bold', ha='center', va='center', color='#FFFFFF')
        # Dashed lifeline
        ax.plot([cx, cx], [84, 8], color='#94A3B8', linestyle='--', linewidth=1.2)

    # Activation bars
    ax.add_patch(patches.Rectangle((21.2, 38), 1.6, 44, facecolor='#FFFFFF', edgecolor='#0F172A', linewidth=1))
    ax.add_patch(patches.Rectangle((64.2, 12), 1.6, 70, facecolor='#FFFFFF', edgecolor='#0F172A', linewidth=1))
    ax.add_patch(patches.Rectangle((107.2, 56), 1.6, 26, facecolor='#FFFFFF', edgecolor='#0F172A', linewidth=1))
    ax.add_patch(patches.Rectangle((107.2, 12), 1.6, 22, facecolor='#FFFFFF', edgecolor='#0F172A', linewidth=1))

    # Calls & Messages
    # Step 1: User A sends
    ax.text(22, 80, '1. AppendLocalLog: YYYY-MM.jsonl & .idx', fontsize=7.8, fontfamily='monospace', color='#0F172A')
    ax.annotate('', xy=(64.2, 76), xytext=(22.8, 76), arrowprops=dict(arrowstyle="->", color="#0F172A", lw=1.4))
    ax.text(43.5, 77.5, 'MessagePacket [Len: 4B | Type: 200 | JSON Payload]', fontsize=7.8, fontfamily='monospace', ha='center', color='#0F172A')

    # Step 2: Server checks
    ax.text(66.5, 73, '2. Parse 4B Header -> Lookup ActiveConnectionMap("User_B")', fontsize=7.8, fontfamily='monospace', color='#334155')

    # ALT BLOCK 1: ONLINE
    r_alt1 = patches.Rectangle((8, 52), 114, 18, facecolor='#F8FAFC', edgecolor='#0284C7', linewidth=1, linestyle='--')
    ax.add_patch(r_alt1)
    ax.text(10, 68, '[ALT: User B is Online]', fontsize=7.8, fontweight='bold', color='#0284C7')

    ax.annotate('', xy=(107.2, 64), xytext=(65.8, 64), arrowprops=dict(arrowstyle="->", color="#0284C7", lw=1.4))
    ax.text(86.5, 65.5, 'Relay RAM-to-RAM: MessagePacket (Type 200)', fontsize=7.8, fontfamily='monospace', ha='center', color='#0284C7')

    ax.text(108.5, 60.5, 'Append .jsonl & update .idx -> UI Bubble', fontsize=7.5, fontfamily='monospace', color='#0F172A')

    ax.annotate('', xy=(65.8, 56), xytext=(107.2, 56), arrowprops=dict(arrowstyle="->", color="#16A34A", lw=1.4))
    ax.text(86.5, 57.5, 'PacketAck [Type: 204 | MessageId: UUID]', fontsize=7.8, fontfamily='monospace', ha='center', color='#16A34A')

    ax.annotate('', xy=(22.8, 54), xytext=(64.2, 54), arrowprops=dict(arrowstyle="->", color="#16A34A", lw=1.4, linestyle=':'))
    ax.text(43.5, 55.2, 'StatusUpdate: "Delivered"', fontsize=7.5, fontfamily='monospace', ha='center', color='#16A34A')

    # ALT BLOCK 2: OFFLINE
    r_alt2 = patches.Rectangle((8, 10), 114, 39, facecolor='#F8FAFC', edgecolor='#DC2626', linewidth=1, linestyle='--')
    ax.add_patch(r_alt2)
    ax.text(10, 47, '[ALT: User B is Offline]', fontsize=7.8, fontweight='bold', color='#DC2626')

    ax.text(66.5, 44, 'Enqueue FIFO: PendingQueue[User_B].Enqueue(item, TTL = 7d)', fontsize=7.8, fontfamily='monospace', color='#DC2626')
    ax.annotate('', xy=(22.8, 41), xytext=(64.2, 41), arrowprops=dict(arrowstyle="->", color="#DC2626", lw=1.2, linestyle=':'))
    ax.text(43.5, 42.2, 'PacketAck [Type: 204 | Status: "PendingOnServer"]', fontsize=7.5, fontfamily='monospace', ha='center', color='#DC2626')

    # Time skip
    ax.text(65, 34, '- - - - - - - - - - - [ User B Connects & Authenticates later ] - - - - - - - - - - -', 
            fontsize=8, style='italic', ha='center', color='#64748B')

    ax.annotate('', xy=(65.8, 28), xytext=(107.2, 28), arrowprops=dict(arrowstyle="->", color="#0F172A", lw=1.4))
    ax.text(86.5, 29.5, 'LoginPacket [Type: 101 | MSSV: "2351220198"]', fontsize=7.8, fontfamily='monospace', ha='center', color='#0F172A')

    ax.text(66.5, 25, 'Auth OK -> FlushQueue: while(TryDequeue(&msg))', fontsize=7.8, fontfamily='monospace', color='#0F172A')

    ax.annotate('', xy=(107.2, 20), xytext=(65.8, 20), arrowprops=dict(arrowstyle="->", color="#0284C7", lw=1.4))
    ax.text(86.5, 21.5, 'Flush Delivery: MessagePacket (Type 200)', fontsize=7.8, fontfamily='monospace', ha='center', color='#0284C7')

    ax.annotate('', xy=(65.8, 14), xytext=(107.2, 14), arrowprops=dict(arrowstyle="->", color="#16A34A", lw=1.4))
    ax.text(86.5, 15.5, 'PacketAck [Type: 204] -> Server Deletes from RAM', fontsize=7.8, fontfamily='monospace', ha='center', color='#16A34A')

    plt.tight_layout()
    plt.savefig('e:/DAU-Chat-Team5/diagrams/diagram_message_flow.png', dpi=300)
    plt.close()
    print('2. Generated tech sequence diagram')

# 3. File Streaming Pipeline - Blueprint
def create_tech_file_flow_diagram():
    fig, ax = plt.subplots(figsize=(13, 8.5), dpi=300)
    ax.set_xlim(0, 130)
    ax.set_ylim(0, 95)
    ax.axis('off')
    fig.patch.set_facecolor(COLOR_BG)

    # Title
    ax.text(65, 91.5, 'QUY TRÌNH TRUYỀN TỆP PHÂN MẢNH (CHUNKING 64KB) & KIỂM TRA SHA-256', 
            fontsize=13, fontweight='bold', ha='center', va='center', color=COLOR_TEXT_MAIN)
    ax.text(65, 88.5, 'Zero-Buffer RAM Stream Relay & Dual Receiver Action Specification', 
            fontsize=9.5, style='italic', ha='center', va='center', color=COLOR_TEXT_MUTED)

    # 3 Main Stages: Sender, Server, Receiver
    # SENDER (Left)
    r_snd = patches.Rectangle((4, 42), 37, 43, facecolor='#F8FAFC', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_snd)
    r_snd_h = patches.Rectangle((4, 81), 37, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_snd_h)
    ax.text(22.5, 83, 'A. SENDER PIPELINE (BÊN GỬI)', fontsize=9.5, fontweight='bold', ha='center', va='center', color='#FFFFFF')

    snd_steps = [
        '1. Staged File Tray: Select CAD/BIM File',
        '   (e.g., DoAnKienTruc_K23.dwg - 250MB)',
        '2. Compute Source SHA-256:',
        '   Hash_Original = SHA256.ComputeHash()',
        '3. Send FileTransferRequest (Type: 301):',
        '   [FileId | Name | Length | TotalChunks | Hash]',
        '4. Chunking Loop (64KB = 65,536 Bytes):',
        '   while ((read = stream.Read(buf, 0, 64KB)) > 0)',
        '   Pack & Send FileChunkPacket (Type: 302)',
        '5. Memory: O(1) buffer (Never load entire file)'
    ]
    for i, st in enumerate(snd_steps):
        ax.text(6, 76.5 - i * 3.4, st, fontsize=7.8, fontfamily='monospace', color='#0F172A', fontweight='bold' if any(k in st for k in ['1.', '2.', '3.', '4.', '5.']) else 'normal')

    # SERVER (Center)
    r_srv = patches.Rectangle((46.5, 42), 37, 43, facecolor='#F8FAFC', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_srv)
    r_srv_h = patches.Rectangle((46.5, 81), 37, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_srv_h)
    ax.text(65, 83, 'B. SERVER TRANSIENT RELAY', fontsize=9.5, fontweight='bold', ha='center', va='center', color='#FFFFFF')

    srv_steps = [
        '1. Handshake Verification (Type: 301):',
        '   Tra cứu RecipientId trong ActiveConnectionMap',
        '   Forward FileTransferRequest -> Recipient',
        '2. Direct RAM-to-RAM Stream Piping:',
        '   Receive 64KB Chunk from Sender Socket',
        '   Immediate Write to Recipient Socket',
        '3. Zero Disk Storage on Server:',
        '   KHÔNG ghi tệp tạm vào ổ cứng Server',
        '   KHÔNG giữ buffer trong RAM',
        '4. Server Hardware Load: O(1) Memory'
    ]
    for i, st in enumerate(srv_steps):
        ax.text(48.5, 76.5 - i * 3.4, st, fontsize=7.8, fontfamily='monospace', color='#0F172A', fontweight='bold' if any(k in st for k in ['1.', '2.', '3.', '4.']) else 'normal')

    # RECEIVER (Right)
    r_rcv = patches.Rectangle((89, 42), 37, 43, facecolor='#F8FAFC', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_rcv)
    r_rcv_h = patches.Rectangle((89, 81), 37, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_rcv_h)
    ax.text(107.5, 83, 'C. RECEIVER PIPELINE (BÊN NHẬN)', fontsize=9.5, fontweight='bold', ha='center', va='center', color='#FFFFFF')

    rcv_steps = [
        '1. Accept Transfer Request (Type: 301)',
        '2. Initialize Temporary File on Disk:',
        '   path = LocalData/.../DoAnKienTruc.dwg.part',
        '3. Write Sequential Chunks into FileStream',
        '4. Verification upon 100% Completion:',
        '   Hash_Received = SHA256.ComputeHash(.part)',
        '   Assert(Hash_Original == Hash_Received)',
        '5. Integrity OK -> Atomic Rename:',
        '   File.Move(filename.part, filename)',
        '6. Update file_manifest.jsonl Registry'
    ]
    for i, st in enumerate(rcv_steps):
        ax.text(91, 76.5 - i * 3.4, st, fontsize=7.8, fontfamily='monospace', color='#0F172A', fontweight='bold' if any(k in st for k in ['1.', '2.', '3.', '4.', '5.', '6.']) else 'normal')

    # Interconnecting arrows
    ax.annotate('', xy=(46.5, 62), xytext=(41, 62), arrowprops=dict(arrowstyle="->", color="#0284C7", lw=2))
    ax.text(43.7, 63.8, '64KB Chunk', fontsize=7, fontfamily='monospace', ha='center', color='#0284C7')

    ax.annotate('', xy=(89, 62), xytext=(83.5, 62), arrowprops=dict(arrowstyle="->", color="#0284C7", lw=2))
    ax.text(86.2, 63.8, 'Stream Relay', fontsize=7, fontfamily='monospace', ha='center', color='#0284C7')

    # DUAL RECEIVER ACTIONS SECTION (Bottom Box)
    r_act = patches.Rectangle((4, 6), 122, 31, facecolor='#FFFFFF', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_act)
    r_act_h = patches.Rectangle((4, 33), 122, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_act_h)
    ax.text(65, 35, 'D. DUAL RECEIVER ACTIONS - THAO TÁC TƯƠNG TÁC TỆP CỦA BÊN NHẬN (WPF UI)', fontsize=10, fontweight='bold', ha='center', va='center', color='#FFFFFF')

    # 2 Action boxes inside
    r_act1 = patches.Rectangle((8, 9), 56, 21, facecolor='#F8FAFC', edgecolor='#334155', linewidth=1.2)
    ax.add_patch(r_act1)
    ax.text(36, 26.5, 'HÀNH ĐỘNG 1: "MỞ FILE" (QUICK VIEW / OS LAUNCH)', fontsize=8.8, fontweight='bold', ha='center', color='#0F172A')
    act1_desc = [
        '• Mục đích: Mở xem ngay nội dung tệp tại thư mục ứng dụng',
        '• Triển khai C#:',
        '  ProcessStartInfo psi = new ProcessStartInfo(localPath) {',
        '      UseShellExecute = true',
        '  };',
        '  Process.Start(psi);',
        '• Cơ chế: Hệ điều hành Windows tự kích hoạt phần mềm mặc định',
        '  (AutoCAD cho .dwg, Adobe Photoshop cho .psd, Word cho .docx)'
    ]
    for i, line in enumerate(act1_desc):
        ax.text(10, 23.5 - i * 2.2, line, fontsize=7.5, fontfamily='monospace' if 'Process' in line else 'sans-serif', color='#334155')

    r_act2 = patches.Rectangle((66, 9), 56, 21, facecolor='#F8FAFC', edgecolor='#334155', linewidth=1.2)
    ax.add_patch(r_act2)
    ax.text(94, 26.5, 'HÀNH ĐỘNG 2: "LƯU VỀ MÁY" (SAVE AS DIALOG)', fontsize=8.8, fontweight='bold', ha='center', color='#0F172A')
    act2_desc = [
        '• Mục đích: Xuất tệp ra ổ đĩa ngoài vùng sandbox của app',
        '• Triển khai C#:',
        '  SaveFileDialog sfd = new SaveFileDialog {',
        '      FileName = Path.GetFileName(localPath),',
        '      Filter = "Tệp đính kèm|*" + Path.GetExtension(localPath)',
        '  };',
        '  if (sfd.ShowDialog() == true) File.Copy(localPath, sfd.FileName);',
        '• Cơ chế: Người dùng tự do chọn Desktop, USB hoặc ổ đĩa cá nhân'
    ]
    for i, line in enumerate(act2_desc):
        ax.text(68, 23.5 - i * 2.2, line, fontsize=7.5, fontfamily='monospace' if 'SaveFileDialog' in line or 'File.Copy' in line else 'sans-serif', color='#334155')

    plt.tight_layout()
    plt.savefig('e:/DAU-Chat-Team5/diagrams/diagram_file_streaming_flow.png', dpi=300)
    plt.close()
    print('3. Generated tech file flow diagram')

# 4. LAN Vault Sync - Architecture Flow
def create_tech_lan_vault_diagram():
    fig, ax = plt.subplots(figsize=(13, 8.5), dpi=300)
    ax.set_xlim(0, 130)
    ax.set_ylim(0, 95)
    ax.axis('off')
    fig.patch.set_facecolor(COLOR_BG)

    # Title
    ax.text(65, 91.5, 'SƠ ĐỒ ĐỒNG BỘ ĐA MÁY QUA MẠNG LAN & CHẾ ĐỘ PHÒNG MÁY LAB (LAB PC MODE)', 
            fontsize=13, fontweight='bold', ha='center', va='center', color=COLOR_TEXT_MAIN)
    ax.text(65, 88.5, 'Socket PacketType 701-706, Server Hot-Cache Vault (1GB Quota) & LRU Engine', 
            fontsize=9.5, style='italic', ha='center', va='center', color=COLOR_TEXT_MUTED)

    # Workstation 1 (Lab PC)
    r_w1 = patches.Rectangle((4, 43), 36, 42, facecolor='#F8FAFC', edgecolor='#DC2626', linewidth=1.4)
    ax.add_patch(r_w1)
    r_w1_h = patches.Rectangle((4, 81), 36, 4, facecolor='#DC2626', edgecolor='#DC2626', linewidth=1.4)
    ax.add_patch(r_w1_h)
    ax.text(22, 83, 'WORKSTATION 1: PHÒNG THỰC HÀNH LAB', fontsize=9, fontweight='bold', ha='center', va='center', color='#FFFFFF')

    w1_steps = [
        '1. Sinh viên nhấn "Đăng xuất" tại máy Lab',
        '2. UI Dialog: "Bật chế độ máy Lab?"',
        '3. Client kích hoạt Auto-Push:',
        '   ZipFile.CreateFromDirectory(',
        '     "LocalData/{UserId}/", tempZip)',
        '4. Gửi VaultPushRequest (Type: 701):',
        '   [UserId | FileSize | Hash | Stream]',
        '5. Nhận xác nhận VaultPushAck (702)',
        '6. AUTO-WIPE LOCAL STORAGE:',
        '   Directory.Delete("LocalData/{UserId}/")',
        '   => Xóa sạch 100% dữ liệu phòng máy!'
    ]
    for i, line in enumerate(w1_steps):
        ax.text(6, 76.5 - i * 3.3, line, fontsize=7.8, fontfamily='monospace' if 'ZipFile' in line or 'Directory' in line else 'sans-serif', 
                color='#991B1B' if 'AUTO-WIPE' in line or 'Xóa sạch' in line else '#0F172A',
                fontweight='bold' if 'AUTO-WIPE' in line or any(k in line for k in ['1.', '2.', '3.', '4.', '5.', '6.']) else 'normal')

    # SERVER VAULT (Center)
    r_v = patches.Rectangle((46, 38), 38, 47, facecolor='#F8FAFC', edgecolor='#0F172A', linewidth=1.5)
    ax.add_patch(r_v)
    r_v_h = patches.Rectangle((46, 81), 38, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.5)
    ax.add_patch(r_v_h)
    ax.text(65, 83, 'SERVER VAULT (HOT-CACHE VÙNG ĐỆM)', fontsize=9, fontweight='bold', ha='center', va='center', color='#FFFFFF')

    v_steps = [
        '• ServerData/Vaults/{UserId}/latest_snapshot.bin',
        '• Hạn ngạch Nghiêm ngặt: Tối đa 1GB / User',
        '• Thuật toán Dọn dẹp LRU / Oldest-First:',
        '  Khi tổng dung lượng Server vượt ngưỡng an toàn,',
        '  tự động giải phóng bản sao lưu cũ nhất.',
        '• Tiếp nhận gói tin Socket 701-706:',
        '  - 701: VaultPushRequest (Đẩy snapshot lên)',
        '  - 702: VaultPushAck (Xác nhận lưu đệm xong)',
        '  - 703: VaultPullRequest (Yêu cầu kéo snapshot)',
        '  - 704: VaultPullResponse (Bơm stream tệp nén)',
        '  - 705: VaultStatusQuery (Kiểm tra snapshot)',
        '  - 706: VaultStatusReply (Báo có/không snapshot)'
    ]
    for i, line in enumerate(v_steps):
        ax.text(48, 76.5 - i * 3.3, line, fontsize=7.8, fontfamily='monospace' if 'latest_snapshot' in line or '70' in line else 'sans-serif', 
                color='#0F172A', fontweight='bold' if 'Hạn ngạch' in line or 'LRU' in line else 'normal')

    # Workstation 2 (New PC / Home Laptop)
    r_w2 = patches.Rectangle((90, 43), 36, 42, facecolor='#F8FAFC', edgecolor='#16A34A', linewidth=1.4)
    ax.add_patch(r_w2)
    r_w2_h = patches.Rectangle((90, 81), 36, 4, facecolor='#16A34A', edgecolor='#16A34A', linewidth=1.4)
    ax.add_patch(r_w2_h)
    ax.text(108, 83, 'WORKSTATION 2: MÁY TÍNH MỚI (LAPTOP)', fontsize=9, fontweight='bold', ha='center', va='center', color='#FFFFFF')

    w2_steps = [
        '1. Đăng nhập User trên máy tính mới',
        '2. Client gửi VaultStatusQuery (705)',
        '3. Server báo có snapshot (706, 85MB)',
        '4. UI Modal Prompt: "Phát hiện bản sao lưu',
        '   trên Server LAN. Bạn có muốn phục hồi?"',
        '5. Đồng ý -> Gửi VaultPullRequest (703)',
        '6. Server truyền VaultPullResponse (704)',
        '7. Giải nén vào thư mục cục bộ:',
        '   ZipFile.ExtractToDirectory(',
        '     stream, "LocalData/{UserId}/")',
        '   => Toàn bộ tin nhắn & file có ngay!'
    ]
    for i, line in enumerate(w2_steps):
        ax.text(92, 76.5 - i * 3.3, line, fontsize=7.8, fontfamily='monospace' if 'ZipFile' in line or '70' in line else 'sans-serif', 
                color='#15803D' if 'Toàn bộ' in line else '#0F172A',
                fontweight='bold' if 'Toàn bộ' in line or any(k in line for k in ['1.', '2.', '3.', '4.', '5.', '6.', '7.']) else 'normal')

    # Connecting arrows
    ax.annotate('', xy=(46, 64), xytext=(40, 64), arrowprops=dict(arrowstyle="->", color="#DC2626", lw=2))
    ax.text(43, 66, 'Push (701)', fontsize=7.2, fontfamily='monospace', ha='center', color='#DC2626', fontweight='bold')

    ax.annotate('', xy=(90, 64), xytext=(84, 64), arrowprops=dict(arrowstyle="->", color="#16A34A", lw=2))
    ax.text(87, 66, 'Pull (703/704)', fontsize=7.2, fontfamily='monospace', ha='center', color='#16A34A', fontweight='bold')

    # Bottom summary box: Real Engineering Problem Solving
    r_sum = patches.Rectangle((4, 6), 122, 28, facecolor='#FFFFFF', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_sum)
    r_sum_h = patches.Rectangle((4, 30), 122, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_sum_h)
    ax.text(65, 32, 'GIẢI PHÁP ĐỒNG BỘ ĐA MÁY THỰC TẾ CHO ĐỒ ÁN LẬP TRÌNH MẠNG', fontsize=10, fontweight='bold', ha='center', va='center', color='#FFFFFF')

    sum_points = [
        '• Giải quyết triệt để nghịch lý của Local-First: Người dùng không bị "giam lỏng" dữ liệu trên 1 máy tính duy nhất.',
        '• Bảo vệ tải trọng Server: Server KHÔNG lưu dữ liệu vĩnh viễn; cơ chế Hot-Cache 1GB + LRU giữ cho đĩa cứng Server luôn an toàn.',
        '• Đảm bảo tính trung thực môn Lập trình mạng: Toàn bộ quá trình đồng bộ vận hành trên Raw TCP Socket (PacketType 701-706),',
        '  không phụ thuộc vào bất kỳ dịch vụ Cloud (Google Drive/Firebase/OneDrive) của bên thứ ba nào.'
    ]
    for i, pt in enumerate(sum_points):
        ax.text(6, 24 - i * 5, pt, fontsize=8.2, color='#0F172A')

    plt.tight_layout()
    plt.savefig('e:/DAU-Chat-Team5/diagrams/diagram_lan_vault_sync_flow.png', dpi=300)
    plt.close()
    print('4. Generated tech LAN vault diagram')

# 5. Database ERD - Engineering Crow's Foot Schema
def create_tech_erd_diagram():
    fig, ax = plt.subplots(figsize=(13, 8.8), dpi=300)
    ax.set_xlim(0, 130)
    ax.set_ylim(0, 100)
    ax.axis('off')
    fig.patch.set_facecolor(COLOR_BG)

    # Title
    ax.text(65, 96.5, 'SƠ ĐỒ QUAN HỆ THỰC THỂ CƠ SỞ DỮ LIỆU (ERD) & LIÊN KẾT LƯU TRỮ LAI', 
            fontsize=13, fontweight='bold', ha='center', va='center', color=COLOR_TEXT_MAIN)
    ax.text(65, 93.5, 'Server Metadata SQLite Schema (7 Tables) & Client Local-First Data Linkage', 
            fontsize=9.5, style='italic', ha='center', va='center', color=COLOR_TEXT_MUTED)

    # Database Entity Tables (Header in Dark Slate, Content in White with borders)
    def draw_entity_table(x, y, w, h, name, fields):
        # Outline
        r = patches.Rectangle((x, y), w, h, facecolor='#FFFFFF', edgecolor='#0F172A', linewidth=1.3)
        ax.add_patch(r)
        # Header
        rh = patches.Rectangle((x, y + h - 5), w, 5, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.3)
        ax.add_patch(rh)
        ax.text(x + w/2, y + h - 2.5, name, fontsize=8.8, fontweight='bold', ha='center', va='center', color='#FFFFFF')
        # Content
        for i, (pkfk, col, dtype) in enumerate(fields):
            cy = y + h - 8.5 - i * 3.4
            color_tag = '#DC2626' if pkfk == 'PK' else ('#0284C7' if pkfk == 'FK' else '#64748B')
            ax.text(x + 1.5, cy, pkfk, fontsize=7.2, fontweight='bold', color=color_tag)
            ax.text(x + 6, cy, col, fontsize=7.5, fontfamily='monospace', color='#0F172A')
            ax.text(x + w - 1.5, cy, dtype, fontsize=7.2, fontfamily='monospace', ha='right', color='#475569')

    # Tables definitions
    # Roles
    draw_entity_table(6, 68, 24, 21, 'Roles (Vai trò)', [
        ('PK', 'RoleId', 'INTEGER'),
        ('', 'RoleName', 'TEXT(20)'),
        ('', 'Description', 'TEXT'),
        ('', 'Permissions', 'TEXT')
    ])

    # Users
    draw_entity_table(36, 56, 28, 33, 'Users (Tài khoản)', [
        ('PK', 'UserId', 'TEXT(36)'),
        ('FK', 'RoleId', 'INTEGER'),
        ('', 'Username', 'TEXT(50)'),
        ('', 'PasswordHash', 'TEXT(256)'),
        ('', 'FullName', 'TEXT(100)'),
        ('', 'Email', 'TEXT(100)'),
        ('', 'IsActive', 'INTEGER(1)'),
        ('', 'CreatedAt', 'DATETIME')
    ])

    # Classes
    draw_entity_table(70, 68, 26, 21, 'Classes (Lớp học)', [
        ('PK', 'ClassId', 'TEXT(20)'),
        ('', 'ClassName', 'TEXT(100)'),
        ('FK', 'LecturerId', 'TEXT(36)'),
        ('', 'AcademicYear', 'TEXT(20)')
    ])

    # Rooms
    draw_entity_table(102, 66, 24, 23, 'Rooms (Phòng chat)', [
        ('PK', 'RoomId', 'TEXT(36)'),
        ('FK', 'ClassId', 'TEXT(20)'),
        ('FK', 'CreatedBy', 'TEXT(36)'),
        ('', 'RoomName', 'TEXT(100)'),
        ('', 'RoomType', 'TEXT(10)')
    ])

    # ClassMembers
    draw_entity_table(70, 40, 26, 18, 'ClassMembers', [
        ('PK,FK', 'ClassId', 'TEXT(20)'),
        ('PK,FK', 'UserId', 'TEXT(36)'),
        ('', 'JoinedAt', 'DATETIME'),
        ('', 'Status', 'TEXT(20)')
    ])

    # RoomMembers
    draw_entity_table(102, 38, 24, 18, 'RoomMembers', [
        ('PK,FK', 'RoomId', 'TEXT(36)'),
        ('PK,FK', 'UserId', 'TEXT(36)'),
        ('', 'RoleInRoom', 'TEXT(20)'),
        ('', 'JoinedAt', 'DATETIME')
    ])

    # SystemAuditLogs
    draw_entity_table(6, 36, 24, 22, 'SystemAuditLogs', [
        ('PK', 'LogId', 'INTEGER'),
        ('FK', 'UserId', 'TEXT(36)'),
        ('', 'ActionType', 'TEXT(50)'),
        ('', 'IpAddress', 'TEXT(45)'),
        ('', 'Timestamp', 'DATETIME')
    ])

    # Relationship lines
    # Roles 1 -- N Users
    ax.plot([30, 36], [78, 78], color='#0F172A', linewidth=1.3)
    ax.text(31, 79.5, '1', fontsize=7.5, fontweight='bold', color='#0F172A')
    ax.text(34, 79.5, 'N', fontsize=7.5, fontweight='bold', color='#0F172A')

    # Users 1 -- N ClassMembers
    ax.plot([64, 70], [66, 50], color='#0F172A', linewidth=1.3)

    # Classes 1 -- N ClassMembers
    ax.plot([83, 83], [68, 58], color='#0F172A', linewidth=1.3)

    # Classes 1 -- N Rooms
    ax.plot([96, 102], [78, 78], color='#0F172A', linewidth=1.3)

    # Rooms 1 -- N RoomMembers
    ax.plot([114, 114], [66, 56], color='#0F172A', linewidth=1.3)

    # Users 1 -- N RoomMembers
    ax.plot([64, 102], [62, 46], color='#0F172A', linewidth=1.3, linestyle=':')

    # Users 1 -- N SystemAuditLogs
    ax.plot([36, 30], [64, 52], color='#0F172A', linewidth=1.3)

    # CLIENT LOCAL-FIRST DATA BOUNDARY (Bottom Box)
    r_client_erd = patches.Rectangle((6, 6), 120, 24, facecolor='#F8FAFC', edgecolor='#16A34A', linewidth=1.4, linestyle='--')
    ax.add_patch(r_client_erd)
    ax.text(66, 26, 'KHÔNG GIAN LƯU TRỮ CỤC BỘ CLIENT LOCAL-FIRST (TÁCH BIỆT KHỎI SQLITE SERVER)', 
            fontsize=9.5, fontweight='bold', ha='center', va='center', color='#16A34A')

    client_details = [
        '• Messages/{RoomId}/YYYY-MM.jsonl: Ghi nối tiếp Append-only (Id, RoomId, SenderId, Content, Timestamp, Status)',
        '• Messages/{RoomId}/YYYY-MM.idx: Chỉ mục nhị phân định vị byte offset cố định 16 bytes phục vụ đọc phân trang O(1)',
        '• Files/{RoomId}/*: Lưu trữ toàn bộ tệp CAD, BIM, ảnh đồ án tải về đã xác thực mã băm SHA-256',
        '• file_manifest.jsonl: Danh mục quản lý tệp cục bộ (FileId, FileName, FileSize, Sha256Hash, DownloadedAt, SavedPath)'
    ]
    for i, line in enumerate(client_details):
        ax.text(8, 20.5 - i * 3.4, line, fontsize=7.8, fontfamily='monospace', color='#0F172A')

    # Dotted line linking ERD to Local-First
    ax.annotate('', xy=(66, 30), xytext=(66, 38), arrowprops=dict(arrowstyle="->", color="#16A34A", lw=1.8, linestyle=':'))
    ax.text(67, 33.5, 'Liên kết tham chiếu mềm qua RoomId & UserId (Không thực hiện SQL JOIN qua mạng)', 
            fontsize=7.5, fontfamily='monospace', color='#16A34A', fontweight='bold')

    plt.tight_layout()
    plt.savefig('e:/DAU-Chat-Team5/diagrams/diagram_erd.png', dpi=300)
    plt.close()
    print('5. Generated tech ERD diagram')

if __name__ == '__main__':
    create_tech_architecture_diagram()
    create_tech_sequence_diagram()
    create_tech_file_flow_diagram()
    create_tech_lan_vault_diagram()
    create_tech_erd_diagram()
    print('All 5 professional tech-dev diagrams successfully regenerated!')
