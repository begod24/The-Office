namespace Office.Data
{
    /// <summary>
    /// Scene names in one place so no system holds a magic string. Technical Plan §3.3.
    /// <see cref="Boot"/> is build index 0 and never unloads; everything else is additive.
    /// </summary>
    public static class SceneNames
    {
        public const string Boot = "SCN_Boot";
        public const string MainMenu = "SCN_MainMenu";
        public const string Lobby = "SCN_Lobby";
        public const string Sandbox = "SCN_Sandbox";
        public const string RunBase = "SCN_RunBase";
    }
}
