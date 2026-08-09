using Office.Core;

namespace Office.Gameplay
{
    /// <summary>
    /// Registers the services that gameplay owns. Runs from <c>GameBootstrap</c> in the boot
    /// scene, alongside the network installer.
    /// </summary>
    /// <remarks>
    /// Ordered after the network installer so that anything wanting the network object pool at
    /// install time finds it. Nothing here needs that yet — the ordering is stated so the next
    /// service added does not have to discover it the hard way.
    /// </remarks>
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

            // Destroys the parked instances and the persistent root with them. Without this a
            // domain reload would leave a "~ImpactEffects" object behind every play session.
            effects?.Clear();
            effects = null;
        }
    }
}
