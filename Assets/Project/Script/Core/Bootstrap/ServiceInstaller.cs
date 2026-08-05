using UnityEngine;

namespace Office.Core
{
    public abstract class ServiceInstaller : MonoBehaviour
    {
        public abstract int Order { get; }

        public abstract void Install();

        public virtual void Uninstall() { }
    }
}
