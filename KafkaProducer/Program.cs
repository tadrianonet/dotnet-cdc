using Confluent.Kafka;
using System.Text.Json;

namespace KafkaProducer;

public class EventoEcommerce
{
    public string Timestamp { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? ProductId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

class Program
{
    private static readonly string[] Eventos = { "login", "view_product", "add_to_cart", "purchase", "logout" };
    private static readonly string[] Usuarios = { "user001", "user002", "user003", "user004", "user005" };
    private static readonly string[] Produtos = { "produto_A", "produto_B", "produto_C", "produto_D" };
    private static readonly Random Random = new();

    static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 Iniciando produtor de eventos de e-commerce (.NET 10)...");

        var config = new ProducerConfig
        {
            BootstrapServers = "localhost:9092",
            ClientId = "dotnet-producer"
        };

        using var producer = new ProducerBuilder<string, string>(config).Build();

        try
        {
            for (int i = 1; i <= 10; i++) // Reduzido para 10 para teste rápido
            {
                // Gerar evento
                var evento = GerarEventoEcommerce();
                var eventoJson = JsonSerializer.Serialize(evento);

                // Enviar para tópico (usando UserId como chave)
                var result = await producer.ProduceAsync("eventos-paralelos", 
                    new Message<string, string>
                    {
                        Key = evento.UserId,
                        Value = eventoJson
                    });

                Console.WriteLine($"📨 Evento {i}: {evento.EventType} - " +
                                $"Usuário: {evento.UserId} - " +
                                $"Partição: {result.Partition.Value} - " +
                                $"Offset: {result.Offset.Value}");

                // Pausa entre eventos
                await Task.Delay(Random.Next(500, 1000));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro: {ex.Message}");
        }
        finally
        {
            producer.Flush(TimeSpan.FromSeconds(10));
            Console.WriteLine("✅ Produtor finalizado!");
        }
    }

    private static EventoEcommerce GerarEventoEcommerce()
    {
        return new EventoEcommerce
        {
            Timestamp = DateTime.UtcNow.ToString("O"),
            UserId = Usuarios[Random.Next(Usuarios.Length)],
            EventType = Eventos[Random.Next(Eventos.Length)],
            ProductId = Random.NextDouble() > 0.3 ? Produtos[Random.Next(Produtos.Length)] : null,
            SessionId = $"session_{Random.Next(1000, 9999)}",
            Value = Math.Round((decimal)(Random.NextDouble() * 490 + 10), 2)
        };
    }
}