namespace RecoverSaveGen3.Lib;

public static class CounterCompare
{
    private const int First = 0;
    public const int Second = 1;
    private const int Same = 2;

    public static int CompareCounters(uint counter1, uint counter2)
    {
        // Uninitialized -- only continue if a rollover case (humanly impossible)
        if (counter1 == uint.MaxValue && counter2 != uint.MaxValue - 1)
            return Second;
        if (counter2 == uint.MaxValue && counter1 != uint.MaxValue - 1)
            return First;

        // Different
        if (counter1 > counter2)
            return First;
        if (counter1 < counter2)
            return Second;

        return Same;
    }
}
