namespace Companion.Services;

public interface IWfbGsConfigParser
{
    string TxPower { get; set; }
    string GetUpdatedConfigString();
}