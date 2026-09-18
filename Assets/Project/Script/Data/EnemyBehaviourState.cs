namespace Office.Data
{
    public enum EnemyBehaviourState : byte
    {
        Idle = 0,

        Investigating = 1,

        Chasing = 2,

        Attacking = 3,

        Dead = 4,

        // Appended rather than slotted in next to Idle, where it would read better: the value
        // rides a NetworkVariable, and renumbering the ones already on the wire would make two
        // builds disagree about what a byte means. The handshake refuses mismatched builds, so
        // this is belt and braces — but the belt costs nothing.
        Patrolling = 5
    }
}
