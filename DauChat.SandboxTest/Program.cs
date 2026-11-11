using DauChat.Core.Networking;
using DauChat.Shared.Enums;
using DauChat.Shared.Models;
using DauChat.Shared.Networking;

namespace DauChat.SandboxTest;

internal class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== BẮT ĐẦU TEST MẠNG TV2 ===");

        int port = 9050; // Cổng chuẩn trong ProtocolConstants của TV1
        string ip = "127.0.0.1";

        // 1. Khởi động Server
        var server = new NetworkService();
        Console.WriteLine("[Server] Đang khởi động...");
        var startResult = await server.StartServerAsync(ip, port);
        if (!startResult.Success)
        {
            Console.WriteLine($"[Server] Lỗi: {startResult.Message}");
            return;
        }
        Console.WriteLine($"[Server] Đã chạy tại {ip}:{port}");

        // Đăng ký sự kiện Server nhận tin
        server.PacketReceived += (s, packet) =>
        {
            Console.WriteLine($"[Server Nhận] Từ: {packet.SenderId} -> Đến: {packet.TargetId} | Loại: {packet.Type}");

            // Nếu Client gửi tin, Server tự động Relay (chuyển tiếp) tới người nhận
            if (packet.TargetId != null && packet.Type == PacketType.DirectMessage)
            {
                Console.WriteLine($"[Server] Đang Relay tin nhắn tới {packet.TargetId}...");
                _ = server.SendToUserAsync(packet.TargetId, packet);
            }
        };

        // 2. Tạo Client A và kết nối
        var clientA = new NetworkService();
        clientA.CurrentUserId = "user_A";
        clientA.PacketReceived += (s, packet) => Console.WriteLine($"[Client A Nhận] Từ {packet.SenderId}: {packet.PayloadJson}");

        Console.WriteLine("[Client A] Đang kết nối...");
        await clientA.ConnectAsync(ip, port);

        // Giả lập Login cho A (Dùng LoginRequest như file Enum của TV1)
        var loginA = NetworkPacket.Create(PacketType.LoginRequest, "user_A", "Login", targetId: "user_A");
        await clientA.SendPacketAsync(loginA);

        // 3. Tạo Client B và kết nối
        var clientB = new NetworkService();
        clientB.CurrentUserId = "user_B";
        clientB.PacketReceived += (s, packet) => Console.WriteLine($"[Client B Nhận] Từ {packet.SenderId}: {packet.PayloadJson}");

        Console.WriteLine("[Client B] Đang kết nối...");
        await clientB.ConnectAsync(ip, port);

        // Giả lập Login cho B
        var loginB = NetworkPacket.Create(PacketType.LoginRequest, "user_B", "Login", targetId: "user_B");
        await clientB.SendPacketAsync(loginB);

        // Đợi 1 chút để Server kịp xử lý login
        await Task.Delay(1000);

        // 4. Client A gửi tin nhắn cho Client B
        Console.WriteLine("\n--- Client A gửi tin nhắn cho Client B ---");
        var chatPacket = NetworkPacket.Create(
            PacketType.DirectMessage, // Dùng DirectMessage như file Enum của TV1
            "user_A",
            payload: "Chào B, đây là tin nhắn test mạng Local-First!",
            targetId: "user_B"
        );
        await clientA.SendPacketAsync(chatPacket);

        // Đợi 2 giây để xem kết quả
        await Task.Delay(2000);

        Console.WriteLine("\n=== KẾT THÚC TEST ===");
        Console.ReadLine();
    }
}