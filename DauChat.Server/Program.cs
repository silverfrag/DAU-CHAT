using DauChat.Core.AuthAndRooms;
using DauChat.Core.Networking;
using DauChat.Server.Metadata;
using DauChat.Server.Vault;
using DauChat.Shared.Constants;
using DauChat.Shared.Interfaces;

Console.WriteLine("=================================================");
Console.WriteLine("  DAU CHAT SERVER - ĐH KIẾN TRÚC ĐÀ NẴNG (TEAM 5)");
Console.WriteLine("=================================================");
Console.WriteLine($"Cổng mặc định : {ProtocolConstants.DefaultServerHost}:{ProtocolConstants.DefaultServerPort}");
Console.WriteLine($"Chunk size    : {ProtocolConstants.DefaultFileChunkSizeBytes / 1024} KB");
Console.WriteLine();

var databasePath = Path.Combine(AppContext.BaseDirectory, "Data", "dauchat-metadata.db");
using var metadataStore = new MetadataStore(databasePath);
metadataStore.SeedAdmin("admin", "Admin@123", "System Administrator", "admin@dau.edu.vn");

var networkService = new NetworkService();
var metadataHandler = new MetadataRequestHandler(databasePath);
networkService.PacketReceived += metadataHandler.HandlePacket;

var vaultHandler = new ServerVaultHandler();
networkService.PacketReceived += vaultHandler.HandlePacket;

IAuthRoomService authRoomService = new AuthRoomService(networkService);

Console.WriteLine("Metadata SQLite đã sẵn sàng.");
Console.WriteLine("Tài khoản phát triển: admin / Admin@123");
try
{
    var serverResult = await networkService.StartServerAsync(ProtocolConstants.DefaultServerHost, ProtocolConstants.DefaultServerPort);
    Console.WriteLine(serverResult.Success ? "Socket Server đã khởi động." : $"Socket Server chưa khởi động: {serverResult.Message}");
}
catch (NotImplementedException)
{
    Console.WriteLine("Socket Server chưa chạy: cần tích hợp NetworkService hoàn chỉnh từ TV2.");
}

Console.WriteLine("TV3 Metadata Handler đã đăng ký.");
Console.WriteLine("Nhấn Ctrl+C để dừng.");
await Task.Delay(Timeout.Infinite);
