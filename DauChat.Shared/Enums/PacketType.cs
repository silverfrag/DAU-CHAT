namespace DauChat.Shared.Enums;

/// <summary>
/// Định danh loại gói tin TCP dùng cho PacketDispatcher (TV2) và các service TV3, TV4, TV5.
/// Bám sát các mã module từ MOD_01_01 đến MOD_08_04.
/// </summary>
public enum PacketType
{
    Unknown = 0,
    ErrorResponse = 1,

    /// <summary>Gói xác nhận đã nhận để xóa dữ liệu khỏi PendingQueue (MOD_02_07).</summary>
    PacketAck = 2,

    #region Phân hệ 1: Tài khoản & Phân quyền (TV3 - MOD_01_01 -> MOD_01_13)
    LoginRequest = 101,
    LoginResponse = 102,
    LogoutRequest = 103,
    LogoutResponse = 104,
    ChangePasswordRequest = 105,
    ChangePasswordResponse = 106,
    GetProfileRequest = 107,
    GetProfileResponse = 108,
    UpdateProfileRequest = 109,
    UpdateProfileResponse = 110,
    CreateUserRequest = 111,
    CreateUserResponse = 112,
    UpdateUserRequest = 113,
    UpdateUserResponse = 114,
    LockUserRequest = 115,
    LockUserResponse = 116,
    UnlockUserRequest = 117,
    UnlockUserResponse = 118,
    ResetPasswordRequest = 119,
    ResetPasswordResponse = 120,
    AssignRoleRequest = 121,
    AssignRoleResponse = 122,
    GetUsersRequest = 123,
    GetUsersResponse = 124,
    ForceDisconnectNotice = 125,
    #endregion

    #region Phân hệ 2: Kết nối, Trạng thái & Hàng đợi trung chuyển (TV2 - MOD_02_01 -> MOD_02_07)
    Connect = 200,
    Disconnect = 201,
    Heartbeat = 202,
    PresenceBroadcast = 203,
    PendingRelayFlush = 204,
    #endregion

    #region Phân hệ 3: Chat cá nhân 1-1 (TV4 - MOD_03_01 -> MOD_03_06)
    DirectMessage = 301,
    DirectMessageDelivered = 302,
    #endregion

    #region Phân hệ 4: Phòng chat & Tin nhắn nhóm (TV3 & TV4 - MOD_04_01 -> MOD_04_15)
    CreateRoomRequest = 401,
    CreateRoomResponse = 402,
    UpdateRoomRequest = 403,
    UpdateRoomResponse = 404,
    DeleteRoomRequest = 405,
    DeleteRoomResponse = 406,
    GetRoomMembersRequest = 407,
    GetRoomMembersResponse = 408,
    AddRoomMembersRequest = 409,
    AddRoomMembersResponse = 410,
    RemoveRoomMemberRequest = 411,
    RemoveRoomMemberResponse = 412,
    GetUserRoomsRequest = 413,
    GetUserRoomsResponse = 414,
    LeaveRoomRequest = 415,
    LeaveRoomResponse = 416,
    GroupMessage = 417,
    PinAnnouncementRequest = 418,
    PinAnnouncementBroadcast = 419,
    UnpinAnnouncementRequest = 420,
    UnpinAnnouncementBroadcast = 421,
    RoomChangedNotice = 422,
    #endregion

    #region Phân hệ 5: Truyền tệp theo luồng Chunked Streaming (TV5 - MOD_05_01 -> MOD_05_05)
    FileTransferStart = 501,
    FileChunk = 502,
    FileTransferComplete = 503,
    FileTransferAck = 504,
    FileTransferError = 505,
    #endregion

    #region Phân hệ 6: Quản lý Lớp học (TV3 - MOD_06_01 -> MOD_06_07)
    GetClassesRequest = 601,
    GetClassesResponse = 602,
    CreateClassRequest = 603,
    CreateClassResponse = 604,
    UpdateClassRequest = 605,
    UpdateClassResponse = 606,
    DeleteClassRequest = 607,
    DeleteClassResponse = 608,
    GetClassMembersRequest = 609,
    GetClassMembersResponse = 610,
    AddClassMembersRequest = 611,
    AddClassMembersResponse = 612,
    RemoveClassMembersRequest = 613,
    RemoveClassMembersResponse = 614,
    LinkClassRoomRequest = 615,
    LinkClassRoomResponse = 616,
    #endregion

    #region Phân hệ 8: Quản trị & Giám sát Server (TV2 - MOD_08_01 -> MOD_08_04)
    ServerStartRequest = 801,
    ServerStopRequest = 802,
    ServerControlResponse = 803,
    ServerStatsRequest = 804,
    ServerStatsResponse = 805,
    ConnectedClientsRequest = 806,
    ConnectedClientsResponse = 807,
    ConnectionLogsRequest = 808,
    ConnectionLogsResponse = 809
    #endregion
}

