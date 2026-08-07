namespace Domus.Application.Devices;

public sealed class HistoryRetentionOptions
{
    public const string SectionName = "HistoryRetention";

    /// <summary>Quantos dias manter comandos/eventos (histórico do app).</summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>Intervalo entre execuções do worker de limpeza.</summary>
    public int IntervalHours { get; set; } = 24;

    /// <summary>Máximo de linhas apagadas por tabela em cada lote.</summary>
    public int BatchSize { get; set; } = 2000;
}
