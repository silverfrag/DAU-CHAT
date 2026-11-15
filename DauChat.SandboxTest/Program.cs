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
        Console.WriteLine("=== BẮT ĐẦU TEST XẢ PENDING QUEUE (TV2) ===");

        int port = 9050;
        string ip = "127.0.0.1";

        // 1. Khởi động Server
        var server = new NetworkService();
        await server.StartServerAsync(ip, port);
        Console.WriteLine("[Server] Đã chạy.");

        // ĐÂY LÀ ĐOẠN QUAN TRỌNG BỊ THIẾU: Server lắng nghe và chuyển tiếp tin nhắn
        server.PacketReceived += (s, packet) =>
        {
            Console.WriteLine($"[Server Nhận] Từ: {packet.SenderId} -> Đến: {packet.TargetId} | Loại: {packet.Type}");

            // Nếu là tin nhắn 1-1, Server đẩy vào hàm SendToUserAsync 
            // (Hàm này sẽ tự biết B đang offline để ném vào Queue)
            if (packet.Type == PacketType.DirectMessage && packet.TargetId != null)
            {
                _ = server.SendToUserAsync(packet.TargetId, packet);
            }
        };

        // 2. Client A kết nối và Đăng nhập
        var clientA = new NetworkService();
        clientA.CurrentUserId = "user_A";
        await clientA.ConnectAsync(ip, port);
        await clientA.SendPacketAsync(NetworkPacket.Create(PacketType.LoginRequest, "user_A", "Login", "user_A"));
        await Task.Delay(500); // Đợi Server map UserId

        // 3. Client A gửi tin nhắn cho B (Lúc này B CHƯA ĐĂNG NHẬP)
        Console.WriteLine("\n--- Client A gửi tin cho B (B đang Offline) ---");
        var chatPacket = NetworkPacket.Create(
            PacketType.DirectMessage,
            "user_A",
            payload: "Tin nhắn này gửi lúc B offline. B sẽ nhận được khi đăng nhập!",
            targetId: "user_B"
        );
        await clientA.SendPacketAsync(chatPacket);
        await Task.Delay(1000);

        // 4. Client B bắt đầu kết nối và Đăng nhập
        Console.WriteLine("\n--- Bây giờ Client B mới Đăng nhập ---");
        var clientB = new NetworkService();
        clientB.CurrentUserId = "user_B";
        clientB.PacketReceived += (s, packet) =>
            Console.WriteLine($"[Client B Nhận] Từ {packet.SenderId}: {packet.PayloadJson}");

        await clientB.ConnectAsync(ip, port);

        // Gửi Login, Server sẽ kích hoạt hàm FlushPendingQueueAsync xả tin nhắn
        await clientB.SendPacketAsync(NetworkPacket.Create(PacketType.LoginRequest, "user_B", "Login", "user_B"));

        await Task.Delay(2000); // Đợi xả queue

        Console.WriteLine("\n=== KẾT THÚC TEST ===");
        Console.ReadLine();
    }
}