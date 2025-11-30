namespace Companion.Models;

public class WfbConfig
{
    public string Unit { get; set; }
    public string Wlan { get; set; }
    public string Region { get; set; }
    public string Channel { get; set; }
    public int TxPower { get; set; }
    public int DriverTxPowerOverride { get; set; }
    public int Bandwidth { get; set; }
    public int Stbc { get; set; }
    public int Ldpc { get; set; }
    public int McsIndex { get; set; }
    public int Stream { get; set; }
    public long LinkId { get; set; }
    public int UdpPort { get; set; }
    public int RcvBuf { get; set; }
    public string FrameType { get; set; }
    public int FecK { get; set; }
    public int FecN { get; set; }
    public int PoolTimeout { get; set; }
    public string GuardInterval { get; set; }
}