namespace Office.Data
{
    public enum SessionPhase : byte
    {
        Offline = 0,
        Initialising = 1,
        Creating = 2,
        Joining = 3,
        InSession = 4,
        Leaving = 5,
        Failed = 6
    }
}
