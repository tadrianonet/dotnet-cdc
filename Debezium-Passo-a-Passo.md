# Debezium com SQL Server: Passo a Passo Prático

Este guia mostra como evoluir do CDC básico do SQL Server para uma solução completa de streaming de dados usando Debezium + Kafka, baseado no ambiente que já configuramos anteriormente.

## Pré-requisitos

- Ambiente CDC do SQL Server funcionando (do guia anterior)
- Docker e Docker Compose instalados
- 8GB de RAM disponível (recomendado)
- Portas 1433, 2181, 9092, 8083 disponíveis

---

## PARTE 1: Preparação do Ambiente

### Passo 1: Verificar ambiente CDC existente

Primeiro, vamos verificar se o ambiente CDC está funcionando:

```bash
# Verificar se o container SQL Server está rodando
docker ps | grep sqlserver-cdc

# Se não estiver rodando, iniciar
docker start sqlserver-cdc
```

Conecte no SQL Server e verifique se o CDC está habilitado:

```sql
-- Verificar CDC habilitado
USE EcommerceCDC;

SELECT 
    name,
    is_cdc_enabled
FROM sys.databases
WHERE name = 'EcommerceCDC';

-- Verificar tabelas com CDC
SELECT 
    s.name AS schema_name,
    t.name AS table_name,
    t.is_tracked_by_cdc
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.is_tracked_by_cdc = 1;
```

**O que esperar:** CDC deve estar habilitado (is_cdc_enabled = 1) e as tabelas Produtos e Pedidos devem aparecer com is_tracked_by_cdc = 1.

### Passo 2: Criar estrutura Docker Compose

Crie um arquivo `docker-compose-debezium.yml`:

```yaml
version: '3.8'

services:
  # Zookeeper - coordenação do cluster Kafka
  zookeeper:
    image: confluentinc/cp-zookeeper:7.4.0
    hostname: zookeeper
    container_name: zookeeper
    ports:
      - "2181:2181"
    environment:
      ZOOKEEPER_CLIENT_PORT: 2181
      ZOOKEEPER_TICK_TIME: 2000
    networks:
      - debezium-network

  # Kafka - plataforma de streaming
  kafka:
    image: confluentinc/cp-kafka:7.4.0
    hostname: kafka
    container_name: kafka
    depends_on:
      - zookeeper
    ports:
      - "9092:9092"
      - "9101:9101"
    environment:
      KAFKA_BROKER_ID: 1
      KAFKA_ZOOKEEPER_CONNECT: 'zookeeper:2181'
      KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: PLAINTEXT:PLAINTEXT,PLAINTEXT_HOST:PLAINTEXT
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka:29092,PLAINTEXT_HOST://localhost:9092
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
      KAFKA_TRANSACTION_STATE_LOG_MIN_ISR: 1
      KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: 1
      KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS: 0
      KAFKA_JMX_PORT: 9101
      KAFKA_JMX_HOSTNAME: localhost
    networks:
      - debezium-network

  # Kafka Connect com Debezium
  connect:
    image: debezium/connect:2.4
    hostname: connect
    container_name: connect
    depends_on:
      - kafka
    ports:
      - "8083:8083"
    environment:
      BOOTSTRAP_SERVERS: kafka:29092
      GROUP_ID: 1
      CONFIG_STORAGE_TOPIC: docker-connect-configs
      CONFIG_STORAGE_REPLICATION_FACTOR: 1
      OFFSET_FLUSH_INTERVAL_MS: 10000
      OFFSET_STORAGE_TOPIC: docker-connect-offsets
      OFFSET_STORAGE_REPLICATION_FACTOR: 1
      STATUS_STORAGE_TOPIC: docker-connect-status
      STATUS_STORAGE_REPLICATION_FACTOR: 1
      KEY_CONVERTER: org.apache.kafka.connect.json.JsonConverter
      VALUE_CONVERTER: org.apache.kafka.connect.json.JsonConverter
      INTERNAL_KEY_CONVERTER: org.apache.kafka.connect.json.JsonConverter
      INTERNAL_VALUE_CONVERTER: org.apache.kafka.connect.json.JsonConverter
    networks:
      - debezium-network

  # Kafka UI para visualização
  kafka-ui:
    image: provectuslabs/kafka-ui:latest
    hostname: kafka-ui
    container_name: kafka-ui
    depends_on:
      - kafka
    ports:
      - "8080:8080"
    environment:
      KAFKA_CLUSTERS_0_NAME: local
      KAFKA_CLUSTERS_0_BOOTSTRAPSERVERS: kafka:29092
    networks:
      - debezium-network

networks:
  debezium-network:
    driver: bridge
```

**Explicação dos componentes:**
- **Zookeeper**: coordena o cluster Kafka (configuração, eleição de líderes)
- **Kafka**: plataforma de streaming que receberá os eventos CDC
- **Connect**: framework para conectores, incluindo Debezium
- **Kafka UI**: interface web para visualizar tópicos e mensagens

### Passo 3: Iniciar o ambiente Debezium

```bash
# Iniciar todos os serviços
docker-compose -f docker-compose-debezium.yml up -d

# Verificar se todos os containers estão rodando
docker-compose -f docker-compose-debezium.yml ps
```

**O que esperar:** Todos os serviços devem aparecer como "Up". Aguarde cerca de 2-3 minutos para que tudo inicialize completamente.

```bash
# Verificar logs se houver problemas
docker-compose -f docker-compose-debezium.yml logs kafka
docker-compose -f docker-compose-debezium.yml logs connect
```

### Passo 4: Verificar conectividade

```bash
# Testar conectividade com Kafka
docker exec -it kafka kafka-topics --bootstrap-server localhost:9092 --list

# Verificar se Kafka Connect está funcionando
curl -H "Accept:application/json" localhost:8083/
```

**O que esperar:** 
- Lista de tópicos (pode estar vazia inicialmente)
- Resposta JSON do Kafka Connect com versão e commit

---

## PARTE 2: Configuração do Conector Debezium

### Passo 5: Conectar redes Docker

Como temos o SQL Server em um container separado, precisamos conectá-lo à rede do Debezium:

```bash
# Conectar SQL Server à rede Debezium
docker network connect debezium-passo-a-passo-debezium-network sqlserver-cdc
```

### Passo 6: Configurar conector SQL Server

Crie um arquivo `sqlserver-connector.json`:

```json
{
  "name": "ecommerce-connector",
  "config": {
    "connector.class": "io.debezium.connector.sqlserver.SqlServerConnector",
    "database.hostname": "sqlserver-cdc",
    "database.port": "1433",
    "database.user": "sa",
    "database.password": "MinhaSenh@123",
    "database.names": "EcommerceCDC",
    "database.encrypt": "false",
    "topic.prefix": "ecommerce",
    "table.include.list": "dbo.Produtos,dbo.Pedidos",
    "database.history.kafka.bootstrap.servers": "kafka:29092",
    "database.history.kafka.topic": "ecommerce.history",
    "snapshot.mode": "initial",
    "transforms": "unwrap",
    "transforms.unwrap.type": "io.debezium.transforms.ExtractNewRecordState",
    "transforms.unwrap.drop.tombstones": "false",
    "key.converter": "org.apache.kafka.connect.json.JsonConverter",
    "value.converter": "org.apache.kafka.connect.json.JsonConverter",
    "key.converter.schemas.enable": "false",
    "value.converter.schemas.enable": "false"
  }
}
```

**Explicação da configuração:**
- `connector.class`: especifica o conector Debezium para SQL Server
- `database.*`: configurações de conexão com o banco
- `topic.prefix`: prefixo para os tópicos Kafka (ecommerce.dbo.Produtos)
- `table.include.list`: tabelas específicas para capturar
- `snapshot.mode`: captura estado inicial antes do streaming
- `transforms.unwrap`: simplifica estrutura dos eventos

### Passo 7: Registrar o conector

```bash
# Registrar conector no Kafka Connect
curl -i -X POST -H "Accept:application/json" -H "Content-Type:application/json" \
  localhost:8083/connectors/ -d @sqlserver-connector.json
```

**O que esperar:** Resposta HTTP 201 Created com configuração do conector.

```bash
# Verificar status do conector
curl -H "Accept:application/json" localhost:8083/connectors/

# Verificar detalhes do conector
curl -H "Accept:application/json" localhost:8083/connectors/ecommerce-connector/status
```

**O que esperar:** Status "RUNNING" para o conector e suas tasks.

### Passo 8: Verificar tópicos criados

```bash
# Listar tópicos criados pelo Debezium
docker exec -it kafka kafka-topics --bootstrap-server localhost:9092 --list | grep ecommerce
```

**O que esperar:** Tópicos como:
- `ecommerce.dbo.Produtos`
- `ecommerce.dbo.Pedidos`
- `ecommerce.history`

---

## PARTE 3: Testando a Captura de Mudanças

### Passo 9: Consumir eventos em tempo real

Abra um terminal para consumir eventos de produtos:

```bash
# Consumir eventos da tabela Produtos
docker exec -it kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic ecommerce.dbo.Produtos \
  --from-beginning \
  --property print.key=true
```

**Deixe este terminal aberto** - ele mostrará os eventos em tempo real.

### Passo 10: Gerar mudanças no SQL Server

Em outro terminal, conecte ao SQL Server e execute mudanças:

```sql
USE EcommerceCDC;

-- Inserir novo produto
INSERT INTO Produtos (Nome, Preco, Estoque, Categoria) VALUES
('Smartphone Samsung', 1299.99, 15, 'Eletrônicos');

-- Atualizar preço
UPDATE Produtos 
SET Preco = 1199.99, DataAtualizacao = GETDATE()
WHERE Nome = 'Smartphone Samsung';

-- Deletar produto
DELETE FROM Produtos WHERE Nome = 'Smartphone Samsung';
```

**O que esperar no terminal do consumidor:**

```json
// Evento de INSERT
{"ProdutoID":8}	{
  "ProdutoID": 8,
  "Nome": "Smartphone Samsung",
  "Preco": 1299.99,
  "Estoque": 15,
  "Categoria": "Eletrônicos",
  "DataCriacao": "2024-01-15T10:30:00Z",
  "DataAtualizacao": "2024-01-15T10:30:00Z"
}

// Evento de UPDATE
{"ProdutoID":8}	{
  "ProdutoID": 8,
  "Nome": "Smartphone Samsung",
  "Preco": 1199.99,
  "Estoque": 15,
  "Categoria": "Eletrônicos",
  "DataCriacao": "2024-01-15T10:30:00Z",
  "DataAtualizacao": "2024-01-15T10:31:00Z"
}

// Evento de DELETE
{"ProdutoID":8}	null
```

### Passo 11: Usar Kafka UI para visualização

Acesse http://localhost:8080 no seu navegador para ver:

- **Tópicos**: lista de todos os tópicos criados
- **Mensagens**: conteúdo dos eventos em formato JSON
- **Consumidores**: grupos de consumidores ativos
- **Configuração**: detalhes dos conectores

**Navegação no Kafka UI:**
1. Clique em "Topics" no menu lateral
2. Selecione "ecommerce.dbo.Produtos"
3. Clique em "Messages" para ver os eventos
4. Use filtros para buscar eventos específicos

---

## PARTE 4: Consumidor Personalizado

### Passo 12: Criar consumidor Python

Crie um arquivo `consumer.py`:

```python
from kafka import KafkaConsumer
import json
import logging

# Configurar logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Configurar consumidor Kafka
consumer = KafkaConsumer(
    'ecommerce.dbo.Produtos',
    bootstrap_servers=['localhost:9092'],
    value_deserializer=lambda m: json.loads(m.decode('utf-8')) if m else None,
    key_deserializer=lambda m: json.loads(m.decode('utf-8')) if m else None,
    group_id='ecommerce-processor',
    auto_offset_reset='earliest'
)

def processar_evento_produto(key, value):
    """Processa eventos de mudança de produtos"""
    
    if value is None:
        # Evento de DELETE
        produto_id = key.get('ProdutoID') if key else 'unknown'
        logger.info(f"🗑️  PRODUTO DELETADO: ID {produto_id}")
        
        # Aqui você pode:
        # - Remover do cache Redis
        # - Atualizar índices de busca
        # - Enviar notificação
        
    else:
        # Evento de INSERT ou UPDATE
        produto_id = value.get('ProdutoID')
        nome = value.get('Nome')
        preco = value.get('Preco')
        estoque = value.get('Estoque')
        
        logger.info(f"📦 PRODUTO ATUALIZADO: {nome} (ID: {produto_id})")
        logger.info(f"   💰 Preço: R$ {preco}")
        logger.info(f"   📊 Estoque: {estoque}")
        
        # Aqui você pode:
        # - Atualizar cache de produtos
        # - Reindexar sistema de busca
        # - Atualizar recomendações
        # - Enviar para data warehouse

def main():
    logger.info("🚀 Iniciando consumidor de eventos de produtos...")
    
    try:
        for message in consumer:
            key = message.key
            value = message.value
            
            logger.info(f"📨 Evento recebido: offset {message.offset}")
            processar_evento_produto(key, value)
            
    except KeyboardInterrupt:
        logger.info("⏹️  Parando consumidor...")
    finally:
        consumer.close()

if __name__ == "__main__":
    main()
```

### Passo 13: Executar consumidor personalizado

```bash
# Instalar dependências Python
pip install kafka-python

# Executar consumidor
python consumer.py
```

**Deixe o consumidor rodando** e execute mudanças no SQL Server para ver o processamento em tempo real.

### Passo 14: Testar processamento completo

Execute estas operações no SQL Server:

```sql
-- Inserir vários produtos
INSERT INTO Produtos (Nome, Preco, Estoque, Categoria) VALUES
('iPhone 15', 4999.99, 5, 'Eletrônicos'),
('MacBook Pro', 8999.99, 3, 'Eletrônicos'),
('AirPods Pro', 1299.99, 20, 'Áudio');

-- Simular vendas (reduzir estoque)
UPDATE Produtos SET Estoque = Estoque - 1 WHERE Nome = 'iPhone 15';
UPDATE Produtos SET Estoque = Estoque - 2 WHERE Nome = 'AirPods Pro';

-- Ajustar preços
UPDATE Produtos SET Preco = 4799.99 WHERE Nome = 'iPhone 15';
UPDATE Produtos SET Preco = 8499.99 WHERE Nome = 'MacBook Pro';
```

**O que esperar no consumidor Python:**
- Logs detalhados de cada mudança
- Processamento em tempo real (< 1 segundo)
- Informações estruturadas sobre produtos

---

## PARTE 5: Monitoramento e Troubleshooting

### Passo 15: Monitorar performance

```bash
# Verificar lag dos consumidores
docker exec -it kafka kafka-consumer-groups \
  --bootstrap-server localhost:9092 \
  --describe --group ecommerce-processor

# Verificar métricas do conector
curl localhost:8083/connectors/ecommerce-connector/status | jq

# Verificar tópicos e partições
docker exec -it kafka kafka-topics \
  --bootstrap-server localhost:9092 \
  --describe --topic ecommerce.dbo.Produtos
```

### Passo 16: Verificar logs de troubleshooting

```bash
# Logs do Kafka Connect
docker logs connect

# Logs específicos do conector
curl localhost:8083/connectors/ecommerce-connector/tasks/0/status

# Verificar se CDC está funcionando no SQL Server
```

```sql
-- Verificar jobs CDC no SQL Server
SELECT 
    j.name AS job_name,
    j.enabled,
    ja.last_executed_step_date,
    ja.last_execution_outcome
FROM msdb.dbo.sysjobs j
LEFT JOIN msdb.dbo.sysjobactivity ja ON j.job_id = ja.job_id
WHERE j.name LIKE '%cdc%';
```

### Passo 17: Comandos úteis de manutenção

```bash
# Reiniciar conector se necessário
curl -X POST localhost:8083/connectors/ecommerce-connector/restart

# Pausar conector
curl -X PUT localhost:8083/connectors/ecommerce-connector/pause

# Retomar conector
curl -X PUT localhost:8083/connectors/ecommerce-connector/resume

# Deletar conector
curl -X DELETE localhost:8083/connectors/ecommerce-connector
```

---

## PARTE 6: Casos de Uso Avançados

### Passo 18: Filtrar eventos específicos

Modifique a configuração do conector para capturar apenas certas colunas:

```json
{
  "name": "ecommerce-connector-filtered",
  "config": {
    "connector.class": "io.debezium.connector.sqlserver.SqlServerConnector",
    "database.hostname": "sqlserver-cdc",
    "database.port": "1433",
    "database.user": "sa",
    "database.password": "MinhaSenh@123",
    "database.names": "EcommerceCDC",
    "topic.prefix": "ecommerce-filtered",
    "table.include.list": "dbo.Produtos",
    "column.include.list": "dbo.Produtos.ProdutoID,dbo.Produtos.Nome,dbo.Produtos.Preco",
    "transforms": "unwrap,filter",
    "transforms.unwrap.type": "io.debezium.transforms.ExtractNewRecordState",
    "transforms.filter.type": "org.apache.kafka.connect.transforms.Filter",
    "transforms.filter.predicate": "price-filter",
    "predicates": "price-filter",
    "predicates.price-filter.type": "org.apache.kafka.connect.transforms.predicates.TopicNameMatches",
    "predicates.price-filter.pattern": ".*Produtos.*"
  }
}
```

### Passo 19: Transformações customizadas

Exemplo de transformação para enriquecer eventos:

```json
{
  "transforms": "unwrap,addMetadata",
  "transforms.unwrap.type": "io.debezium.transforms.ExtractNewRecordState",
  "transforms.addMetadata.type": "org.apache.kafka.connect.transforms.InsertField$Value",
  "transforms.addMetadata.timestamp.field": "event_timestamp",
  "transforms.addMetadata.static.field": "source_system",
  "transforms.addMetadata.static.value": "ecommerce-sql-server"
}
```

---

## Limpeza do Ambiente

### Para parar tudo:

```bash
# Parar consumidor Python (Ctrl+C)

# Parar ambiente Debezium
docker-compose -f docker-compose-debezium.yml down

# Parar SQL Server (se necessário)
docker stop sqlserver-cdc
```

### Para remover completamente:

```bash
# Remover containers e volumes
docker-compose -f docker-compose-debezium.yml down -v

# Remover imagens (opcional)
docker rmi debezium/connect:2.4 confluentinc/cp-kafka:7.4.0 confluentinc/cp-zookeeper:7.4.0
```

---

## Resumo do que foi demonstrado

1. **Evolução do CDC**: do CDC nativo para streaming com Debezium
2. **Ambiente completo**: Kafka + Zookeeper + Connect + UI
3. **Configuração de conector**: SQL Server para Kafka via Debezium
4. **Captura em tempo real**: mudanças aparecem instantaneamente no Kafka
5. **Consumidores**: tanto console quanto aplicação Python personalizada
6. **Monitoramento**: ferramentas para acompanhar performance e saúde
7. **Casos avançados**: filtros, transformações e configurações específicas

## Vantagens demonstradas do Debezium

- **Latência ultra-baixa**: mudanças aparecem em < 1 segundo
- **Desacoplamento total**: SQL Server não sabe que Kafka existe
- **Escalabilidade**: múltiplos consumidores independentes
- **Confiabilidade**: tolerância a falhas e recuperação automática
- **Flexibilidade**: transformações e filtros em tempo real
- **Observabilidade**: métricas e logs detalhados

Este ambiente fornece uma base sólida para implementar arquiteturas event-driven em produção, com todas as garantias de confiabilidade e performance necessárias para sistemas críticos.
