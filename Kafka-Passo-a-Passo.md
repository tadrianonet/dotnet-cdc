# Apache Kafka: Passo a Passo Prático

Este guia mostra como explorar, configurar e usar o Apache Kafka evoluindo do ambiente Zookeeper que já temos funcionando, focando em operações práticas, produtores, consumidores e casos de uso reais.

## Pré-requisitos

- Ambiente Zookeeper cluster funcionando (do guia anterior)
- Kafka rodando conectado ao ensemble Zookeeper
- Conhecimento básico de streaming de dados
- Portas 9092, 9093, 9094 disponíveis para cluster Kafka

---

## PARTE 1: Explorando o Kafka Existente

### Passo 1: Verificar status do ambiente atual

Primeiro, vamos verificar se o ambiente Zookeeper + Kafka está funcionando:

```bash
# Verificar containers rodando
docker-compose -f docker-compose-zk-cluster.yml ps

# Verificar se Kafka está respondendo
docker exec kafka kafka-broker-api-versions --bootstrap-server localhost:9092
```

**O que esperar:** Todos os containers "Up" e Kafka respondendo com lista de APIs disponíveis.

### Passo 2: Explorar configuração do Kafka

```bash
# Ver configuração do Kafka
docker exec kafka cat /etc/kafka/server.properties | head -20

# Ver variáveis de ambiente do Kafka
docker exec kafka env | grep KAFKA

# Verificar conectividade com Zookeeper
docker logs kafka | grep -i zookeeper | tail -5
```

**O que observar:**
- `KAFKA_BROKER_ID`: identificador único do broker (1)
- `KAFKA_ZOOKEEPER_CONNECT`: conexão com ensemble Zookeeper
- `KAFKA_ADVERTISED_LISTENERS`: como clientes se conectam

### Passo 3: Comandos básicos do Kafka

```bash
# Listar tópicos existentes
docker exec kafka kafka-topics --bootstrap-server localhost:9092 --list

# Ver detalhes de um tópico específico
docker exec kafka kafka-topics --bootstrap-server localhost:9092 --describe --topic sensor-data

# Verificar grupos de consumidores ativos
docker exec kafka kafka-consumer-groups --bootstrap-server localhost:9092 --list
```

**Explicação:** Kafka organiza dados em tópicos, que são divididos em partições para paralelização.

---

## PARTE 2: Operações com Tópicos

### Passo 4: Criar tópicos com diferentes configurações

```bash
# Tópico simples (1 partição, sem replicação)
docker exec kafka kafka-topics --bootstrap-server localhost:9092 \
  --create --topic eventos-simples \
  --partitions 1 --replication-factor 1

# Tópico com múltiplas partições
docker exec kafka kafka-topics --bootstrap-server localhost:9092 \
  --create --topic eventos-paralelos \
  --partitions 3 --replication-factor 1

# Tópico com configurações específicas
docker exec kafka kafka-topics --bootstrap-server localhost:9092 \
  --create --topic eventos-configurados \
  --partitions 2 --replication-factor 1 \
  --config retention.ms=3600000 \
  --config compression.type=snappy
```

**Explicação dos parâmetros:**
- `--partitions`: número de partições (paralelismo)
- `--replication-factor`: cópias dos dados (tolerância a falhas)
- `--config`: configurações específicas do tópico

### Passo 5: Modificar configurações de tópicos

```bash
# Alterar configuração de retenção
docker exec kafka kafka-configs --bootstrap-server localhost:9092 \
  --entity-type topics --entity-name eventos-configurados \
  --alter --add-config retention.ms=7200000

# Ver configurações de um tópico
docker exec kafka kafka-configs --bootstrap-server localhost:9092 \
  --entity-type topics --entity-name eventos-configurados \
  --describe

# Aumentar número de partições (só pode aumentar, nunca diminuir)
docker exec kafka kafka-topics --bootstrap-server localhost:9092 \
  --alter --topic eventos-paralelos --partitions 5
```

### Passo 6: Verificar detalhes dos tópicos criados

```bash
# Listar todos os tópicos
docker exec kafka kafka-topics --bootstrap-server localhost:9092 --list

# Descrição detalhada de todos os tópicos
docker exec kafka kafka-topics --bootstrap-server localhost:9092 --describe

# Verificar apenas tópicos que criamos
docker exec kafka kafka-topics --bootstrap-server localhost:9092 --list | grep eventos
```

---

## PARTE 3: Produtores de Dados

### Passo 7: Produtor console básico

```bash
# Abrir produtor console (deixar rodando em terminal separado)
docker exec -it kafka kafka-console-producer \
  --bootstrap-server localhost:9092 \
  --topic eventos-simples
```

**No terminal do produtor, digite algumas mensagens:**
```
Primeira mensagem de teste
Segunda mensagem
Mensagem com timestamp: $(date)
```

**Pressione Ctrl+C para sair do produtor.**

### Passo 8: Produtor com chave

```bash
# Produtor com chave (para controlar particionamento)
docker exec -it kafka kafka-console-producer \
  --bootstrap-server localhost:9092 \
  --topic eventos-paralelos \
  --property "parse.key=true" \
  --property "key.separator=:"
```

**No terminal do produtor, digite mensagens com chave:**
```
usuario1:Login realizado
usuario1:Página acessada
usuario2:Login realizado
usuario3:Compra finalizada
usuario1:Logout
```

**Explicação:** Mensagens com a mesma chave vão para a mesma partição, garantindo ordem.

### Passo 9: Produtor C# .NET 10 personalizado

Primeiro, crie um projeto .NET console:

```bash
# Criar projeto .NET 10
dotnet new console -n KafkaProducer
cd KafkaProducer

# Adicionar pacote Confluent.Kafka
dotnet add package Confluent.Kafka --version 2.3.0
dotnet add package System.Text.Json --version 8.0.0
```

Substitua o conteúdo do arquivo `Program.cs`:

```csharp
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
            for (int i = 1; i <= 50; i++)
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
                await Task.Delay(Random.Next(500, 2000));
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
```

Execute o produtor:

```bash
# Compilar e executar
dotnet run

# Ou executar em background
dotnet run &
```

**O que esperar:** 50 eventos gerados com diferentes tipos, usuários e produtos, distribuídos pelas partições.

---

## PARTE 4: Consumidores de Dados

### Passo 10: Consumidor console básico

```bash
# Consumir mensagens do início do tópico
docker exec kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic eventos-simples \
  --from-beginning

# Em outro terminal, consumir apenas novas mensagens
docker exec kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic eventos-paralelos \
  --property print.key=true \
  --property key.separator=" => "
```

### Passo 11: Consumidor com grupo

```bash
# Consumidor 1 do grupo "processadores"
docker exec kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic eventos-paralelos \
  --group processadores \
  --property print.key=true &

# Consumidor 2 do mesmo grupo (em paralelo)
docker exec kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic eventos-paralelos \
  --group processadores \
  --property print.key=true &

# Aguardar um pouco e parar os consumidores
sleep 30
pkill -f kafka-console-consumer
```

**Explicação:** Consumidores do mesmo grupo dividem as partições entre si (load balancing).

### Passo 12: Consumidor C# .NET 10 avançado

Crie um novo projeto para o consumidor:

```bash
# Criar projeto consumidor
dotnet new console -n KafkaConsumer
cd KafkaConsumer

# Adicionar pacotes necessários
dotnet add package Confluent.Kafka --version 2.3.0
dotnet add package System.Text.Json --version 8.0.0
```

Substitua o conteúdo do arquivo `Program.cs`:

```csharp
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
```

Execute o consumidor:

```bash
# Compilar e executar
dotnet run

# Ou executar em background
dotnet run &
```

**Deixe rodando** e execute o produtor em outro terminal para ver o processamento em tempo real.

---

## PARTE 5: Cluster Kafka Multi-Broker

### Passo 13: Criar cluster Kafka com 3 brokers

Primeiro, vamos parar o ambiente atual e criar um cluster completo:

```bash
# Parar ambiente atual
docker-compose -f docker-compose-zk-cluster.yml down
```

Crie um novo arquivo `docker-compose-kafka-cluster.yml`:

```yaml
version: '3.8'

services:
  # Zookeeper Ensemble
  zk1:
    image: confluentinc/cp-zookeeper:7.4.0
    hostname: zk1
    container_name: zk1
    ports:
      - "2181:2181"
    environment:
      ZOOKEEPER_SERVER_ID: 1
      ZOOKEEPER_CLIENT_PORT: 2181
      ZOOKEEPER_TICK_TIME: 2000
      ZOOKEEPER_INIT_LIMIT: 5
      ZOOKEEPER_SYNC_LIMIT: 2
      ZOOKEEPER_SERVERS: zk1:2888:3888;zk2:2888:3888;zk3:2888:3888
    networks:
      - kafka-network

  zk2:
    image: confluentinc/cp-zookeeper:7.4.0
    hostname: zk2
    container_name: zk2
    ports:
      - "2182:2181"
    environment:
      ZOOKEEPER_SERVER_ID: 2
      ZOOKEEPER_CLIENT_PORT: 2181
      ZOOKEEPER_TICK_TIME: 2000
      ZOOKEEPER_INIT_LIMIT: 5
      ZOOKEEPER_SYNC_LIMIT: 2
      ZOOKEEPER_SERVERS: zk1:2888:3888;zk2:2888:3888;zk3:2888:3888
    networks:
      - kafka-network

  zk3:
    image: confluentinc/cp-zookeeper:7.4.0
    hostname: zk3
    container_name: zk3
    ports:
      - "2183:2181"
    environment:
      ZOOKEEPER_SERVER_ID: 3
      ZOOKEEPER_CLIENT_PORT: 2181
      ZOOKEEPER_TICK_TIME: 2000
      ZOOKEEPER_INIT_LIMIT: 5
      ZOOKEEPER_SYNC_LIMIT: 2
      ZOOKEEPER_SERVERS: zk1:2888:3888;zk2:2888:3888;zk3:2888:3888
    networks:
      - kafka-network

  # Kafka Cluster (3 brokers)
  kafka1:
    image: confluentinc/cp-kafka:7.4.0
    hostname: kafka1
    container_name: kafka1
    depends_on:
      - zk1
      - zk2
      - zk3
    ports:
      - "9092:9092"
    environment:
      KAFKA_BROKER_ID: 1
      KAFKA_ZOOKEEPER_CONNECT: 'zk1:2181,zk2:2181,zk3:2181'
      KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: PLAINTEXT:PLAINTEXT,PLAINTEXT_HOST:PLAINTEXT
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka1:29092,PLAINTEXT_HOST://localhost:9092
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 3
      KAFKA_TRANSACTION_STATE_LOG_MIN_ISR: 2
      KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: 3
      KAFKA_DEFAULT_REPLICATION_FACTOR: 3
      KAFKA_MIN_INSYNC_REPLICAS: 2
    networks:
      - kafka-network

  kafka2:
    image: confluentinc/cp-kafka:7.4.0
    hostname: kafka2
    container_name: kafka2
    depends_on:
      - zk1
      - zk2
      - zk3
    ports:
      - "9093:9092"
    environment:
      KAFKA_BROKER_ID: 2
      KAFKA_ZOOKEEPER_CONNECT: 'zk1:2181,zk2:2181,zk3:2181'
      KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: PLAINTEXT:PLAINTEXT,PLAINTEXT_HOST:PLAINTEXT
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka2:29092,PLAINTEXT_HOST://localhost:9093
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 3
      KAFKA_TRANSACTION_STATE_LOG_MIN_ISR: 2
      KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: 3
      KAFKA_DEFAULT_REPLICATION_FACTOR: 3
      KAFKA_MIN_INSYNC_REPLICAS: 2
    networks:
      - kafka-network

  kafka3:
    image: confluentinc/cp-kafka:7.4.0
    hostname: kafka3
    container_name: kafka3
    depends_on:
      - zk1
      - zk2
      - zk3
    ports:
      - "9094:9092"
    environment:
      KAFKA_BROKER_ID: 3
      KAFKA_ZOOKEEPER_CONNECT: 'zk1:2181,zk2:2181,zk3:2181'
      KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: PLAINTEXT:PLAINTEXT,PLAINTEXT_HOST:PLAINTEXT
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka3:29092,PLAINTEXT_HOST://localhost:9094
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 3
      KAFKA_TRANSACTION_STATE_LOG_MIN_ISR: 2
      KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: 3
      KAFKA_DEFAULT_REPLICATION_FACTOR: 3
      KAFKA_MIN_INSYNC_REPLICAS: 2
    networks:
      - kafka-network

  # Kafka UI para visualização
  kafka-ui:
    image: provectuslabs/kafka-ui:latest
    hostname: kafka-ui
    container_name: kafka-ui
    depends_on:
      - kafka1
      - kafka2
      - kafka3
    ports:
      - "8080:8080"
    environment:
      KAFKA_CLUSTERS_0_NAME: local-cluster
      KAFKA_CLUSTERS_0_BOOTSTRAPSERVERS: kafka1:29092,kafka2:29092,kafka3:29092
      KAFKA_CLUSTERS_0_ZOOKEEPER: zk1:2181,zk2:2181,zk3:2181
    networks:
      - kafka-network

networks:
  kafka-network:
    driver: bridge
```

**Explicação das configurações importantes:**
- `KAFKA_DEFAULT_REPLICATION_FACTOR: 3`: replicação padrão para novos tópicos
- `KAFKA_MIN_INSYNC_REPLICAS: 2`: mínimo de réplicas sincronizadas
- Cada broker tem ID único e porta diferente

### Passo 14: Iniciar cluster completo

```bash
# Iniciar cluster Kafka completo
docker-compose -f docker-compose-kafka-cluster.yml up -d

# Aguardar inicialização (pode demorar 2-3 minutos)
sleep 120

# Verificar se todos estão rodando
docker-compose -f docker-compose-kafka-cluster.yml ps
```

**O que esperar:** 7 containers rodando (3 Zookeeper + 3 Kafka + 1 UI).

### Passo 15: Testar cluster com replicação

```bash
# Criar tópico com replicação completa
docker exec kafka1 kafka-topics --bootstrap-server localhost:9092 \
  --create --topic eventos-replicados \
  --partitions 6 --replication-factor 3

# Verificar distribuição das réplicas
docker exec kafka1 kafka-topics --bootstrap-server localhost:9092 \
  --describe --topic eventos-replicados

# Listar brokers no cluster
docker exec kafka1 kafka-broker-api-versions --bootstrap-server localhost:9092,localhost:9093,localhost:9094
```

**O que observar:** Cada partição deve ter 3 réplicas distribuídas pelos brokers.

---

## PARTE 6: Tolerância a Falhas e Performance

### Passo 16: Testar tolerância a falhas do cluster

```bash
# Criar produtor para tópico replicado
docker exec -it kafka1 kafka-console-producer \
  --bootstrap-server localhost:9092,localhost:9093,localhost:9094 \
  --topic eventos-replicados &
PRODUCER_PID=$!

# Em outro terminal, criar consumidor
docker exec kafka2 kafka-console-consumer \
  --bootstrap-server localhost:9092,localhost:9093,localhost:9094 \
  --topic eventos-replicados \
  --group teste-tolerancia &
CONSUMER_PID=$!

# Parar um broker (simular falha)
docker stop kafka2

# Verificar se sistema continua funcionando
sleep 10
echo "Testando após falha do kafka2..."

# Verificar status do tópico
docker exec kafka1 kafka-topics --bootstrap-server localhost:9092,localhost:9094 \
  --describe --topic eventos-replicados

# Parar processos de teste
kill $PRODUCER_PID $CONSUMER_PID 2>/dev/null
```

**O que esperar:** Sistema continua funcionando mesmo com um broker parado.

### Passo 17: Monitoramento de performance

Use comandos diretos do Kafka para monitoramento:

```bash
# Informações do cluster
docker exec kafka kafka-topics --bootstrap-server localhost:9092 --list | wc -l
echo "📊 Número de tópicos no cluster"

# Verificar grupos de consumidores
docker exec kafka kafka-consumer-groups --bootstrap-server localhost:9092 --list

# Monitorar lag de consumidores específicos
docker exec kafka kafka-consumer-groups --bootstrap-server localhost:9092 \
  --describe --group analytics-processor-dotnet

# Verificar métricas de performance
docker exec kafka kafka-log-dirs --bootstrap-server localhost:9092 --describe --json
```

### Passo 18: Teste de performance com múltiplos produtores

```bash
# Teste de performance do produtor
docker exec kafka1 kafka-producer-perf-test \
  --topic eventos-replicados \
  --num-records 10000 \
  --record-size 1024 \
  --throughput 1000 \
  --producer-props bootstrap.servers=localhost:9092,localhost:9093,localhost:9094

# Teste de performance do consumidor
docker exec kafka1 kafka-consumer-perf-test \
  --topic eventos-replicados \
  --messages 10000 \
  --bootstrap-server localhost:9092,localhost:9093,localhost:9094
```

**O que observar:** Throughput (mensagens/segundo) e latência do cluster.

---

## PARTE 7: Casos de Uso Avançados

### Passo 19: Streaming de dados em tempo real com C#

Crie um pipeline completo de streaming:

```bash
# Criar projeto stream processor
dotnet new console -n KafkaStreamProcessor
cd KafkaStreamProcessor

# Adicionar pacotes necessários
dotnet add package Confluent.Kafka --version 2.3.0
dotnet add package System.Text.Json --version 8.0.0
```

Substitua o conteúdo do arquivo `Program.cs`:

```csharp
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
```

Execute o stream processor:

```bash
# Compilar e executar
dotnet run

# Em outro terminal, executar o produtor para gerar eventos
cd ../KafkaProducer
dotnet run &
```

### Passo 20: Verificar resultados do streaming

```bash
# Verificar tópicos criados pelo stream processor
docker exec kafka1 kafka-topics --bootstrap-server localhost:9092 --list | grep -E "(alertas|compras|recomendacoes)"

# Ver alertas de fraude gerados
docker exec kafka1 kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic alertas-fraude \
  --from-beginning \
  --max-messages 5

# Ver compras processadas
docker exec kafka1 kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic compras-processadas \
  --from-beginning \
  --max-messages 5

# Ver recomendações geradas
docker exec kafka1 kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic recomendacoes \
  --from-beginning \
  --max-messages 5
```

---

## PARTE 8: Kafka UI e Monitoramento Visual

### Passo 21: Explorar Kafka UI

Acesse http://localhost:8080 no seu navegador para explorar:

**Principais funcionalidades:**
1. **Overview**: visão geral do cluster
2. **Brokers**: status e configuração dos brokers
3. **Topics**: lista e detalhes dos tópicos
4. **Consumers**: grupos de consumidores e lag
5. **Messages**: visualizar mensagens dos tópicos

**Navegação prática:**
```
1. Acesse "Topics" → selecione "eventos-replicados"
2. Clique em "Messages" para ver as mensagens
3. Vá em "Consumers" para ver grupos ativos
4. Explore "Brokers" para ver distribuição
```

### Passo 22: Monitoramento de métricas

```bash
# Verificar grupos de consumidores e lag
docker exec kafka1 kafka-consumer-groups \
  --bootstrap-server localhost:9092 \
  --describe --group analytics-processor

# Ver estatísticas de tópicos
docker exec kafka1 kafka-log-dirs \
  --bootstrap-server localhost:9092 \
  --describe --json | jq '.brokers[0].logDirs[0].topics'

# Verificar configuração do cluster
docker exec kafka1 kafka-configs \
  --bootstrap-server localhost:9092 \
  --entity-type brokers --entity-name 1 --describe
```

---

## PARTE 9: Limpeza e Scripts de Automação

### Passo 23: Scripts de automação

```bash
# Criar script de deploy completo
cat > deploy_kafka_cluster.sh << 'EOF'
#!/bin/bash

echo "🚀 Iniciando deploy do cluster Kafka completo..."

# Parar ambiente anterior
docker-compose -f docker-compose-zk-cluster.yml down 2>/dev/null

# Iniciar novo cluster
docker-compose -f docker-compose-kafka-cluster.yml up -d

# Aguardar inicialização
echo "⏳ Aguardando inicialização do cluster..."
sleep 120

# Verificar saúde do cluster
echo "🔍 Verificando saúde do cluster..."

# Verificar Zookeeper
for i in 1 2 3; do
    if docker exec zk$i test -f /var/lib/zookeeper/data/myid; then
        echo "✅ ZK$i: OK"
    else
        echo "❌ ZK$i: FALHA"
    fi
done

# Verificar Kafka
for i in 1 2 3; do
    port=$((9091 + i))
    if docker exec kafka$i kafka-broker-api-versions --bootstrap-server localhost:$port >/dev/null 2>&1; then
        echo "✅ Kafka$i: OK"
    else
        echo "❌ Kafka$i: FALHA"
    fi
done

# Criar tópicos de exemplo
echo "📝 Criando tópicos de exemplo..."
docker exec kafka1 kafka-topics --bootstrap-server localhost:9092 \
  --create --topic eventos-exemplo --partitions 6 --replication-factor 3 \
  --if-not-exists

echo "✅ Deploy concluído!"
echo "🌐 Kafka UI disponível em: http://localhost:8080"
echo "🔗 Bootstrap servers: localhost:9092,localhost:9093,localhost:9094"
echo "🔧 Para testar com .NET: dotnet new console -n TestKafka && cd TestKafka"
echo "   dotnet add package Confluent.Kafka --version 2.3.0"
EOF

chmod +x deploy_kafka_cluster.sh
```

### Passo 24: Limpeza do ambiente

```bash
# Parar todos os processos .NET
pkill -f dotnet 2>/dev/null

# Parar cluster Kafka
docker-compose -f docker-compose-kafka-cluster.yml down

# Limpar volumes (opcional - remove todos os dados)
docker-compose -f docker-compose-kafka-cluster.yml down -v

# Voltar ao ambiente simples (se necessário)
docker-compose -f docker-compose-zk-cluster.yml up -d
```

---

## Resumo do que foi demonstrado

1. **Exploração do Kafka**: configuração, tópicos, comandos básicos
2. **Operações com tópicos**: criação, configuração, modificação
3. **Produtores**: console, com chave, .NET C# personalizado
4. **Consumidores**: console, grupos, .NET C# com analytics
5. **Cluster multi-broker**: 3 brokers com replicação completa
6. **Tolerância a falhas**: testada com parada de brokers
7. **Performance**: testes de throughput e latência
8. **Streaming avançado**: processamento em tempo real, detecção de fraude com C#
9. **Monitoramento**: Kafka UI, métricas, grupos de consumidores
10. **Automação**: scripts de deploy e manutenção

## Conceitos fundamentais aplicados

- **Tópicos e partições**: paralelização e escalabilidade
- **Replicação**: tolerância a falhas e alta disponibilidade
- **Grupos de consumidores**: load balancing e processamento paralelo
- **Chaves de mensagem**: garantia de ordem por partição
- **Offsets**: controle de progresso e reprocessamento
- **ISR (In-Sync Replicas)**: consistência e durabilidade
- **Streaming**: processamento em tempo real de eventos

## Casos de uso demonstrados

- **E-commerce analytics**: eventos de usuário, compras, recomendações
- **Detecção de fraude**: alertas em tempo real baseados em regras
- **Processamento de stream**: transformação e enriquecimento de dados
- **Monitoramento**: métricas de performance e saúde do cluster
- **Tolerância a falhas**: continuidade operacional com falhas de hardware

Este ambiente Kafka está pronto para uso em produção e demonstra todos os conceitos fundamentais de streaming de dados, desde operações básicas até casos de uso avançados com processamento em tempo real.

**🎉 Jornada completa CDC → Debezium → Zookeeper → Kafka finalizada!** 🚀
