using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Domus.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Domus.Infrastructure.Notifications;

public sealed class ExpoPushOptions
{
    public const string SectionName = "ExpoPush";

    public string ApiUrl { get; set; } = "https://exp.host/--/api/v2/push/send";
    public string? AccessToken { get; set; }
}

public sealed class ExpoPushNotificationSender : IPushNotificationSender
{
    private readonly HttpClient _http;
    private readonly ExpoPushOptions _options;
    private readonly ILogger<ExpoPushNotificationSender> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ExpoPushNotificationSender(
        HttpClient http,
        IOptions<ExpoPushOptions> options,
        ILogger<ExpoPushNotificationSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PushSendResult>> SendAsync(
        IReadOnlyList<PushNotificationMessage> messages,
        CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0)
        {
            return [];
        }

        var results = new List<PushSendResult>(messages.Count);

        foreach (var chunk in messages.Chunk(100))
        {
            var payload = chunk.Select(m => new ExpoPushRequest
            {
                To = m.Token,
                Title = m.Title,
                Body = m.Body,
                Sound = m.Sound,
                Badge = m.Badge,
                Priority = m.Priority,
                ChannelId = m.ChannelId,
                Data = m.Data
            }).ToArray();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (!string.IsNullOrWhiteSpace(_options.AccessToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
                }

                request.Content = JsonContent.Create(payload, options: JsonOptions);
                using var response = await _http.SendAsync(request, cancellationToken);
                var raw = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Expo Push HTTP {Status}: {Body}",
                        (int)response.StatusCode,
                        raw);

                    results.AddRange(chunk.Select(m => new PushSendResult(
                        m.Token,
                        false,
                        ErrorCode: "http_error",
                        ErrorMessage: raw)));
                    continue;
                }

                var parsed = JsonSerializer.Deserialize<ExpoPushResponse>(raw, JsonOptions);
                var tickets = parsed?.Data ?? [];

                for (var i = 0; i < chunk.Length; i++)
                {
                    var message = chunk[i];
                    var ticket = i < tickets.Count ? tickets[i] : null;
                    if (ticket is null)
                    {
                        results.Add(new PushSendResult(message.Token, false, ErrorCode: "missing_ticket"));
                        continue;
                    }

                    if (string.Equals(ticket.Status, "ok", StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new PushSendResult(message.Token, true, ticket.Id));
                        continue;
                    }

                    var errorCode = ticket.Details?.Error ?? ticket.Message ?? "push_error";
                    var shouldRemove = string.Equals(errorCode, "DeviceNotRegistered", StringComparison.OrdinalIgnoreCase);
                    results.Add(new PushSendResult(
                        message.Token,
                        false,
                        ticket.Id,
                        errorCode,
                        ticket.Message,
                        shouldRemove));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Falha ao chamar Expo Push API");
                results.AddRange(chunk.Select(m => new PushSendResult(
                    m.Token,
                    false,
                    ErrorCode: "exception",
                    ErrorMessage: ex.Message)));
            }
        }

        return results;
    }

    private sealed class ExpoPushRequest
    {
        public string To { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? Sound { get; set; }
        public int? Badge { get; set; }
        public string? Priority { get; set; }
        public string? ChannelId { get; set; }
        public IReadOnlyDictionary<string, string>? Data { get; set; }
    }

    private sealed class ExpoPushResponse
    {
        public List<ExpoPushTicket> Data { get; set; } = [];
    }

    private sealed class ExpoPushTicket
    {
        public string Status { get; set; } = string.Empty;
        public string? Id { get; set; }
        public string? Message { get; set; }
        public ExpoPushTicketDetails? Details { get; set; }
    }

    private sealed class ExpoPushTicketDetails
    {
        public string? Error { get; set; }
    }
}
