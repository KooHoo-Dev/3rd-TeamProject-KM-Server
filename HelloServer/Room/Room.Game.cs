namespace HelloServer;

public partial class Room
{
    private GameSession session;
    private bool isGameStarted = false;

    private void RegisterGameHandlers()
    {
        RegisterGameHandler<SetBoardReadyMessage>(ProtocolHeader.SET_BOARD_READY, HandleSetBoardReadyAsync);
        
        RegisterGameHandler<RollDiceMessage>(ProtocolHeader.ROLL_DICE, HandleRollDiceAsync);
        RegisterGameHandler<DrawGoldCardMessage>(ProtocolHeader.DRAW_GOLD_CARD, HandleDrawGoldCardAsync);
        RegisterGameHandler<TurnFinishedMessage>(ProtocolHeader.TURN_FINISHED, HandleTurnFinishedAsync);
        
        RegisterGameHandler<UpdateEconomyMessage>(ProtocolHeader.UPDATE_ECONOMY, HandleUpdateEconomyAsync);
        
        RegisterGameHandler<AddIncapacitationCountMessage>(ProtocolHeader.ADD_INCAPACITATION_COUNT, HandleAddIncapacitationCountAsync);
    }

    private void RegisterGameHandler<T>(string type, Func<Member, T, Task> handler)
    {
        RegisterHandler<T>(type, async (members, message) =>
        {
            await gate.WaitAsync();

            try
            {
                if (session == null) return;
                if (message == null) return;
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
        if (session.ReportBoardReady(member.User.Id) == false)
            return;

        await BroadcastAsync(session.CreateEconomyUpdatedMessage());
        await BroadcastAsync(session.CreateTurnStartedMessage());
    }
    
    private async Task HandleRollDiceAsync(Member member, RollDiceMessage msg)
    {
        DiceRolledMessage result = 
            session.TryRollDice(member.User.Id, msg.TurnId);

        if (result != null) await BroadcastAsync(result);
    }

    private async Task HandleDrawGoldCardAsync(Member member, DrawGoldCardMessage msg)
    {
        GoldCardDrawnMessage result = 
            session.TryDrawGoldCard(member.User.Id, msg.TurnId, msg.CardIds);
        
        if (result != null) await BroadcastAsync(result);
    }

    private async Task HandleTurnFinishedAsync(Member member, TurnFinishedMessage msg)
    {
        bool isAdvanced =
            session.ReportTurnFinished(member.User.Id, msg.TurnId);

        if (isAdvanced == false) return;
        
        object message = 
            session.Phase == SessionPhase.Ended ? 
                new GameEndedMessage() : 
                session.CreateTurnStartedMessage();
        
        await BroadcastAsync(message);
    }

    private async Task HandleUpdateEconomyAsync(Member _, UpdateEconomyMessage msg)
    {
        for (int i = 0; i < msg.Updates.Length; i++)
        {
            EconomyUpdate update = msg.Updates[i];
            session.TryChangeGold(update.UserId, update.Amount);
        }

        EconomyUpdatedMessage result = session.CreateEconomyUpdatedMessage();
        await BroadcastAsync(result);
    }

    private Task HandleAddIncapacitationCountAsync(Member member, AddIncapacitationCountMessage msg)
    {
        session.AddIncapacitationCount(member.User.Id, msg.Count);
        return Task.CompletedTask;
    }

    #endregion

    private async Task ChangeGoldAsync(string memberId, int amount)
    {
        if (session.TryChangeGold(memberId, amount) == false)
            return;
        
        await BroadcastAsync(session.CreateEconomyUpdatedMessage());
    }
}
