namespace HelloServer;

public partial class Room
{
    private GameSession session;
    private bool isGameStarted = false;

    private void RegisterGameHandlers()
    {
        RegisterGameHandler<SetBoardReadyMessage>(ProtocolHeader.SET_BOARD_READY, HandleSetBoardReadyAsync);
        RegisterGameHandler<RollDiceMessage>(ProtocolHeader.ROLL_DICE, HandleRollDiceAsync);
        RegisterGameHandler<TurnFinishedMessage>(ProtocolHeader.TURN_FINISHED, HandleTurnFinishedAsync);
    }

    private void RegisterGameHandler<T>(string type, Func<Member, T, Task> handler)
    {
        RegisterHandler<T>(type, async (members, message) =>
        {
            await gate.WaitAsync();

            try
            {
                if (session == null) return;
                await handler(members, message);
            }
            finally
            {
                gate.Release();
            }
        });
    }

    #region HANDLE_RECEIVED_MESSAGE

    private async Task HandleSetBoardReadyAsync(Member member, SetBoardReadyMessage _)
    {
        if (session.ReportBoardReady(member.User.Id)) 
            await BroadcastAsync(session.CreateTurnStartedMessage());
    }
    
    private async Task HandleRollDiceAsync(Member member, RollDiceMessage msg)
    {
        if (msg == null) return;

        DiceRolledMessage result = 
            session.TryRollDice(member.User.Id, msg.TurnId);

        if (result != null) await BroadcastAsync(result);
    }

    private async Task HandleTurnFinishedAsync(Member member, TurnFinishedMessage msg)
    {
        if (msg == null) return;

        bool isAdvanced =
            session.ReportTurnFinished(member.User.Id, msg.TurnId);

        if (isAdvanced == false) return;
        
        object message = 
            session.Phase == SessionPhase.Ended ? 
                new GameEndedMessage() : 
                session.CreateTurnStartedMessage();
        
        await BroadcastAsync(message);
    }

    #endregion
}
