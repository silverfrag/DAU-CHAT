import matplotlib.pyplot as plt
import matplotlib.patches as patches
import os

os.makedirs('e:/DAU-Chat-Team5/diagrams', exist_ok=True)

# Technical Dev Styling - Clean, Monochromatic / Engineering Slate Blueprint
plt.rcParams['font.sans-serif'] = 'Arial'
plt.rcParams['font.family'] = 'sans-serif'
plt.rcParams['figure.autolayout'] = False

COLOR_BG = '#FFFFFF'
COLOR_BORDER = '#0F172A'       # Slate 900
COLOR_PANEL_BG = '#F8FAFC'     # Slate 50
COLOR_BOX_BG = '#FFFFFF'       # Pure White
COLOR_HEADER_BG = '#1E293B'    # Slate 800 (Dark tech header)
COLOR_TEXT_MAIN = '#0F172A'    # Slate 900
COLOR_TEXT_MUTED = '#475569'   # Slate 600
COLOR_LINE = '#1E293B'         # Dark slate
COLOR_ACCENT_BLUE = '#0284C7'  # Sky 600 (Tech Accent)
COLOR_ACCENT_GREEN = '#16A34A' # Emerald 600
COLOR_ACCENT_RED = '#DC2626'   # Red 600

def draw_cardinality_badge(ax, x, y, text, color='#0F172A'):
    """Draws a clean white badge with rounded border for ERD cardinality (1, N)"""
    bbox_props = dict(boxstyle='round,pad=0.25', fc='#FFFFFF', ec=color, lw=1.0)
    ax.text(x, y, text, fontsize=7.5, fontweight='bold', ha='center', va='center', color=color, bbox=bbox_props)

def draw_orthogonal_arrow(ax, points, color='#0F172A', lw=1.5, linestyle='-', mutation_scale=14):
    """Draws a right-angle (Manhattan) connector through points with a solid arrow at the end"""
    xs = [p[0] for p in points]
    ys = [p[1] for p in points]
    if len(points) > 2:
        ax.plot(xs[:-1], ys[:-1], color=color, linewidth=lw, linestyle=linestyle)
    ax.annotate('', xy=points[-1], xytext=points[-2],
                arrowprops=dict(arrowstyle="-|>", color=color, lw=lw, linestyle=linestyle, mutation_scale=mutation_scale))

def draw_orthogonal_line(ax, points, color='#0F172A', lw=1.5, linestyle='-'):
    """Draws a right-angle (Manhattan) connector through points without arrows"""
    xs = [p[0] for p in points]
    ys = [p[1] for p in points]
    ax.plot(xs, ys, color=color, linewidth=lw, linestyle=linestyle)


# ==============================================================================
# 1. SYSTEM ARCHITECTURE - C4 COMPONENT BLUEPRINT
# ==============================================================================
def create_tech_architecture_diagram():
    fig, ax = plt.subplots(figsize=(13.8, 9.2), dpi=300)
    ax.set_xlim(0, 138)
    ax.set_ylim(0, 96)
    ax.axis('off')
    fig.patch.set_facecolor(COLOR_BG)

    # Title Banner
    ax.text(69, 93, 'SƠ ĐỒ KIẾN TRÚC TỔNG THỂ HỆ THỐNG DAU CHAT (HYBRID LOCAL-FIRST)', 
            fontsize=13, fontweight='bold', ha='center', va='center', color=COLOR_TEXT_MAIN)
    ax.text(69, 90, 'C4 Component Architecture & Network Protocol Specification (.NET 10 LTS)', 
            fontsize=9.5, style='italic', ha='center', va='center', color=COLOR_TEXT_MUTED)

    # CLIENT CONTAINER (Left)
    r_client = patches.Rectangle((4, 6), 54, 80, facecolor='#F8FAFC', edgecolor='#0F172A', linewidth=1.5)
    ax.add_patch(r_client)
    r_c_tag = patches.Rectangle((4, 82), 54, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.5)
    ax.add_patch(r_c_tag)
    ax.text(31, 84, 'CLIENT WORKSTATION (WPF .NET 10 LTS - MVVM)', fontsize=10.5, fontweight='bold', ha='center', va='center', color='#FFFFFF')

    # Client subcomponents
    c_boxes = [
        (6, 69, 50, 11, 'Presentation Layer (WPF XAML / MVVM)', 
         'MainShellView, ChatWorkspaceView, StorageBackupView\nModalsOverlay (LAN Sync Prompt & Lab PC Mode)\nStagedFileTray (Zalo-style Pre-send File Buffer)', '#FFFFFF'),

        (6, 54, 50, 12, 'Application & Storage Engine (Local-First)', 
         'MessagingEngine: Append-only Write to YYYY-MM.jsonl\nBinaryIndexManager: Binary Index .idx (Offset + Timestamp O(1))\nChunkFileTransferService: 64KB Chunking & SHA-256 Engine\nLocalStorageBackupService: .zip Archive & Manifest Tracker', '#FFFFFF'),

        (6, 39, 50, 12, 'Local Data Store (Isolated Client Sandbox)', 
         'LocalData/{UserId}/Messages/{RoomId}/YYYY-MM.jsonl (Append Log)\nLocalData/{UserId}/Messages/{RoomId}/YYYY-MM.idx (Binary Index)\nLocalData/{UserId}/Files/{RoomId}/* (Downloaded Artifacts)\nfile_manifest.jsonl (File Hash & Metadata Registry)', '#F1F5F9'),

        (6, 25, 50, 11, 'Dual Receiver Actions Handler', 
         'Action 1 - "Mở file": Process.Start(path) -> OS Default Shell Launch\nAction 2 - "Lưu về máy": SaveFileDialog -> File.Copy() Target Folder\nNo Sandbox Restriction; Full OS Native Interop', '#FFFFFF'),

        (6, 9, 50, 13, 'Transport Adapter (Raw TCP Network Client)', 
         'TcpClient NetworkStream, Async/Await Non-blocking I/O\nBig-Endian 4-Byte Length-Prefix Framing: [Len: 4B][Type: 1B][Payload]\nHeartbeat Keeper (Ping/Pong 15s) & Auto-Reconnect State Machine', '#FFFFFF')
    ]

    for x, y, w, h, title, details, bg in c_boxes:
        rect = patches.Rectangle((x, y), w, h, facecolor=bg, edgecolor='#334155', linewidth=1.2)
        ax.add_patch(rect)
        ax.text(x + 2, y + h - 2.8, title, fontsize=9.2, fontweight='bold', color='#0F172A')
        ax.text(x + 2, y + (h - 3)/2, details, fontsize=7.8, color='#334155', fontfamily='monospace' if 'LocalData' in details else 'sans-serif')

    # Internal Client Arrows
    draw_orthogonal_arrow(ax, [(31, 69), (31, 66)], color='#0284C7', lw=1.4)
    draw_orthogonal_arrow(ax, [(20, 54), (20, 51)], color='#16A34A', lw=1.4)
    draw_orthogonal_arrow(ax, [(20, 39), (20, 36)], color='#0284C7', lw=1.4)
    draw_orthogonal_arrow(ax, [(42, 54), (42, 22)], color='#0F172A', lw=1.4)
    draw_orthogonal_arrow(ax, [(46, 22), (46, 54)], color='#0284C7', lw=1.4)

    # SERVER CONTAINER (Right)
    r_server = patches.Rectangle((80, 6), 54, 80, facecolor='#F8FAFC', edgecolor='#0F172A', linewidth=1.5)
    ax.add_patch(r_server)
    r_s_tag = patches.Rectangle((80, 82), 54, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.5)
    ax.add_patch(r_s_tag)
    ax.text(107, 84, 'SERVER DAU CHAT HOST (.NET 10 LTS DAEMON)', fontsize=10.5, fontweight='bold', ha='center', va='center', color='#FFFFFF')

    s_boxes = [
        (82, 69, 50, 11, 'Socket Listener & Packet Dispatcher', 
         'TcpListener (Port 5000 / 8888), ThreadPool Async Dispatch\nReadExactAsync(4) Header Decoder -> Length Verification\nActiveConnectionMap: ConcurrentDictionary<string, TcpClient>', '#FFFFFF'),

        (82, 50, 50, 16, 'Transient Relay Engine (RAM-to-RAM Bridge)', 
         'Online Routing: Direct Socket Stream Pipe (RAM-to-RAM, Zero Disk)\nOffline Routing: PendingQueue (ConcurrentQueue FIFO, TTL = 7d)\nPacketAck Handler: Instant RAM Buffer Release upon Delivery\nZero File Stored Permanently; Server RAM Consumption = O(1)', '#FFFFFF'),

        (82, 30, 50, 17, 'Server Metadata Store (SQLite Engine)', 
         'dauchat-metadata.db (Microsoft.Data.Sqlite)\nTables: Roles, Users, Classes, ClassMembers, Rooms,\n        RoomMembers, SystemAuditLogs\nStrict Constraint: NO Messages and NO Files in SQL Database\nPure Metadata Registry for Auth, Class Roster & RBAC', '#F1F5F9'),

        (82, 9, 50, 18, 'Server Vault Hot-Cache (LAN Multi-Device Sync)', 
         'Storage Path: ServerData/Vaults/{UserId}/latest_snapshot.bin\nStrict Quota Enforcement: Max 1GB Hot-Cache per Student User\nLRU / Oldest-First Automated Eviction Engine\nHandles VaultPushRequest (701) & VaultPullRequest (703)\nEnables Seamless PC-to-PC Mobility across Lab Workstations', '#FFFFFF')
    ]

    for x, y, w, h, title, details, bg in s_boxes:
        rect = patches.Rectangle((x, y), w, h, facecolor=bg, edgecolor='#334155', linewidth=1.2)
        ax.add_patch(rect)
        ax.text(x + 2, y + h - 2.8, title, fontsize=9.2, fontweight='bold', color='#0F172A')
        ax.text(x + 2, y + (h - 3)/2, details, fontsize=7.8, color='#334155', fontfamily='monospace' if 'dauchat-metadata' in details else 'sans-serif')

    # Internal Server Arrows
    draw_orthogonal_arrow(ax, [(107, 69), (107, 66)], color='#0284C7', lw=1.4)
    draw_orthogonal_arrow(ax, [(107, 50), (107, 47)], color='#0F172A', lw=1.4)
    draw_orthogonal_arrow(ax, [(107, 30), (107, 27)], color='#16A34A', lw=1.4)

    # NETWORK CHANNEL (Center)
    r_chan = patches.Rectangle((61, 12), 16, 68, facecolor='#FFFFFF', edgecolor='#0284C7', linewidth=1.5, linestyle='--')
    ax.add_patch(r_chan)
    ax.text(69, 76, 'NETWORK\nCHANNEL', fontsize=8.5, fontweight='bold', ha='center', va='center', color='#0284C7')
    ax.text(69, 68.5, 'LAN 1Gbps\nRaw TCP Socket\nPort 5000', fontsize=7.5, ha='center', va='center', color='#0F172A')
    
    # Framing packet box inside channel
    ax.text(69, 61.5, 'PACKET FRAME:', fontsize=7, fontweight='bold', ha='center', color='#334155')
    r_f1 = patches.Rectangle((62, 54), 14, 5.5, facecolor='#E2E8F0', edgecolor='#475569', linewidth=1)
    ax.add_patch(r_f1)
    ax.text(69, 56.7, '[4B Length Big-Endian]', fontsize=6.8, fontfamily='monospace', ha='center', va='center', color='#0F172A')

    r_f2 = patches.Rectangle((62, 46.5), 14, 5.5, facecolor='#E2E8F0', edgecolor='#475569', linewidth=1)
    ax.add_patch(r_f2)
    ax.text(69, 49.2, '[1B PacketType 1..7]', fontsize=6.8, fontfamily='monospace', ha='center', va='center', color='#0F172A')

    r_f3 = patches.Rectangle((62, 32), 14, 12, facecolor='#E2E8F0', edgecolor='#475569', linewidth=1)
    ax.add_patch(r_f3)
    ax.text(69, 38, '[N Bytes Payload:\nJSON / 64KB Chunk\nStream Data]', fontsize=6.8, fontfamily='monospace', ha='center', va='center', color='#0F172A')

    # Channel Connectors with clean solid arrowheads
    # Client to Channel (Send)
    draw_orthogonal_arrow(ax, [(56, 17), (61, 17)], color='#0284C7', lw=1.6, mutation_scale=14)
    ax.text(58.5, 18.5, 'Send', fontsize=6.8, ha='center', color='#0284C7', fontweight='bold')

    # Channel to Client (Recv)
    draw_orthogonal_arrow(ax, [(61, 13), (56, 13)], color='#0F172A', lw=1.6, mutation_scale=14)
    ax.text(58.5, 14.5, 'Recv', fontsize=6.8, ha='center', color='#0F172A', fontweight='bold')

    # Channel to Server (Ingress)
    draw_orthogonal_arrow(ax, [(77, 17), (82, 17)], color='#0284C7', lw=1.6, mutation_scale=14)
    ax.text(79.5, 18.5, 'Dispatch', fontsize=6.8, ha='center', color='#0284C7', fontweight='bold')

    # Server to Channel (Egress)
    draw_orthogonal_arrow(ax, [(82, 13), (77, 13)], color='#0F172A', lw=1.6, mutation_scale=14)
    ax.text(79.5, 14.5, 'Stream', fontsize=6.8, ha='center', color='#0F172A', fontweight='bold')

    # Top interconnect arrows between high-level tiers
    draw_orthogonal_arrow(ax, [(56, 74.5), (61, 74.5)], color='#0284C7', lw=1.6, mutation_scale=14)
    draw_orthogonal_arrow(ax, [(77, 74.5), (82, 74.5)], color='#0284C7', lw=1.6, mutation_scale=14)

    plt.tight_layout()
    plt.savefig('e:/DAU-Chat-Team5/diagrams/diagram_system_architecture.png', dpi=300)
    plt.close()
    print('1. Generated tech architecture diagram with perfect arrows')


# ==============================================================================
# 2. MESSAGE FLOW - UML SEQUENCE DIAGRAM
# ==============================================================================
def create_tech_sequence_diagram():
    fig, ax = plt.subplots(figsize=(13.8, 9.5), dpi=300)
    ax.set_xlim(0, 138)
    ax.set_ylim(0, 102)
    ax.axis('off')
    fig.patch.set_facecolor(COLOR_BG)

    # Title
    ax.text(69, 98, 'BIỂU ĐỒ TUẦN TỰ ĐỊNH TUYẾN TIN NHẮN (UML SEQUENCE DIAGRAM)', 
            fontsize=13, fontweight='bold', ha='center', va='center', color=COLOR_TEXT_MAIN)
    ax.text(69, 95, 'TCP Socket Length-prefix, RAM Transient Relay & Offline PendingQueue Lifecycle', 
            fontsize=9.5, style='italic', ha='center', va='center', color=COLOR_TEXT_MUTED)

    # Lifeline actors
    actors = [
        ('Client A (Bên gửi)\n[User A]', 25),
        ('DAU Chat Server\n[Transient Relay & FIFO]', 69),
        ('Client B (Bên nhận)\n[User B]', 113)
    ]

    for label, cx in actors:
        r = patches.Rectangle((cx - 16, 85), 32, 7, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.2)
        ax.add_patch(r)
        ax.text(cx, 88.5, label, fontsize=8.8, fontweight='bold', ha='center', va='center', color='#FFFFFF')
        # Dashed lifeline
        ax.plot([cx, cx], [85, 7], color='#94A3B8', linestyle='--', linewidth=1.2)

    # Activation bars (clean white with black borders)
    ax.add_patch(patches.Rectangle((24.2, 38), 1.6, 47, facecolor='#FFFFFF', edgecolor='#0F172A', linewidth=1.2))
    ax.add_patch(patches.Rectangle((68.2, 10), 1.6, 75, facecolor='#FFFFFF', edgecolor='#0F172A', linewidth=1.2))
    ax.add_patch(patches.Rectangle((112.2, 54), 1.6, 28, facecolor='#FFFFFF', edgecolor='#0F172A', linewidth=1.2))
    ax.add_patch(patches.Rectangle((112.2, 10), 1.6, 24, facecolor='#FFFFFF', edgecolor='#0F172A', linewidth=1.2))

    # Self-call at Client A: AppendLocalLog
    draw_orthogonal_arrow(ax, [(25.8, 82), (32, 82), (32, 78), (25.8, 78)], color='#0F172A', lw=1.3, mutation_scale=12)
    ax.text(33.5, 80, '1. AppendLocalLog: YYYY-MM.jsonl & .idx', fontsize=7.5, fontfamily='monospace', va='center', color='#0F172A')

    # Step 1: User A sends MessagePacket to Server
    draw_orthogonal_arrow(ax, [(25.8, 74), (68.2, 74)], color='#0F172A', lw=1.5, mutation_scale=14)
    ax.text(47, 75.8, 'MessagePacket [Len: 4B | Type: 200 | JSON Payload]', fontsize=7.8, fontfamily='monospace', ha='center', color='#0F172A')

    # Self-call at Server: Decode & Route
    draw_orthogonal_arrow(ax, [(69.8, 71.5), (76, 71.5), (76, 68), (69.8, 68)], color='#334155', lw=1.3, mutation_scale=12)
    ax.text(77.5, 69.8, '2. Parse 4B Header -> Lookup ActiveConnectionMap("User_B")', fontsize=7.5, fontfamily='monospace', va='center', color='#334155')

    # ALT BLOCK 1: ONLINE
    r_alt1 = patches.Rectangle((8, 51), 122, 16, facecolor='#F8FAFC', edgecolor='#0284C7', linewidth=1.2, linestyle='--')
    ax.add_patch(r_alt1)
    ax.text(10, 65, '[ALT: User B is Online - Direct RAM Relay]', fontsize=8, fontweight='bold', color='#0284C7')

    # Server relays to User B
    draw_orthogonal_arrow(ax, [(69.8, 62), (112.2, 62)], color='#0284C7', lw=1.5, mutation_scale=14)
    ax.text(91, 63.8, 'Relay RAM-to-RAM: MessagePacket (Type 200)', fontsize=7.8, fontfamily='monospace', ha='center', color='#0284C7')

    # Self-call at Client B
    draw_orthogonal_arrow(ax, [(113.8, 59.5), (119, 59.5), (119, 56.5), (113.8, 56.5)], color='#0284C7', lw=1.3, mutation_scale=12)
    ax.text(120.5, 58, 'Append .jsonl & show UI', fontsize=7.2, fontfamily='monospace', va='center', color='#0F172A')

    # User B sends PacketAck to Server
    draw_orthogonal_arrow(ax, [(112.2, 53.5), (69.8, 53.5)], color='#16A34A', lw=1.5, mutation_scale=14)
    ax.text(91, 55, 'PacketAck [Type: 204 | MessageId: UUID]', fontsize=7.8, fontfamily='monospace', ha='center', color='#16A34A')

    # Server notifies User A
    draw_orthogonal_arrow(ax, [(68.2, 52), (25.8, 52)], color='#16A34A', lw=1.4, linestyle=':', mutation_scale=14)
    ax.text(47, 53.5, 'StatusUpdate: "Delivered"', fontsize=7.5, fontfamily='monospace', ha='center', color='#16A34A')

    # ALT BLOCK 2: OFFLINE
    r_alt2 = patches.Rectangle((8, 8), 122, 40, facecolor='#F8FAFC', edgecolor='#DC2626', linewidth=1.2, linestyle='--')
    ax.add_patch(r_alt2)
    ax.text(10, 46, '[ALT: User B is Offline - PendingQueue Lifecycle]', fontsize=8, fontweight='bold', color='#DC2626')

    # Self-call: Enqueue
    draw_orthogonal_arrow(ax, [(69.8, 43.5), (76, 43.5), (76, 40.5), (69.8, 40.5)], color='#DC2626', lw=1.3, mutation_scale=12)
    ax.text(77.5, 42, 'Enqueue FIFO: PendingQueue[User_B].Enqueue(item, TTL = 7d)', fontsize=7.5, fontfamily='monospace', va='center', color='#DC2626')

    # Server ACKs User A: Pending
    draw_orthogonal_arrow(ax, [(68.2, 38), (25.8, 38)], color='#DC2626', lw=1.4, linestyle=':', mutation_scale=14)
    ax.text(47, 39.5, 'PacketAck [Type: 204 | Status: "PendingOnServer"]', fontsize=7.5, fontfamily='monospace', ha='center', color='#DC2626')

    # Time skip separator
    ax.plot([14, 124], [32, 32], color='#94A3B8', linestyle=':', linewidth=1)
    ax.text(69, 32.8, '[ . . . Sau đó: User B mở máy, kết nối Socket & Đăng nhập . . . ]', 
            fontsize=7.8, style='italic', ha='center', va='center', color='#64748B', bbox=dict(boxstyle='square,pad=0.2', fc='#FFFFFF', ec='#CBD5E1', lw=0.8))

    # User B LoginPacket
    draw_orthogonal_arrow(ax, [(112.2, 26), (69.8, 26)], color='#0F172A', lw=1.5, mutation_scale=14)
    ax.text(91, 27.8, 'LoginPacket [Type: 101 | MSSV: "2351220198"]', fontsize=7.8, fontfamily='monospace', ha='center', color='#0F172A')

    # Self-call: Flush
    draw_orthogonal_arrow(ax, [(69.8, 22.5), (76, 22.5), (76, 19.5), (69.8, 19.5)], color='#0284C7', lw=1.3, mutation_scale=12)
    ax.text(77.5, 21, 'Auth OK -> FlushQueue: while(TryDequeue(&msg))', fontsize=7.5, fontfamily='monospace', va='center', color='#0284C7')

    # Flush Delivery to User B
    draw_orthogonal_arrow(ax, [(69.8, 15.5), (112.2, 15.5)], color='#0284C7', lw=1.5, mutation_scale=14)
    ax.text(91, 17.2, 'Flush Delivery: MessagePacket (Type 200)', fontsize=7.8, fontfamily='monospace', ha='center', color='#0284C7')

    # User B final ACK
    draw_orthogonal_arrow(ax, [(112.2, 11), (69.8, 11)], color='#16A34A', lw=1.5, mutation_scale=14)
    ax.text(91, 12.5, 'PacketAck [Type: 204] -> Server giải phóng khỏi RAM', fontsize=7.8, fontfamily='monospace', ha='center', color='#16A34A')

    plt.tight_layout()
    plt.savefig('e:/DAU-Chat-Team5/diagrams/diagram_message_flow.png', dpi=300)
    plt.close()
    print('2. Generated tech sequence diagram with perfect arrows')


# ==============================================================================
# 3. FILE STREAMING PIPELINE - BLUEPRINT
# ==============================================================================
def create_tech_file_flow_diagram():
    fig, ax = plt.subplots(figsize=(13.8, 9.5), dpi=300)
    ax.set_xlim(0, 138)
    ax.set_ylim(0, 96)
    ax.axis('off')
    fig.patch.set_facecolor(COLOR_BG)

    # Title
    ax.text(69, 93, 'QUY TRÌNH TRUYỀN TỆP PHÂN MẢNH (CHUNKING 64KB) & KIỂM TRA SHA-256', 
            fontsize=13, fontweight='bold', ha='center', va='center', color=COLOR_TEXT_MAIN)
    ax.text(69, 90, 'Zero-Buffer RAM Stream Relay & Dual Receiver Action Specification', 
            fontsize=9.5, style='italic', ha='center', va='center', color=COLOR_TEXT_MUTED)

    # 3 Main Stages: Sender, Server, Receiver
    # SENDER (Left)
    r_snd = patches.Rectangle((4, 43), 38, 43, facecolor='#F8FAFC', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_snd)
    r_snd_h = patches.Rectangle((4, 82), 38, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_snd_h)
    ax.text(23, 84, 'A. SENDER PIPELINE (BÊN GỬI)', fontsize=9.5, fontweight='bold', ha='center', va='center', color='#FFFFFF')

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
        ax.text(6, 77.5 - i * 3.4, st, fontsize=7.8, fontfamily='monospace', color='#0F172A', 
                fontweight='bold' if any(k in st for k in ['1.', '2.', '3.', '4.', '5.']) else 'normal')

    # SERVER (Center)
    r_srv = patches.Rectangle((49, 43), 38, 43, facecolor='#F8FAFC', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_srv)
    r_srv_h = patches.Rectangle((49, 82), 38, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_srv_h)
    ax.text(68, 84, 'B. SERVER TRANSIENT RELAY', fontsize=9.5, fontweight='bold', ha='center', va='center', color='#FFFFFF')

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
        ax.text(51, 77.5 - i * 3.4, st, fontsize=7.8, fontfamily='monospace', color='#0F172A', 
                fontweight='bold' if any(k in st for k in ['1.', '2.', '3.', '4.']) else 'normal')

    # RECEIVER (Right)
    r_rcv = patches.Rectangle((94, 43), 40, 43, facecolor='#F8FAFC', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_rcv)
    r_rcv_h = patches.Rectangle((94, 82), 40, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_rcv_h)
    ax.text(114, 84, 'C. RECEIVER PIPELINE (BÊN NHẬN)', fontsize=9.5, fontweight='bold', ha='center', va='center', color='#FFFFFF')

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
        ax.text(96, 77.5 - i * 3.4, st, fontsize=7.8, fontfamily='monospace', color='#0F172A', 
                fontweight='bold' if any(k in st for k in ['1.', '2.', '3.', '4.', '5.', '6.']) else 'normal')

    # Interconnecting horizontal arrows with crisp solid heads
    # Step 1: Handshake
    draw_orthogonal_arrow(ax, [(42, 73), (49, 73)], color='#0284C7', lw=1.8, mutation_scale=14)
    ax.text(45.5, 74.8, 'Request 301', fontsize=7.2, fontfamily='monospace', ha='center', color='#0284C7', fontweight='bold')

    draw_orthogonal_arrow(ax, [(87, 73), (94, 73)], color='#0284C7', lw=1.8, mutation_scale=14)
    ax.text(90.5, 74.8, 'Forward 301', fontsize=7.2, fontfamily='monospace', ha='center', color='#0284C7', fontweight='bold')

    # Step 2: Stream Chunks
    draw_orthogonal_arrow(ax, [(42, 60), (49, 60)], color='#0284C7', lw=2.0, mutation_scale=14)
    ax.text(45.5, 61.8, '64KB Chunk', fontsize=7.2, fontfamily='monospace', ha='center', color='#0284C7', fontweight='bold')

    draw_orthogonal_arrow(ax, [(87, 60), (94, 60)], color='#0284C7', lw=2.0, mutation_scale=14)
    ax.text(90.5, 61.8, 'Stream Relay', fontsize=7.2, fontfamily='monospace', ha='center', color='#0284C7', fontweight='bold')

    # Step 3: ACK return
    draw_orthogonal_arrow(ax, [(94, 49), (87, 49)], color='#16A34A', lw=1.5, mutation_scale=14)
    draw_orthogonal_arrow(ax, [(49, 49), (42, 49)], color='#16A34A', lw=1.5, mutation_scale=14)
    ax.text(68, 49.5, 'Delivery Ack (303)', fontsize=7.2, fontfamily='monospace', ha='center', color='#16A34A', fontweight='bold')

    # DUAL RECEIVER ACTIONS SECTION (Bottom Box)
    r_act = patches.Rectangle((4, 5), 130, 32, facecolor='#FFFFFF', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_act)
    r_act_h = patches.Rectangle((4, 33), 130, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_act_h)
    ax.text(69, 35, 'D. DUAL RECEIVER ACTIONS - THAO TÁC TƯƠNG TÁC TỆP CỦA BÊN NHẬN (WPF UI)', fontsize=10, fontweight='bold', ha='center', va='center', color='#FFFFFF')

    # CRITICAL FLOW CONNECTOR FROM RECEIVER PIPELINE DOWN TO DUAL ACTIONS
    draw_orthogonal_arrow(ax, [(114, 43), (114, 37)], color='#0284C7', lw=2.2, mutation_scale=16)
    ax.text(114, 40, 'Khớp SHA-256 -> Kích hoạt UI Phản hồi Kép', fontsize=7.5, fontfamily='monospace', ha='center', va='bottom', color='#0284C7', fontweight='bold',
            bbox=dict(boxstyle='square,pad=0.2', fc='#FFFFFF', ec='#0284C7', lw=0.8))

    # 2 Action boxes inside
    r_act1 = patches.Rectangle((8, 8), 58, 22, facecolor='#F8FAFC', edgecolor='#334155', linewidth=1.2)
    ax.add_patch(r_act1)
    ax.text(37, 26.5, 'HÀNH ĐỘNG 1: "MỞ FILE" (QUICK VIEW / OS LAUNCH)', fontsize=8.8, fontweight='bold', ha='center', color='#0F172A')
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

    r_act2 = patches.Rectangle((72, 8), 58, 22, facecolor='#F8FAFC', edgecolor='#334155', linewidth=1.2)
    ax.add_patch(r_act2)
    ax.text(101, 26.5, 'HÀNH ĐỘNG 2: "LƯU VỀ MÁY" (SAVE AS DIALOG)', fontsize=8.8, fontweight='bold', ha='center', color='#0F172A')
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
        ax.text(74, 23.5 - i * 2.2, line, fontsize=7.5, fontfamily='monospace' if 'SaveFileDialog' in line or 'File.Copy' in line else 'sans-serif', color='#334155')

    # Sub-arrows branching into Action 1 and Action 2
    draw_orthogonal_arrow(ax, [(69, 33), (37, 30.5)], color='#0284C7', lw=1.5, mutation_scale=12)
    draw_orthogonal_arrow(ax, [(69, 33), (101, 30.5)], color='#0284C7', lw=1.5, mutation_scale=12)

    plt.tight_layout()
    plt.savefig('e:/DAU-Chat-Team5/diagrams/diagram_file_streaming_flow.png', dpi=300)
    plt.close()
    print('3. Generated tech file flow diagram with perfect arrows')


# ==============================================================================
# 4. LAN VAULT SYNC - ARCHITECTURE FLOW
# ==============================================================================
def create_tech_lan_vault_diagram():
    fig, ax = plt.subplots(figsize=(13.8, 9.5), dpi=300)
    ax.set_xlim(0, 138)
    ax.set_ylim(0, 96)
    ax.axis('off')
    fig.patch.set_facecolor(COLOR_BG)

    # Title
    ax.text(69, 93, 'SƠ ĐỒ ĐỒNG BỘ ĐA MÁY QUA MẠNG LAN & CHẾ ĐỘ PHÒNG MÁY LAB (LAB PC MODE)', 
            fontsize=13, fontweight='bold', ha='center', va='center', color=COLOR_TEXT_MAIN)
    ax.text(69, 90, 'Socket PacketType 701-706, Server Hot-Cache Vault (1GB Quota) & LRU Engine', 
            fontsize=9.5, style='italic', ha='center', va='center', color=COLOR_TEXT_MUTED)

    # Workstation 1 (Lab PC, Left, Red alert theme)
    r_w1 = patches.Rectangle((4, 43), 38, 43, facecolor='#F8FAFC', edgecolor='#DC2626', linewidth=1.5)
    ax.add_patch(r_w1)
    r_w1_h = patches.Rectangle((4, 82), 38, 4, facecolor='#DC2626', edgecolor='#DC2626', linewidth=1.5)
    ax.add_patch(r_w1_h)
    ax.text(23, 84, 'WORKSTATION 1: PHÒNG THỰC HÀNH LAB', fontsize=9.2, fontweight='bold', ha='center', va='center', color='#FFFFFF')

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
        ax.text(6, 77.5 - i * 3.4, line, fontsize=7.8, fontfamily='monospace' if 'ZipFile' in line or 'Directory' in line else 'sans-serif', 
                color='#991B1B' if 'AUTO-WIPE' in line or 'Xóa sạch' in line else '#0F172A',
                fontweight='bold' if 'AUTO-WIPE' in line or any(k in line for k in ['1.', '2.', '3.', '4.', '5.', '6.']) else 'normal')

    # Internal Auto-Wipe Arrow in Workstation 1
    draw_orthogonal_arrow(ax, [(37, 52), (40, 52), (40, 46.5), (37, 46.5)], color='#DC2626', lw=1.6, mutation_scale=12)
    ax.text(38.5, 49, 'Kích hoạt\nXóa trắng', fontsize=6.8, ha='center', va='center', color='#DC2626', fontweight='bold')

    # SERVER VAULT (Center)
    r_v = patches.Rectangle((49, 39), 40, 47, facecolor='#F8FAFC', edgecolor='#0F172A', linewidth=1.5)
    ax.add_patch(r_v)
    r_v_h = patches.Rectangle((49, 82), 40, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.5)
    ax.add_patch(r_v_h)
    ax.text(69, 84, 'SERVER VAULT (HOT-CACHE VÙNG ĐỆM)', fontsize=9.2, fontweight='bold', ha='center', va='center', color='#FFFFFF')

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
        ax.text(51, 77.5 - i * 3.4, line, fontsize=7.8, fontfamily='monospace' if 'latest_snapshot' in line or '70' in line else 'sans-serif', 
                color='#0F172A', fontweight='bold' if 'Hạn ngạch' in line or 'LRU' in line else 'normal')

    # Workstation 2 (New PC / Home Laptop, Green theme)
    r_w2 = patches.Rectangle((96, 43), 38, 43, facecolor='#F8FAFC', edgecolor='#16A34A', linewidth=1.5)
    ax.add_patch(r_w2)
    r_w2_h = patches.Rectangle((96, 82), 38, 4, facecolor='#16A34A', edgecolor='#16A34A', linewidth=1.5)
    ax.add_patch(r_w2_h)
    ax.text(115, 84, 'WORKSTATION 2: MÁY TÍNH MỚI (LAPTOP)', fontsize=9.2, fontweight='bold', ha='center', va='center', color='#FFFFFF')

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
        ax.text(98, 77.5 - i * 3.4, line, fontsize=7.8, fontfamily='monospace' if 'ZipFile' in line or '70' in line else 'sans-serif', 
                color='#15803D' if 'Toàn bộ' in line else '#0F172A',
                fontweight='bold' if 'Toàn bộ' in line or any(k in line for k in ['1.', '2.', '3.', '4.', '5.', '6.', '7.']) else 'normal')

    # Connecting arrows between Workstation 1 and Server
    draw_orthogonal_arrow(ax, [(42, 69), (49, 69)], color='#DC2626', lw=2.0, mutation_scale=15)
    ax.text(45.5, 70.8, 'Push (701)', fontsize=7.2, fontfamily='monospace', ha='center', color='#DC2626', fontweight='bold')

    draw_orthogonal_arrow(ax, [(49, 61), (42, 61)], color='#16A34A', lw=1.8, mutation_scale=14)
    ax.text(45.5, 62.8, 'Ack (702)', fontsize=7.2, fontfamily='monospace', ha='center', color='#16A34A', fontweight='bold')

    # Connecting arrows between Workstation 2 and Server
    draw_orthogonal_arrow(ax, [(96, 73), (89, 73)], color='#0284C7', lw=1.8, mutation_scale=14)
    ax.text(92.5, 74.8, 'Query (705)', fontsize=7.2, fontfamily='monospace', ha='center', color='#0284C7', fontweight='bold')

    draw_orthogonal_arrow(ax, [(89, 65), (96, 65)], color='#0284C7', lw=1.8, mutation_scale=14)
    ax.text(92.5, 66.8, 'Reply (706)', fontsize=7.2, fontfamily='monospace', ha='center', color='#0284C7', fontweight='bold')

    draw_orthogonal_arrow(ax, [(96, 57), (89, 57)], color='#16A34A', lw=1.8, mutation_scale=14)
    ax.text(92.5, 58.8, 'Pull (703)', fontsize=7.2, fontfamily='monospace', ha='center', color='#16A34A', fontweight='bold')

    draw_orthogonal_arrow(ax, [(89, 49), (96, 49)], color='#16A34A', lw=2.0, mutation_scale=15)
    ax.text(92.5, 50.8, 'Stream (704)', fontsize=7.2, fontfamily='monospace', ha='center', color='#16A34A', fontweight='bold')

    # Bottom summary box: Real Engineering Problem Solving
    r_sum = patches.Rectangle((4, 5), 130, 28, facecolor='#FFFFFF', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_sum)
    r_sum_h = patches.Rectangle((4, 29), 130, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_sum_h)
    ax.text(69, 31, 'GIẢI PHÁP ĐỒNG BỘ ĐA MÁY THỰC TẾ CHO ĐỒ ÁN LẬP TRÌNH MẠNG', fontsize=10, fontweight='bold', ha='center', va='center', color='#FFFFFF')

    sum_points = [
        '• Giải quyết triệt để nghịch lý của Local-First: Người dùng không bị "giam lỏng" dữ liệu trên 1 máy tính duy nhất.',
        '• Bảo vệ tải trọng Server: Server KHÔNG lưu dữ liệu vĩnh viễn; cơ chế Hot-Cache 1GB + LRU giữ cho đĩa cứng Server luôn an toàn.',
        '• Đảm bảo tính trung thực môn Lập trình mạng: Toàn bộ quá trình đồng bộ vận hành trên Raw TCP Socket (PacketType 701-706),',
        '  không phụ thuộc vào bất kỳ dịch vụ Cloud (Google Drive/Firebase/OneDrive) của bên thứ ba nào.'
    ]
    for i, pt in enumerate(sum_points):
        ax.text(6, 23.5 - i * 5, pt, fontsize=8.2, color='#0F172A')

    plt.tight_layout()
    plt.savefig('e:/DAU-Chat-Team5/diagrams/diagram_lan_vault_sync_flow.png', dpi=300)
    plt.close()
    print('4. Generated tech LAN vault diagram with perfect arrows')


# ==============================================================================
# 5. DATABASE ERD & HYBRID LINKAGE - COMPREHENSIVE RE-ENGINEERING
# ==============================================================================
def create_tech_erd_diagram():
    fig, ax = plt.subplots(figsize=(14.0, 9.8), dpi=300)
    ax.set_xlim(0, 140)
    ax.set_ylim(0, 102)
    ax.axis('off')
    fig.patch.set_facecolor(COLOR_BG)

    # Title
    ax.text(70, 98.5, 'SƠ ĐỒ QUAN HỆ THỰC THỂ CƠ SỞ DỮ LIỆU (ERD) & LIÊN KẾT LƯU TRỮ LAI', 
            fontsize=13, fontweight='bold', ha='center', va='center', color=COLOR_TEXT_MAIN)
    ax.text(70, 95.5, 'Server Metadata SQLite Schema (7 Tables) & Client Local-First Data Linkage', 
            fontsize=9.5, style='italic', ha='center', va='center', color=COLOR_TEXT_MUTED)

    # Database Entity Tables
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
            color_tag = '#DC2626' if pkfk == 'PK' else ('#0284C7' if 'FK' in pkfk else '#64748B')
            ax.text(x + 1.5, cy, pkfk, fontsize=7.0, fontweight='bold', color=color_tag)
            ax.text(x + 6.2, cy, col, fontsize=7.4, fontfamily='monospace', color='#0F172A')
            ax.text(x + w - 1.5, cy, dtype, fontsize=7.0, fontfamily='monospace', ha='right', color='#475569')

    # Tables definitions with perfect grid alignment
    # TOP ROW:
    # 1. Roles (x: 5 to 30)
    draw_entity_table(5, 68, 25, 23, 'Roles (Vai trò)', [
        ('PK', 'RoleId', 'INTEGER'),
        ('', 'RoleName', 'TEXT(20)'),
        ('', 'Description', 'TEXT'),
        ('', 'Permissions', 'TEXT')
    ])

    # 2. Users (x: 37 to 66)
    draw_entity_table(37, 56, 29, 35, 'Users (Tài khoản)', [
        ('PK', 'UserId', 'TEXT(36)'),
        ('FK', 'RoleId', 'INTEGER'),
        ('', 'Username', 'TEXT(50)'),
        ('', 'PasswordHash', 'TEXT(256)'),
        ('', 'FullName', 'TEXT(100)'),
        ('', 'Email', 'TEXT(100)'),
        ('', 'IsActive', 'INTEGER(1)'),
        ('', 'CreatedAt', 'DATETIME')
    ])

    # 3. Classes (x: 73 to 100)
    draw_entity_table(73, 68, 27, 23, 'Classes (Lớp học)', [
        ('PK', 'ClassId', 'TEXT(20)'),
        ('', 'ClassName', 'TEXT(100)'),
        ('FK', 'LecturerId', 'TEXT(36)'),
        ('', 'AcademicYear', 'TEXT(20)')
    ])

    # 4. Rooms (x: 108 to 135)
    draw_entity_table(108, 66, 27, 25, 'Rooms (Phòng chat)', [
        ('PK', 'RoomId', 'TEXT(36)'),
        ('FK', 'ClassId', 'TEXT(20)'),
        ('FK', 'CreatedBy', 'TEXT(36)'),
        ('', 'RoomName', 'TEXT(100)'),
        ('', 'RoomType', 'TEXT(10)')
    ])

    # MIDDLE ROW:
    # 5. SystemAuditLogs (x: 5 to 30)
    draw_entity_table(5, 33, 25, 24, 'SystemAuditLogs', [
        ('PK', 'LogId', 'INTEGER'),
        ('FK', 'UserId', 'TEXT(36)'),
        ('', 'ActionType', 'TEXT(50)'),
        ('', 'IpAddress', 'TEXT(45)'),
        ('', 'Timestamp', 'DATETIME')
    ])

    # 6. ClassMembers (x: 73 to 100)
    draw_entity_table(73, 35, 27, 22, 'ClassMembers', [
        ('PK,FK', 'ClassId', 'TEXT(20)'),
        ('PK,FK', 'UserId', 'TEXT(36)'),
        ('', 'JoinedAt', 'DATETIME'),
        ('', 'Status', 'TEXT(20)')
    ])

    # 7. RoomMembers (x: 108 to 135)
    draw_entity_table(108, 35, 27, 22, 'RoomMembers', [
        ('PK,FK', 'RoomId', 'TEXT(36)'),
        ('PK,FK', 'UserId', 'TEXT(36)'),
        ('', 'RoleInRoom', 'TEXT(20)'),
        ('', 'JoinedAt', 'DATETIME')
    ])

    # ==========================================================================
    # CLEAN ORTHOGONAL (MANHATTAN) CONNECTORS - ZERO CROSSING OVER TABLES!
    # ==========================================================================
    
    # 1. Roles (1) <---> (N) Users: Direct horizontal link
    draw_orthogonal_line(ax, [(30, 80), (37, 80)], color=COLOR_LINE, lw=1.5)
    draw_cardinality_badge(ax, 32, 80, '1')
    draw_cardinality_badge(ax, 35, 80, 'N')

    # 2. Users (Lecturer 1) <---> (N) Classes: Direct horizontal link across gap
    draw_orthogonal_line(ax, [(66, 80), (73, 80)], color=COLOR_LINE, lw=1.5)
    draw_cardinality_badge(ax, 68, 80, '1')
    draw_cardinality_badge(ax, 71, 80, 'N')
    ax.text(69.5, 81.8, 'LecturerId', fontsize=6.8, fontfamily='monospace', ha='center', color='#0284C7', fontweight='bold')

    # 3. Classes (1) <---> (N) Rooms: Direct horizontal link
    draw_orthogonal_line(ax, [(100, 79), (108, 79)], color=COLOR_LINE, lw=1.5)
    draw_cardinality_badge(ax, 102, 79, '1')
    draw_cardinality_badge(ax, 106, 79, 'N')

    # 4. Classes (1) <---> (N) ClassMembers: Direct vertical link
    draw_orthogonal_line(ax, [(86.5, 68), (86.5, 57)], color=COLOR_LINE, lw=1.5)
    draw_cardinality_badge(ax, 86.5, 65, '1')
    draw_cardinality_badge(ax, 86.5, 60, 'N')

    # 5. Rooms (1) <---> (N) RoomMembers: Direct vertical link
    draw_orthogonal_line(ax, [(121.5, 66), (121.5, 57)], color=COLOR_LINE, lw=1.5)
    draw_cardinality_badge(ax, 121.5, 63.5, '1')
    draw_cardinality_badge(ax, 121.5, 59.5, 'N')

    # 6. Users (1) <---> (N) ClassMembers: Right-angle elbow connector
    # Routes out from Users right edge at y=60, goes right to x=69.5, down to y=46, right to ClassMembers left edge
    draw_orthogonal_line(ax, [(66, 60), (69.5, 60), (69.5, 46), (73, 46)], color=COLOR_LINE, lw=1.5)
    draw_cardinality_badge(ax, 67.5, 60, '1')
    draw_cardinality_badge(ax, 71.5, 46, 'N')

    # 7. Users (1) <---> (N) RoomMembers: DEDICATED CORRIDOR ROUTING UNDERNEATH TABLES!
    # Routes from Users bottom at (51.5, 56) down to clear corridor at y=29.5,
    # traverses right across x=51.5 to x=121.5 (beneath ClassMembers), then up to RoomMembers bottom at (121.5, 35)
    draw_orthogonal_line(ax, [(51.5, 56), (51.5, 29.5), (121.5, 29.5), (121.5, 35)], color='#0284C7', lw=1.5, linestyle='-')
    draw_cardinality_badge(ax, 51.5, 53.5, '1', color='#0284C7')
    draw_cardinality_badge(ax, 121.5, 37.5, 'N', color='#0284C7')
    # Label badge placed directly on the corridor
    ax.text(86.5, 29.5, ' Quan hệ N - M qua RoomMembers (1 User : N RoomMembers) ', 
            fontsize=7.2, fontfamily='monospace', ha='center', va='center', color='#0284C7', fontweight='bold',
            bbox=dict(boxstyle='square,pad=0.2', fc='#FFFFFF', ec='#0284C7', lw=0.8))

    # 8. Users (1) <---> (N) SystemAuditLogs: Left-angle elbow connector
    # Routes out from Users left edge at y=62, goes left to x=33.5, down to y=44, left to SystemAuditLogs right edge at (30, 44)
    draw_orthogonal_line(ax, [(37, 62), (33.5, 62), (33.5, 44), (30, 44)], color=COLOR_LINE, lw=1.5)
    draw_cardinality_badge(ax, 35.5, 62, '1')
    draw_cardinality_badge(ax, 31.5, 44, 'N')

    # ==========================================================================
    # CLIENT LOCAL-FIRST DATA BOUNDARY (Bottom Container)
    # ==========================================================================
    r_client_erd = patches.Rectangle((5, 5), 130, 20, facecolor='#F8FAFC', edgecolor='#16A34A', linewidth=1.5, linestyle='--')
    ax.add_patch(r_client_erd)
    ax.text(70, 22.5, 'KHÔNG GIAN LƯU TRỮ CỤC BỘ CLIENT LOCAL-FIRST (TÁCH BIỆT KHỎI SQLITE SERVER)', 
            fontsize=9.5, fontweight='bold', ha='center', va='center', color='#16A34A')

    client_details = [
        '• Messages/{RoomId}/YYYY-MM.jsonl: Ghi nối tiếp Append-only (Id, RoomId, SenderId, Content, Timestamp, Status)',
        '• Messages/{RoomId}/YYYY-MM.idx: Chỉ mục nhị phân định vị byte offset cố định 16 bytes phục vụ đọc phân trang O(1)',
        '• Files/{RoomId}/*: Lưu trữ toàn bộ tệp CAD, BIM, ảnh đồ án tải về đã xác thực mã băm SHA-256',
        '• file_manifest.jsonl: Danh mục quản lý tệp cục bộ (FileId, FileName, FileSize, Sha256Hash, DownloadedAt, SavedPath)'
    ]
    for i, line in enumerate(client_details):
        ax.text(7, 17.5 - i * 3.4, line, fontsize=7.8, fontfamily='monospace', color='#0F172A')

    # PROMINENT DOWNWARD BRIDGE: ERD TIER -> LOCAL-FIRST CONTAINER
    draw_orthogonal_arrow(ax, [(69.5, 29.5), (69.5, 25)], color='#16A34A', lw=2.0, mutation_scale=16)
    ax.text(70, 27.2, 'Tham chiếu mềm qua RoomId & UserId (Zero-Join qua mạng)', 
            fontsize=7.2, fontfamily='monospace', ha='center', va='center', color='#16A34A', fontweight='bold',
            bbox=dict(boxstyle='square,pad=0.2', fc='#FFFFFF', ec='#16A34A', lw=0.8))

    plt.tight_layout()
    plt.savefig('e:/DAU-Chat-Team5/diagrams/diagram_erd.png', dpi=300)
    plt.close()
    print('5. Generated tech ERD diagram with perfect orthogonal lines and zero collisions!')


if __name__ == '__main__':
    create_tech_architecture_diagram()
    create_tech_sequence_diagram()
    create_tech_file_flow_diagram()
    create_tech_lan_vault_diagram()
    create_tech_erd_diagram()
    print('All 5 diagrams successfully rebuilt with professional engineering arrows!')
