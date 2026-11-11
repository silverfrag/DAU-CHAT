using DauChat.Core.AuthAndRooms;
using DauChat.Core.Networking;
using DauChat.Shared.Constants;
using DauChat.Shared.Interfaces;

Console.WriteLine("=================================================");
Console.WriteLine("  DAU CHAT SERVER - ĐH KIẾN TRÚC ĐÀ NẴNG (TEAM 5)");
Console.WriteLine("=================================================");
Console.WriteLine($"Cổng mặc định : {ProtocolConstants.DefaultServerHost}:{ProtocolConstants.DefaultServerPort}");
Console.WriteLine($"Chunk size    : {ProtocolConstants.DefaultFileChunkSizeBytes / 1024} KB");
Console.WriteLine();

INetworkService networkService = new NetworkService();
IAuthRoomService authRoomService = new AuthRoomService(networkService);

Console.WriteLine("Khung Server đã sẵn sàng:");
Console.WriteLine("- TV2: Triển khai TcpListener, PacketDispatcher, PendingQueue (FIFO+TTL) tại DauChat.Core/Networking & DauChat.Server.");
Console.WriteLine("- TV3: Triển khai Server Metadata Store (SQLite/JSON) cho Users, Roles, Classes, Rooms, RoomMembers.");
