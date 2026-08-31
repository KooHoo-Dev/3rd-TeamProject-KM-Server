using System.Net.WebSockets;
using System.Text.Json;

namespace HelloServer;

public partial class Room
{
    private void RegisterLobbyHandlers()
    {
        RegisterHandler<SetGameReadyMessage>(ProtocolHeader.SET_GAME_READY, HandleSetGameReadyAsync);
        RegisterHandler<StartGameMessage>(ProtocolHeader.START_GAME, HandleStartGameAsync);
    }

    #region JOIN_AND_LEAVE

    private async Task<Member> JoinAsync(MemberConnection connection, string id, CancellationToken token)
    {
        string first = await connection.ReceiveTextAsync(token);
        if (string.IsNullOrWhiteSpace(first)) return null;
        
        MessageHeader header = JsonSerializer.Deserialize<MessageHeader>(first);
        if (header == null) return null;
        if (header.Type != ProtocolHeader.HELLO)
        {
            Console.WriteLine($"[{code}] First message is not {ProtocolHeader.HELLO} : {first}");
            return null;
        }
        
        HelloMessage hello = JsonSerializer.Deserialize<HelloMessage>(first);

        Member member = new Member
        {
            Connection = connection,
            LastLogAt = DateTime.Now,
            User = new User
            {
                Id = id,
                Nickname = hello.Nickname.Trim()
            }
        };

        await gate.WaitAsync(token);

        try
        {
            if (members.Count >= MAX_MEMBERS)
            {
                Console.WriteLine(
                    $"[{code}] Entry denied: room is full. " +
                    $"{member.User.Nickname}({member.User.Id})");

                await connection.CloseAsync(
                    WebSocketCloseStatus.PolicyViolation,
                    "Room is full.",
                    token);
                
                return null;
            }

            member.User.IsHost = members.Count == 0;

            List<User> joined = members.Values
                .Select(m => m.User)
                .ToList();

            WelcomeMessage msg = new WelcomeMessage
            {
                RoomCode = code,
                User = member.User,
                Users = joined.ToArray()
            };
            await SendAsync(member, msg);

            members[member.User.Id] = member;
            memberOrder.Add(member.User.Id);

            JoinMessage joinMsg = new JoinMessage { User = member.User };
            await BroadcastAsync(joinMsg, member.User.Id);
        }
        finally
        {
            gate.Release();
        }

        Console.WriteLine($"[{code}] {member.User.Nickname}({member.User.Id}) is joined.");
        return member;
    }

    private async Task LeaveAsync(Member member)
    {
        await gate.WaitAsync();

        try
        {
            string id = member.User.Id;
            
            members.Remove(id, out _);
            memberOrder.Remove(id);

            LeaveMessage msg = new LeaveMessage { Id = id };
            await BroadcastAsync(msg, id);

        }
        finally
        {
            gate.Release();
        }
        
        Console.WriteLine($"[{code}] {member.User.Nickname}({member.User.Id}) is left.");
    }

    #endregion

    #region HANDLE_RECEIVED_MESSAGE

    private async Task HandleSetGameReadyAsync(Member member, SetGameReadyMessage msg)
    {
        await gate.WaitAsync();

        try
        {
            member.User.IsReady = msg.IsReady;

            GameReadySetMessage result = new GameReadySetMessage
            {
                Id = member.User.Id,
                IsReady = member.User.IsReady
            };
            await BroadcastAsync(result);
        }
        finally
        {
            gate.Release();
        }
        
        Console.WriteLine($"[{code}] Is {member.User.Id} ready: {msg.IsReady}");
    }

    private async Task HandleStartGameAsync(Member member, StartGameMessage _)
    {
        await gate.WaitAsync();

        try
        {
            if (isGameStarted) return;                                        // 시작되지 않은 게임만 시작 가능
            if (member.User.IsHost == false) return;                      // 방장만 시작 가능
            if (members.Count < 2) return;                                // 2명 이상이여야 시작 가능
            if (members.Values.Any(m => m.User.IsReady == false)) return; // 모두가 준비 상태여야 시작 가능

            string[] ids = memberOrder.ToArray();

            session = new GameSession(ids);
            isGameStarted = true;

            GameStartedMessage msg = new GameStartedMessage { MemberIds = ids };
            await BroadcastAsync(msg);
        }
        finally
        {
            gate.Release();
        }
    }

    #endregion
}
