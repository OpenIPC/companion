namespace Companion.Models;

public class TelemetryCommands
{
    public const string Extra =
        "sed -i 's/mavfwd --channels \\\"$channels\\\" --master \\\"$serial\\\" --baudrate \\\"$baud\\\" -p 100 -t -a \\\"$aggregate\\\" \\\\/mavfwd --channels \\\"$channels\\\" --master \\\"$serial\\\" --baudrate \\\"$baud\\\" -a \\\"$aggregate\\\" --wait 5 --persist 50 -t \\\\/' /usr/bin/telemetry";
}