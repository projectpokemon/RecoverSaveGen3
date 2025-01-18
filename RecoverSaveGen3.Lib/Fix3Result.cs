namespace RecoverSaveGen3.Lib;

[Flags]
public enum Fix3Result
{
    None = 0,
    TooSmall = 1,
    TooBig = 2,
    MissingCriticalBlocks = 4,

    MissingBoxBlocks = 8,
    MissingExtraBlocks = 16,
    Inflated = 32,
    Recovered = 64,
}