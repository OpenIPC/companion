namespace Companion.Models;

/**
 * https://github.com/svpcom/wfb-ng
 * https://github.com/OpenIPC/firmware/tree/master/general/package/wifibroadcast-ng/files
 */
public class WfbYaml
{
    public const string WfbTxPower = "wireless.txpower";
    public const string WfbChannel = "wireless.channel";
    public const string WfbBandwidth = "wireless.width";
    public const string WfbMlink = "wireless.mlink";
    
    public const string BroadcastMcsIndex = "broadcast.mcs_index"; 
    public const string BroadcastFecK = "broadcast.fec_k";
    public const string BroadcastFecN = "broadcast.fec_n";
    public const string BroadcastStbc = "broadcast.stbc";
    public const string BroadcastLdpc = "broadcast.ldpc";
    
    public const string TelemetrySerialPort = "telemetry.serial";
    public const string TelemetryRouter = "telemetry.router";
    public const string TelemetryOsdFps = "telemetry.osd_fps";
    
    
    
}