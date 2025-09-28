using Confluent.Kafka;
using System.Text.Json;

namespace KafkaStreamProcessor;

public class EventoEcommerce
{
    public string Timestamp { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? ProductId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

class StreamProcessor
{
    private readonly IProducer<string, string> _producer;
    private readonly IConsumer<string, string> _consumer;
    private readonly Random _random = new();
    private int _alertasEnviados = 0;
    private int _comprasProcessadas = 0;

    public StreamProcessor()
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = "localhost:9092"
        };

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = "stream-processor-dotnet",
            AutoOffsetReset = AutoOffsetReset.Latest
        };

        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        _consumer.Subscribe("eventos-paralelos");
    }

    public async Task ProcessarEventos(CancellationToken cancellationToken)
    {
        Console.WriteLine("🔄 Iniciando processamento de stream (.NET)...");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = _consumer.Consume(TimeSpan.FromMilliseconds(1000));
                
                if (result?.Message != null)
                {
                    var evento = JsonSerializer.Deserialize<EventoEcommerce>(result.Message.Value);
                    if (evento == null) continue;

                    // Detectar padrões suspeitos
                    if (DetectarFraude(evento))
                    {
                        await EnviarAlertaFraude(evento);
                    }

                    // Processar compras
                    if (evento.EventType == "purchase")
                    {
                        await ProcessarCompra(evento);
                    }

                    // Análise de comportamento
                    if (evento.EventType == "view_product")
                    {
                        await AnalisarInteresse(evento);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("⏹️  Parando stream processor...");
        }
    }

    private bool DetectarFraude(EventoEcommerce evento)
    {
        // Regra simples: compras muito altas
        return evento.EventType == "purchase" && evento.Value > 400;
    }

    private async Task EnviarAlertaFraude(EventoEcommerce evento)
    {
        var alerta = new
        {
            timestamp = DateTime.UtcNow.ToString("O"),
            tipo = "FRAUDE_DETECTADA",
            user_id = evento.UserId,
            valor = evento.Value,
            detalhes = evento
        };

        var alertaJson = JsonSerializer.Serialize(alerta);
        await _producer.ProduceAsync("alertas-fraude", 
            new Message<string, string> { Key = evento.UserId, Value = alertaJson });

        Interlocked.Increment(ref _alertasEnviados);
        
        Console.WriteLine($"🚨 ALERTA DE FRAUDE: {evento.UserId} - " +
                         $"Valor: R$ {evento.Value:F2}");
    }

    private async Task ProcessarCompra(EventoEcommerce evento)
    {
        var compra = new
        {
            timestamp = DateTime.UtcNow.ToString("O"),
            user_id = evento.UserId,
            product_id = evento.ProductId,
            valor = evento.Value,
            categoria = "eletrônicos",
            desconto_aplicado = _random.NextDouble() > 0.5
        };

        var compraJson = JsonSerializer.Serialize(compra);
        await _producer.ProduceAsync("compras-processadas",
            new Message<string, string> { Key = evento.UserId, Value = compraJson });

        Interlocked.Increment(ref _comprasProcessadas);
        
        Console.WriteLine($"💰 Compra processada: {evento.UserId} - " +
                         $"R$ {evento.Value:F2}");
    }

    private async Task AnalisarInteresse(EventoEcommerce evento)
    {
        if (_random.NextDouble() <= 0.3) // 30% de chance de recomendar
        {
            var recomendacao = new
            {
                timestamp = DateTime.UtcNow.ToString("O"),
                user_id = evento.UserId,
                produto_visualizado = evento.ProductId,
                produtos_recomendados = new[] { "produto_X", "produto_Y" },
                score_interesse = _random.NextDouble() * 0.5 + 0.5
            };

            var recomendacaoJson = JsonSerializer.Serialize(recomendacao);
            await _producer.ProduceAsync("recomendacoes",
                new Message<string, string> { Key = evento.UserId, Value = recomendacaoJson });

            Console.WriteLine($"🎯 Recomendação gerada para: {evento.UserId}");
        }
    }

    public void ImprimirEstatisticas()
    {
        Console.WriteLine($"\n📊 Estatísticas:");
        Console.WriteLine($"   Alertas de fraude: {_alertasEnviados}");
        Console.WriteLine($"   Compras processadas: {_comprasProcessadas}");
    }

    public void Dispose()
    {
        _consumer?.Close();
        _producer?.Flush(TimeSpan.FromSeconds(10));
        _producer?.Dispose();
        _consumer?.Dispose();
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        // Criar tópicos necessários
        await CriarTopicos();

        using var processor = new StreamProcessor();
        using var cts = new CancellationTokenSource();

        // Capturar Ctrl+C
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        // Task para estatísticas periódicas
        var statsTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(30000, cts.Token);
                processor.ImprimirEstatisticas();
            }
        }, cts.Token);

        try
        {
            await processor.ProcessarEventos(cts.Token);
        }
        finally
        {
            processor.Dispose();
            Console.WriteLine("✅ Stream processor finalizado!");
        }
    }

    private static async Task CriarTopicos()
    {
        var topicos = new[] { "alertas-fraude", "compras-processadas", "recomendacoes" };
        
        foreach (var topico in topicos)
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "docker",
                        Arguments = $"exec kafka kafka-topics --bootstrap-server localhost:9092 " +
                                   $"--create --topic {topico} --partitions 3 --replication-factor 1 " +
                                   $"--if-not-exists",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Erro ao criar tópico {topico}: {ex.Message}");
            }
        }
    }
}