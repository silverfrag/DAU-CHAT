# DAU Chat - Yeu cau va kien truc du an

## 1. Thong tin du an

- **Truong:** Dai hoc Kien truc Da Nang
- **Hoc phan:** Lap trinh mang
- **De tai:** Ung dung Chat noi bo DAU Chat
- **Nhom:** Team 5
- **Nen tang:** C# / .NET 10 / WPF / TCP Socket
- **Kien truc:** Local-First Client + Transient Relay Server

## 2. Phan cong thanh vien

| Thanh vien | Pham vi |
|---|---|
| TV1 - Bui Tran Nhat Hung | Frontend Client UI va System Integration |
| TV2 - Phan Trung Nghia | Networking, Socket Server Core, Transient Queue |
| TV3 - Nguyen Thanh Long | Account, Auth, RBAC, Class/Room, Server Metadata |
| TV4 - Duong Thi Yen Nhi | Messaging Engine, Local-First Message Log |
| TV5 - Hong Quoc Tuan | Chunked File Streaming, Local Storage, Backup ZIP |

## 3. Quy tac pham vi code

- `DauChat.Shared` la thu muc dung chung va read-only doi voi TV2-TV5.
- Khong tu y doi ten interface, DTO, `NetworkPacket` hoac `PacketType`.
- TV1 doc quyen sua `DauChat.Client.UI` va toan bo XAML/UI logic.
- TV2 chi sua `DauChat.Core/Networking` va phan Socket/Relay cua `DauChat.Server`.
- TV3 chi sua `DauChat.Core/AuthAndRooms` va phan Metadata cua `DauChat.Server`.
- TV4 chi sua `DauChat.Core/Messaging`.
- TV5 chi sua `DauChat.Core/FileAndStorage`.
- TV2-TV5 khong tao cua so XAML; dung `DauChat.SandboxTest` de kiem thu logic.
- TV3, TV4, TV5 khong tu tao ket noi TCP; bat buoc dung `INetworkService` cua TV2.

## 4. Cay chuc nang

### MOD_01 - Account, Authentication va RBAC (TV3)

- `MOD_01_01`: Dang nhap.
- `MOD_01_02`: Dang xuat.
- `MOD_01_03`: Doi mat khau.
- `MOD_01_04`: Xem thong tin ca nhan.
- `MOD_01_05`: Cap nhat thong tin ca nhan.
- `MOD_01_06`: Admin them tai khoan.
- `MOD_01_07`: Admin sua tai khoan.
- `MOD_01_08`: Admin khoa tai khoan va ngat phien online.
- `MOD_01_09`: Admin mo khoa tai khoan.
- `MOD_01_10`: Admin cap lai mat khau.
- `MOD_01_11`: Gan quyen Admin.
- `MOD_01_12`: Gan quyen Giang vien.
- `MOD_01_13`: Gan quyen Sinh vien.

Quyen vai tro:

| Chuc nang | Admin | Giang vien | Sinh vien |
|---|---:|---:|---:|
| Dang nhap, dang xuat, doi mat khau | Co | Co | Co |
| Xem/cap nhat ho so ca nhan | Co | Co | Co |
| Quan tri tai khoan va RBAC | Co | Khong | Khong |
| Quan ly lop | Co | Theo phan cong | Khong |
| Quan ly phong/thanh vien | Co | Co | Khong |
| Gui va nhan tin nhan | Co | Co | Co |
| Truyen va quan ly file | Co | Co | Co |

### MOD_02 - Networking va Transient Queue (TV2)

- Ket noi va ngat ket noi TCP.
- Multi-client async.
- Length-prefix framing: 4 byte do dai + payload.
- Online presence va heartbeat.
- Relay RAM-to-RAM khi nguoi nhan online.
- Pending Queue FIFO khi nguoi nhan offline.
- Xoa packet khi nhan ACK hoac het TTL.
- Quan ly server stats, client connection va connection log.

### MOD_03 - Chat ca nhan (TV4)

- Mo va liet ke hoi thoai 1-1.
- Gui va nhan tin nhan.
- Doc lich su tu local.
- Xoa lich su local.

### MOD_04 - Phong chat (TV3 + TV4)

TV3 quan ly metadata phong:

- `MOD_04_01`: Tao phong.
- `MOD_04_02`: Sua phong.
- `MOD_04_03`: Xoa phong.
- `MOD_04_04`: Xem thanh vien.
- `MOD_04_05`: Them thanh vien.
- `MOD_04_06`: Xoa thanh vien.

TV4 quan ly chat trong phong:

- Xem phong dang tham gia.
- Vao/rời phong.
- Broadcast va nhan tin nhan nhom.
- Doc/xoa lich su local cua phong.
- Ghim va bo ghim thong bao quan trong cua giang vien (`MOD_04_14`, `MOD_04_15`).

### MOD_05 - Truyen file theo stream (TV5)

- Cat file thanh chunk 64 KB.
- Ghi chunk vao file `.part`.
- Ghep file sau khi nhan du chunk.
- Kiem tra SHA-256.
- Theo doi tien trinh.
- Liet ke, mo va xoa file local.

### MOD_06 - Lop hoc va nhom hoc (TV3)

- `MOD_06_01`: Xem danh sach lop.
- `MOD_06_02`: Them lop.
- `MOD_06_03`: Sua lop.
- `MOD_06_04`: Xoa lop.
- `MOD_06_05`: Xem thanh vien lop.
- `MOD_06_06`: Them/xoa thanh vien lop.
- `MOD_06_07`: Tao va lien ket phong chat theo lop.

### MOD_07 - Local-First Storage (TV4 + TV5)

- Tin nhan luu append-only trong file `.jsonl`.
- Doc lich su theo trang/lazy-load.
- File co the co chi muc `.idx`.
- Tai lieu luu trong `file_manifest.jsonl`.
- Thong ke dung luong theo phong/hoi thoai.
- Dọn file cu theo chien luoc Oldest-first.
- Backup va restore bang file `.zip`.

### MOD_08 - Quan tri Server (TV2)

- Khoi dong/dung Socket Server.
- Xem IP, port, uptime va stats.
- Xem client dang ket noi.
- Xem log ket noi/ngat ket noi va bo dem packet.

## 5. Kien truc luu tru Local-First

### 5.1. Server Metadata - TV3

Server chi luu du lieu nhe va ben vung:

- Users.
- Roles.
- Classes.
- ClassMembers.
- Rooms.
- RoomMembers.

Co the dung SQLite hoac JSON co khoa luong. Du an hien tai dung SQLite qua `Microsoft.Data.Sqlite`.

Server Metadata **khong luu** lich su tin nhan va file dai han.

### 5.2. Server Transient Relay - TV2

- Khi user online: chuyen tiep truc tiep tu socket nguoi gui sang socket nguoi nhan.
- Khi user offline: dua packet vao FIFO Pending Queue.
- Moi packet pending co TTL.
- Xoa packet sau ACK hoac khi het TTL.
- Server khong tro thanh kho luu lich su chat.

### 5.3. Client Local-First - TV4/TV5

Tin nhan:

```text
LocalData/{UserId}/Messages/{RoomOrPartnerId}/YYYY-MM.jsonl
LocalData/{UserId}/Messages/{RoomOrPartnerId}/YYYY-MM.idx
```

File:

```text
LocalData/{UserId}/Files/{RoomOrPartnerId}/
LocalData/{UserId}/Files/file_manifest.jsonl
```

Tin nhan moi duoc ghi bang `FileMode.Append`. File nhan duoc ghi tung chunk vao `.part`, sau khi SHA-256 hop le thi doi ten thanh file chinh thuc.

## 6. Server Metadata cua TV3

### 6.1. Bang du lieu

- `Users`: tai khoan, password hash, ho ten, email, role, status, class.
- `Classes`: ma lop, ten lop, khoa, khoa/nganh, giang vien.
- `ClassMembers`: quan he lop-nguoi dung.
- `Rooms`: phong chat, owner, loai phong, class lien ket.
- `RoomMembers`: quan he phong-nguoi dung.

### 6.2. Bao mat mat khau

- Khong luu plain text.
- Dung PBKDF2-SHA256.
- Moi mat khau co salt rieng.
- So sanh hash bang constant-time comparison.

### 6.3. Kiem tra quyen

RBAC phai duoc kiem tra o server, khong chi o UI/client. Client khong duoc tu quyet dinh minh la Admin.

Khi Admin khoa tai khoan:

```text
Cap nhat Users.Status = Locked
		↓
Goi INetworkService.KickUserAsync
		↓
TV2 dong socket cua user
		↓
Client nhan ForceDisconnectNotice
```

## 7. Nguyen tac giao tiep packet

TV3 tao packet va gui qua `INetworkService.SendAndWaitAsync`:

```text
AuthRoomService
	↓
NetworkPacket request
	↓
INetworkService cua TV2
	↓
PacketDispatcher
	↓
Metadata Handler
	↓
SQLite
	↓
NetworkPacket response
	↓
OperationResult<T>
```

Khong tao `TcpClient` trong `DauChat.Core/AuthAndRooms`.

## 8. Mapping packet cua TV3

| Nghiep vu | Request | Response |
|---|---|---|
| Login | `LoginRequest` | `LoginResponse` |
| Logout | `LogoutRequest` | `LogoutResponse` |
| Doi mat khau | `ChangePasswordRequest` | `ChangePasswordResponse` |
| Profile | `GetProfileRequest` | `GetProfileResponse` |
| User | `CreateUserRequest`, `UpdateUserRequest` | Response tuong ung |
| Khoa user | `LockUserRequest` | `LockUserResponse` |
| Role | `AssignRoleRequest` | `AssignRoleResponse` |
| Room | `CreateRoomRequest`, `UpdateRoomRequest` | Response tuong ung |
| Thanh vien room | `GetRoomMembersRequest`, `AddRoomMembersRequest` | Response tuong ung |
| Class | `CreateClassRequest`, `UpdateClassRequest` | Response tuong ung |
| Thanh vien class | `GetClassMembersRequest`, `AddClassMembersRequest` | Response tuong ung |
| Link room | `LinkClassRoomRequest` | `LinkClassRoomResponse` |

## 9. Cau truc solution

```text
DauChat.Shared/       DTO, enum, interface, NetworkPacket - read-only
DauChat.Core/
  Networking/         TV2
  AuthAndRooms/       TV3
  Messaging/          TV4
  FileAndStorage/     TV5
DauChat.Server/       Socket Relay + Metadata SQLite
DauChat.Client.UI/    WPF UI - TV1
DauChat.SandboxTest/  Kiem thu logic TV2-TV5
```

## 10. Cac file TV3 da trien khai

- `DauChat.Core/AuthAndRooms/AuthRoomService.cs`: tao request, gui qua `INetworkService`, doc response va phat event metadata.
- `DauChat.Server/Metadata/MetadataStore.cs`: tao schema SQLite, seed Admin, hash/verify password.
- `DauChat.Server/Program.cs`: khoi tao MetadataStore.
- `DauChat.Server/DauChat.Server.csproj`: them package `Microsoft.Data.Sqlite`.

## 11. Tai khoan phat trien ban dau

```text
Username: admin
Password: Admin@123
```

Can thay doi mat khau nay trong moi moi truong that.

## 12. Kiem thu toi thieu

- Login dung va sai mat khau.
- Tu choi login khi user bi khoa.
- Tao user trung username.
- Chi Admin duoc khoa user va gan role.
- Them thanh vien khong bi trung.
- Khong cho user khong co quyen sua room/class.
- Tao room theo class va dong bo thanh vien.
- Password khong xuat hien trong database dang plain text.
- Pending packet bi xoa sau ACK/TTL.
- File nhan sai SHA-256 bi tu choi.

## 13. Lenh build va chay

```powershell
dotnet build DauChat.sln
dotnet run --project .\DauChat.Server\DauChat.Server.csproj
dotnet run --project .\DauChat.SandboxTest\DauChat.SandboxTest.csproj
dotnet run --project .\DauChat.Client.UI\DauChat.Client.UI.csproj
```

> Luu y: chuc nang end-to-end can TV2 hoan thien `NetworkService`, `PacketDispatcher` va vong lap Socket Server. TV3 da hoan thien phan request/response va Metadata SQLite trong pham vi code cua minh.
