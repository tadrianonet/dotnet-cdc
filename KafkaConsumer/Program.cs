using Confluent.Kafka;
using System.Collections.Concurrent;
using System.Text.Json;

namespace KafkaConsumer;

public class EventoEcommerce
{
    public string Timestamp { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? ProductId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class EstatisticasRealTime
{
    public int TotalEventos { get; set; }
    public ConcurrentDictionary<string, int> EventosPorTipo { get; } = new();
    public ConcurrentDictionary<string, int> EventosPorUsuario { get; } = new();
    public decimal ValorTotal { get; set; }
    public ConcurrentDictionary<string, bool> SessoesAtivas { get; } = new();
}

class Program
{
    private static readonly EstatisticasRealTime Stats = new();
    private static readonly object StatsLock = new();

    static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 Iniciando consumidor analytics (.NET 10)...");

        var config = new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = "analytics-processor-dotnet",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            ClientId = "dotnet-consumer"
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("eventos-paralelos");

        // Task para imprimir estatísticas periodicamente
        var statsTask = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(30000); // A cada 30 segundos
                ImprimirEstatisticas();
            }
        });

        try
        {
            int contador = 0;
            while (true)
            {
                var consumeResult = consumer.Consume(TimeSpan.FromMilliseconds(1000));
                
                if (consumeResult?.Message != null)
                {
                    ProcessarEvento(consumeResult);
                    contador++;

                    // Imprimir estatísticas a cada 10 eventos
                    if (contador % 10 == 0)
                    {
                        ImprimirEstatisticas();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("⏹️  Parando consumidor...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro: {ex.Message}");
        }
        finally
        {
            consumer.Close();
            ImprimirEstatisticas();
            Console.WriteLine("✅ Consumidor finalizado!");
        }
    }

    private static void ProcessarEvento(ConsumeResult<string, string> result)
    {
        try
        {
            var evento = JsonSerializer.Deserialize<EventoEcommerce>(result.Message.Value);
            if (evento == null) return;

            lock (StatsLock)
            {
                // Atualizar estatísticas
                Stats.TotalEventos++;
                Stats.EventosPorTipo.AddOrUpdate(evento.EventType, 1, (key, val) => val + 1);
                Stats.EventosPorUsuario.AddOrUpdate(evento.UserId, 1, (key, val) => val + 1);
                Stats.ValorTotal += evento.Value;
                Stats.SessoesAtivas.TryAdd(evento.SessionId, true);
            }

            // Log do evento
            Console.WriteLine($"📊 Evento processado: {evento.EventType} - " +
                            $"Usuário: {result.Message.Key} - " +
                            $"Partição: {result.Partition.Value} - " +
                            $"Offset: {result.Offset.Value}");

            // Processar eventos específicos
            if (evento.EventType == "purchase")
            {
                Console.WriteLine($"💰 COMPRA DETECTADA: {result.Message.Key} - " +
                                $"Produto: {evento.ProductId} - " +
                                $"Valor: R$ {evento.Value:F2}");
            }
            else if (evento.EventType == "login")
            {
                Console.WriteLine($"🔐 LOGIN: {result.Message.Key} - Sessão: {evento.SessionId}");
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"❌ Erro ao deserializar evento: {ex.Message}");
        }
    }

    private static void ImprimirEstatisticas()
    {
        lock (StatsLock)
        {
            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine("📈 ESTATÍSTICAS EM TEMPO REAL (.NET)");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine($"Total de eventos: {Stats.TotalEventos}");
            Console.WriteLine($"Valor total: R$ {Stats.ValorTotal:F2}");
            Console.WriteLine($"Sessões ativas: {Stats.SessoesAtivas.Count}");

            Console.WriteLine("\n📊 Eventos por tipo:");
            foreach (var kvp in Stats.EventosPorTipo.OrderByDescending(x => x.Value))
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }

            Console.WriteLine("\n👥 Top usuários:");
            foreach (var kvp in Stats.EventosPorUsuario.OrderByDescending(x => x.Value).Take(3))
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value} eventos");
            }
            Console.WriteLine(new string('=', 50));
        }
    }
}