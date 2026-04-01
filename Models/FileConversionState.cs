namespace OfiConvert.Models
{
    public enum FileConversionState
    {
        Pending,
        Validating,
        Converting,
        Retrying,
        Paused,
        Completed,
        Error,
        Skipped
    }
}
