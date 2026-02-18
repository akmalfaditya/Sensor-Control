namespace MVCS.Server.Models;

public class CompassLog
{
    public int Id { get; set; }
    public int Heading { get; set; }
    public string Cardinal { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class WaterLevelLog
{
    public int Id { get; set; }
    public double Level { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class PumpLog
{
    public int Id { get; set; }
    public bool IsOn { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class LedLog
{
    public int Id { get; set; }
    public string HexColor { get; set; } = "#000000";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
