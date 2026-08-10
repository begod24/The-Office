namespace Office.Data
{
    public static class SceneNames
    {
        public const string Boot = "SCN_Boot";
        public const string MainMenu = "SCN_MainMenu";
        public const string Lobby = "SCN_Lobby";
        public const string Sandbox = "SCN_Sandbox";
        public const string RunBase = "SCN_RunBase";

        /// <summary>
        /// The screens in front of the game: boot, the menu and the lobby. Everything else is
        /// a place the player stands in.
        /// </summary>
        /// <remarks>
        /// Asked this way round on purpose. The front end is a closed set that builders own,
        /// while levels are authored, arrive without anyone editing code, and are named by
        /// whoever adds them — so a list of gameplay scenes would be out of date the first
        /// time someone adds a floor. Menu music, ambience and anything else that belongs to
        /// one side of that line therefore keeps working for a scene nobody has written yet.
        /// <para>
        /// An unnamed scene counts as front end: the failure that follows is menu music one
        /// beat too long, rather than a level that loads in silence with nothing logged.
        /// </para>
        /// </remarks>
        public static bool IsFrontEnd(string sceneName) =>
            string.IsNullOrEmpty(sceneName) || sceneName is Boot or MainMenu or Lobby;

        public static bool IsGameplay(string sceneName) => !IsFrontEnd(sceneName);
    }
}
