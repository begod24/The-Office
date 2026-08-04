namespace Office.Data
{
    /// <summary>
    /// Authoritative phase of the session. Technical Plan §7.1.
    /// Replicated by the server; no system may infer the current phase from anything else.
    /// </summary>
    public enum GameState : byte
    {
        Boot = 0,
        MainMenu = 1,
        Lobby = 2,
        Generating = 3,
        InRun = 4,
        FloorTransition = 5,
        RunComplete = 6,
        RunFailed = 7
    }
}
