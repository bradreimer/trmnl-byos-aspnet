namespace TrmnlByos.Models;

public sealed record LogRequest(
    LogEntry[] logs
);

public sealed record LogEntry(
    int id,
    string message,
    string wifi_status,
    long created_at,
    int sleep_duration,
    int refresh_rate,
    int free_heap_size,
    int max_alloc_size,
    string source_path,
    string wake_reason,
    string firmware_version,
    int retry,
    float battery_voltage,
    int source_line,
    string special_function,
    int wifi_signal
);
