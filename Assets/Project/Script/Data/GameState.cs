namespace Office.Data
{
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
