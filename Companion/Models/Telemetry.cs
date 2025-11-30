namespace Companion.Models;

// ### telemetry.conf
// ### key=value
public static class Telemetry
{
    public const string Serial = "serial";
    public const string Baud = "baud";
    public const string Router = "router";
    public const string Fps = "fps";
    public const string Wlan = "wlan";
    public const string Bandwidth = "bandwidth";
    public const string Stbc = "stbc";
    public const string Ldpc = "ldpc";
    public const string McsIndex = "mcs_index";
    public const string StreamRx = "stream_rx";
    public const string StreamTx = "stream_tx";
    public const string LinkId = "link_id";
    public const string FrameType = "frame_type";
    public const string PortRx = "port_rx";
    public const string PortTx = "port_tx";
    public const string FecK = "fec_k";
    public const string FecN = "fec_n";
    public const string PoolTimeout = "pool_timeout";
    public const string GuardInterval = "guard_interval";
    public const string OneWay = "one_way";
    public const string Aggregate = "aggregate";
    public const string Channels = "channels";
    public const string Value = "value";
    public const string TelemetryConf = "telemetry.conf";
    public const string RcChannel = "channels";
}

// ### unit: drone or gs
// unit=drone
//
// serial=/dev/ttyS2
// baud=115200
//
// ### router: use simple mavfwd (0) or classic mavlink-routerd (1)
// router=0
//
// wlan=wlan0
// bandwidth=20
// stbc=1
// ldpc=1
// mcs_index=1
// stream_rx=144
// stream_tx=16
// link_id=7669206
// frame_type=data
// port_rx=14551
// port_tx=14550
// fec_k=1
// fec_n=2
// pool_timeout=0
// guard_interval=long
// one_way=false
// aggregate=15
//
// ### for mavfwd: RC override channels to parse after first 4 and call /usr/sbin/channels.sh $ch $val, default 0
// channels=80,100,144,160,200,240,280,320,360,400,440,480,520,560,600,640,680,720,760,800,840,880,920,960,1000