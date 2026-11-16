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
# 1. SYSTEM ARCHITECTURE - C4 COMPONENT BLUEPRINT (Symmetrical, Zero-Overlap)
# ==============================================================================
def create_tech_architecture_diagram():
    fig, ax = plt.subplots(figsize=(13.8, 9.4), dpi=300)
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

    # Client subcomponents arranged logically:
    # Top: UI -> App Engine -> Network Client (level with Channel!) -> Local Store -> Dual Actions
    c_boxes = [
        (6, 69, 50, 11, 'Presentation Layer (WPF XAML / MVVM)', 
         'MainShellView, ChatWorkspaceView, StorageBackupView\nModalsOverlay (LAN Sync Prompt & Lab PC Mode)\nStagedFileTray (Zalo-style Pre-send File Buffer)', '#FFFFFF'),

        (6, 53, 50, 12, 'Application & Storage Engine (Local-First)', 
         'MessagingEngine: Append-only Write to YYYY-MM.jsonl\nBinaryIndexManager: Binary Index .idx (Offset + Timestamp O(1))\nChunkFileTransferService: 64KB Chunking & SHA-256 Engine\nLocalStorageBackupService: .zip Archive & Manifest Tracker', '#FFFFFF'),

        (6, 37, 50, 12, 'Transport Adapter (Raw TCP Network Client)', 
         'TcpClient NetworkStream, Async/Await Non-blocking I/O\nBig-Endian 4-Byte Length-Prefix Framing: [Len: 4B][Type: 1B][Payload]\nHeartbeat Keeper (Ping/Pong 15s) & Auto-Reconnect State Machine', '#FFFFFF'),

        (6, 22, 50, 11, 'Local Data Store (Isolated Client Sandbox)', 
         'LocalData/{UserId}/Messages/{RoomId}/YYYY-MM.jsonl (Append Log)\nLocalData/{UserId}/Messages/{RoomId}/YYYY-MM.idx (Binary Index)\nLocalData/{UserId}/Files/{RoomId}/* (Downloaded Artifacts)', '#F1F5F9'),

        (6, 8, 50, 11, 'Dual Receiver Actions Handler', 
         'Action 1 - "Mở file": Process.Start(path) -> OS Default Shell Launch\nAction 2 - "Lưu về máy": SaveFileDialog -> File.Copy() Target Folder\nNo Sandbox Restriction; Full OS Native Interop', '#FFFFFF')
    ]

    for x, y, w, h, title, details, bg in c_boxes:
        rect = patches.Rectangle((x, y), w, h, facecolor=bg, edgecolor='#334155', linewidth=1.2)
        ax.add_patch(rect)
        ax.text(x + 2, y + h - 2.8, title, fontsize=9.2, fontweight='bold', color='#0F172A')
        ax.text(x + 2, y + (h - 3)/2, details, fontsize=7.8, color='#334155', fontfamily='monospace' if 'LocalData' in details else 'sans-serif')

    # Internal Client Clean Flow Arrows (Zero text cutting!)
    # UI -> Engine
    draw_orthogonal_arrow(ax, [(31, 69), (31, 65)], color='#0284C7', lw=1.5, mutation_scale=12)
    # Engine <-> Transport Adapter (Direct adjacent connection)
    draw_orthogonal_arrow(ax, [(26, 53), (26, 49)], color='#0F172A', lw=1.5, mutation_scale=12)
    draw_orthogonal_arrow(ax, [(36, 49), (36, 53)], color='#0284C7', lw=1.5, mutation_scale=12)
    # Engine -> Local Store (Left corridor routing)
    draw_orthogonal_arrow(ax, [(12, 53), (12, 33)], color='#16A34A', lw=1.5, mutation_scale=12)
    # Local Store -> Dual Actions
    draw_orthogonal_arrow(ax, [(31, 22), (31, 19)], color='#0284C7', lw=1.5, mutation_scale=12)

    # SERVER CONTAINER (Right)
    r_server = patches.Rectangle((80, 6), 54, 80, facecolor='#F8FAFC', edgecolor='#0F172A', linewidth=1.5)
    ax.add_patch(r_server)
    r_s_tag = patches.Rectangle((80, 82), 54, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.5)
    ax.add_patch(r_s_tag)
    ax.text(107, 84, 'SERVER DAU CHAT HOST (.NET 10 LTS DAEMON)', fontsize=10.5, fontweight='bold', ha='center', va='center', color='#FFFFFF')

    # Server subcomponents arranged symmetrically:
    s_boxes = [
        (82, 69, 50, 11, 'Server Metadata Store (SQLite Engine)', 
         'dauchat-metadata.db (Microsoft.Data.Sqlite)\nTables: Roles, Users, Classes, ClassMembers, Rooms,\n        RoomMembers, SystemAuditLogs\nStrict Constraint: NO Messages and NO Files in SQL Database', '#F1F5F9'),

        (82, 53, 50, 12, 'Transient Relay Engine (RAM-to-RAM Bridge)', 
         'Online Routing: Direct Socket Stream Pipe (RAM-to-RAM, Zero Disk)\nOffline Routing: PendingQueue (ConcurrentQueue FIFO, TTL = 7d)\nPacketAck Handler: Instant RAM Buffer Release upon Delivery', '#FFFFFF'),

        (82, 37, 50, 12, 'Socket Listener & Packet Dispatcher', 
         'TcpListener (Port 5000 / 8888), ThreadPool Async Dispatch\nReadExactAsync(4) Header Decoder -> Length Verification\nActiveConnectionMap: ConcurrentDictionary<string, TcpClient>', '#FFFFFF'),

        (82, 8, 50, 25, 'Server Vault Hot-Cache (LAN Multi-Device Sync)', 
         'Storage Path: ServerData/Vaults/{UserId}/latest_snapshot.bin\nStrict Quota Enforcement: Max 1GB Hot-Cache per Student User\nLRU / Oldest-First Automated Eviction Engine\nHandles VaultPushRequest (701) & VaultPullRequest (703)\nEnables Seamless PC-to-PC Mobility across Lab Workstations\nZero Permanent File Retention on Server Host Disk', '#FFFFFF')
    ]

    for x, y, w, h, title, details, bg in s_boxes:
        rect = patches.Rectangle((x, y), w, h, facecolor=bg, edgecolor='#334155', linewidth=1.2)
        ax.add_patch(rect)
        ax.text(x + 2, y + h - 2.8, title, fontsize=9.2, fontweight='bold', color='#0F172A')
        ax.text(x + 2, y + (h - 3)/2, details, fontsize=7.8, color='#334155', fontfamily='monospace' if 'dauchat-metadata' in details else 'sans-serif')

    # Internal Server Clean Flow Arrows
    # Socket Listener -> Transient Relay
    draw_orthogonal_arrow(ax, [(107, 49), (107, 53)], color='#0284C7', lw=1.5, mutation_scale=12)
    # Transient Relay <-> Metadata Store
    draw_orthogonal_arrow(ax, [(107, 65), (107, 69)], color='#0F172A', lw=1.5, mutation_scale=12)
    # Socket Listener -> Server Vault
    draw_orthogonal_arrow(ax, [(107, 37), (107, 33)], color='#16A34A', lw=1.5, mutation_scale=12)

    # NETWORK CHANNEL (Center - Exactly facing Transport Adapter & Socket Listener!)
    r_chan = patches.Rectangle((61, 10), 16, 70, facecolor='#FFFFFF', edgecolor='#0284C7', linewidth=1.5, linestyle='--')
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

    r_f3 = patches.Rectangle((62, 28), 14, 15, facecolor='#E2E8F0', edgecolor='#475569', linewidth=1)
    ax.add_patch(r_f3)
    ax.text(69, 35.5, '[N Bytes Payload:\nJSON Message /\n64KB File Chunk /\nVault Snapshot]', fontsize=6.6, fontfamily='monospace', ha='center', va='center', color='#0F172A')

    # DIRECT LEVEL NETWORK CONNECTORS (Client <-> Channel <-> Server)
    # Client Transport Adapter -> Channel Ingress (Send)
    draw_orthogonal_arrow(ax, [(56, 45), (61, 45)], color='#0284C7', lw=1.8, mutation_scale=14)
    ax.text(58.5, 46.8, 'Send', fontsize=7, ha='center', color='#0284C7', fontweight='bold')

    # Channel Egress -> Client Transport Adapter (Recv)
    draw_orthogonal_arrow(ax, [(61, 39), (56, 39)], color='#0F172A', lw=1.8, mutation_scale=14)
    ax.text(58.5, 40.8, 'Recv', fontsize=7, ha='center', color='#0F172A', fontweight='bold')

    # Channel Egress -> Server Socket Listener (Dispatch)
    draw_orthogonal_arrow(ax, [(77, 45), (82, 45)], color='#0284C7', lw=1.8, mutation_scale=14)
    ax.text(79.5, 46.8, 'Dispatch', fontsize=7, ha='center', color='#0284C7', fontweight='bold')

    # Server Socket Listener -> Channel (Stream Out)
    draw_orthogonal_arrow(ax, [(82, 39), (77, 39)], color='#0F172A', lw=1.8, mutation_scale=14)
    ax.text(79.5, 40.8, 'Stream', fontsize=7, ha='center', color='#0F172A', fontweight='bold')

    plt.tight_layout()
    plt.savefig('e:/DAU-Chat-Team5/diagrams/diagram_system_architecture.png', dpi=300)
    plt.close()
    print('1. Generated tech architecture diagram (symmetrical layout, zero overlap)')


# ==============================================================================
# 2. MESSAGE FLOW - UML SEQUENCE DIAGRAM (Spacious, Zero-Collision)
# ==============================================================================
def create_tech_sequence_diagram():
    fig, ax = plt.subplots(figsize=(14.0, 9.8), dpi=300)
    ax.set_xlim(0, 140)
    ax.set_ylim(0, 104)
    ax.axis('off')
    fig.patch.set_facecolor(COLOR_BG)

    # Title
    ax.text(70, 100, 'BIỂU ĐỒ TUẦN TỰ ĐỊNH TUYẾN TIN NHẮN (UML SEQUENCE DIAGRAM)', 
            fontsize=13, fontweight='bold', ha='center', va='center', color=COLOR_TEXT_MAIN)
    ax.text(70, 97, 'TCP Socket Length-prefix, RAM Transient Relay & Offline PendingQueue Lifecycle', 
            fontsize=9.5, style='italic', ha='center', va='center', color=COLOR_TEXT_MUTED)

    # Lifeline actors with ample spacing
    actors = [
        ('Client A (Bên gửi)\n[User A]', 24),
        ('DAU Chat Server\n[Transient Relay & FIFO]', 70),
        ('Client B (Bên nhận)\n[User B]', 116)
    ]

    for label, cx in actors:
        r = patches.Rectangle((cx - 16, 87), 32, 7, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.2)
        ax.add_patch(r)
        ax.text(cx, 90.5, label, fontsize=8.8, fontweight='bold', ha='center', va='center', color='#FFFFFF')
        # Dashed lifeline
        ax.plot([cx, cx], [87, 6], color='#94A3B8', linestyle='--', linewidth=1.2)

    # Activation bars (clean white with black borders)
    ax.add_patch(patches.Rectangle((23.2, 38), 1.6, 49, facecolor='#FFFFFF', edgecolor='#0F172A', linewidth=1.2))
    ax.add_patch(patches.Rectangle((69.2, 9), 1.6, 78, facecolor='#FFFFFF', edgecolor='#0F172A', linewidth=1.2))
    ax.add_patch(patches.Rectangle((115.2, 53), 1.6, 29, facecolor='#FFFFFF', edgecolor='#0F172A', linewidth=1.2))
    ax.add_patch(patches.Rectangle((115.2, 9), 1.6, 25, facecolor='#FFFFFF', edgecolor='#0F172A', linewidth=1.2))

    # Self-call at Client A: AppendLocalLog
    draw_orthogonal_arrow(ax, [(24.8, 84), (31, 84), (31, 80), (24.8, 80)], color='#0F172A', lw=1.3, mutation_scale=12)
    ax.text(32.5, 82, '1. AppendLocalLog: YYYY-MM.jsonl & .idx', fontsize=7.5, fontfamily='monospace', va='center', color='#0F172A')

    # Step 1: User A sends MessagePacket to Server
    draw_orthogonal_arrow(ax, [(24.8, 75), (69.2, 75)], color='#0F172A', lw=1.5, mutation_scale=14)
    ax.text(47, 76.8, 'MessagePacket [Len: 4B | Type: 200 | JSON Payload]', fontsize=7.8, fontfamily='monospace', ha='center', color='#0F172A')

    # Self-call at Server: Decode & Route (Cleanly positioned on right of server lifeline!)
    draw_orthogonal_arrow(ax, [(70.8, 71.5), (76, 71.5), (76, 68), (70.8, 68)], color='#334155', lw=1.3, mutation_scale=12)
    ax.text(77.5, 69.8, '2. Parse 4B Header -> Lookup ActiveConnectionMap("User_B")', fontsize=7.5, fontfamily='monospace', va='center', color='#334155')

    # ALT BLOCK 1: ONLINE
    r_alt1 = patches.Rectangle((6, 50), 128, 17, facecolor='#F8FAFC', edgecolor='#0284C7', linewidth=1.2, linestyle='--')
    ax.add_patch(r_alt1)
    ax.text(8, 65, '[ALT: User B is Online - Direct RAM Relay]', fontsize=8, fontweight='bold', color='#0284C7')

    # Server relays to User B
    draw_orthogonal_arrow(ax, [(70.8, 62), (115.2, 62)], color='#0284C7', lw=1.5, mutation_scale=14)
    ax.text(93, 63.8, 'Relay RAM-to-RAM: MessagePacket (Type 200)', fontsize=7.8, fontfamily='monospace', ha='center', color='#0284C7')

    # Self-call at Client B (Cleanly within box boundary!)
    draw_orthogonal_arrow(ax, [(116.8, 59.5), (121.5, 59.5), (121.5, 56.5), (116.8, 56.5)], color='#0284C7', lw=1.3, mutation_scale=12)
    ax.text(122.5, 58, 'Append .jsonl\n& show UI', fontsize=7.0, fontfamily='monospace', va='center', color='#0F172A')

    # User B sends PacketAck to Server
    draw_orthogonal_arrow(ax, [(115.2, 53.5), (70.8, 53.5)], color='#16A34A', lw=1.5, mutation_scale=14)
    ax.text(93, 55, 'PacketAck [Type: 204 | MessageId: UUID]', fontsize=7.8, fontfamily='monospace', ha='center', color='#16A34A')

    # Server notifies User A
    draw_orthogonal_arrow(ax, [(69.2, 52), (24.8, 52)], color='#16A34A', lw=1.4, linestyle=':', mutation_scale=14)
    ax.text(47, 53.5, 'StatusUpdate: "Delivered"', fontsize=7.5, fontfamily='monospace', ha='center', color='#16A34A')

    # ALT BLOCK 2: OFFLINE
    r_alt2 = patches.Rectangle((6, 7), 128, 41, facecolor='#F8FAFC', edgecolor='#DC2626', linewidth=1.2, linestyle='--')
    ax.add_patch(r_alt2)
    ax.text(8, 46, '[ALT: User B is Offline - PendingQueue Lifecycle]', fontsize=8, fontweight='bold', color='#DC2626')

    # Self-call: Enqueue
    draw_orthogonal_arrow(ax, [(70.8, 43.5), (76, 43.5), (76, 40.5), (70.8, 40.5)], color='#DC2626', lw=1.3, mutation_scale=12)
    ax.text(77.5, 42, 'Enqueue FIFO: PendingQueue[User_B].Enqueue(item, TTL = 7d)', fontsize=7.5, fontfamily='monospace', va='center', color='#DC2626')

    # Server ACKs User A: Pending
    draw_orthogonal_arrow(ax, [(69.2, 38), (24.8, 38)], color='#DC2626', lw=1.4, linestyle=':', mutation_scale=14)
    ax.text(47, 39.5, 'PacketAck [Type: 204 | Status: "PendingOnServer"]', fontsize=7.5, fontfamily='monospace', ha='center', color='#DC2626')

    # Time skip separator
    ax.plot([12, 128], [32, 32], color='#94A3B8', linestyle=':', linewidth=1)
    ax.text(70, 32.8, '[ . . . Sau đó: User B mở máy, kết nối Socket & Đăng nhập . . . ]', 
            fontsize=7.8, style='italic', ha='center', va='center', color='#64748B', bbox=dict(boxstyle='square,pad=0.2', fc='#FFFFFF', ec='#CBD5E1', lw=0.8))

    # User B LoginPacket
    draw_orthogonal_arrow(ax, [(115.2, 26), (70.8, 26)], color='#0F172A', lw=1.5, mutation_scale=14)
    ax.text(93, 27.8, 'LoginPacket [Type: 101 | MSSV: "2351220198"]', fontsize=7.8, fontfamily='monospace', ha='center', color='#0F172A')

    # Self-call: Flush
    draw_orthogonal_arrow(ax, [(70.8, 22.5), (76, 22.5), (76, 19.5), (70.8, 19.5)], color='#0284C7', lw=1.3, mutation_scale=12)
    ax.text(77.5, 21, 'Auth OK -> FlushQueue: while(TryDequeue(&msg))', fontsize=7.5, fontfamily='monospace', va='center', color='#0284C7')

    # Flush Delivery to User B
    draw_orthogonal_arrow(ax, [(70.8, 15.5), (115.2, 15.5)], color='#0284C7', lw=1.5, mutation_scale=14)
    ax.text(93, 17.2, 'Flush Delivery: MessagePacket (Type 200)', fontsize=7.8, fontfamily='monospace', ha='center', color='#0284C7')

    # User B final ACK
    draw_orthogonal_arrow(ax, [(115.2, 11), (70.8, 11)], color='#16A34A', lw=1.5, mutation_scale=14)
    ax.text(93, 12.5, 'PacketAck [Type: 204] -> Server giải phóng khỏi RAM', fontsize=7.8, fontfamily='monospace', ha='center', color='#16A34A')

    plt.tight_layout()
    plt.savefig('e:/DAU-Chat-Team5/diagrams/diagram_message_flow.png', dpi=300)
    plt.close()
    print('2. Generated tech sequence diagram (zero collision, clean spacing)')


# ==============================================================================
# 3. FILE STREAMING PIPELINE - BLUEPRINT (Zero Server-Text Collision, Clean Dual Actions)
# ==============================================================================
def create_tech_file_flow_diagram():
    fig, ax = plt.subplots(figsize=(14.0, 9.8), dpi=300)
    ax.set_xlim(0, 140)
    ax.set_ylim(0, 98)
    ax.axis('off')
    fig.patch.set_facecolor(COLOR_BG)

    # Title
    ax.text(70, 95, 'QUY TRÌNH TRUYỀN TỆP PHÂN MẢNH (CHUNKING 64KB) & KIỂM TRA SHA-256', 
            fontsize=13, fontweight='bold', ha='center', va='center', color=COLOR_TEXT_MAIN)
    ax.text(70, 92, 'Zero-Buffer RAM Stream Relay & Dual Receiver Action Specification', 
            fontsize=9.5, style='italic', ha='center', va='center', color=COLOR_TEXT_MUTED)

    # 3 Main Stages: Sender, Server, Receiver (widened gaps)
    # SENDER (Left)
    r_snd = patches.Rectangle((4, 43), 37, 44, facecolor='#F8FAFC', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_snd)
    r_snd_h = patches.Rectangle((4, 83), 37, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_snd_h)
    ax.text(22.5, 85, 'A. SENDER PIPELINE (BÊN GỬI)', fontsize=9.5, fontweight='bold', ha='center', va='center', color='#FFFFFF')

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
        ax.text(6, 78.5 - i * 3.4, st, fontsize=7.8, fontfamily='monospace', color='#0F172A', 
                fontweight='bold' if any(k in st for k in ['1.', '2.', '3.', '4.', '5.']) else 'normal')

    # SERVER (Center)
    r_srv = patches.Rectangle((50, 43), 40, 44, facecolor='#F8FAFC', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_srv)
    r_srv_h = patches.Rectangle((50, 83), 40, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_srv_h)
    ax.text(70, 85, 'B. SERVER TRANSIENT RELAY', fontsize=9.5, fontweight='bold', ha='center', va='center', color='#FFFFFF')

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
        ax.text(52, 78.5 - i * 3.4, st, fontsize=7.8, fontfamily='monospace', color='#0F172A', 
                fontweight='bold' if any(k in st for k in ['1.', '2.', '3.', '4.']) else 'normal')

    # RECEIVER (Right)
    r_rcv = patches.Rectangle((99, 43), 37, 44, facecolor='#F8FAFC', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_rcv)
    r_rcv_h = patches.Rectangle((99, 83), 37, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_rcv_h)
    ax.text(117.5, 85, 'C. RECEIVER PIPELINE (BÊN NHẬN)', fontsize=9.5, fontweight='bold', ha='center', va='center', color='#FFFFFF')

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
        ax.text(101, 78.5 - i * 3.4, st, fontsize=7.8, fontfamily='monospace', color='#0F172A', 
                fontweight='bold' if any(k in st for k in ['1.', '2.', '3.', '4.', '5.', '6.']) else 'normal')

    # Interconnecting horizontal arrows with spacious gaps
    # Step 1: Handshake
    draw_orthogonal_arrow(ax, [(41, 74), (50, 74)], color='#0284C7', lw=1.8, mutation_scale=14)
    ax.text(45.5, 75.8, 'Req 301', fontsize=7.2, fontfamily='monospace', ha='center', color='#0284C7', fontweight='bold')

    draw_orthogonal_arrow(ax, [(90, 74), (99, 74)], color='#0284C7', lw=1.8, mutation_scale=14)
    ax.text(94.5, 75.8, 'Fwd 301', fontsize=7.2, fontfamily='monospace', ha='center', color='#0284C7', fontweight='bold')

    # Step 2: Stream Chunks
    draw_orthogonal_arrow(ax, [(41, 60), (50, 60)], color='#0284C7', lw=2.0, mutation_scale=14)
    ax.text(45.5, 61.8, '64KB Chunk', fontsize=7.2, fontfamily='monospace', ha='center', color='#0284C7', fontweight='bold')

    draw_orthogonal_arrow(ax, [(90, 60), (99, 60)], color='#0284C7', lw=2.0, mutation_scale=14)
    ax.text(94.5, 61.8, 'Relay', fontsize=7.2, fontfamily='monospace', ha='center', color='#0284C7', fontweight='bold')

    # Step 3: ACK return (Placing label cleanly in the gap, ZERO TEXT INSIDE SERVER BOX!)
    draw_orthogonal_arrow(ax, [(99, 48), (90, 48)], color='#16A34A', lw=1.5, mutation_scale=14)
    ax.text(94.5, 49.5, 'Ack 303', fontsize=7.0, fontfamily='monospace', ha='center', color='#16A34A', fontweight='bold')

    draw_orthogonal_arrow(ax, [(50, 48), (41, 48)], color='#16A34A', lw=1.5, mutation_scale=14)
    ax.text(45.5, 49.5, 'Ack 303', fontsize=7.0, fontfamily='monospace', ha='center', color='#16A34A', fontweight='bold')

    # DUAL RECEIVER ACTIONS SECTION (Bottom Box)
    r_act = patches.Rectangle((4, 5), 132, 32, facecolor='#FFFFFF', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_act)
    r_act_h = patches.Rectangle((4, 33), 132, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_act_h)
    ax.text(70, 35, 'D. DUAL RECEIVER ACTIONS - THAO TÁC TƯƠNG TÁC TỆP CỦA BÊN NHẬN (WPF UI)', fontsize=10, fontweight='bold', ha='center', va='center', color='#FFFFFF')

    # Clean vertical connector from Receiver Pipeline to Dual Actions (No diagonal lines!)
    draw_orthogonal_arrow(ax, [(117.5, 43), (117.5, 37)], color='#0284C7', lw=2.2, mutation_scale=16)
    ax.text(117.5, 40, 'Khớp SHA-256 -> Kích hoạt UI Phản hồi Kép', fontsize=7.5, fontfamily='monospace', ha='center', va='bottom', color='#0284C7', fontweight='bold',
            bbox=dict(boxstyle='square,pad=0.2', fc='#FFFFFF', ec='#0284C7', lw=0.8))

    # 2 Action boxes inside
    r_act1 = patches.Rectangle((8, 8), 59, 22, facecolor='#F8FAFC', edgecolor='#334155', linewidth=1.2)
    ax.add_patch(r_act1)
    ax.text(37.5, 26.5, 'HÀNH ĐỘNG 1: "MỞ FILE" (QUICK VIEW / OS LAUNCH)', fontsize=8.8, fontweight='bold', ha='center', color='#0F172A')
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

    r_act2 = patches.Rectangle((73, 8), 59, 22, facecolor='#F8FAFC', edgecolor='#334155', linewidth=1.2)
    ax.add_patch(r_act2)
    ax.text(102.5, 26.5, 'HÀNH ĐỘNG 2: "LƯU VỀ MÁY" (SAVE AS DIALOG)', fontsize=8.8, fontweight='bold', ha='center', color='#0F172A')
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
        ax.text(75, 23.5 - i * 2.2, line, fontsize=7.5, fontfamily='monospace' if 'SaveFileDialog' in line or 'File.Copy' in line else 'sans-serif', color='#334155')

    # Orthogonal distributor connectors inside Dual Actions
    draw_orthogonal_line(ax, [(70, 33), (70, 31)], color='#0284C7', lw=1.5)
    draw_orthogonal_arrow(ax, [(70, 31), (37.5, 31), (37.5, 30)], color='#0284C7', lw=1.5, mutation_scale=12)
    draw_orthogonal_arrow(ax, [(70, 31), (102.5, 31), (102.5, 30)], color='#0284C7', lw=1.5, mutation_scale=12)

    plt.tight_layout()
    plt.savefig('e:/DAU-Chat-Team5/diagrams/diagram_file_streaming_flow.png', dpi=300)
    plt.close()
    print('3. Generated tech file flow diagram (zero server-text collision, clean orthogonal branching)')


# ==============================================================================
# 4. LAN VAULT SYNC - ARCHITECTURE FLOW (Spacious 9-unit gaps, Clean Typography)
# ==============================================================================
def create_tech_lan_vault_diagram():
    fig, ax = plt.subplots(figsize=(14.0, 9.8), dpi=300)
    ax.set_xlim(0, 140)
    ax.set_ylim(0, 98)
    ax.axis('off')
    fig.patch.set_facecolor(COLOR_BG)

    # Title
    ax.text(70, 95, 'SƠ ĐỒ ĐỒNG BỘ ĐA MÁY QUA MẠNG LAN & CHẾ ĐỘ PHÒNG MÁY LAB (LAB PC MODE)', 
            fontsize=13, fontweight='bold', ha='center', va='center', color=COLOR_TEXT_MAIN)
    ax.text(70, 92, 'Socket PacketType 701-706, Server Hot-Cache Vault (1GB Quota) & LRU Engine', 
            fontsize=9.5, style='italic', ha='center', va='center', color=COLOR_TEXT_MUTED)

    # Workstation 1 (Lab PC, Left, Red alert theme)
    r_w1 = patches.Rectangle((4, 43), 36, 44, facecolor='#F8FAFC', edgecolor='#DC2626', linewidth=1.5)
    ax.add_patch(r_w1)
    r_w1_h = patches.Rectangle((4, 83), 36, 4, facecolor='#DC2626', edgecolor='#DC2626', linewidth=1.5)
    ax.add_patch(r_w1_h)
    ax.text(22, 85, 'WORKSTATION 1: PHÒNG THỰC HÀNH LAB', fontsize=9.2, fontweight='bold', ha='center', va='center', color='#FFFFFF')

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
        ax.text(6, 78.5 - i * 3.4, line, fontsize=7.8, fontfamily='monospace' if 'ZipFile' in line or 'Directory' in line else 'sans-serif', 
                color='#991B1B' if 'AUTO-WIPE' in line or 'Xóa sạch' in line else '#0F172A',
                fontweight='bold' if 'AUTO-WIPE' in line or any(k in line for k in ['1.', '2.', '3.', '4.', '5.', '6.']) else 'normal')

    # Internal Auto-Wipe Arrow safely INSIDE Workstation 1 (Zero border cutting!)
    draw_orthogonal_arrow(ax, [(33, 52), (37, 52), (37, 47), (33, 47)], color='#DC2626', lw=1.6, mutation_scale=12)
    ax.text(32, 49.5, 'Kích hoạt\nXóa trắng', fontsize=6.8, ha='right', va='center', color='#DC2626', fontweight='bold')

    # SERVER VAULT (Center - 42 units wide)
    r_v = patches.Rectangle((49, 41), 42, 48, facecolor='#F8FAFC', edgecolor='#0F172A', linewidth=1.5)
    ax.add_patch(r_v)
    r_v_h = patches.Rectangle((49, 85), 42, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.5)
    ax.add_patch(r_v_h)
    ax.text(70, 87, 'SERVER VAULT (HOT-CACHE VÙNG ĐỆM)', fontsize=9.2, fontweight='bold', ha='center', va='center', color='#FFFFFF')

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
        ax.text(51, 80.5 - i * 3.4, line, fontsize=7.8, fontfamily='monospace' if 'latest_snapshot' in line or '70' in line else 'sans-serif', 
                color='#0F172A', fontweight='bold' if 'Hạn ngạch' in line or 'LRU' in line else 'normal')

    # Workstation 2 (New PC / Home Laptop, Green theme)
    r_w2 = patches.Rectangle((100, 43), 36, 44, facecolor='#F8FAFC', edgecolor='#16A34A', linewidth=1.5)
    ax.add_patch(r_w2)
    r_w2_h = patches.Rectangle((100, 83), 36, 4, facecolor='#16A34A', edgecolor='#16A34A', linewidth=1.5)
    ax.add_patch(r_w2_h)
    ax.text(118, 85, 'WORKSTATION 2: MÁY TÍNH MỚI (LAPTOP)', fontsize=9.2, fontweight='bold', ha='center', va='center', color='#FFFFFF')

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
        ax.text(102, 78.5 - i * 3.4, line, fontsize=7.8, fontfamily='monospace' if 'ZipFile' in line or '70' in line else 'sans-serif', 
                color='#15803D' if 'Toàn bộ' in line else '#0F172A',
                fontweight='bold' if 'Toàn bộ' in line or any(k in line for k in ['1.', '2.', '3.', '4.', '5.', '6.', '7.']) else 'normal')

    # Spacious 9-unit connectors between Workstation 1 and Server
    draw_orthogonal_arrow(ax, [(40, 70), (49, 70)], color='#DC2626', lw=2.0, mutation_scale=15)
    ax.text(44.5, 71.8, 'Push (701)', fontsize=7.2, fontfamily='monospace', ha='center', color='#DC2626', fontweight='bold')

    draw_orthogonal_arrow(ax, [(49, 61), (40, 61)], color='#16A34A', lw=1.8, mutation_scale=14)
    ax.text(44.5, 62.8, 'Ack (702)', fontsize=7.2, fontfamily='monospace', ha='center', color='#16A34A', fontweight='bold')

    # Spacious 9-unit connectors between Workstation 2 and Server
    draw_orthogonal_arrow(ax, [(100, 73), (91, 73)], color='#0284C7', lw=1.8, mutation_scale=14)
    ax.text(95.5, 74.8, 'Query (705)', fontsize=7.2, fontfamily='monospace', ha='center', color='#0284C7', fontweight='bold')

    draw_orthogonal_arrow(ax, [(91, 65), (100, 65)], color='#0284C7', lw=1.8, mutation_scale=14)
    ax.text(95.5, 66.8, 'Reply (706)', fontsize=7.2, fontfamily='monospace', ha='center', color='#0284C7', fontweight='bold')

    draw_orthogonal_arrow(ax, [(100, 57), (91, 57)], color='#16A34A', lw=1.8, mutation_scale=14)
    ax.text(95.5, 58.8, 'Pull (703)', fontsize=7.2, fontfamily='monospace', ha='center', color='#16A34A', fontweight='bold')

    draw_orthogonal_arrow(ax, [(91, 49), (100, 49)], color='#16A34A', lw=2.0, mutation_scale=15)
    ax.text(95.5, 50.8, 'Stream (704)', fontsize=7.2, fontfamily='monospace', ha='center', color='#16A34A', fontweight='bold')

    # Bottom summary box: Real Engineering Problem Solving
    r_sum = patches.Rectangle((4, 5), 132, 30, facecolor='#FFFFFF', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_sum)
    r_sum_h = patches.Rectangle((4, 31), 132, 4, facecolor='#1E293B', edgecolor='#0F172A', linewidth=1.4)
    ax.add_patch(r_sum_h)
    ax.text(70, 33, 'GIẢI PHÁP ĐỒNG BỘ ĐA MÁY THỰC TẾ CHO ĐỒ ÁN LẬP TRÌNH MẠNG', fontsize=10, fontweight='bold', ha='center', va='center', color='#FFFFFF')

    sum_points = [
        '• Giải quyết triệt để nghịch lý của Local-First: Người dùng không bị "giam lỏng" dữ liệu trên 1 máy tính duy nhất.',
        '• Bảo vệ tải trọng Server: Server KHÔNG lưu dữ liệu vĩnh viễn; cơ chế Hot-Cache 1GB + LRU giữ cho đĩa cứng Server luôn an toàn.',
        '• Đảm bảo tính trung thực môn Lập trình mạng: Toàn bộ quá trình đồng bộ vận hành trên Raw TCP Socket (PacketType 701-706),',
        '  hoàn toàn không phụ thuộc vào bất kỳ dịch vụ Cloud (Google Drive / Firebase / OneDrive) của bên thứ ba nào.'
    ]
    for i, pt in enumerate(sum_points):
        ax.text(6, 25.5 - i * 4.8, pt, fontsize=8.0, color='#0F172A')

    plt.tight_layout()
    plt.savefig('e:/DAU-Chat-Team5/diagrams/diagram_lan_vault_sync_flow.png', dpi=300)
    plt.close()
    print('4. Generated tech LAN vault diagram (spacious labels, clean typography)')


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
    draw_entity_table(5, 32, 25, 24, 'SystemAuditLogs', [
        ('PK', 'LogId', 'INTEGER'),
        ('FK', 'UserId', 'TEXT(36)'),
        ('', 'ActionType', 'TEXT(50)'),
        ('', 'IpAddress', 'TEXT(45)'),
        ('', 'Timestamp', 'DATETIME')
    ])

    # 6. ClassMembers (x: 73 to 100)
    draw_entity_table(73, 34, 27, 23, 'ClassMembers', [
        ('PK,FK', 'ClassId', 'TEXT(20)'),
        ('PK,FK', 'UserId', 'TEXT(36)'),
        ('', 'JoinedAt', 'DATETIME'),
        ('', 'Status', 'TEXT(20)')
    ])

    # 7. RoomMembers (x: 108 to 135)
    draw_entity_table(108, 34, 27, 23, 'RoomMembers', [
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

    # 2. Users (Lecturer 1) <---> (N) Classes: Direct horizontal link
    draw_orthogonal_line(ax, [(66, 80), (73, 80)], color=COLOR_LINE, lw=1.5)
    draw_cardinality_badge(ax, 68, 80, '1')
    draw_cardinality_badge(ax, 71, 80, 'N')
    # Label placed safely above line so it never touches badges
    ax.text(69.5, 82.8, 'LecturerId', fontsize=7.2, fontfamily='monospace', ha='center', color='#0284C7', fontweight='bold')

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
    draw_orthogonal_line(ax, [(66, 61), (69.5, 61), (69.5, 46), (73, 46)], color=COLOR_LINE, lw=1.5)
    draw_cardinality_badge(ax, 67.5, 61, '1')
    draw_cardinality_badge(ax, 71.5, 46, 'N')

    # 7. Users (1) <---> (N) RoomMembers: DEDICATED CORRIDOR ROUTING UNDERNEATH TABLES!
    # Runs along y=31.5, between ClassMembers bottom (y=34) and Local-First box top (y=24)
    draw_orthogonal_line(ax, [(51.5, 56), (51.5, 31.5), (121.5, 31.5), (121.5, 34)], color='#0284C7', lw=1.5, linestyle='-')
    draw_cardinality_badge(ax, 51.5, 53.5, '1', color='#0284C7')
    draw_cardinality_badge(ax, 121.5, 36.5, 'N', color='#0284C7')
    # Label badge on the corridor (Placed at x=86.5, y=31.5)
    ax.text(86.5, 31.5, ' Quan hệ N - M qua RoomMembers (1 User : N RoomMembers) ', 
            fontsize=7.2, fontfamily='monospace', ha='center', va='center', color='#0284C7', fontweight='bold',
            bbox=dict(boxstyle='square,pad=0.2', fc='#FFFFFF', ec='#0284C7', lw=0.8))

    # 8. Users (1) <---> (N) SystemAuditLogs: Left-angle elbow connector
    draw_orthogonal_line(ax, [(37, 61), (33.5, 61), (33.5, 44), (30, 44)], color=COLOR_LINE, lw=1.5)
    draw_cardinality_badge(ax, 35.5, 61, '1')
    draw_cardinality_badge(ax, 31.5, 44, 'N')

    # ==========================================================================
    # CLIENT LOCAL-FIRST DATA BOUNDARY (Bottom Container)
    # ==========================================================================
    r_client_erd = patches.Rectangle((5, 5), 130, 20, facecolor='#F8FAFC', edgecolor='#16A34A', linewidth=1.5, linestyle='--')
    ax.add_patch(r_client_erd)
    ax.text(70, 22.5, 'KHÔNG GIAN LƯU TRỮ CỤC BỘ CLIENT LOCAL-FIRST (TÁCH BIỆT KHỎI SQLITE SERVER)', 
            fontsize=9.5, fontweight='bold', ha='center', va='center', color='#16A34A')
    ax.text(70, 19.5, '[ LIÊN KẾT LAI: Tham chiếu mềm qua RoomId & UserId - Zero SQL Join qua mạng LAN ]', 
            fontsize=7.5, fontfamily='monospace', ha='center', va='center', color='#16A34A', fontweight='bold')

    client_details = [
        '• Messages/{RoomId}/YYYY-MM.jsonl: Ghi nối tiếp Append-only (Id, RoomId, SenderId, Content, Timestamp, Status)',
        '• Messages/{RoomId}/YYYY-MM.idx: Chỉ mục nhị phân định vị byte offset cố định 16 bytes phục vụ đọc phân trang O(1)',
        '• Files/{RoomId}/*: Lưu trữ toàn bộ tệp CAD, BIM, ảnh đồ án tải về đã xác thực mã băm SHA-256',
        '• file_manifest.jsonl: Danh mục quản lý tệp cục bộ (FileId, FileName, FileSize, Sha256Hash, DownloadedAt, SavedPath)'
    ]
    for i, line in enumerate(client_details):
        ax.text(7, 15.5 - i * 3.3, line, fontsize=7.6, fontfamily='monospace', color='#0F172A')

    # PROMINENT DOWNWARD BRIDGE: Placed at x=44 (under Users left side), ZERO OVERLAP with corridor badge at x=86.5!
    draw_orthogonal_arrow(ax, [(44, 31.5), (44, 25)], color='#16A34A', lw=2.0, mutation_scale=16)

    plt.tight_layout()
    plt.savefig('e:/DAU-Chat-Team5/diagrams/diagram_erd.png', dpi=300)
    plt.close()
    print('5. Generated tech ERD diagram (zero collisions, completely decoupled labels)')


if __name__ == '__main__':
    create_tech_architecture_diagram()
    create_tech_sequence_diagram()
    create_tech_file_flow_diagram()
    create_tech_lan_vault_diagram()
    create_tech_erd_diagram()
    print('All 5 diagrams successfully rebuilt with professional engineering precision!')
