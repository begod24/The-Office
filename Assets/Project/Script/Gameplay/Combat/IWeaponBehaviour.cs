using Office.Data;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    public interface IWeaponBehaviour
    {
        WeaponAim Probe(in WeaponContext context);

        WeaponOutcome ServerResolve(in WeaponContext context, in WeaponAim aim,
            NetworkManager manager, out Vector3 point);
    }

    public enum WeaponOutcome : byte
    {
        Missed = 0,

        Absorbed = 1,

        Connected = 2
    }

    public readonly struct WeaponContext
    {
        public readonly CombatConfig Config;

        public readonly WeaponProfile Profile;

        public readonly Transform Body;

        public readonly Transform Aim;

        public readonly ulong AttackerClientId;

        public WeaponContext(CombatConfig config, in WeaponProfile profile, Transform body,
            Transform aim, ulong attackerClientId)
        {
            Config = config;
            Profile = profile;
            Body = body;
            Aim = aim;
            AttackerClientId = attackerClientId;
        }
    }
}
