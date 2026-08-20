using NUnit.Framework;
using Office.Data;

namespace Office.Tests.EditMode
{
    public sealed class SceneNamesTests
    {
        [Test]
        public void BootMenuAndLobby_AreFrontEnd()
        {
            Assert.IsTrue(SceneNames.IsFrontEnd(SceneNames.Boot));
            Assert.IsTrue(SceneNames.IsFrontEnd(SceneNames.MainMenu));
            Assert.IsTrue(SceneNames.IsFrontEnd(SceneNames.Lobby));
        }

        [Test]
        public void TheSandboxIsGameplay()
        {
            Assert.IsTrue(SceneNames.IsGameplay(SceneNames.Sandbox));
        }

        [Test]
        public void AnAuthoredLevelIsGameplayWithoutBeingListed()
        {
            Assert.IsTrue(SceneNames.IsGameplay("SCN_Level_1"));
            Assert.IsTrue(SceneNames.IsGameplay("SCN_Floor_Accounting"));
            Assert.IsTrue(SceneNames.IsGameplay(SceneNames.RunBase));
        }

        [Test]
        public void AnUnnamedSceneIsFrontEnd()
        {
            Assert.IsTrue(SceneNames.IsFrontEnd(null));
            Assert.IsTrue(SceneNames.IsFrontEnd(string.Empty));
        }
    }
}
