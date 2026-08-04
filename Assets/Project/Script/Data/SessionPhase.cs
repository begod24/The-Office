namespace Office.Data
{
    /// <summary>
    /// Lifecycle of the online session, independent of <see cref="GameState"/>.
    /// A session can exist while the game is still in the lobby, and the game must survive a
    /// session failure without crashing — see Technical Plan §2.6.4 for the same rule applied
    /// to voice.
    /// </summary>
    public enum SessionPhase : byte
    {
        Offline = 0,
        Initialising = 1,
        Creating = 2,
        Joining = 3,
        InSession = 4,
        Leaving = 5,
        Failed = 6
    }
}
