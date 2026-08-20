namespace Office.Data
{
    public static class SceneNames
    {
        public const string Boot = "SCN_Boot";
        public const string MainMenu = "SCN_MainMenu";
        public const string Lobby = "SCN_Lobby";
        public const string Sandbox = "SCN_Sandbox";
        public const string RunBase = "SCN_RunBase";

        public static bool IsFrontEnd(string sceneName) =>
            string.IsNullOrEmpty(sceneName) || sceneName is Boot or MainMenu or Lobby;

        public static bool IsGameplay(string sceneName) => !IsFrontEnd(sceneName);
    }
}
