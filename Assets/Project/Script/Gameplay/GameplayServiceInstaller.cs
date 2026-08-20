using Office.Core;

namespace Office.Gameplay
{
    public sealed class GameplayServiceInstaller : ServiceInstaller
    {
        private ImpactEffectPool effects;

        public override int Order => 20;

        public override void Install()
        {
            effects = new ImpactEffectPool();
            ServiceLocator.Register<IImpactEffectPool>(effects);
        }

        public override void Uninstall()
        {
            ServiceLocator.Unregister<IImpactEffectPool>();

            effects?.Clear();
            effects = null;
        }
    }
}
