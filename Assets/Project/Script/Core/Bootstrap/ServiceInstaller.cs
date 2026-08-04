using UnityEngine;

namespace Office.Core
{
    /// <summary>
    /// One installer per assembly that owns services. This is how the composition root stays in
    /// <c>Office.Core</c> while registering services that live in assemblies Core is forbidden
    /// to reference: <see cref="GameBootstrap"/> knows only this base type, and each higher
    /// assembly supplies its own subclass. Dependencies still point downward only.
    /// </summary>
    public abstract class ServiceInstaller : MonoBehaviour
    {
        /// <summary>Lower runs first. Core installs at 0, leave gaps for later insertions.</summary>
        public abstract int Order { get; }

        public abstract void Install();

        /// <summary>Runs in reverse order on teardown. Release sockets, sessions and native handles here.</summary>
        public virtual void Uninstall() { }
    }
}
