namespace Office.Data
{
    /// <summary>
    /// What a connected player is, as opposed to what their body is doing. It outlives the
    /// avatar: bodies are despawned at the end of every run and respawned at the start of the
    /// next, and this is the thing that remembers across the gap.
    ///
    /// Spectating is deliberately not a value here. A dead player watching a teammate is not a
    /// fifth thing they can be — it is what Dead looks like from inside their own camera, and
    /// SpectatorCamera already derives it from the same vitals this does. Two sources for one
    /// fact is how they come to disagree.
    /// </summary>
    public enum PlayerStatus : byte
    {
        // Connected, no body: the lobby, the loading screen, and the gap between two runs.
        Lobby = 0,

        Alive = 1,

        Downed = 2,

        Dead = 3
    }
}
